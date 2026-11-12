#!/usr/bin/env bash
# Serveur MCP CodeScene en mode LOCAL, sans jeton : l'analyse Code Health tourne sur la machine,
# contre ce dépôt Git, avec le référentiel codescene/code-health-rules.json
# (rendu visible à CodeScene par le lien .codescene/code-health-rules.json).
# Conception : codescene/agent-codescene.md, §5.2.
set -euo pipefail

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

# Mode local : aucune variable d'authentification ni d'instance, même si le shell en définit.
unset CS_ACCESS_TOKEN CS_ONPREM_URL CS_ACCOUNT_ID CS_DEFAULT_PROJECT_ID

export CS_DISABLE_VERSION_CHECK=true
# Liste blanche : analyse locale uniquement. Pas de rules_config_set_* (l'agent ne doit pas
# assouplir les règles), pas d'outils d'instance (hotspots, dette, ownership, usage).
export CS_ENABLED_TOOLS="analyze_change_set,pre_commit_code_health_safeguard,code_health_review,code_health_score,rules_config_validate,rules_config_list_thresholds,verify_installation,explain_code_health"

# Version épinglée (publiée depuis au moins 14 jours au moment du choix).
exec npx -y @codescene/codehealth-mcp@1.5.16
