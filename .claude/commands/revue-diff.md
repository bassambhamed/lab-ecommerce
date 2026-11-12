---
description: Revue critique du diff courant (branche et modifications non commitées)
argument-hint: "[focus optionnel]"
allowed-tools: Bash(git diff *), Bash(git status *), Bash(git log *)
---
## Contexte
- Branche : !`git branch --show-current`
- État : !`git status --short`
- Commits de la branche : !`git log --oneline main..HEAD`
- Diff de la branche par rapport à main : !`git diff main...HEAD`
- Modifications non commitées : !`git diff HEAD`

## Mission
Relis ce diff comme un reviewer exigeant. Tu ne modifies aucun fichier.
Focus demandé : $ARGUMENTS

Cherche en priorité :
- les comportements cassés ou les cas oubliés (valeurs nulles, vides, limites) ;
- les changements de contrat : JSON renvoyé, format des erreurs, DTO copiés dans
  Ordering (`CatalogProduct`) et dans le Web (`ProductDto`, `OrderDto`...) ;
- ce qui sort du périmètre annoncé ;
- l'absence de preuve : quel test ou quelle commande montre que ça marche ?

## Format de sortie
Un tableau `gravité | fichier:ligne | constat | correction proposée`, trié par gravité :
bloquant, majeur, mineur. Termine par un verdict en une ligne : « livrable » ou
« à corriger », et la liste des bloquants. Si tu ne trouves rien, dis-le et explique
ce que tu as vérifié.
