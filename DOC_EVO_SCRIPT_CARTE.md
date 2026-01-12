# Évolution: extraire la “Card” dans un script dédié (Godot 4.5.1 Mono / C#)

Objectif: sortir progressivement tout ce qui concerne **la carte 3D** (rendu, SubViewport, matériaux, animations, micro-motion, hit-test/clic) de `TimedRunUI` vers un script dédié, **sans changer le gameplay**.

Contrainte: garder `TimedRunUI` comme orchestrateur (run/timer/score/questions), et déplacer le “comment la carte se comporte” dans un composant réutilisable.

> État actuel: la découpe se fait via `partial class TimedRunUI` (ex: `TimedRunUI.Input.cs`, `TimedRunUI.UI.cs`, `TimedRunUI.Materials.cs`, `TimedRunUI.AnimationsTweens.cs`, `TimedRunUI.Audio.cs`).
> Cette doc décrit une évolution ultérieure vers un script séparé.

---

## Pourquoi un script dédié “Card”

- **Lisibilité**: `TimedRunUI` décrit *quand* on change d’état; la carte décrit *comment* elle s’affiche/s’anime.
- **Couplage réduit**: la carte n’a pas besoin de connaître les tokens de run, les stats, etc.
- **Réutilisable**: possibilité de faire une `Card.tscn` plus tard.
- **Debug plus simple**: on isole les bugs de rendu/animation/clic.

---

## Architecture cible

### TimedRunUI (orchestrateur)
Responsabilités conservées:
- Timer / scoring / stats par domaine
- Sélection de question + progression (NextQuestion, fin de run)
- UI hors carte (boutons, top row, panels)
- Audio global si souhaité (ou délégué plus tard)

### CardView (nouveau script)
Responsabilités déplacées:
- Références de nœuds *de la carte* (rig, meshes, viewports, colliders)
- Rendu: matériaux/shader de la carte, textures procédurales
- Animations: draw / flip / discard / shuffle / idle / micro-motion
- Input local: raycast/clic et mapping vers index de réponse

Communication:
- `TimedRunUI` appelle des méthodes “haut niveau” de `CardView`.
- `CardView` émet des signaux/événements en retour (`AnswerClicked`, `ContinueClicked`).

---

## API proposée (minimaliste)

Créer un script C# `Scripts/CardView.cs` attaché au nœud “CardRig” (ou un nœud parent de la carte).

### Signaux
- `AnswerClicked(int answerIndex)`
- `ContinueClicked()` (ou un `CardClicked()` + la logique “continue” dans TimedRunUI)

### Méthodes (exemples)
- `InitializeLook()` : construit textures procédurales + matériaux (actuellement dans `TimedRunUI.Materials.cs`)
- `SetFrontContent(domain, timer, score, question, answers...)` : alimente le SubViewport recto
- `SetBackStatePending()` / `SetBackStateResult(bool correct)`
- `AnimateDrawFromDeck(...)`, `AnimateFlipToBack(...)`, `AnimateFlipToFront(...)`, `AnimateSendAway(...)`
- `SetInteractionEnabled(bool enabled)`

Note: on n’est pas obligé de tout faire d’un coup. L’API peut commencer petite.

---

## Stratégie de migration (incrémentale, safe)

Principe: **déplacer d’abord des blocs autonomes**, compiler, valider dans Godot, puis continuer.

### Étape 0 — Préparation (aucun changement fonctionnel)
- Identifier la hiérarchie de nœuds côté carte dans `Scenes/Main3D.tscn`.
- Choisir l’endroit où attacher `CardView`:
  - Option A (simple): attacher sur le nœud déjà utilisé comme rig (`_cardRig`).
  - Option B (propre): créer un nœud `Card` parent et y attacher le script.

### Étape 1 — Extraire “Look/Materials” (faible risque)
Candidat idéal: tout le contenu déjà isolé dans `Scripts/TimedRunUI.Materials.cs`:
- `BuildProceduralTextures()`
- `CreateNoiseTexture(...)`
- `ApplyViewportToCardMaterials()`
- `ApplySceneMaterials()` (si on veut aussi le décor)
- `AnimateAmbientLight()` (si la lampe est considérée “look”) 

Action:
- Copier ces méthodes dans `CardView`.
- Remplacer leurs accès aux champs par:
  - des `NodePath` exportés, ou
  - des `GetNode<T>("...")` depuis le nœud de la carte.
- Dans `TimedRunUI`, remplacer l’appel direct par `cardView.InitializeLook()`.

Validation:
- `dotnet build`
- lancer la scène: la carte doit rendre pareil.

### Étape 2 — Extraire Animations Tweens (moyen risque)
Candidat: `Scripts/TimedRunUI.AnimationsTweens.cs`.

Action:
- Déplacer l’état d’animation de carte dans `CardView` (ex: `_cardTween`, `_cardIsBackSide`, ancre idle, etc.).
- Exposer des méthodes d’animation.
- Garder les timings identiques.

Validation:
- flips/draw/discard identiques visuellement
- aucune régression de “continue click”/lock interaction

### Étape 3 — Extraire Input/hit-test (moyen risque)
Candidat: `Scripts/TimedRunUI.Input.cs`.

Action:
- Déplacer le raycast + mapping `TryMapHitToAnswerIndex` dans `CardView`.
- Émettre `AnswerClicked(answerIndex)`.
- `TimedRunUI` se contente de:
  - écouter l’événement
  - appeler la logique de choix (`ChooseAnswer(answerIndex)` ou équivalent)

Validation:
- clic sur réponses fonctionne au pixel près
- pas de double-clic / pas d’input pendant animation (si c’était le cas avant)

### Étape 4 — Extraire le contenu SubViewport (UI “dans la carte”)
Candidat: parties de `Scripts/TimedRunUI.UI.cs` qui alimentent les labels/boutons du SubViewport.

Action:
- `CardView` possède les références vers les labels/boutons du SubViewport.
- `TimedRunUI` lui passe un modèle simple (question + réponses + état).

---

## Implémentation: recommandations Godot C#

### Références de nœuds
Deux approches:

1) **NodePath exportés** (le plus stable à long terme)
- Dans `CardView`, déclarer des `[Export] public NodePath CardFrontViewportPath { get; set; }` etc.
- Assigner dans l’inspecteur.

2) **GetNode par chemin** (rapide mais plus fragile)
- `GetNode<SubViewport>("FrontViewport")`.

Recommandation: NodePath exportés pour les nœuds critiques (meshes/viewports/collider).

### Événements
En C#, on peut utiliser:
- `public event Action<int>? AnswerClicked;`
- ou `[Signal] public delegate void AnswerClickedEventHandler(int answerIndex);`

Les signaux Godot sont pratiques pour brancher aussi en GDScript si besoin.

### Isolation des données
Éviter que `CardView` connaisse:
- `_runToken`, `_questionToken`, `_domainStats`, etc.

Préférer des entrées claires:
- `CardView.ShowQuestion(Question q, IReadOnlyList<string> answers, ...)`.

---

## Checklist de validation (à chaque étape)

- `dotnet build` OK
- Dans Godot: aucune erreur dans la console
- Flip/draw/shuffle/discard identiques
- Clic sur réponses identique (zones, latence, désactivation pendant animations)
- Run complet: score/timer inchangés

---

## Quand envisager une `Card.tscn`

Quand `CardView` devient stable, on peut:
- extraire la hiérarchie “carte” en scène réutilisable `Scenes/Card.tscn`
- instancier depuis `Main3D.tscn`
- exposer un `CardView` public, plug-and-play.

C’est une évolution “plus tard” (pas nécessaire pour le gain de lisibilité immédiat).
