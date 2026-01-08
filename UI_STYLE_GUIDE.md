# Guide UI — MiniJeuCartesAWS

## Règle d’or
Lisible en 1 seconde, sans overlay intrusif.

## Répartition HUD vs Carte
HUD (CanvasLayer)
- Score (post-it), timer, domaine: informations persistantes.
- Doit rester lisible même pendant le flip.

Carte (SubViewport)
- Question + réponses: uniquement du contenu “imprimé sur la carte”.
- Feedback: sur le recto, par option (“Bonne” / “Mauvaise”) à droite.

## Feedback (recto)
- Quand une réponse est choisie:
  - La bonne option affiche “Bonne” en vert.
  - Si la réponse est fausse, l’option choisie affiche “Mauvaise” en rouge.
- Ne pas recolorer toute la barre (on garde les teintes de base). Le feedback est textuel.
- Le hover ne doit plus s’appliquer après réponse.

## Hover
- Discret: léger éclaircissement + alpha.
- Jamais plus fort que le feedback de résultat.

## Lisibilité
- Contraste: le texte doit rester lisible en facile/moyen/difficile.
- Verso: ne pas réutiliser la couleur de texte du recto si elle dépend de la difficulté.

## Ressources existantes à privilégier
- `Resources/CardUI/LabelSettings_*.tres`
- `Resources/CardUI/StyleBoxFlat_AnswerBar*.tres`
- `Resources/CardUI/StyleBoxFlat_PostIt.tres`
