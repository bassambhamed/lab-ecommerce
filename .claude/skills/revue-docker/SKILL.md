---
name: revue-docker
description: Revue d'une image Docker d'un service ECommerce (catalog, ordering, gateway, web) - Dockerfile, .dockerignore, image construite et scan de vulnérabilités. Construction multi-étapes, image de base épinglée, utilisateur non root, port 8080, cache des couches, secrets. À utiliser après avoir écrit ou modifié un Dockerfile, et avant de livrer une image. Ne modifie rien.
argument-hint: "<catalog|ordering|gateway|web>"
allowed-tools: Read, Grep, Glob, Bash(docker build *), Bash(docker image inspect *), Bash(docker history *), Bash(trivy image *), Bash(jq *), Bash(mkdir -p preuves/lab7*), Bash(bash *.claude/skills/revue-docker/verifier-image.sh *)
---

# Revue Docker d'un service ECommerce

Service à relire : `$ARGUMENTS`. Sans argument, demande lequel ; ne relis pas les quatre d'un coup.

| Service | Dockerfile | Image |
|---|---|---|
| catalog | `src/ECommerce.Catalog.Api/Dockerfile` | `ecommerce/catalog:1.0` |
| ordering | `src/ECommerce.Ordering.Api/Dockerfile` | `ecommerce/ordering:1.0` |
| gateway | `src/ECommerce.Gateway/Dockerfile` | `ecommerce/gateway:1.0` |
| web | `src/ECommerce.Web/Dockerfile` | `ecommerce/web:1.0` |

Tu relis, tu prouves, tu recommandes : tu ne modifies aucun fichier. Les corrections viennent
après, sur décision de l'utilisateur.

## 1. Lire le Dockerfile et le `.dockerignore`

Pour chaque critère, note `OK` ou `À corriger`, avec la ligne qui le prouve (`fichier:ligne`) :

1. **Multi-étapes** : le SDK construit, l'étape finale ne contient que le runtime.
2. **Images épinglées** : chaque `FROM` externe a une version exacte **et** un digest
   (`tag@sha256:…`). Un tag seul (`10.0`, `latest`) change sans prévenir.
3. **Image finale chiseled** (`aspnet:<version>-noble-chiseled`) : ni shell ni gestionnaire de
   paquets, donc moins de vulnérabilités.
4. **Non root** : `USER $APP_UID` dans l'étape finale (utilisateur numérique 1654 fourni par
   l'image).
5. **Port 8080** : `EXPOSE 8080` ; c'est le port par défaut d'ASP.NET Core dans les images .NET 8 et
   plus (`ASPNETCORE_HTTP_PORTS`).
6. **Cache** : les `.csproj` sont copiés et restaurés **avant** le code ; `dotnet publish
   --no-restore` ensuite. Piège du Web (Blazor) : sans fichier `.razor` au moment du restore, le
   paquet qui fournit `_framework/blazor.web.js` n'est pas restauré ; le restore du Web doit
   recevoir `-p:RequiresAspNetWebAssets=true`.
7. **`.dockerignore`** à la racine : `bin/`, `obj/`, `.git/`, `.env`, clés et certificats exclus.
8. **Aucun secret** dans `ENV`, `ARG` ou une étape `RUN` : une valeur de `ARG` reste lisible dans
   `docker history`.

## 2. Construire et contrôler l'image

Depuis la racine du dépôt :

```bash
docker build -f <Dockerfile> -t <image> .
bash ${CLAUDE_SKILL_DIR}/verifier-image.sh <image> <Dockerfile>
```

Le script est la référence pour les contrôles déterministes (utilisateur, shell, SDK, port,
secrets, digests). Recopie son tableau tel quel. Code 0 : tout passe ; 1 : un contrôle échoue ;
2 : le script n'a pas pu tourner (image absente, Docker arrêté).

## 3. Scanner l'image

```bash
mkdir -p preuves/lab7/scans/revue
trivy image --quiet --scanners vuln --format json --output preuves/lab7/scans/revue/<service>.json <image>
jq '[.Results[]? | .Vulnerabilities[]? | .Severity] | group_by(.) | map({(.[0]): length}) | add' preuves/lab7/scans/revue/<service>.json
```

Ne charge pas le rapport complet dans la conversation : il peut faire des milliers de lignes.

## 4. Déléguer l'analyse au sous-agent `docker-reviewer`

Confie-lui, dans une demande autosuffisante : le chemin du Dockerfile, celui du `.dockerignore`,
celui du rapport de scan, et la sortie du script. Il lit le rapport à ta place et renvoie des
correctifs priorisés. Ne lui demande pas de modifier quoi que ce soit.

## Restitution

1. Le tableau des 8 critères : `critère | statut | preuve (fichier:ligne) | correctif proposé`.
2. La sortie de `verifier-image.sh`, puis la synthèse du scan par gravité.
3. Le rapport de `docker-reviewer`, sans le réécrire.
4. Un verdict en une ligne : « image livrable » ou « à corriger », et la liste des décisions
   qui reviennent à l'utilisateur (recommandation à accepter ou à refuser, vulnérabilité sans
   correctif à documenter).
