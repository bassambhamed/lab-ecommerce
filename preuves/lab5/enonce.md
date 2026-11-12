# Lab 5 · Retrouver les commandes d’un client

> Partie 7 des supports · Mécanismes : critères visuels, `ui-verifier`, MCP Playwright

## Pourquoi ce lab

Un build vert ne prouve ni le filtrage, ni le clavier, ni le comportement quand Ordering tombe. On décrit les critères observables, on confie leur vérification à un agent séparé et on conserve les captures et les limites de la vérification.

## Objectifs

- Filtrer les commandes côté interface, pendant la frappe et sans changer l'API.
- Traiter chargement, erreur et liste vide sans bloquer le premier affichage.
- Vérifier focus, clavier et noms accessibles dans un vrai navigateur.

## Point de départ

Copie à la fin du lab 4, Docker et AppHost fonctionnels, Node.js 20+, `jq`. Branche `feat/orders-customer-filter`. Le serveur MCP Playwright utilise une session isolée, avec certificats locaux ignorés uniquement pour ce lab.

**Installer le dispositif du lab.** Depuis n'importe quel dossier :

```bash
cp -R "$FORMATION"/labs/lab5-frontend/.claude/. ~/lab-ecommerce/.claude/
git -C ~/lab-ecommerce status --short .claude
git -C ~/lab-ecommerce diff .claude/settings.json
```

Le `settings.json` du lab reprend les règles de tous les labs précédents et remplace le vôtre : si `git diff` montre la suppression de règles que vous aviez ajoutées, remettez-les. Relancez ensuite Claude Code : les réglages et les hooks sont lus au démarrage.

> Votre copie n'est pas à la fin du lab précédent ? Voir « Rattraper un lab » dans [`labs/README.md`](../README.md#rattraper-un-lab).

## Mise en situation

Le support cherche toutes les commandes de Lovelace et perd du temps dans l'historique. Il utilise parfois uniquement le clavier. Une panne d'Ordering doit laisser la page utilisable et permettre une reprise.

## Étapes

Toutes les commandes se lancent depuis la racine de votre copie, sauf indication contraire.

### Étape 1 · Lire les critères avant de coder

Ouvrez `ressources/maquette-filtre.png` et `ressources/criteres-filtre-commandes.md`. Copiez les critères dans votre copie :

```bash
mkdir -p preuves/lab5/captures
cp "$FORMATION"/labs/lab5-frontend/ressources/criteres-filtre-commandes.md preuves/lab5/criteres.md
``` Les critères F1–F5, K1–K3, V1, E1–E4 et R1 sont la recette. Le code et les messages visibles restent en anglais.

Créez au moins cinq commandes via le catalogue ou la passerelle, dont deux pour `Ada Lovelace` et `ada lovelace`, une pour `Alan Turing`. Notez leurs identifiants. Ne supprimez pas les données pour simplifier la recette.

### Étape 2 · Une demande bornée

```text
Sur /orders seulement, réalise les critères de ressources/criteres-filtre-commandes.md
(je te fournis ce fichier). Filtre côté client par Contains OrdinalIgnoreCase après Trim,
pendant oninput. Garde les cartes Bootstrap, les DTO, l'API et les autres pages.
Échap et Clear filter vident la saisie en gardant/rendant le focus au champ.
Prévois chargement status, erreur alert sans détails techniques et reprise par Refresh.
Une nouvelle navigation doit afficher Loading en moins de deux secondes quand Ordering
ne répond pas. Explique le cycle de rendu Blazor choisi. Build puis recette navigateur.
```

Le corrigé conserve le prérendu, mais déclenche la requête dans `OnAfterRenderAsync(firstRender)` : la première réponse HTML ne dépend pas de l'API. Il journalise l'erreur côté serveur, annule la requête précédente si Refresh est redemandé et ignore ses résultats obsolètes. Le filtre reste actif pendant une panne.

### Étape 3 · Installer et déléguer la recette

```bash
cp "$FORMATION"/labs/lab5-frontend/ressources/.mcp.json .mcp.json
dotnet build ECommerce.slnx
```

Le `.mcp.json` conserve PostgreSQL du lab 4 et ajoute Playwright. Relancez Claude et vérifiez `/mcp`.

```text
Utilise le sous-agent ui-verifier puis /verifier-ui. Exécute la recette sur
https://localhost:7218/orders avec les critères fournis. Pour chaque ID : résultat,
action exécutée et preuve. Range les captures dans preuves/lab5/captures. N'affirme pas
qu'un critère passe si tu n'as pas pu exécuter l'action dans le navigateur.
```

Phase 1 : saisissez `LOVELACE`, `   turing  ` puis `zzz`. Parcourez Tab, Entrée, Échap ; vérifiez le focus après disparition de Clear filter et la région `status`.

Phase 2 : arrêtez **vous-même** Ordering dans le dashboard Aspire, attendez `Finished`, cliquez Refresh puis ouvrez l'URL dans une nouvelle navigation. Vérifiez l'indicateur immédiatement, puis l'erreur avec rôle `alert`. Le délai dépend de la résilience HTTP ; mesurez-le au lieu de figer une durée.

Phase 3 : redémarrez Ordering, attendez `Running`, saisissez `ada` et activez Refresh au clavier. Le filtre reste et les résultats reviennent.

### Étape 4 · Arbitrer et livrer

Relisez les captures et le tableau du sous-agent. Une capture unique ne démontre pas une séquence clavier ; notez aussi les actions et le nom accessible observés. Corrigez les écarts puis rejouez uniquement les cas concernés et la reprise après panne. `/ship Filtre client accessible et reprise des commandes`.

## Critères d'acceptation

- [ ] Tous les critères F, K, V, E et R de la ressource sont vérifiés.
- [ ] Filtre pendant la frappe, Trim et casse ignorée, compteur status, vide distinct.
- [ ] Focus visible, Échap et Clear filter gardent le parcours clavier.
- [ ] Premier HTML et chargement visibles sans attendre Ordering ; erreur récupérable.
- [ ] API et contrats inchangés ; build et tests existants passent.

## Preuves à fournir

Dans `preuves/lab5/` : tableau ID/action/résultat/preuve, captures nommées par critère, durée du premier affichage et de l'erreur, messages de console pertinents, arbitrages de revue et sortie du build. Ne joignez aucune donnée personnelle réelle.

## Aides progressives

<details><summary>Indice 1 · Il faut appuyer sur Entrée</summary>Un input lié à onchange ne filtre pas pendant la frappe. Utilisez @bind:event="oninput".</details>

<details><summary>Indice 2 · Page blanche pendant la panne</summary>Une requête attendue dans OnInitializedAsync retarde le prérendu. Différez la requête au premier rendu interactif ; StateHasChanged doit ensuite afficher son résultat.</details>

<details><summary>Indice 3 · Le focus disparaît</summary>Gardez une ElementReference sur le champ et appelez FocusAsync quand Clear filter disparaît.</details>

## Pour aller plus loin

Vérifiez à 320 px et à 200 % de zoom. Pour un historique volumineux, concevez une pagination et un filtre côté API avec paramètres validés.

## Débrief

Qu'a vu le navigateur que le build ne voit pas ? Quelle preuve manque à une capture ? **À retenir :** critères observables, scénarios de panne et parcours clavier font partie du comportement livré.

## Les fichiers du lab

- `.claude/settings.json` : règles cumulées et permissions navigateur bornées.
- `.claude/agents/ui-verifier.md` : mission de recette séparée.
- `.claude/skills/verifier-ui/SKILL.md` : procédure de recette.
- `ressources/.mcp.json` : PostgreSQL et Playwright ; `criteres-filtre-commandes.md` et `maquette-filtre.png` : recette et référence visuelle.
- `solution/src/ECommerce.Web/Components/Pages/Orders.razor` et `.razor.css` : filtre, états et focus ; `solution/src/ECommerce.Web/README.md` : documentation actualisée.

## Références

[Cycle de vie Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/lifecycle?view=aspnetcore-10.0) · [Modes de rendu](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes?view=aspnetcore-10.0).
