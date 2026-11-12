#!/usr/bin/env bash
# Contrôles déterministes d'une image .NET de l'application ECommerce.
#
#   bash verifier-image.sh <image> [Dockerfile]
#
# Sur l'image : utilisateur non root et numérique, port 8080, absence de shell et de SDK,
# aucune variable d'environnement ni étape de construction qui ressemble à un secret.
# Sur le Dockerfile (facultatif) : plusieurs étapes, images de base épinglées par digest,
# image finale chiseled, instruction USER, aucun secret dans ENV ou ARG.
#
# Code de retour : 0 si tout passe, 1 si un contrôle échoue, 2 si le script ne peut pas tourner.
set -uo pipefail

image="${1:-}"
dockerfile="${2:-}"
echecs=0

[[ -n "$image" ]] || { echo "Usage : verifier-image.sh <image> [Dockerfile]" >&2; exit 2; }
for outil in docker jq; do
  command -v "$outil" >/dev/null 2>&1 || { echo "Outil manquant : $outil" >&2; exit 2; }
done
config="$(docker image inspect --format '{{json .Config}}' "$image" 2>/dev/null)" || {
  echo "Image introuvable : $image (construisez-la d'abord avec docker build)." >&2
  exit 2
}
[[ -z "$dockerfile" || -f "$dockerfile" ]] || { echo "Dockerfile introuvable : $dockerfile" >&2; exit 2; }

# Noms de variables et valeurs qui ressemblent à des secrets.
SECRET_NOM='(PASSWORD|PASSWD|SECRET|TOKEN|APIKEY|API_KEY|CONNECTIONSTRINGS)'
SECRET_VALEUR='(ecpk_(live|test)_|Password=|Pwd=|-----BEGIN)'

ligne() { # ligne <colonne 1> <colonne 2> <colonne 3> : alignement en caractères, pas en octets
  printf '%s%*s %s%*s %s\n' "$1" $((44 - ${#1})) '' "$2" $((6 - ${#2})) '' "$3"
}

verifier() { # verifier <libellé> <0 si OK> [détail]
  local statut="OK"
  if [[ "$2" != 0 ]]; then
    statut="ÉCHEC"
    echecs=$((echecs + 1))
  fi
  ligne "$1" "$statut" "${3:-}"
}

printf 'Image : %s\n\n' "$image"
ligne "Contrôle" "Statut" "Détail"

# --- Image -----------------------------------------------------------------------------
utilisateur="$(jq -r '.User // ""' <<<"$config")"
[[ -n "$utilisateur" && "$utilisateur" != root && "$utilisateur" != 0 && "$utilisateur" != 0:* ]]
verifier "Utilisateur non root" $? "User=${utilisateur:-<vide>, donc root>}"
[[ "$utilisateur" =~ ^[0-9]+(:[0-9]+)?$ && "${utilisateur%%:*}" != 0 ]]
verifier "Utilisateur numérique (runAsNonRoot)" $? "${utilisateur:-<vide>}"

jq -e '.ExposedPorts // {} | has("8080/tcp")' <<<"$config" >/dev/null
verifier "Port 8080 exposé" $? "$(jq -r '.ExposedPorts // {} | keys | join(" ")' <<<"$config")"

# Sans shell, « docker run --entrypoint sh » échoue avant de lancer quoi que ce soit.
docker run --rm --network none --entrypoint sh "$image" -c true >/dev/null 2>&1
[[ $? -ne 0 ]]
verifier "Pas de shell (image chiseled)" $? ""

# Le SDK ne doit pas partir en production : « dotnet --list-sdks » ne liste rien.
sdks="$(docker run --rm --network none --entrypoint dotnet "$image" --list-sdks 2>/dev/null)"
[[ -z "$sdks" ]]
verifier "Aucun SDK .NET dans l'image" $? "$(head -n 1 <<<"$sdks")"

noms="$(jq -r '.Env // [] | .[] | split("=")[0]' <<<"$config" | grep -i -E "$SECRET_NOM" | paste -sd ' ' -)"
[[ -z "$noms" ]]
verifier "Aucun secret dans ENV" $? "$noms"

historique="$(docker history --no-trunc --format '{{.CreatedBy}}' "$image" 2>/dev/null)"
! grep -q -E "$SECRET_VALEUR" <<<"$historique"
verifier "Aucun secret dans l'historique des couches" $? ""

taille="$(docker image inspect --format '{{.Size}}' "$image")"
ligne "Taille" "info" "$((taille / 1000000)) Mo"

# --- Dockerfile ------------------------------------------------------------------------
if [[ -n "$dockerfile" ]]; then
  echo
  printf 'Dockerfile : %s\n\n' "$dockerfile"
  # Lignes utiles : sans commentaires, continuations de ligne non prises en compte.
  instructions="$(grep -v -E '^[[:space:]]*(#|$)' "$dockerfile")"
  froms="$(grep -i -E '^[[:space:]]*FROM[[:space:]]' <<<"$instructions")"
  nb_from="$(grep -c . <<<"$froms")"
  [[ "$nb_from" -ge 2 ]]
  verifier "Construction multi-étapes" $? "$nb_from étapes"
  # Une étape qui part d'une étape précédente (FROM build) n'a pas besoin de digest.
  etapes="$(awk '{for (i = 1; i < NF; i++) if (tolower($i) == "as") print $(i + 1)}' <<<"$froms")"
  non_epinglees="$(awk '{for (i = 2; i <= NF; i++) if ($i !~ /^--/) {print $i; break}}' <<<"$froms" |
    grep -v -x -F -f <(printf '%s\n' "$etapes" "scratch") | grep -v '@sha256:' | paste -sd ' ' -)"
  [[ -z "$non_epinglees" ]]
  verifier "Images de base épinglées par digest" $? "$non_epinglees"
  finale="$(tail -n 1 <<<"$froms" | awk '{print $2}')"
  [[ "$finale" == *chiseled* ]]
  verifier "Image finale chiseled" $? "${finale%%@*}"
  derniere_etape="$(awk 'tolower($1) == "from" {etape = ""} {etape = etape $0 "\n"} END {printf "%s", etape}' <<<"$instructions")"
  grep -q -i -E '^[[:space:]]*USER[[:space:]]' <<<"$derniere_etape"
  verifier "USER dans l'étape finale" $? "$(grep -i -E '^[[:space:]]*USER' <<<"$derniere_etape" | xargs)"
  # Seuls l'instruction et le nom de la variable sont affichés, jamais la valeur.
  secrets_df="$(grep -i -E '^[[:space:]]*(ENV|ARG)[[:space:]]' <<<"$instructions" |
    grep -i -E "$SECRET_NOM|$SECRET_VALEUR" |
    sed -E 's/^[[:space:]]*([A-Za-z]+)[[:space:]]+([A-Za-z0-9_]+).*/\1 \2/' | paste -sd ',' -)"
  [[ -z "$secrets_df" ]]
  verifier "Aucun secret dans ENV ou ARG" $? "$secrets_df"
fi

echo
if ((echecs == 0)); then
  echo "IMAGE OK"
  exit 0
fi
echo "IMAGE KO : $echecs contrôle(s) en échec."
exit 1
