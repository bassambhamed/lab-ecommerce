# Les 9 scénarios du lab 2

Les scénarios sont **donnés** : votre travail est d'obtenir des tests justes, lisibles et fiables. Les requêtes s'adressent directement aux API (sans la passerelle) : `/api/products` pour Catalog, `/api/orders` pour Ordering.

## Catalog.Api

| # | Scénario | Requête | Attendu |
|---|---|---|---|
| 1 | Liste des produits | `GET /api/products` | 200, 4 produits |
| 2 | Produit par identifiant | `GET /api/products/1` | 200, « Mechanical Keyboard » à 119,99 |
| 3 | Produit inconnu | `GET /api/products/999` | 404 |

## Ordering.Api (Catalog remplacé par un faux)

| # | Scénario | Corps de `POST /api/orders` | Attendu |
|---|---|---|---|
| 4 | Commande valide | `{"customer":"Ada","items":[{"productId":1,"quantity":2}]}` | 201, en-tête `Location`, statut `Confirmed`, total 239,98 |
| 5 | Panier vide | `{"customer":"Ada","items":[]}` | 400 |
| 6 | Produit inconnu | `{"customer":"Ada","items":[{"productId":99,"quantity":1}]}` | 400, le message cite le produit 99 |
| 7 | Client vide | `{"customer":"","items":[{"productId":1,"quantity":1}]}` | 400 |
| 8 | Quantité nulle | `{"customer":"Ada","items":[{"productId":1,"quantity":0}]}` | 400 |
| 9 | Propriété `items` absente | `{"customer":"Ada"}` | **400** (aujourd'hui : 500) |

Les scénarios 7 et 8 reposent sur le correctif du lab 1. Le scénario 9 est le bug signalé par le client : il doit être **rouge** avant d'être corrigé.
