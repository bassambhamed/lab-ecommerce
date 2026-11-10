# Lab 1 — Preuves « avant »

Capturées le 2026-11-10 à 12:24, sur le commit `672b64d`, contre l'application en cours d'exécution via la passerelle (`http://localhost:5067`).

## 1. Client vide : la commande est acceptée (201)

```bash
curl -i -X POST http://localhost:5067/ordering/api/orders -H 'Content-Type: application/json' \
  -d '{"customer":"","items":[{"productId":1,"quantity":2}]}'
```

```
HTTP/1.1 201 Created
Content-Type: application/json; charset=utf-8
Date: Tue, 10 Nov 2026 11:24:24 GMT
Server: Kestrel
Location: https://localhost:7082/api/orders/5
Transfer-Encoding: chunked

{"id":5,"customer":"","createdAt":"2026-11-10T11:24:24.991863+00:00","status":"Confirmed","items":[{"id":6,"productId":1,"productName":"Mechanical Keyboard","unitPrice":119.99,"quantity":2}],"total":239.98}
```

## 2. Quantité négative : le total est négatif

```bash
curl -s -X POST http://localhost:5067/ordering/api/orders -H 'Content-Type: application/json' \
  -d '{"customer":"Ada","items":[{"productId":1,"quantity":-3}]}' | jq .total
```

```
-359.97
```
