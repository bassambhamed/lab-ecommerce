#!/usr/bin/env bash
# Crée ou met à jour le rôle PostgreSQL lecteur_mcp (lecture seule) dans le conteneur
# PostgreSQL de l'AppHost, et range son mot de passe hors du dépôt pour le serveur MCP.
#
# Usage, depuis le dossier de formation, pendant que l'AppHost de la copie tourne :
#   bash labs/lab4-postgresql/ressources/creer-role-lecture-seule.sh ~/lab-ecommerce
#
# - Le mot de passe du superutilisateur (généré par Aspire, rangé dans les user-secrets de
#   l'AppHost) n'est jamais lu : psql tourne dans le conteneur, avec la variable
#   POSTGRES_PASSWORD que l'AppHost lui a donnée.
# - Le mot de passe du rôle est tiré au hasard et écrit dans
#   ~/.config/lab-ecommerce/postgres-lecture.env (droits 600). Il passe à psql par l'entrée
#   standard : il n'apparaît ni à l'écran, ni dans la liste des processus, ni dans le dépôt.
#   Si le fichier existe déjà, son mot de passe est réutilisé.
# - Rejouable. Pour changer le mot de passe : supprimez le fichier, puis relancez le script.
#
# Code de retour : 0 si le rôle est prêt, 1 si sa création ou sa vérification échoue,
# 2 si le script ne peut pas tourner.
set -uo pipefail

copie="${1:-}"
ici="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
sql="$ici/role-lecture-seule.sql"
dossier_secrets="$HOME/.config/lab-ecommerce"
fichier="$dossier_secrets/postgres-lecture.env"

arret() { echo "$*" >&2; exit 2; }

[[ -n "$copie" && -f "$copie/ECommerce.slnx" ]] ||
  arret "Usage : $0 <copie>   (le dossier qui contient ECommerce.slnx, par exemple ~/lab-ecommerce)"
grep -q 'AddPostgres' "$copie/src/ECommerce.AppHost/AppHost.cs" 2>/dev/null ||
  arret "L'AppHost de $copie ne déclare pas PostgreSQL : terminez d'abord la partie A du lab."
[[ -f "$sql" ]] || arret "Fichier introuvable : $sql"
command -v docker >/dev/null 2>&1 && docker info >/dev/null 2>&1 || arret "Docker ne répond pas : démarrez Docker Desktop."

# --- Le conteneur PostgreSQL de l'AppHost (DCP le nomme postgres-<suffixe>) ----------------
if [[ -n "${CONTENEUR_POSTGRES:-}" ]]; then
  conteneur="$CONTENEUR_POSTGRES"
else
  candidats="$(docker ps --filter label=com.microsoft.developer.usvc-dev.name --format '{{.Names}}' |
    grep -E '^postgres-' || true)"
  case "$(grep -c . <<<"$candidats")" in
    0) arret "Aucun conteneur PostgreSQL d'AppHost ne tourne : démarrez l'AppHost de la copie, puis relancez." ;;
    1) conteneur="$candidats" ;;
    *) arret "Plusieurs conteneurs PostgreSQL d'AppHost tournent ($(paste -sd ' ' - <<<"$candidats")) :
arrêtez les autres AppHost, ou relancez avec CONTENEUR_POSTGRES=<nom>." ;;
  esac
fi
echo "Conteneur PostgreSQL de l'AppHost : $conteneur"

# psql dans le conteneur, avec le compte administrateur que l'AppHost y a configuré.
psql_admin() {
  docker exec -i "$conteneur" sh -c \
    'PGPASSWORD="$POSTGRES_PASSWORD" exec psql -X -q -v ON_ERROR_STOP=1 -h 127.0.0.1 -U "$POSTGRES_USER" -d postgres "$@"' \
    psql "$@"
}

bases="$(psql_admin -At -c "SELECT count(*) FROM pg_database WHERE datname IN ('catalogdb', 'orderingdb')")" ||
  arret "Connexion administrateur impossible dans $conteneur."
[[ "$bases" == 2 ]] ||
  arret "Les bases catalogdb et orderingdb n'existent pas encore : attendez que catalog et ordering aient démarré."

# --- Le mot de passe du rôle, hors du dépôt ------------------------------------------------
if [[ -f "$fichier" ]]; then
  mdp="$(sed -n 's/^PGPASSWORD=//p' "$fichier")"
  [[ -n "$mdp" ]] || arret "$fichier ne contient pas de ligne PGPASSWORD= : supprimez-le, puis relancez."
  echo "Mot de passe existant réutilisé : $fichier"
else
  mdp="$(od -An -tx1 -N24 /dev/urandom | tr -d ' \n')"
  mkdir -p "$dossier_secrets" && chmod 700 "$dossier_secrets" || arret "Création impossible : $dossier_secrets"
  (umask 077 && printf 'PGPASSWORD=%s\n' "$mdp" >"$fichier") || arret "Écriture impossible : $fichier"
  echo "Mot de passe tiré au hasard, rangé dans $fichier (droits 600)."
fi

# --- Le rôle et ses droits -------------------------------------------------------------------
echo
echo "Rôle lecteur_mcp et droits de lecture :"
if ! { printf '\\set mot_de_passe %s\n' "$mdp"; cat "$sql"; } | psql_admin -f -; then
  echo "Échec du script $sql." >&2
  exit 1
fi

# --- Vérification : connexion par TCP avec le mot de passe, comme le fera le serveur MCP ----
echo "Vérification de la connexion de lecteur_mcp :"
for base in catalogdb orderingdb; do
  if tables="$(docker exec -i --env-file "$fichier" "$conteneur" psql -X -At -h 127.0.0.1 -U lecteur_mcp -d "$base" \
    -c "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public'" 2>&1)"; then
    echo "  $base : connexion OK, $tables table(s) visible(s)."
  else
    echo "  $base : ÉCHEC ($tables)" >&2
    exit 1
  fi
done

cat <<FIN

Rôle prêt. Le serveur MCP lira le mot de passe dans $fichier
(option --env-file de docker run) : il ne passe ni par la session Claude Code, ni par Git.
Étape suivante : copier ressources/.mcp.json à la racine de la copie, puis relancer Claude Code.
FIN
