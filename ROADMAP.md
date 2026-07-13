# Roadmap — MiniJeuCartesAWS

Ce document décrit :
- l'état actuel du jeu (ce qui existe déjà)
- la cible ("où on veut aller")
- le plan de route (priorités + critères d'acceptation)

## État actuel (juillet 2026)

### Gameplay
- 4 modes de jeu en place : **Chrono** (timed run), **Infini**, **Examen** (60 min), **Renforcement** (déblocable, tirage boosté sur les questions ratées via `WrongStreak`).
- Tirage pondéré par domaines (CloudConcepts / Security / Technology / Billing) + biais difficulté (1–3).
- **Deck physique** : pile visible, tirage depuis le deck, défausse, animation de shuffle au refill.
- **Profils joueurs** : stats par question (Asked/Correct/streaks), achievements, meilleurs scores.
- Style de carte par difficulté : d1 papier clair, d2 bleuté, d3 sombre "collector" texte or.
- Architecture multi-certifications prête (`Certifications[]`, sélecteur dans le menu) — un seul deck branché (CCP FR).

### Contenu
- **448 questions** (0 doublon, QC passé) + **93 leçons** dans `Data/`.
- Répartition domaines : Technology 248 (55 %), Security 93 (21 %), CloudConcepts 63 (14 %), Billing 44 (10 %).
- Poids de l'examen CLF-C02 pour comparaison : Technology 34 %, Security 30 %, CloudConcepts 24 %, Billing 12 %.

### Présentation
- Tabletop 3D "lofi", carte ratio poker 1,4:1.
- Lisibilité vérifiée en jeu (juillet 2026) : viewports 2867×2048 ratio exact, MSAA 2D 4x, texte net. Rien à corriger.

### Distribution
- v1.0.0 publiée (janvier 2026) : Windows, Linux, macOS.
- ⚠️ Dernier run CI "Build macOS" en échec (cause inconnue, logs expirés).

## Plan de route

### V6.5 — Hygiène & fondations ✅ (fait, juillet 2026)

1. ✅ `git-lfs` installé (3.7.1) — `git status`/`diff` fiables.
2. ✅ Travail en attente committé (doc + CI en commits séparés).
3. ✅ Fichiers parasites supprimés.
4. ✅ CI corrigée : Godot 4.6.3 + .NET 9 (le dernier échec venait du moteur 4.5.1 sur un projet 4.6 et du conflit global.json 9.0 vs setup-dotnet 8.0). Reste à valider sur un tag de test.
5. ✅ `REGLES_DU_JEU.md` à jour (4 modes + profils).

Bonus fait dans la foulée : persistance atomique des profils/settings (+ backup `.corrupt`), purge du code mort (`Signals.cs`, caméra auto, ~130 lignes), toolchain Godot 4.5.1 retirée du LFS (~230 Mo), scripts `tools/*.sh` réparés (godot-mono résolu dynamiquement).

### V7 — Examen blanc complet ✅ (fait, juillet 2026)

Implémenté dans `Scripts/TimedRunUI.Exam.cs` :

1. ✅ Jeu **fixe de 65 questions sans répétition**, quotas par domaine selon les poids officiels (24/30/34/12 → 16/19/22/8).
2. ✅ Mode strict : pas de correction pendant l'examen, enchaînement sans verso, HUD "Question x/65". (Navigation marquer/revenir : non fait, amélioration possible.)
3. ✅ Écran de résultats : score 100–1000 (seuil 700, REÇU/RECALÉ), répartition par domaine, questions ratées avec la bonne réponse (les non-répondues comptent fausses).
4. ✅ Les examens sont enregistrés dans l'historique du profil (`RunRecord.GameMode`). (Vue dédiée de progression : à faire.)

### V8 — Contenu : équilibrage & liaison leçons (fil rouge, par lots)

1. Rééquilibrer vers les poids CLF-C02 : ajouter par lots ~40 CloudConcepts, ~30 Security, ~20 Billing (objectif intermédiaire : Technology < 45 %).
2. Script de contrôle qualité réutilisable (`tools/qc_deck.py`) : doublons exacts/proches, structure, distributions — à lancer avant chaque commit de contenu.
3. Lier questions ↔ leçons : depuis le verso d'une question ratée, bouton "voir la leçon" (les leçons existent déjà dans `course_practitioner.json`).

**Critères d'acceptation :** QC automatisé passant ; chaque question ratée offre un chemin vers la leçon correspondante.

### V9 — Deuxième certification

L'architecture est prête (`Certifications[]` dans `TimedRunUI.Modes.cs`) : ajouter un deck **AWS Solutions Architect Associate (SAA-C03)** — commencer petit (~150 questions d2–d3) et réutiliser le QC de V8.

**Critères d'acceptation :** le sélecteur du menu propose 2 certifications fonctionnelles avec stats séparées par deck.

### V10 — Release & distribution

1. Tag **v1.1.0** (448 questions + refactor + examen blanc) une fois V6.5 et V7 faits.
2. Page itch.io (gratuit) pour la visibilité — décision à valider.
3. Contrainte connue : **pas d'export web possible** (Godot 4 + C# ne supporte pas l'export HTML5) ; le web impliquerait un portage GDScript, hors scope.

## Dette technique (fil rouge, opportuniste)

- `TimedRunUI` est une god class (~51 Ko + 14 fichiers partiels). Extraction progressive, en commençant par la logique pure testable : tirage du deck / pondération / scoring → classes sans dépendance Godot + tests `dotnet test`.
- Code mort : `FrameCameraForReadability()` / `AutoFrameCameraOnReady` (jamais activé) — supprimer ou documenter.
- CI : ajouter un job léger sur push (dotnet build + validation JSON du deck) pour attraper les régressions avant les tags.

## Risques / pièges
- Godot C# : l'éditeur peut garder une DLL "ancienne" si on ne rebuild pas.
- git-lfs absent sur une machine = `git status` mensonger (vu en juillet 2026).
- Perf : SubViewports très grands + shaders peuvent coûter sur machines modestes.

## Définition de "terminé" (jalon en cours : V7)
- Un candidat CCP peut s'entraîner (Chrono/Renforcement) **et** se tester (Examen blanc) dans le jeu.
- Il sait, chiffres à l'appui, s'il est prêt pour le vrai examen.
