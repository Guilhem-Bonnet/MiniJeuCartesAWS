# Règles du jeu — MiniJeuCartesAWS

## Objectif pédagogique

S'entraîner pour la certification **AWS Certified Cloud Practitioner** avec un format rapide et répétable.

- Comprendre à quoi servent les services AWS les plus courants
- Savoir associer un besoin → le bon service
- Renforcer la mémorisation via des explications courtes

## Principe

Jeu solo offline. Quatre modes de jeu :

| Mode | Règle |
|------|-------|
| **Chrono** | Répondre au maximum de questions avant la fin du chrono (durée réglable) |
| **Infini** | Sans chrono, pour réviser tranquillement |
| **Examen** | Simulation type examen (temps limité long) |
| **Renforcement** | Déblocable — le tirage privilégie les questions ratées récemment |

Les profils joueurs enregistrent les stats par question (réussites, séries d'erreurs), les meilleurs scores et les succès.

Boucle (ressenti “jeu de cartes”) :
1. Une carte apparaît face avant avec une question et 4 réponses
2. Le joueur choisit (clavier 1–4)
3. La carte se retourne (verso) et affiche la correction + explication
4. La partie enchaîne sur la carte suivante

## Score

- `Total` : nombre de questions répondues
- `Correct` : nombre de bonnes réponses

Le score affiché est : Correct/Total.

On affiche aussi une répartition par domaine en fin de run.

## Fin de partie

La partie se termine quand le **chrono** arrive à zéro.

Le système de questions est **infini** (réutilisation / tirage pondéré) : on ne “vide” pas définitivement le deck.

## Persistance (offline)

Le jeu est offline : les données sont locales.

Note : à ce stade, la persistance historique n’est pas l’objectif principal (on privilégie le run).

## Règles de conception (simplicité)

- Tout doit être jouable au clavier (1–4).
- Feedback immédiat (correct/faux + explication).
- Lisibilité avant tout (texte sur la carte, contrastes).
- Pas de punition “hard” : l’erreur sert l’apprentissage.

## Roadmap

Voir `ROADMAP.md` (V7 : examen blanc complet ; V9 : packs par certification). Le deck physique et la révision ciblée (mode Renforcement) sont déjà en place.
