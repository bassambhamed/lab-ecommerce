# Conception — Agent « santé du code » (projet ECommerce, référentiel local)

> Document de conception et plan d'action. Branche : `chore/codescene`. Emplacement : `codescene/agent-codescene.md`.
> Statut (2026-11-11) : **tous les composants sont créés et testés hors boucle réelle**. Analyse **100 % locale**,
> pilotée par le référentiel `codescene/code-health-rules.json`. Prochaine étape : lancer `/codescene-sante`.

---

## 1. Objectif

Donner à Claude Code, **pour ce seul projet et en local**, la capacité de :

1. **scanner** le code C# modifié par la branche selon le référentiel d'équipe `codescene/code-health-rules.json` ;
2. **corriger** les régressions par petits refactorings sans changement de comportement, **re-scanner** — en boucle
   jusqu'à **zéro bloqueur** ;
3. produire un **rapport HTML** de chaque exécution.

**Aucun compte, aucun jeton, aucun service externe.** Le dépôt https://github.com/codescene-oss/codescene-mcp-server
a servi une fois de source (format du référentiel, noms de règles et de seuils, méthode de refactoring) ; il n'est plus
consulté.

## 2. Décisions

| Sujet | Décision | Conséquence |
|---|---|---|
| **Moteur d'analyse** | Analyseur local `.claude/tools/sante-locale/scan.cs` (Roslyn), qui lit le référentiel | Le moteur CodeScene exige une licence (§9) : écarté. On perd le score officiel 1–10 et les smells fins ; on garde les seuils du référentiel |
| **Référentiel** | `codescene/code-health-rules.json`, format CodeScene, seule source des règles | Modifiable par un humain uniquement (`deny` Edit/Write) |
| Définition d'un **bloqueur** | Violation d'une règle **absente de la base** (merge-base avec la branche de base) **ou aggravée** | La dette déjà présente sur la base ne bloque pas |
| **Base** | Paramètre ; par défaut `main`. Choisir la branche dont on est parti | `chore/codescene` part de `feat/postgresql-categories` : avec `main`, le travail non fusionné de cette branche parente entre dans le périmètre |
| **Périmètre** | `.cs` modifiés depuis la base (committés, non committés, nouveaux), hors dossiers cachés | `src/` corrigé ; `tests/` scanné, jamais modifié ; code généré ignoré par le référentiel |
| **Installation** | Tout en **local** au projet | `.claude/settings.local.json` (non versionné) ; aucun commit sans demande ; aucun serveur MCP |

## 3. Le référentiel — `codescene/code-health-rules.json`

Premier `rule set` dont le `matching_content_path` correspond = seul appliqué (ordre du plus spécifique au plus général).
Validé le 2026-11-11 par l'outil CodeScene `rules_config_validate` : `8 rule set(s), 18 rule override(s), 8 threshold override(s)`.

| # | `matching_content_path` | Contenu | Motif |
|---|---|---|---|
| 1 | `**/Migrations/**` | Large/Complex/Brain Method, Bumpy Road Ahead à 0.0 | Migrations EF générées → fichier ignoré |
| 2 | `**/*.Designer.cs` | idem | Code généré |
| 3 | `**/*.g.cs` | idem | Code généré |
| 4 | `tests/**` | Large Assertion Blocks 0.0, Duplicated Assertion Blocks 0.3, Large Method 0.5 ; duplication dès 15 lignes | Tests d'intégration |
| 5 | `src/ECommerce.ServiceDefaults/**` | Large Method 0.5, Complex Method 0.5 | Gabarit Aspire |
| 6 | `src/ECommerce.*.Api/Endpoints/**` | 50 lignes, complexité 8, imbrication 3 | Logique métier (`Validate()`, création de commande) |
| 7 | `src/ECommerce.*.Api/Data/**` | Primitive Obsession 0.5, 70 lignes, complexité 10 | DbContext |
| 8 | `**/*.cs` | 70 lignes, complexité 10 | Défaut équipe |

Historique : l'ancienne version ciblait `src/*.Domain/**` et `src/*.Infrastructure/**`, qui ne correspondent à aucun
projet ; ces rule sets ont été remplacés (6 et 7) et ServiceDefaults ajouté (5). `.codescene/code-health-rules.json`
est un lien symbolique vers ce fichier (emplacement lu par CodeScene ; inutile à l'analyseur local, conservé).

## 4. Architecture

```
 /codescene-scan (commande, lecture seule)        /codescene-sante <base> (skill, orchestration)
          │                                                 │ compile l'analyseur, scan initial
          │                                                 ▼
          │                                   Agent codescene-sante  ◄────────────────┐
          │                                   ┌──────────────────────────┐             │
          ▼                                   │ 1 scanner                │             │ exit 2
   Analyseur local scan.cs  ◄─────────────────│ 2 corriger (petits pas)  │             │ « bloqueurs
   (Roslyn, hors ligne)                       │ 3 build + tests          │             │  restants »
          │                                   │ 4 écrire etat.json       │             │
          ▼                                   └────────────┬─────────────┘             │
   codescene/code-health-rules.json                        │ fin d'agent               │
   + version de base (git show merge-base)                 ▼                           │
                                         Hook SubagentStop codescene-boucle.sh ────────┘
                                         (relance l'analyseur lui-même ; borné)
                                                           │ 0 bloqueur / borne atteinte
                                                           ▼
                                         Scan final indépendant (skill) → final.json
                                                           ▼
                                         rapport.mjs → artifacts/codescene/<run>/rapport.html
```

## 5. Composants

### 5.1 Analyseur local — `.claude/tools/sante-locale/scan.cs`

Application .NET 10 « fichier unique » (`#:package Microsoft.CodeAnalysis.CSharp@5.0.0`, déjà en cache : hors ligne).

```bash
dotnet build .claude/tools/sante-locale/scan.cs
dotnet run --no-build .claude/tools/sante-locale/scan.cs -- --base <branche> [--fichier <chemin>] [--regles <json>]
```

| Règle (nom CodeScene) | Mesure, par méthode | Seuil (référentiel, sinon défaut C# CodeScene) |
|---|---|---|
| Large Method | lignes de la déclaration | `function_lines_of_code_warning` (70) |
| Complex Method | complexité cyclomatique : 1 + if, boucles, case, catch, `?:`, `&&`, `\|\|`, `??` | `function_cyclomatic_complexity_warning` (10) |
| Deep, Nested Complexity | profondeur de structures de contrôle imbriquées (`else if` = même niveau) | `function_nesting_depth_warning` (4) |
| Complex Conditional | opérateurs `&&`/`\|\|` dans une même condition (dépassement si ≥ seuil) | `function_complex_conditional_branches_warning` (2) |

- Méthodes mesurées : méthodes, constructeurs, fonctions locales, accesseurs (les lambdas comptent dans leur méthode).
- Une règle de poids 0 désactive le contrôle ; Large **et** Complex Method à 0 → fichier ignoré (code généré).
- Comparaison avec la version du fichier au **merge-base** (`git show`) : violation **bloquante** si absente de la base
  ou valeur aggravée. Fichier nouveau : toute violation est bloquante.
- Sortie JSON (`bloqueurs[]`, `fichiers[]` avec `ruleSet`, `violations`, `violationsBase`, `details[]`).
  Code 0 = aucun bloqueur, 1 = bloqueurs, 2 = erreur.
- Défauts C# relevés le 2026-11-11 avec `rules_config_list_thresholds("C#")` de CodeScene.

### 5.2 Agent — `.claude/agents/codescene-sante.md`

Outils : Read, Edit, Grep, Glob, Bash, Write (aucun MCP). Une itération : scanner → s'arrêter si zéro bloqueur, 5
itérations ou pas de progrès → corriger chaque violation bloquante de `src/` en petits pas (clauses de garde,
suppression des redondances, condition nommée, extraction de méthode), en gardant **les mêmes contrôles dans le même
ordre et les mêmes messages** → `--fichier` après chaque pas → build + tests (rouge → annuler le pas) → écrire
`artifacts/codescene/<RUN>/iteration-<n>.json` et `artifacts/codescene/etat.json`
(`violationsAvant`, `violationsApres`, `corrigees`, `restantes`, `statut`, `tests`, `arret`).
Interdits : référentiel, analyseur, `tests/**`, `Migrations/`, commit/push/reset/stash, `#pragma` ou déplacement de code
pour tromper la mesure.

### 5.3 Skill — `/codescene-sante [base]` (`.claude/skills/codescene-sante/`)

Compile l'analyseur → scan initial (code 0 : rien à faire, pas d'agent) → `RUN` horodaté, `initial.json` → agent →
**scan final indépendant** (`final.json`, fait foi) → build + tests → `rapport.mjs`. Restitue verdict, tableau par
fichier, tests, chemin du rapport. Ne commite rien.

**`rapport.mjs`** (Node, sans dépendance) → `artifacts/codescene/<RUN>/rapport.html` autonome (clair/sombre, sans CDN) :
verdict (source : `final.json`, sinon dernière itération), violations bloquantes avant → après par fichier, itérations,
build/tests.

### 5.4 Commande — `/codescene-scan [base]` (`.claude/commands/codescene-scan.md`)

Lecture seule : compile et lance l'analyseur, lit chaque méthode en cause, propose une correction. Tableau
`gravité | fichier:ligne | méthode | règle | valeur / seuil (base) | correction proposée` ; gravité **bloqueur**
(nouveau ou aggravé) ou **dette** (inchangé depuis la base).

### 5.5 Hook — `.claude/hooks/codescene-boucle.sh` (SubagentStop, matcher `codescene-sante`)

Relance **lui-même** l'analyseur (base lue dans `etat.json`) : un état qui prétend « 0 bloqueur » ne fait pas sortir
l'agent. Repli sur `etat.json` si l'analyseur échoue. `exit 2` + liste des bloqueurs tant qu'il en reste ; sortie
après 5 itérations, si les bloqueurs n'évoluent plus, ou après 2 rappels si l'état n'est jamais écrit.
Échappatoire : `CLAUDE_SKIP_CODESCENE_LOOP=1`. Hooks existants aussi actifs : `formater.sh`, `stop-tests-verts.sh`,
`garde-git.sh`.

### 5.6 Réglages — `.claude/settings.local.json`

- `allow` : compilation et exécution de l'analyseur, `rapport.mjs`.
- `deny` : Edit/Write sur `codescene/code-health-rules.json`, `.codescene/**`, `.claude/tools/sante-locale/**`
  (ni le référentiel ni la mesure ne peuvent être assouplis par l'agent — ni par Claude sans que l'utilisateur lève
  l'interdiction).
- `hooks.SubagentStop` → `codescene-boucle.sh`.

## 6. Récapitulatif des fichiers

| Type | Fichier | Versionné |
|---|---|---|
| Document de conception | `codescene/agent-codescene.md` | non (sauf demande) |
| Référentiel | `codescene/code-health-rules.json` (+ lien `.codescene/code-health-rules.json`) | non (sauf demande) |
| Analyseur | `.claude/tools/sante-locale/scan.cs` | non |
| Agent | `.claude/agents/codescene-sante.md` | non |
| Skill + rapport | `.claude/skills/codescene-sante/SKILL.md`, `rapport.mjs` | non |
| Commande | `.claude/commands/codescene-scan.md` | non |
| Hook | `.claude/hooks/codescene-boucle.sh` | non |
| Réglages | `.claude/settings.local.json` (ignoré par `~/.config/git/ignore`) | non |
| Lanceur MCP CodeScene (inutilisé) | `.claude/mcp/codescene.sh` — serveur désenregistré, gardé si une licence arrive | non |
| Démonstration | commit `24f6bb4` : `Validate()` de Catalog et Ordering volontairement dégradées | **oui** (branche `chore/codescene`) |

`CLAUDE.md` n'est pas modifié : il est versionné et partagé, ce dispositif est local.

## 7. Risques et limites

| Risque | Parade |
|---|---|
| L'agent « corrige » en assouplissant les règles ou la mesure | `deny` sur le référentiel et l'analyseur ; interdits de l'agent |
| Refactoring qui change le comportement | Mêmes contrôles, même ordre, mêmes messages ; build + tests après chaque itération, annulation si rouge ; hook `stop-tests-verts.sh` |
| L'agent se déclare vert à tort | Le hook relance l'analyseur ; scan final indépendant dans la skill |
| Boucle infinie | 5 itérations, arrêt sans progrès, 2 rappels max sans état, échappatoire |
| Mauvaise base → bloqueurs hérités d'une branche parente | Base en paramètre ; la commande signale le cas |
| Mesure moins riche que CodeScene (pas de score 1–10, pas de Bumpy Road, Brain Method, duplication) | Assumé : seuls les seuils du référentiel sont appliqués |
| Bloqueur dans `tests/` jamais corrigé | Remonté tel quel ; la boucle s'arrête « sans progrès » |

## 8. Résultats de vérification (2026-11-11)

| Contrôle | Résultat |
|---|---|
| Analyseur, base `feat/postgresql-categories` | 2 bloqueurs, exactement les méthodes dégradées : `CatalogEndpoints.Validate` (complexité 11/8, imbrication 6/3, condition 2/2) ; `OrderingEndpoints.Validate` (53/50 lignes, complexité 14/8, imbrication 6/3, condition 2/2) |
| Analyseur, base `main` | + `FakeCatalogHandler.SendAsync` (tests, condition 3/2) hérité de la branche des tests ; dette `MapOrderingEndpoints` 60/50 inchangée → non bloquante |
| Hook, 10 scénarios simulés | 10/10 ; état absent plafonné à 2 rappels |
| Hook, analyseur réel | état prétendant 0 bloqueur → le hook trouve les 2 bloqueurs et bloque (exit 2) |
| Rapport sur itérations factices | violations 3 → 0, verdict tiré de `final.json` |
| Application avec le code dégradé | 19 tests verts, `SMOKE OK` |

## 9. Historique : essai du moteur CodeScene (abandonné)

Serveur MCP `@codescene/codehealth-mcp@1.5.16` lancé en local sans jeton : `rules_config_validate` et
`rules_config_list_thresholds` fonctionnent, mais `code_health_review`, `code_health_score` et `analyze_change_set`
répondent « No access token configured » (connexion OAuth ou jeton exigés : activation de licence) et la CLI contacte
CodeScene Cloud. L'utilisateur a refusé la connexion : le moteur a été remplacé par l'analyseur local, le serveur
désenregistré (`claude mcp remove codescene -s local`).

## 10. Plan d'action

| Étape | Action | État |
|---|---|---|
| 0 | Document de conception | ✅ |
| 1 | Référentiel amélioré et validé | ✅ |
| 2 | Essai du moteur CodeScene sans jeton | ✅ constat : licence exigée → abandonné |
| 3 | Analyseur local `scan.cs` | ✅ testé sur les deux bases |
| 4 | Agent, skill, commande, hook, rapport, réglages basculés sur l'analyseur | ✅ |
| 5 | Code volontairement dégradé, commité (`24f6bb4`), application vérifiée | ✅ |
| 6 | **Redémarrer Claude Code** (nouvelles permissions et serveur MCP retiré) | ⏳ utilisateur |
| 7 | `/codescene-scan feat/postgresql-categories` (diagnostic) | ⏳ |
| 8 | `/codescene-sante feat/postgresql-categories` : boucle jusqu'à 0 bloqueur, rapport HTML | ⏳ |
| 9 | Relecture du diff par l'utilisateur ; commit uniquement sur demande | ⏳ |
