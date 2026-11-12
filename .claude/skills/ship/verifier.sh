#!/usr/bin/env bash
# Vérifications avant livraison : build, tests, puis format des fichiers C# modifiés.
# S'arrête au premier échec. Code de retour : 0 si tout passe, 1 sinon.
#
# Le format n'est vérifié que sur les fichiers modifiés par la branche : la base de code
# contient déjà 7 écarts connus dans src/ECommerce.ServiceDefaults/Extensions.cs.
set -uo pipefail
export DOTNET_CLI_UI_LANGUAGE=en DOTNET_NOLOGO=1

racine="$(git rev-parse --show-toplevel 2>/dev/null)" || { echo "Pas dans un dépôt Git." >&2; exit 1; }
cd "$racine" || exit 1

etape() { # etape <libellé> <commande...> : n'affiche la sortie complète qu'en cas d'échec
  local libelle="$1" sortie
  shift
  if sortie="$("$@" 2>&1)"; then
    echo "OK     $libelle"
  else
    echo "ÉCHEC  $libelle"
    echo "$sortie" | grep -E -i 'error|failed|échec|warn' | head -n 30
    echo "(commande : $*)"
    exit 1
  fi
}

etape "Build (dotnet build ECommerce.slnx)" dotnet build ECommerce.slnx
etape "Tests (dotnet test ECommerce.slnx)" dotnet test ECommerce.slnx --no-build

# Fichiers C# modifiés : par rapport à main, plus les modifications non commitées.
base="$(git merge-base HEAD origin/main 2>/dev/null || git merge-base HEAD main 2>/dev/null || true)"
fichiers="$({
  if [[ -n "$base" ]]; then git diff --name-only --diff-filter=ACMR "$base" -- '*.cs'; fi
  git diff --name-only --diff-filter=ACMR HEAD -- '*.cs'
  git ls-files --others --exclude-standard -- '*.cs'
} | sort -u)"

if [[ -z "$fichiers" ]]; then
  echo "OK     Format : aucun fichier C# modifié"
else
  # shellcheck disable=SC2086
  etape "Format des fichiers modifiés ($(echo "$fichiers" | wc -l | tr -d ' ') fichier(s))" \
    dotnet format ECommerce.slnx --verify-no-changes --include $fichiers
fi

echo "Vérifications terminées : prêt à livrer."
