---
name: codex-usage-hud
description: Start, stop, or check the local Codex title-bar usage companion. Use when the user asks to show, launch, hide, stop, restart, or check the status of the Codex usage overlay.
---

# Codex Usage

Manage the native companion through the bundled platform launcher, resolved relative to this skill directory.

- Windows: run `scripts/hud.ps1 -Action Start|Stop|Status`.
- macOS: run `sh scripts/hud.sh Start|Stop|Status`.
- Restart: stop, then start.

The launchers use the bundled self-contained helper for the matching OS/architecture. Keep the response concise; do not narrate implementation details unless an error occurs.

Do not read, display, or transmit Codex credentials. Do not send local usage data to any third party. The overlay itself makes only the existing anonymous request to the public codex-resets.com API.

The plugin does not modify or inject into the Codex desktop app. Its visible interface remains a separate native overlay because plugin UI cannot occupy the operating system caption area.
