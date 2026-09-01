#!/bin/sh
set -eu

package_root=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
bundled_codex=/Applications/ChatGPT.app/Contents/Resources/codex

if command -v codex >/dev/null 2>&1; then
  codex_cli=$(command -v codex)
elif [ -x "$bundled_codex" ]; then
  codex_cli=$bundled_codex
else
  echo "Codex CLI was not found on PATH or at $bundled_codex." >&2
  exit 1
fi

if [ ! -f "$package_root/.agents/plugins/marketplace.json" ] || [ ! -f "$package_root/plugins/codex-usage/.codex-plugin/plugin.json" ]; then
  echo "This folder is not a complete Codex Usage USB package." >&2
  exit 1
fi

# ZIP extraction from a Windows-built package does not reliably preserve Unix mode bits.
# Restore only the two bundled app executables before Codex copies this local package.
for helper in \
  "$package_root/plugins/codex-usage/bin/osx-arm64/Codex Usage.app/Contents/MacOS/CodexUsage.Desktop" \
  "$package_root/plugins/codex-usage/bin/osx-x64/Codex Usage.app/Contents/MacOS/CodexUsage.Desktop"; do
  if [ -f "$helper" ]; then
    chmod +x "$helper"
  fi
done

# Reinstall only this package's own marketplace/plugin so repeating this command updates
# the local test cleanly without requiring Git, Homebrew, .NET, or any other dependency.
"$codex_cli" plugin remove codex-usage@codex-usage-usb >/dev/null 2>&1 || true
"$codex_cli" plugin marketplace remove codex-usage-usb >/dev/null 2>&1 || true
"$codex_cli" plugin marketplace add "$package_root"
"$codex_cli" plugin add codex-usage@codex-usage-usb

echo "Codex Usage installed. Open a new Codex task and use: @Codex Usage Start!"
