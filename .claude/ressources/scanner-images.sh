#!/usr/bin/env bash
# Scanne des images avec trivy et range les rapports dans les preuves de la copie.
#
#   bash "$FORMATION"/labs/lab7-conteneurs/ressources/scanner-images.sh <copie> <libellé> <image>...
#   bash .../scanner-images.sh ~/lab-ecommerce avant ecommerce/catalog:avant
#   bash .../scanner-images.sh ~/lab-ecommerce apres ecommerce/{catalog,ordering,gateway,web}:1.0
#
# Pour chaque image : rapport JSON complet dans <copie>/preuves/lab7/scans/<libellé>/,
# une ligne de synthèse par gravité, puis le détail des vulnérabilités hautes et critiques.
#
# Code de retour : 0 si aucune vulnérabilité haute ou critique, 1 sinon, 2 si le scan ne peut pas tourner.
set -uo pipefail

copie="${1:-}"
libelle="${2:-}"
[[ $# -ge 3 ]] || {
  echo "Usage : scanner-images.sh <copie> <libellé> <image>..." >&2
  exit 2
}
shift 2
[[ -f "$copie/ECommerce.slnx" ]] || { echo "Copie introuvable : $copie (ECommerce.slnx absent)." >&2; exit 2; }
[[ "$libelle" =~ ^[a-z0-9-]+$ ]] || { echo "Libellé invalide : $libelle (minuscules, chiffres, tirets)." >&2; exit 2; }
for outil in trivy jq docker; do
  command -v "$outil" >/dev/null 2>&1 || {
    echo "Outil manquant : $outil." >&2
    [[ "$outil" == trivy ]] && echo "Sans trivy : docker scout cves --only-severity critical,high local://<image>" >&2
    exit 2
  }
done

dossier="$copie/preuves/lab7/scans/$libelle"
mkdir -p "$dossier" || exit 2
total_hautes=0
rapports=()

printf '%-34s %8s %6s %7s %5s %8s\n' "Image" "CRITICAL" "HIGH" "MEDIUM" "LOW" "UNKNOWN"
for image in "$@"; do
  docker image inspect "$image" >/dev/null 2>&1 || { echo "Image introuvable : $image" >&2; exit 2; }
  rapport="$dossier/$(tr '/:@' '___' <<<"$image").json"
  # Vulnérabilités seulement : paquets du système et dépendances .NET (*.deps.json).
  if ! trivy image --quiet --scanners vuln --format json --output "$rapport" "$image"; then
    echo "Échec du scan de $image (base de vulnérabilités inaccessible ?)." >&2
    exit 2
  fi
  compter() { jq --arg g "$1" '[.Results[]? | .Vulnerabilities[]? | select(.Severity == $g)] | length' "$rapport"; }
  c="$(compter CRITICAL)" h="$(compter HIGH)" m="$(compter MEDIUM)" l="$(compter LOW)" u="$(compter UNKNOWN)"
  printf '%-34s %8s %6s %7s %5s %8s\n' "$image" "$c" "$h" "$m" "$l" "$u"
  rapports+=("$rapport")
  total_hautes=$((total_hautes + c + h))
done

echo
echo "Base de vulnérabilités : $(trivy version --format json | jq -r '.VulnerabilityDB.UpdatedAt // "inconnue"')"
echo "Rapports : $dossier"

if ((total_hautes > 0)); then
  echo
  echo "Vulnérabilités hautes et critiques (gravité, identifiant, paquet, version installée, version corrigée) :"
  for rapport in "${rapports[@]}"; do
    jq -r '.ArtifactName as $img | .Results[]? | .Class as $classe | .Vulnerabilities[]?
           | select(.Severity == "HIGH" or .Severity == "CRITICAL")
           | "  \($img)  \(.Severity)  \(.VulnerabilityID)  \(.PkgName)  \(.InstalledVersion)  -> \(.FixedVersion // "aucun correctif publié")  [\($classe)]"' "$rapport"
  done | sort -u
  exit 1
fi
echo "Aucune vulnérabilité haute ou critique."
exit 0
