#!/usr/bin/env bash
# Hook PostToolUse, matcher "Edit|Write" : reformate les espaces du fichier C# modifié.
# Ne bloque jamais : un échec de formatage ne doit pas interrompre le travail.
fichier="$(jq -r '.tool_input.file_path // empty')"
[[ "$fichier" == *.cs ]] || exit 0

cd "$CLAUDE_PROJECT_DIR" || exit 0
relatif="${fichier#"$CLAUDE_PROJECT_DIR"/}"

# --folder : formate le fichier sans charger ni restaurer les projets, donc vite.
dotnet format whitespace --folder --include "$relatif" >/dev/null 2>&1
exit 0
