---
name: codescene-sante
description: Santé du code de la branche courante selon le référentiel local codescene/code-health-rules.json. Lance l'agent codescene-sante (scan local, correction, build/tests, re-scan jusqu'à zéro bloqueur), refait un scan final indépendant et produit un rapport HTML. Aucun service externe, aucun commit.
argument-hint: "[base, défaut main]"
disable-model-invocation: true
allowed-tools: Bash(git *), Bash(dotnet build *), Bash(dotnet test *), Bash(dotnet run --no-build .claude/tools/sante-locale/scan.cs *), Bash(node .claude/skills/codescene-sante/rapport.mjs *), Bash(date *), Bash(mkdir -p artifacts/codescene/*), Write(./artifacts/codescene/**)
---
# Santé du code de la branche (référentiel local)

Conception : `codescene/agent-codescene.md`. Analyse 100 % locale : `.claude/tools/sante-locale/scan.cs` applique le
référentiel `codescene/code-health-rules.json` (lignes, complexité, imbrication, conditions complexes par méthode).
Bloqueur = violation absente de la base ou qui a empiré : la dette déjà présente sur la base ne bloque pas.

## État actuel
- Branche : !`git branch --show-current`
- Fichiers C# modifiés vs main : !`git diff --name-only main...HEAD -- '*.cs'`
- Non commités : !`git status --short -- '*.cs'`

## Base demandée
$ARGUMENTS (vide = `main`). Choisir la branche dont la branche courante est partie : sinon, le travail non encore
fusionné de cette branche parente est compté aussi.

## Étapes : s'arrêter au premier échec

1. **Refuser sur la base elle-même** (ou sur `main`) : proposer une branche et s'arrêter.
2. **Compiler l'analyseur** : `dotnet build .claude/tools/sante-locale/scan.cs` (hors ligne, Roslyn en cache).
3. **Scan initial** : `dotnet run --no-build .claude/tools/sante-locale/scan.cs -- --base <base>`.
   Code 0 → aucun bloqueur : le dire et s'arrêter sans lancer l'agent. Code 2 → restituer l'erreur et s'arrêter.
4. **Préparer** : `RUN` = `date +%Y%m%d-%H%M%S` ; `mkdir -p artifacts/codescene/<RUN>` ; y écrire le scan initial
   (`initial.json`).
5. **Lancer l'agent `codescene-sante`** avec `RUN=<RUN>`, `BASE=<base>`. Le hook SubagentStop
   `codescene-boucle.sh` relance l'analyseur à chaque fin d'agent et le relance tant qu'il reste des bloqueurs
   (5 itérations au plus).
6. **Scan final indépendant** : relancer l'analyseur ; écrire sa sortie dans `artifacts/codescene/<RUN>/final.json`.
   C'est ce scan qui fait foi, pas le récit de l'agent.
7. **Vérifier** : `dotnet build ECommerce.slnx` et `dotnet test ECommerce.slnx` (montrer le résumé).
8. **Rapport** : `node .claude/skills/codescene-sante/rapport.mjs artifacts/codescene/<RUN>`.

## Restitution
- Verdict en une ligne (source : scan final) : `0 bloqueur` ou `N bloqueurs restants`, et la raison de l'arrêt.
- Tableau `fichier | violations avant → après | corrigées | restantes | statut`.
- Tests réussis / échoués ; fichiers modifiés (`git status --short`) ; chemin absolu du rapport HTML.

## Interdits
- Committer, pousser, changer de branche : la personne relit le diff, puis `/ship`.
- Modifier le référentiel (`codescene/**`, `.codescene/**`), l'analyseur, `tests/**` ou `Migrations/`.
