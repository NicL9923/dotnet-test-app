#!/usr/bin/env sh
set -eu

BASE_URL="${MINIONTANK_BASE_URL:-https://app-miniontank-aux-staging.azurewebsites.net}"
SKILL_REPO="${MINIONTANK_SKILL_REPO:-NicL9923/dotnet-test-app}"
SKILL_NAME="${MINIONTANK_SKILL_NAME:-miniontank}"
AGENT_KEY="${MINIONTANK_AGENT_KEY:-}"

if [ -z "$AGENT_KEY" ]; then
  if [ -r /dev/tty ]; then
    printf "Paste your MinionTank agent key: " > /dev/tty
    IFS= read -r AGENT_KEY < /dev/tty
  else
    printf "MINIONTANK_AGENT_KEY is required when no TTY is available.\n" >&2
    exit 1
  fi
fi

case "$AGENT_KEY" in
  agent_*) ;;
  *)
    printf "Agent key must start with 'agent_'.\n" >&2
    exit 1
    ;;
esac

SHELL_RC="$HOME/.profile"
case "${SHELL:-}" in
  *zsh) SHELL_RC="$HOME/.zshrc" ;;
  *bash) SHELL_RC="$HOME/.bashrc" ;;
esac

touch "$SHELL_RC"
if grep -q '^export MINIONTANK_AGENT_KEY=' "$SHELL_RC"; then
  tmp="$(mktemp)"
  sed "s|^export MINIONTANK_AGENT_KEY=.*|export MINIONTANK_AGENT_KEY=\"$AGENT_KEY\"|" "$SHELL_RC" > "$tmp"
  mv "$tmp" "$SHELL_RC"
else
  printf '\nexport MINIONTANK_AGENT_KEY="%s"\n' "$AGENT_KEY" >> "$SHELL_RC"
fi

if grep -q '^export MINIONTANK_BASE_URL=' "$SHELL_RC"; then
  tmp="$(mktemp)"
  sed "s|^export MINIONTANK_BASE_URL=.*|export MINIONTANK_BASE_URL=\"${BASE_URL%/}\"|" "$SHELL_RC" > "$tmp"
  mv "$tmp" "$SHELL_RC"
else
  printf 'export MINIONTANK_BASE_URL="%s"\n' "${BASE_URL%/}" >> "$SHELL_RC"
fi

export MINIONTANK_AGENT_KEY="$AGENT_KEY"
export MINIONTANK_BASE_URL="${BASE_URL%/}"

if command -v gh >/dev/null 2>&1; then
  if gh skill --help >/dev/null 2>&1; then
    gh skill install "$SKILL_REPO" "$SKILL_NAME" --agent github-copilot --scope user || \
      printf "gh skill install failed. Retry with: gh skill install %s %s --agent github-copilot --scope user\n" "$SKILL_REPO" "$SKILL_NAME" >&2
  else
    printf "Your GitHub CLI does not include 'gh skill'. Update gh to 2.90.0+ or install the skill manually.\n" >&2
  fi
else
  printf "GitHub CLI not found. Install gh 2.90.0+ to use 'gh skill install'.\n" >&2
fi

curl -fsS "$MINIONTANK_BASE_URL/api/me" -H "X-Agent-Key: $MINIONTANK_AGENT_KEY" >/dev/null

printf "MinionTank configured. The skill is sourced from %s and can be updated with: gh skill update %s\n" "$SKILL_REPO" "$SKILL_NAME"
printf "Restart your shell and Copilot CLI session so new environment variables are inherited.\n"
