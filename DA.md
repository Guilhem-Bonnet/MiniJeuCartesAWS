# DA — MiniJeuCartesAWS (desktop, offline)

## Intention

Faire ressentir **un jeu de cartes de société** posé sur une table, mais au service d’un entraînement AWS.

Objectifs :
- **Chaleureux / chill** (ambiance “lofi café”)
- **Collector sombre** (cartes premium, cadre, grain papier)
- **Lisible** à distance caméra (texte net, contraste stable)
- **Rythmé** (timed run) sans être agressif

## Périmètre technique

- Desktop (Linux/Windows)
- Godot 4.5.1 Mono (C#)
- Offline : aucun backend
- Deck JSON + génération procédurale de textures (pas d’assets externes requis)

## DA (style)

### Décor

- Table sombre (grain “bois” procédural)
- Mur / arrière-plan froid-bleuté (grain léger)
- Fenêtre lumineuse + neige légère
 - vue sur une lune et des étoiles
 - petits évènement en gif ou micro animations comme une étoile filante ou des aliens sur la lune (rempli de mini ref)
- Lampe de bureau chaude (pulsation subtile)
- Tasse à café avec fumée montante
  - café diminu au rythme du chrono (subtil)
- écran de pc en bout de bureau
  - Parfois s'allume, on y fais passé des petits évènements aléatoire comme des lignes de code, des petits gif (toujours des petites refs sympas subtil chill et qui ne focus pas l'attention (idée: feu de cheminée))
- un chat posé qui regarde la fenetre de dos
- un pot de croyons de couleurs
- un pot de plante (micro animation de tant en tant pour rendre vivant)


### Carte “collector”

- Format “poker” (ratio 1:1.4)
- Corps de carte avec bords arrondis (bevel)
- Face : question + 4 réponses imprimées sur la carte
- Verso : correction + explication (retournement de carte)

Ref : Inscryption, Heart stone, Pokemon TGC
### Différence par difficulté

- Facile : rendu “papier clair”
- Difficile : rendu “sombre collector”

## UX (principes)

- Le joueur doit avoir l’impression de **tirer une carte**, pas de cliquer une UI.
- Hiérarchie : Domaine → Question → Réponses → Correction.
- Contraste stable : pas de couleurs fluo, pas d’écrans qui clignotent.
- Feedback immédiat, lisible en 1 seconde.

## Contraintes (assets)

- Préférence : matériaux simples + textures procédurales + shader léger.
- Blender peut être utilisé si disponible, sinon fallback “procedural + CSG”.

## Fichiers clés (V3)

- `Scenes/Main3D.tscn` (tabletop 3D)
- `Scripts/TimedRunUI.cs` (timed run + rendu face/verso)
- `Data/questions_practitioner.json` (deck)


## Explication jeu 

le joueur est assis sur son bureau dans une ambiance propice a l'apprentissage (chill)
il y a une musique tranquille en fond (playlist lofi girl sur youtube ou autre musique en jeu)
un deck de carte est posé sur la table (taille normale d'un jeu de carte)
un menu nous demande si on est pret ou non (pas trop intrusif et intégré à l'ambiance dans le design)
(pour le jeu de carte la première est juste une couverture stylisé pour signifié le début du deck et pas juste du blanc)
on voit la première carte venir devant la caméra en bas de l'écran comme la pluspart des jeux de carte pc, il faut qu'on puisse bien lire la question
le format de l'écran doit avoir bien été pris en compte, pour avoir une bonne résolution et pas de positionement étrange
le joueur choisi un réponse, la carte se retourne et montre sur le recto la bonne réponse avec une explication du choix du joueur et pourquoi il fallait prendre la bonne réponse ou juste une explication de la réponse si c'est juste
évidemment on voit le score et le chrono dans un coin de l'écran mais rien ne doit etre stressant au contraire un cherche une atmosphère paisible et calme, parfait pour apprendre et mémorisé
les cartes faite sortent de l'écran avec une animation qui les amène sur le coté oposé à la pioche

le deck diminu au fur et a mesure
comme le jeu est infini si le deck ne contient plus de carte une mini animation se fait où on vois les cartes mises de coté etre mélanger pour refaire la pioche
ça permet un aspect réaliste et imersif en justifiant le coté infini meme si les questions ne sont pas les memes
une fois le chono fini on vois notre score avec un petit post-it nous disant nos faiblesses notre note sur quoi on doit se focus et si on est pret pour passé la certification
ensuite on peut appuyé sur rejouer
le deck et remélanger et la partie recommence

Les question et les réponses doivent toujours changé pour éviter le par-coeur
un nombre conséquent de questions doivent etre présent
des pièges ou questions complexe doivent aussi faire parti du jeu
les points des cartes ne sont pas les memes en fonction de la difficulté de la question
