# Critères : filtre des commandes par client

Page : https://localhost:7218/orders
Captures : preuves/lab5/captures/

Données attendues : au moins cinq commandes, dont deux pour Ada Lovelace écrites avec des
casses différentes (« Ada Lovelace » et « ada lovelace ») et une pour Alan Turing.

Chaque critère décrit ce qu'un utilisateur **voit** ou **fait** : il doit pouvoir être vérifié
dans un navigateur, sans lire le code.

## Phase 1 · Ordering en marche

Avant : rien (l'application tourne).

| Id | Critère |
|---|---|
| F1 | Un champ de recherche est affiché au-dessus de la liste, avec un libellé visible « Filter by customer » associé au champ : le nom accessible du champ est « Filter by customer ». |
| F2 | Saisir « LOVELACE » filtre la liste pendant la frappe, sans appuyer sur Entrée : restent seulement, et toutes, les commandes dont le client contient « lovelace », quelle que soit la casse. |
| F3 | Une ligne de texte annonce le nombre de commandes affichées et le total (par exemple « Showing 2 of 5 orders for “LOVELACE” ») ; c'est une région `status`, lue par les lecteurs d'écran. Sans filtre, elle donne le total. |
| F4 | Les espaces autour de la saisie sont ignorés : «   turing  » affiche la commande d'Alan Turing. |
| F5 | Aucun résultat : « zzz » affiche un message distinct de la liste vide, qui rappelle le texte cherché (« No orders match “zzz”. ») ; la liste disparaît. |
| K1 | Au clavier seul, depuis le haut de la page : Tab atteint le champ, puis « Clear filter » (présent seulement s'il y a une saisie), puis « Refresh ». Chaque élément atteint montre un indicateur de focus visible sur la capture. |
| K2 | Échap dans le champ vide le filtre : la liste complète revient et le focus reste dans le champ. |
| K3 | « Clear filter », activé avec Entrée, vide le filtre et rend le focus au champ : le focus ne se perd pas quand le bouton disparaît. |
| V1 | La page garde le style de l'application (Bootstrap, mêmes cartes de commande) ; le menu et les autres pages n'ont pas changé. |

## Phase 2 · Ordering arrêté

Avant : arrêtez la ressource `ordering` dans le tableau de bord Aspire, puis attendez qu'elle soit à l'état `Finished`.

| Id | Critère |
|---|---|
| E1 | Chargement : juste après « Refresh », un indicateur (animation et texte « Loading orders… ») s'affiche à la place de la liste, et reste visible tant que la réponse n'est pas arrivée. Le texte est dans la région `status`. |
| E2 | Erreur : au bout de la tentative (environ 30 s), un message d'erreur clair (rôle `alert`) remplace l'indicateur. Il ne montre aucun détail technique (ni nom d'exception, ni pile d'appels) et dit comment réessayer. |
| E3 | La page reste utilisable : pas de bandeau « An unhandled error has occurred », le champ et « Refresh » restent atteignables et actifs au clavier. |
| E4 | Ouvrir la page (nouvelle navigation vers l'URL) n'est pas bloqué : l'indicateur de chargement apparaît en moins de 2 secondes, pas une page blanche pendant 30 secondes. |

## Phase 3 · Ordering redémarré

Avant : redémarrez la ressource `ordering` dans le tableau de bord, puis attendez qu'elle soit à l'état `Running`.

| Id | Critère |
|---|---|
| R1 | Avec « ada » dans le champ, « Refresh » activé au clavier (Entrée) recharge la liste : le message d'erreur disparaît, le filtre saisi est conservé et appliqué. |
