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

### [2026-07-13] V8 : rééquilibrage à taille constante + liaison leçons sans lessonId
- **Contexte** : Technology à 55 % (cible CLF-C02 : 34 %) ; roadmap exigeait un chemin question ratée → leçon.
- **Décision** : retrait de 96 Technology (quasi-doublons → templates « contrainte principale »/« mot-clé ici » → redondances intra-catégorie, avec garde-fou : aucun service AWS ne perd toute couverture) + ajout de 96 questions générées (45 CC, 41 Sec, 10 Billing, orientées d1/d2). Liaison leçons par **matching services normalisés** à l'exécution (préfixes AWS/Amazon retirés), repli tags/catégorie — pas de champ lessonId dans les données. Touche L sur le verso.
- **Alternatives rejetées** : ajouter sans retirer (deck à 729, dilution) ; champ lessonId explicite (migration de 448 questions + maintenance double).
- **Agent** : Claude Code

### [2026-07-13] V9 : deck SAA-C03 réutilise les 4 domaines moteur
- **Contexte** : SAA-C03 a ses propres domaines (Secure/Resilient/High-Performing/Cost-Optimized) mais le moteur (tirage pondéré, stats, examen) est câblé sur CloudConcepts/Security/Technology/Billing.
- **Décision** : mapper le contenu SAA sur les 4 buckets existants (Security=secure design, Technology=résilience+perf, Billing=coûts, CloudConcepts=principes de design). 150 questions d2-d3, deckId `aws-saa-v1`.
- **Alternatives rejetées** : domaines SAA natifs (refactor DomainWeights/NormalizeDomain par certification — à faire si une 3e certif l'exige). Seuil examen 720 spécifique SAA : reporté.
- **Agent** : Claude Code

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
