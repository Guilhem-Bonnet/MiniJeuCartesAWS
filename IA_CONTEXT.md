# IA_CONTEXT — MiniJeuCartesAWS

But : fournir un maximum d’informations “actionnables” à une IA (ou un dev) pour continuer le projet sans re-découvrir l’architecture.

> Contrainte de travail : ne modifier que ce dossier `MiniJeuCartesAWS`.

## Résumé du jeu

Mini-jeu offline de cartes pour réviser AWS Cloud Practitioner.

- Mode actuel : **V3** “timed run” 3D
- Entrées : clavier 1–4
- Sortie : score + répartition par domaine en fin de run

## État technique

- Moteur : Godot 4.5.1 Mono
- Langage : C# (.NET via Godot)
- Données : JSON (deck) + textures procédurales runtime
- Plateforme : desktop

## Fichiers clés

- `project.godot`
  - scène principale : `res://Scenes/Main3D.tscn`
  - fenêtre : 1920×1080, resizable, stretch `canvas_items`, aspect `keep`

- `Scenes/Main3D.tscn`
  - scène 3D tabletop + carte + HUD minimal

- `Scripts/TimedRunUI.cs`
  - logique du timed run + tirage pondéré + rendu carte face/verso + FX

- `Data/questions_practitioner.json`
  - deck CCP

- Legacy (V1) : `Scenes/Main.tscn`, `Scripts/MainUI.cs`

## Arborescence de scène (Main3D)

Les NodePaths sont importants car `TimedRunUI.cs` fait des `GetNode<>()`.

### Racine
- `Main3D` (Node3D)
  - `WorldEnvironment`
  - `Sun` (DirectionalLight3D)
  - `Camera` (Camera3D)
  - `Set` (Node3D)
    - `Table` (MeshInstance3D)
    - `BackWall` (MeshInstance3D)
    - `Window` (MeshInstance3D)
    - `WarmLamp` (OmniLight3D)
    - `Snow` (GPUParticles3D)
    - `Mug`, `Cat` (MeshInstance3D)
  - `CardRig` (Node3D)
    - `CardBody` (CSGBox3D) — corps de carte avec bevel
    - `CardFace` (MeshInstance3D) — face (plane)
    - `CardBack` (MeshInstance3D) — verso (plane)
    - `CardFrontViewport` (SubViewport)
      - `Face/...` (UI 2D de la face)
        - `FaceDomain` (Label)
        - `FaceQuestion` (Label)
        - `FaceAnswers/FaceA..FaceD` (Label)
    - `CardBackViewport` (SubViewport)
      - `Back/.../BackContent` (RichTextLabel)
    - `Sparkles` (GPUParticles3D)
  - `HUD` (CanvasLayer)
    - `Root` (Control) **script = TimedRunUI.cs**
      - `TopHUD/TopRow/(Domain|Timer|Score)`
      - `Margin/Center/CardPanel/...` (panneau de résumé fin de run)

## Données : format du deck

Chemin : `res://Data/questions_practitioner.json`

Champs (par question) :
- `domain` : `CloudConcepts` | `Security` | `Technology` | `Billing`
- `difficulty` : 1..3
- `category` : string libre (tags)
- `prompt` : string
- `answers` : tableau de 4 strings
- `correctIndex` : 0..3
- `explanation` : string

Validation runtime actuelle (dans `LoadDeck()`):
- réponses = 4
- correctIndex ∈ [0..3]
- prompt non vide

## Gameplay : logique actuelle (TimedRunUI)

- **Timed run** : `TimeLimitSeconds` (export)
- `TryDrawQuestionWeighted(out Question)` tire une question pondérée par domaine
- Mélange des options à chaque question (`BuildShuffledOptions`)
- Après réponse :
  - maj score
  - FX (sparkles / shake)
  - verso: correction + explication
  - retour face avant + carte suivante

## Visuel : matériaux & shaders

- Face/verso : SubViewportTexture appliquée sur des planes (`CardFace` / `CardBack`).
- Shader "collector" (dans C#) :
  - mélange texture de SubViewport + fond (gère alpha)
  - grain papier (NoiseTexture2D)
  - vignette + cadre (frame_color)
  - style dépendant de difficulté (papier clair vs sombre)

- Décor : textures procédurales appliquées à `Table` et `BackWall`.

## Tests / commandes utiles

- Build : `dotnet build`
- Headless : `/home/guilhem/bin/godot --headless --quit --path .`
- Fenêtré court : `/home/guilhem/bin/godot --path . --quit-after 240`

## Pièges fréquents

- Godot C# peut lancer une DLL non à jour si l’éditeur n’a pas rebuild.
  - Action : `Project > Tools > C# > Build`.

- Warnings Vulkan/RADV : souvent non bloquants.
  - Alternative : `--rendering-driver opengl3` pour tester.

- Z-fighting : face/verso trop proches du corps de carte.
  - Action : augmenter l’offset Z des planes.

## Outils disponibles (dans ce repo)

- Godot 4.5.1 Mono
- .NET `dotnet` (build)
- Pas de Blender (à vérifier selon machine; sur cet environnement la commande n’était pas trouvée).

## Roadmap immédiate (deck physique)

Voir `ROADMAP.md`.

Points importants pour une IA :
- Ajouter un `DeckRig` + marqueurs dans `Main3D.tscn`.
- Ne pas casser les NodePaths existants utilisés par `TimedRunUI.cs`.
- Faire des animations (tweens) sans que l’idle ne les écrase (mécanisme `SuppressIdle`).

## Style de contribution

- Changements ciblés et testables.
- Éviter d’ajouter des dépendances externes (assets/licences).
- Préférer : procédural + shaders + géométrie simple (CSG/Primitive).
