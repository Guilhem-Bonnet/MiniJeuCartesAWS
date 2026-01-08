# Roadmap — MiniJeuCartesAWS

Ce document décrit :
- l’état actuel du jeu (ce qui existe déjà)
- la cible (“où on veut aller”)
- le plan de route (priorités + critères d’acceptation)

## État actuel (V3)

### Gameplay
- Timed run : un chrono, le joueur répond au maximum de questions.
- Tirage pondéré par domaines (CloudConcepts / Security / Technology / Billing).
- Biais difficulté (difficulté 1–3) + questions difficiles dans le deck.
- Après réponse : verso avec correction + explication, puis carte suivante.

### Présentation
- Tabletop 3D “lofi” (table, mur, fenêtre lumineuse, neige légère, lampe chaude).
- Carte format standard (ratio poker 1:1.4), style “collector”.
- Rendu : face/verso via SubViewportTexture + shader (grain/cadre/fond), plus textures procédurales décor.

## Cible (vision)

**Ressenti :** “Je joue à un jeu de société avec de vraies cartes”.

### Cible UX/animation
- Un **deck visible** (pile) sur la table.
- Quand on tire : une carte **sort du deck**, vient au **focus** (lisible, sans cacher tout le décor).
- Après réponse : carte **part** vers une zone de défausse.
- La pile du deck **diminue visuellement**.
- Quand le deck visuel est vide : animation de **mélange** / “reformage du deck” (tout en gardant une logique de questions infinie).
- La partie continue tant que le chrono n’est pas terminé.

### Cible lisibilité
- Texte toujours net (pas de scintillement).
- Pas d’artefacts (z-fighting, aliasing, stretching).

## Plan de route (proposé)

### V4 — Deck physique (priorité)

**But :** matérialiser la pile et la trajectoire des cartes.

Étapes proposées :
1. Ajouter un `DeckRig` sur la table (pile + marqueurs `DeckTop`, `CardFocus`, `DiscardTarget`).
2. Faire évoluer `TimedRunUI.cs` pour piloter l’animation :
   - spawn depuis `DeckTop`
   - tween vers `CardFocus`
   - verso après réponse
   - tween vers `DiscardTarget`
3. Faire “diminuer” la pile :
   - soit scaling vertical
   - soit nombre de meshes (N cartes) qui s’épuise
4. Refill : quand pile vide → animation “shuffle”, puis pile redevient pleine.

**Critères d’acceptation :**
- Le joueur voit clairement une pile sur la table.
- La carte active vient du deck (pas pop au centre).
- Après réponse, la carte quitte le focus.
- La pile descend progressivement.
- Refill visible et compréhensible.

### V5 — Polish lisibilité
- Ajuster FOV/distance caméra pour équilibre décor/lisibilité.
- Ajuster tailles SubViewports si nécessaire.
- Améliorer le shader “collector” (cadre par difficulté plus explicite).

### V6 — Contenu
- Augmenter le volume du deck `questions_practitioner.json` (difficulté 2–3, diversité).
- Ajouter un contrôle qualité (questions dupliquées, réponses ambiguës).

## Risques / pièges
- Godot C# : l’éditeur peut garder une DLL “ancienne” si on ne rebuild pas.
- Z-fighting : s’assurer que face/verso ont un offset suffisant.
- Perf : SubViewports très grands + shaders peuvent coûter (sur machines modestes).

## Définition de “terminé”
- Le jeu est jouable du début à la fin du chrono.
- Le ressenti “cartes physiques” est évident (deck + tirage + discard + refill).
- Lisible en 1920×1080 et résolutions courantes.
