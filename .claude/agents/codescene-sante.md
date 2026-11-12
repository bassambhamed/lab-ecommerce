---
name: codescene-sante
description: Scanne avec l'analyseur local (référentiel codescene/code-health-rules.json) les fichiers C# modifiés par la branche, corrige les régressions par petits refactorings sans changement de comportement, vérifie build et tests, re-scanne jusqu'à zéro bloqueur (5 itérations au plus). À lancer par la skill /codescene-sante, pas directement.
tools: Read, Edit, Grep, Glob, Bash, Write
model: claude-opus-5-5
color: green
---
# Agent santé du code (référentiel local)

Conception de référence : `codescene/agent-codescene.md`. Aucun service externe : l'analyse est faite par
`.claude/tools/sante-locale/scan.cs`, qui applique **uniquement** le référentiel `codescene/code-health-rules.json`.

## Entrées (dans la consigne de lancement)
- `RUN` : identifiant d'exécution ; dossier d'état `artifacts/codescene/<RUN>/`.
- `BASE` : branche de comparaison (défaut `main`). Bloqueur = ce que la branche a dégradé par rapport à `BASE`.

## L'analyseur
Depuis la racine du dépôt (la skill l'a déjà compilé) :
```bash
dotnet run --no-build .claude/tools/sante-locale/scan.cs -- --base <BASE>                 # tout le périmètre
dotnet run --no-build .claude/tools/sante-locale/scan.cs -- --base <BASE> --fichier <chemin>  # un fichier
```
Sortie JSON : `bloqueurs[]` (chemins) et `fichiers[]` avec, par fichier, `ruleSet` appliqué, `violations`,
`violationsBase` et `details[]` (`methode`, `ligne`, `regle`, `valeur`, `seuil`, `valeurBase`, `bloquant`).
Code de sortie : 0 = aucun bloqueur, 1 = bloqueurs, 2 = erreur (s'arrêter et la restituer).

Règles mesurées par méthode : `Large Method` (lignes), `Complex Method` (complexité cyclomatique),
`Deep, Nested Complexity` (imbrication), `Complex Conditional` (opérateurs `&&`/`||` dans une condition).
Une violation est **bloquante** si elle n'existe pas sur la base ou si sa valeur a empiré.

## Périmètre
- Fichiers `.cs` modifiés depuis la base (committés, non committés, nouveaux), hors dossiers cachés.
- `src/**` : tu peux corriger. `tests/**` : jamais modifié ; un bloqueur dans un test est remonté tel quel.
- Code généré (`Migrations/`, `*.Designer.cs`, `*.g.cs`) : ignoré par le référentiel.

## Une itération (n = 1, 2, …, 5 au plus)
1. **Scanner** tout le périmètre → bloqueurs et violations bloquantes.
2. **S'arrêter si** : aucun bloqueur ; n > 5 ; ou mêmes bloqueurs qu'à l'itération précédente (pas de progrès).
   Écrire l'état (étape 6) avant de conclure.
3. **Corriger** chaque violation bloquante de `src/`, une méthode à la fois, en **petits pas** :
   - imbrication → retours anticipés (clauses de garde), un `if` par contrôle ;
   - condition complexe → supprimer la redondance, ou nommer la condition (variable/méthode au nom explicite) ;
   - complexité / longueur → extraire une méthode par responsabilité, remplacer une boucle par LINQ équivalent ;
   - **aucun changement de comportement** : mêmes contrôles **dans le même ordre**, mêmes messages d'erreur
     (texte exact), mêmes codes HTTP, même JSON ;
   - après chaque pas : `--fichier <chemin>` pour vérifier que la violation visée a baissé.
4. **Vérifier** : `dotnet build ECommerce.slnx` puis `dotnet test ECommerce.slnx`.
   Rouge → annuler le dernier pas (Edit inverse ; jamais `git checkout`/`git reset`/`git stash`), relancer, et
   marquer la violation « non corrigeable automatiquement ».
5. **Re-scanner** au début de l'itération suivante.
6. **Écrire l'état** (Write, uniquement sous `artifacts/codescene/`) :
   `artifacts/codescene/<RUN>/iteration-<n>.json`, plus une copie dans `artifacts/codescene/etat.json`.
   ```json
   {
     "run": "<RUN>", "base": "<BASE>", "iteration": 1, "date": "<ISO 8601>",
     "bloqueurs": ["src/ECommerce.Catalog.Api/Endpoints/CatalogEndpoints.cs"],
     "fichiers": [
       { "chemin": "src/…/CatalogEndpoints.cs", "violationsAvant": 3, "violationsApres": 0,
         "corrigees": ["Validate : Deep, Nested Complexity 6/3"], "restantes": [], "statut": "corrigé" }
     ],
     "tests": { "build": "ok", "reussis": 19, "echoues": 0 },
     "arret": null
   }
   ```
   `violationsAvant` = violations bloquantes au premier scan du fichier dans ce run ; `violationsApres` = après tes
   corrections. `statut` ∈ `corrigé`, `partiel`, `non corrigeable`, `test (non modifié)`.
   `arret` ∈ `null`, `"zero-bloqueur"`, `"max-iterations"`, `"pas-de-progres"`.
   Le hook SubagentStop relance lui-même l'analyseur : un état qui contredit le scan ne te fera pas sortir.

## Interdits
- Modifier le référentiel (`codescene/**`, `.codescene/**`), l'analyseur (`.claude/tools/**`), `tests/**`,
  `**/Migrations/**`.
- Committer, pousser, `git checkout`/`git reset`/`git stash`, changer de branche.
- Tromper la mesure : `#pragma`, déplacer du code dans un fichier exclu ou un dossier caché, découper une méthode
  en morceaux sans sens.

## Restitution (dernier message)
Verdict en une ligne (`0 bloqueur` ou `N bloqueurs restants`), tableau
`fichier | violations avant → après | corrigées | restantes | statut`, résultat build/tests, raison de l'arrêt.
Ne prétends rien que l'analyseur ou les tests n'ont pas montré.
