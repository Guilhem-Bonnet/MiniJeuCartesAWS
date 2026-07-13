# Journal de Décisions — $project_name

> Les agents enregistrent ici les décisions architecturales et techniques significatives.
> Tu peux aussi en ajouter manuellement.

## Format

```
### [YYYY-MM-DD] Titre court de la décision
- **Contexte** : Pourquoi cette décision s'est posée
- **Décision** : Ce qui a été choisi
- **Alternatives rejetées** : Ce qui a été écarté et pourquoi
- **Agent** : Qui a pris/suggéré cette décision
```

## Décisions

### [2026-07-13] Examen blanc V7 : jeu fixe pré-tiré, mode strict, score AWS
- **Contexte** : le mode Examen n'était qu'un Chrono avec timer 60 min ; V7 exigeait une vraie simulation CLF-C02.
- **Décision** : `TimedRunUI.Exam.cs` — pré-tirage de 65 questions (quotas 16/19/22/8 par plus fort reste sur les poids officiels 24/30/34/12), pas de verso pendant l'examen, score linéaire 100+900×(correct/65) avec seuil 700, non-répondues comptées fausses.
- **Alternatives rejetées** : navigation marquer/revenir (hors scope, la god class rend ça coûteux — noté comme amélioration) ; scoring pondéré par domaine façon AWS réel (opaque, non documenté par AWS).
- **Agent** : Claude Code

### [2026-07-13] Persistance : écriture atomique plutôt que debounce
- **Contexte** : sauvegarde des profils à chaque réponse, non atomique → corruption possible = perte totale de l'historique.
- **Décision** : temp+rename (`AtomicWriteText`) + mise de côté du fichier corrompu en `.corrupt` au chargement. Fréquence de sauvegarde conservée.
- **Alternatives rejetées** : debounce/sauvegarde différée — risque de perdre les dernières réponses en cas de crash, pour un gain I/O négligeable (fichier de quelques dizaines de Ko).
- **Agent** : Claude Code

### [2026-07-13] Toolchain Godot retirée du dépôt (pas de rewrite d'historique)
- **Contexte** : ~230 Mo de Godot Mono 4.5.1 versionnés en LFS (`.tools/`), obsolètes depuis la migration 4.6.3.
- **Décision** : `git rm` + `.gitignore` ; l'historique LFS distant garde les objets (pas de `git lfs migrate` / force-push, trop risqué sur master partagé avec releases).
- **Alternatives rejetées** : rewrite d'historique pour récupérer le quota LFS — à envisager plus tard si le quota devient un problème.
- **Agent** : Claude Code
