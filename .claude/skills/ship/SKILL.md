---
name: ship
description: Livre la branche courante de bout en bout. Vérifie (build, tests, format), relit le diff, committe, pousse et ouvre une PR documentée avec le gabarit de l'équipe.
argument-hint: "[titre de la PR]"
disable-model-invocation: true
allowed-tools: Bash(git *), Bash(gh *), Bash(dotnet *), Bash(bash *.claude/skills/ship/verifier.sh)
---
# Livrer la branche courante

## État actuel
- Branche : !`git branch --show-current`
- Changements non commités : !`git status --short`
- Derniers commits : !`git log --oneline -5`

## Titre demandé
$ARGUMENTS

## Étapes : s'arrêter au premier échec

1. **Refuser** si la branche courante est `main` : proposer un nom de branche
   (`fix/...`, `feat/...`) et s'arrêter.
2. **Vérifier** avec le script du skill, qui enchaîne build, tests et contrôle du format
   sur les fichiers modifiés :
   ```bash
   bash ${CLAUDE_SKILL_DIR}/verifier.sh
   ```
   Montrer sa sortie. En cas d'échec, corriger puis relancer ; ne jamais affaiblir ou
   supprimer un test pour le faire passer.
3. **Relire** le diff complet de la branche (`git diff main...HEAD` et les modifications
   non commitées) comme un reviewer exigeant. Corriger les bloquants, lister les autres
   points dans la PR.
4. **Committer** les changements restants avec un message conventionnel
   (`fix:`, `feat:`, `test:`, `docs:`, `chore:`), en anglais, qui dit pourquoi.
5. **Pousser** : `git push -u origin <branche>`. La permission `ask` du projet demande
   confirmation : c'est voulu.
6. **Ouvrir la PR** avec `gh pr create`, le titre demandé (ou un titre conventionnel) et
   un corps rédigé à partir du gabarit [modele-pr.md](modele-pr.md) : chaque rubrique
   remplie avec des faits (commandes lancées, résultats), aucune laissée vide.

## Restitution
L'URL de la PR, les commandes lancées et leur résultat, et les points laissés ouverts.

## Interdits
- Jamais de `git push --force`, de `git reset --hard` ni de réécriture d'historique.
- Jamais de fusion de la PR : la décision revient à un humain.
