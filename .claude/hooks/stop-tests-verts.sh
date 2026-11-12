#!/usr/bin/env bash
# Hook Stop : empêche Claude de conclure tant que des tests échouent.
#
# - Échappatoire : lancer Claude Code avec CLAUDE_SKIP_STOP_TESTS=1 (phase rouge, urgence).
# - Si rien n'a changé depuis le dernier passage au vert, les tests ne sont pas relancés.
# - Au plus 3 blocages par session (CLAUDE_STOP_TESTS_MAX) : ensuite, le hook laisse
#   Claude conclure et vous prévient par un message.
#
# Code 2 : Claude ne s'arrête pas et reçoit stderr comme consigne. Code 0 : il conclut.
# On ne s'arrête pas sur stop_hook_active : on veut que Claude reprenne jusqu'au vert,
# et c'est le compteur qui empêche une boucle sans fin.
set -uo pipefail

MAX_BLOCAGES="${CLAUDE_STOP_TESTS_MAX:-3}"

if [[ -n "${CLAUDE_SKIP_STOP_TESTS:-}" ]]; then exit 0; fi

entree="$(cat)"
session="$(jq -r '.session_id // "inconnue"' <<<"$entree" 2>/dev/null || echo inconnue)"
projet="${CLAUDE_PROJECT_DIR:-$(jq -r '.cwd // empty' <<<"$entree" 2>/dev/null)}"
cd "$projet" 2>/dev/null || exit 0
[[ -f ECommerce.slnx ]] || exit 0

etat="${TMPDIR:-/tmp}/claude-stop-tests/$session"
mkdir -p "$etat"

# Empreinte de l'arbre de travail : HEAD, modifications suivies et fichiers non suivis.
# Hors d'un dépôt Git, pas d'empreinte fiable : les tests sont relancés à chaque fois.
empreinte=""
if git rev-parse --verify -q HEAD >/dev/null 2>&1; then
  empreinte="$({
    git rev-parse HEAD
    git diff HEAD
    git ls-files --others --exclude-standard -z | xargs -0 cat 2>/dev/null
  } | git hash-object --stdin)"
fi

if [[ -n "$empreinte" && "$(cat "$etat/dernier-vert" 2>/dev/null)" == "$empreinte" ]]; then
  rm -f "$etat/blocages" # rien n'a changé depuis le dernier passage au vert
  exit 0
fi

if sortie="$(DOTNET_CLI_UI_LANGUAGE=en dotnet test ECommerce.slnx 2>&1)"; then
  echo "$empreinte" >"$etat/dernier-vert"
  rm -f "$etat/blocages"
  exit 0
fi

blocages=$(($(cat "$etat/blocages" 2>/dev/null || echo 0) + 1))
echo "$blocages" >"$etat/blocages"

if ((blocages > MAX_BLOCAGES)); then
  jq -n --arg m "Hook Stop : des tests échouent encore après $MAX_BLOCAGES tentatives de correction. Claude peut conclure, mais vérifiez vous-même avec dotnet test ECommerce.slnx." \
    '{systemMessage: $m}'
  exit 0
fi

{
  echo "Des tests échouent (blocage $blocages sur $MAX_BLOCAGES) : corrige le code avant de conclure."
  echo "Ne modifie pas un test pour le faire passer. Si un test te semble faux, arrête-toi et explique pourquoi."
  echo "Extrait de dotnet test :"
  {
    grep -E 'error (CS|MSB)[0-9]+' <<<"$sortie" | sed -E 's/ \[[^]]*\]$//' | sort -u
    grep -E -A4 'Failed [A-Za-z]' <<<"$sortie" | grep -v -E '^\s*(Stack Trace:|at |--$)'
  } | head -n 15
} >&2
exit 2
