#!/usr/bin/env bash
# Hook PreToolUse, matcher "Bash" : bloque les commandes Git qui réécrivent l'historique.
# Complète les règles deny du lab 1 : le motif porte ici sur toute la ligne de commande,
# quelle que soit la place de l'option.
commande="$(jq -r '.tool_input.command // empty')"

if grep -Eq 'git +push.*(--force|(^| )-f( |$))|git +reset +--hard' <<<"$commande"; then
  echo "Interdit dans ce dépôt : $commande" >&2
  echo "Propose une alternative sans réécriture d'historique (nouveau commit, git revert, branche de travail)." >&2
  exit 2 # l'outil n'est pas exécuté ; Claude reçoit ce message
fi
exit 0
