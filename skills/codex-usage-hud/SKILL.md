---
name: codex-usage-hud
description: Start, stop, or check the local Codex title-bar usage HUD. Use when the user asks to show, launch, hide, stop, restart, or check the status of the Codex usage overlay.
---

# Codex Usage

Manage the native companion through the plugin launcher at `../../scripts/hud.ps1`, resolved relative to this skill directory.

- Start or show: run `hud.ps1 -Action Start`.
- Stop or hide: run `hud.ps1 -Action Stop`.
- Check whether it is running: run `hud.ps1 -Action Status`.
- Restart: stop, then start.

The launcher builds the Windows POC incrementally before starting it, then runs it as a hidden background helper. Report the launcher's concise result to the user.

Do not read, display, or transmit Codex credentials. Do not send local usage data to any third party. The overlay itself makes only the existing anonymous request to the public codex-resets.com API.

The plugin does not modify or inject into the Codex desktop app. Its visible interface remains a separate native overlay because plugin UI cannot occupy the operating system caption area.
