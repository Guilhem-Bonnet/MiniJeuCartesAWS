# MiniJeuCartesAWS (Godot desktop, offline)

Mini-jeu de cartes pour s'entraîner à la certification **AWS Certified Cloud Practitioner**.

État actuel : **V3** (timed run 3D, cartes “collector”, face/verso, offline).

- Menu “Prêt ?” : bouton **Paramètres** (audio/volumes/durée) + bouton **Quitter**.
- Les paramètres sont persistés en local (dans `user://mini_jeu_cartes_aws_settings.json`).

- Direction artistique / intentions : `DA.md`
- Règles du jeu : `REGLES_DU_JEU.md`
- Roadmap : `ROADMAP.md`
- Guide pour IA (architecture/paths/tests) : `IA_CONTEXT.md`
- Doc d'évolution (future) : `DOC_EVO_SCRIPT_CARTE.md`

## Lancer (éditeur)

1. Ouvrir Godot 4.x **Mono**
2. Importer ce dossier (`MiniJeuCartesAWS`)
3. Scène principale : `Scenes/Main3D.tscn`

Si tu ne vois pas les derniers changements côté scripts C#, fais :
`Project > Tools > C# > Build` (ou *Build Solution*) puis relance.

## Lancer (CLI)

- Build C# : `dotnet build`
- Smoke test headless : `/home/guilhem/bin/godot --headless --quit --path .`
- Run fenêtré court : `/home/guilhem/bin/godot --path . --quit-after 240`

## Données (deck de questions)

- Deck : `Data/questions_practitioner.json`
- Champs attendus : `domain`, `difficulty`, `category`, `prompt`, `answers[4]`, `correctIndex`, `explanation`

## Structure

- Scène 3D principale : `Scenes/Main3D.tscn`
- Script principal (timed run) : `Scripts/TimedRunUI.cs`

