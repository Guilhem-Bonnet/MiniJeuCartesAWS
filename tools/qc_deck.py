#!/usr/bin/env python3
"""QC du deck de questions — à lancer avant chaque commit de contenu.

Usage:
    python3 tools/qc_deck.py [Data/questions_practitioner.json]

Vérifie :
- structure (champs requis, types, 4 réponses, correctIndex dans [0,3])
- doublons exacts (prompt normalisé) et quasi-doublons (similarité >= 0.85)
- explications manquantes, réponses vides ou dupliquées dans une question
- longueurs (prompt/réponses tenant sur une carte)
- distribution par domaine vs poids CLF-C02 (24/30/34/12) et par difficulté

Sortie non-zéro si une erreur bloquante est trouvée (les écarts de
distribution et quasi-doublons sont des avertissements, pas des erreurs).
Stdlib uniquement — aucun paquet à installer.
"""

from __future__ import annotations

import json
import re
import sys
import unicodedata
from collections import Counter
from difflib import SequenceMatcher
from pathlib import Path

DEFAULT_DECK = Path(__file__).resolve().parent.parent / "Data" / "questions_practitioner.json"

REQUIRED_FIELDS = {
    "domain": str,
    "difficulty": int,
    "category": str,
    "prompt": str,
    "answers": list,
    "correctIndex": int,
    "explanation": str,
    "services": list,
    "tags": list,
    "problem": str,
}

VALID_DOMAINS = {"CloudConcepts", "Security", "Technology", "Billing"}
CLF_C02_WEIGHTS = {"CloudConcepts": 0.24, "Security": 0.30, "Technology": 0.34, "Billing": 0.12}

MAX_PROMPT_LEN = 200      # au-delà, la question ne tient plus sur la carte
MAX_ANSWER_LEN = 120
NEAR_DUP_RATIO = 0.85
DISTRIB_TOLERANCE = 0.05  # écart toléré vs poids CLF-C02 avant avertissement


def normalize(text: str) -> str:
    text = unicodedata.normalize("NFKD", text.lower())
    text = "".join(c for c in text if not unicodedata.combining(c))
    return re.sub(r"[^a-z0-9 ]+", " ", text).strip()


def main() -> int:
    deck_path = Path(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT_DECK
    if not deck_path.exists():
        print(f"❌ Fichier introuvable: {deck_path}")
        return 2

    data = json.loads(deck_path.read_text(encoding="utf-8"))
    questions = data.get("questions", [])
    errors: list[str] = []
    warnings: list[str] = []

    print(f"🔎 QC deck: {deck_path.name} — {len(questions)} questions")

    # --- Structure ---
    for i, q in enumerate(questions):
        for field, ftype in REQUIRED_FIELDS.items():
            if field not in q:
                errors.append(f"q[{i}]: champ manquant '{field}'")
            elif not isinstance(q[field], ftype):
                errors.append(f"q[{i}]: '{field}' devrait être {ftype.__name__}")

        if q.get("domain") not in VALID_DOMAINS:
            errors.append(f"q[{i}]: domaine invalide '{q.get('domain')}'")
        if not 1 <= q.get("difficulty", 0) <= 3:
            errors.append(f"q[{i}]: difficulté hors bornes ({q.get('difficulty')})")

        answers = q.get("answers", [])
        if len(answers) != 4:
            errors.append(f"q[{i}]: {len(answers)} réponses au lieu de 4")
        if any(not str(a).strip() for a in answers):
            errors.append(f"q[{i}]: réponse vide")
        if len({normalize(str(a)) for a in answers}) != len(answers):
            errors.append(f"q[{i}]: réponses dupliquées dans la question")
        if not 0 <= q.get("correctIndex", -1) < len(answers or [None]):
            errors.append(f"q[{i}]: correctIndex hors bornes ({q.get('correctIndex')})")
        if not str(q.get("explanation", "")).strip():
            errors.append(f"q[{i}]: explication manquante")

        prompt = str(q.get("prompt", ""))
        if len(prompt) > MAX_PROMPT_LEN:
            errors.append(f"q[{i}]: prompt trop long ({len(prompt)} > {MAX_PROMPT_LEN})")
        for a in answers:
            if len(str(a)) > MAX_ANSWER_LEN:
                errors.append(f"q[{i}]: réponse trop longue ({len(str(a))} > {MAX_ANSWER_LEN})")

    # --- Doublons exacts ---
    seen: dict[str, int] = {}
    for i, q in enumerate(questions):
        key = normalize(str(q.get("prompt", "")))
        if key in seen:
            errors.append(f"q[{i}] doublon exact de q[{seen[key]}]: {q.get('prompt', '')[:70]}")
        else:
            seen[key] = i

    # --- Quasi-doublons (avertissement) ---
    norms = [normalize(str(q.get("prompt", ""))) for q in questions]
    for i in range(len(norms)):
        for j in range(i + 1, len(norms)):
            # pré-filtre grossier sur la longueur pour éviter O(n²) coûteux
            if abs(len(norms[i]) - len(norms[j])) > 30:
                continue
            ratio = SequenceMatcher(None, norms[i], norms[j]).ratio()
            if ratio >= NEAR_DUP_RATIO:
                warnings.append(f"quasi-doublon q[{i}]/q[{j}] ({ratio:.2f}): {questions[i]['prompt'][:60]}")

    # --- Distributions ---
    domains = Counter(q.get("domain", "?") for q in questions)
    difficulties = Counter(q.get("difficulty", 0) for q in questions)
    total = max(1, len(questions))

    print("\n📊 Domaines (vs poids CLF-C02):")
    for d, weight in CLF_C02_WEIGHTS.items():
        n = domains.get(d, 0)
        pct = n / total
        flag = "  "
        if abs(pct - weight) > DISTRIB_TOLERANCE:
            flag = "⚠️ "
            warnings.append(f"distribution {d}: {pct:.0%} vs cible {weight:.0%} (écart > {DISTRIB_TOLERANCE:.0%})")
        print(f"  {flag}{d:14s} {n:4d} ({pct:5.1%})  cible {weight:.0%}")

    print("\n📊 Difficultés:")
    for d in (1, 2, 3):
        n = difficulties.get(d, 0)
        print(f"    d{d}: {n:4d} ({n / total:5.1%})")

    # --- Bilan ---
    print(f"\n{'─' * 50}")
    for w in warnings[:20]:
        print(f"⚠️  {w}")
    if len(warnings) > 20:
        print(f"⚠️  … et {len(warnings) - 20} autre(s) avertissement(s)")
    for e in errors[:50]:
        print(f"❌ {e}")

    if errors:
        print(f"\n❌ QC FAIL — {len(errors)} erreur(s), {len(warnings)} avertissement(s)")
        return 1

    print(f"\n✅ QC PASS — 0 erreur, {len(warnings)} avertissement(s)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
