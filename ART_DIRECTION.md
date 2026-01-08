# Direction artistique — MiniJeuCartesAWS (tabletop premium)

Date: 2026-01-07

## Objectif
Passer de “fonctionnel” à “tabletop premium” : lisible en 1 seconde, cohérent, calme (pas néon), et avec une sensation de carte/objets sur table.

Priorités UX (ordre)
1) Lisibilité (recto + verso) à distance caméra.
2) Feedback immédiat (bonne/mauvaise) sans overlay intrusif.
3) Cohérence (palette + typo + espacements).
4) “Premium feel” (papier, grain, dorure travaillée) sans effets parasites.

## Décisions verrouillées
1) Post-it score: HUD, toujours visible.
2) Carte sombre: variante “bleu nuit” (moins dure).
3) Typographies: Cinzel (titres) + Inter (texte).

## Moodboard (texte / intentions)
Mots-clés
- “cozy tabletop night desk”, “warm lamp”, “paper grain matte”, “collector card minimal frame”, “foil stamping subtle”, “post-it HUD”.

À rechercher (sans copier de visuels propriétaires)
- Poly Haven / ambientCG (CC0) : papier, bois, grain.
- Références “foil stamping” : dorure mate, légère accroche de lumière (pas miroir).

## Règles UI
HUD
- Le HUD sert aux infos persistantes (score / timer / domaine) + repères.
- Le post-it HUD doit être discret, lisible, et ne pas voler l’attention à la carte.

Carte
- Tout ce qui est “contenu de question” est imprimé sur la carte (SubViewport).
- Éviter les effets “cadre / vignette / halo” qui parasitent la lecture.

Feedback
- Sur le recto, afficher explicitement le résultat par option: “Bonne” / “Mauvaise” (et éventuellement ✅/❌) à droite.
- Conserver des teintes de base cohérentes pour les barres; éviter de recolorer toute la ligne (fatigue visuelle).

## Palette (cibles)
Neutres
- Texte clair: #E9EEF7
- Texte sombre (sur papier): #141820
- Fond bleu nuit: #0F1118 → #121725 (selon lumière)

Accents (feedback)
- Correct: #2EE59C
- Faux: #FF4D5A

Or (dorure)
- Champagne gold: #D9B45A (à utiliser en petites touches, titres/trim)

## Typographies (à intégrer)
Cinzel (titres) + Inter (texte) — licences OFL.
Emplacement conseillé:
- Assets/Fonts/Cinzel/
- Assets/Fonts/Inter/
- Ajouter un ASSET_SOURCES.md qui recense versions et licences.

## Backlog (prochaines itérations)
1) Intégrer réellement Cinzel/Inter (FontFile/LabelSettings) et homogénéiser les tailles.
2) Retravailler la dorure: pas juste une couleur, mais un rendu subtil (micro-variation + léger spéculaire ciblé) sans tomber dans le miroir.
3) Harmoniser le verso: mêmes marges/typo que le recto, hiérarchie claire (titre, choix, explication, CTA).
4) Calmer les couleurs des réponses: saturation plus basse, hover plus discret.
