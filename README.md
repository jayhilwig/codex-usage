# Codex Usage plugin

A Codex plugin that manages a separate, transparent companion window placing live Codex usage and public reset status immediately left of the Windows caption buttons. It does not patch, inject into, or alter the installed Codex app.

The plugin manifest is `.codex-plugin/plugin.json`. Its `codex-usage-hud` skill starts, stops, or checks the native companion through `scripts/hud.ps1`. The operating-system overlay remains a separate process because Codex plugin UI cannot occupy the native Windows caption area.

The Windows implementation is live-verified. macOS now has a native CoreGraphics/Accessibility tracking path and bundled self-contained helpers for USB testing; it still requires real-Mac verification. The OS window-discovery layer remains isolated behind `ICodexWindowTracker`.

## What the POC includes

- Finds the packaged Codex desktop window (`OpenAI.Codex_*\\app\\ChatGPT.exe`) or an unpackaged `OpenAI\\Codex\\Codex.exe` window.
- Renders `5h -- · W -- ↺`, then updates it with real remaining percentages.
- Uses an owned, borderless, no-activate window so it stays above Codex without becoming globally topmost.
- Polls window geometry every 100 ms, follows moves/resizes, handles per-monitor DPI, and hides when Codex is minimized, cloaked, or not visible.
- Opens compact usage and reset cards from the two control regions.
- Uses the platform's native system UI font and compact white, softly shadowed popovers that dismiss when focus moves outside them.
- Reads Codex usage over app-server stdio and reacts to `account/rateLimits/updated` (plus a one-minute fallback refresh).
- Reads the documented anonymous reset endpoint every five minutes, with ETag support and last-success caching.
- Persists only sanitized usage snapshots, public reset data, and a confirmed-event marker under `%LOCALAPPDATA%\\CodexUsageHud\\state.json`.
- Keeps Codex credentials local. The only third-party request is `GET https://codex-resets.com/api/v1/status`.

The three V1 preference controls (launch with Windows, show reset indicator, show usage HUD) are deliberately not in this first POC. There is no installer, updater, tray app, analytics, telemetry, account system, or dashboard. Right-click the HUD to exit it.

## Stack

- **.NET 10 / C#** for a small native process, async stdio JSON handling, and direct Windows interop without a browser runtime.
- **Avalonia 12.1.1** for one UI implementation that can run on Windows and macOS. Only the platform window tracker is OS-specific.
- **Win32 + DWM APIs** for top-level window enumeration, process-path identification, caption-button bounds, minimized/cloaked state, dark-mode hint, ownership, and physical-pixel positioning.

This keeps the cross-platform boundary explicit:

```text
src/CodexUsage.Core/
  usage/  Codex app-server client + normalized usage models
  reset/  public reset client + resolver + minimal local state
  window/ platform-neutral window snapshot/tracker contract
  ui/     platform-neutral HUD view model and time formatting

src/CodexUsage.Desktop/
  ui/       Avalonia title-bar HUD and both popovers
  Platform/ Windows tracker/interop, macOS adapter slot
```

On macOS, `CGWindowListCopyWindowInfo` discovers the visible Codex process/window by bundle ID or executable path. When Accessibility is granted, AX APIs provide reliable geometry and minimized/hidden state. None of the usage, reset, persistence, resolver, or UI code changes.

## Exact data interfaces

### Codex

The app starts the locally installed Codex process as:

```text
codex app-server --stdio
```

It performs the documented initialize handshake and sends:

```json
{ "method": "account/rateLimits/read", "id": 2 }
```

It also listens for:

```text
account/rateLimits/updated
```

The schema was verified against the installed `codex-cli 0.150.0-alpha.8` using:

```powershell
codex app-server generate-json-schema --out .tmp/app-server-schema --experimental
```

The live account response uses the `codex` bucket with:

- `primary.windowDurationMins = 300`
- `secondary.windowDurationMins = 10080`
- `usedPercent`
- `resetsAt` as Unix seconds

The client selects windows by duration rather than assuming primary/secondary ordering. Displayed remaining usage is `clamp(100 - usedPercent, 0, 100)`.

Official reference: [Codex App Server](https://developers.openai.com/codex/app-server).

### codex-resets.com

The docs page points to `/api/openapi.json`. The POC uses the documented status operation:

```text
GET https://codex-resets.com/api/v1/status
```

Relevant fields are `data.latest_reset`, `data.active_watch`, reset/watch timestamps, and `source.url`. No rendered HTML is scraped.

Reference: [codex-resets.com API docs](https://codex-resets.com/api/docs).

## Reset-state resolution

- **Gray:** no fresh, recent regular announcement and no active strong/elevated watch.
- **Amber:** a regular or banked announcement is less than eight hours old but local quota has not met the confirmation threshold; also used for an unexpired `strong` or `elevated` watch.
- **Green:** a recent regular public announcement exists, both five-hour and weekly remaining percentages rose by at least 20 points between local snapshots, at least one is now 90% or higher, the announcement falls within the comparison interval, and neither prior natural reset timestamp fell between those snapshots.
- **Unknown gray:** the public API request failed. Cached public data remains on disk but is not used to show stale certainty.

A confirmed event remains green for six hours. The resolver never treats the public site alone as account confirmation, never sends local usage to that site, and never auto-confirms banked-reset announcements.

## Run

Requirements: Windows 10/11, .NET 10 SDK, a current signed-in Codex CLI on `PATH`, and the Codex desktop window running.

```powershell
dotnet restore src\CodexUsage.Desktop\CodexUsage.Desktop.csproj
dotnet run --project src\CodexUsage.Desktop\CodexUsage.Desktop.csproj
```

After a build, you can also launch the companion directly while Codex is open:

```powershell
.\src\CodexUsage.Desktop\bin\Debug\net10.0\CodexUsage.Desktop.exe
```

Keep the executable with the other files in its output directory; this POC is not packaged as a single-file app yet.

If `codex` is not on `PATH`, set `CODEX_HUD_CODEX_PATH` to the executable path before launching. Right-click the HUD and choose **Exit Codex Usage**, or stop the foreground `dotnet run` process with Ctrl+C.

Run the focused resolver checks:

```powershell
dotnet run --project tests\CodexUsage.Core.Tests\CodexUsage.Core.Tests.csproj
```

## Verification completed

- Debug build: clean, zero warnings/errors.
- Live Codex window detection: packaged Windows app found by executable path.
- Live usage: real percentages and reset times displayed from app-server.
- Live public status: current `/api/v1/status` response parsed successfully.
- Move following: a temporary `160,80` Codex move produced the same `160,80` HUD delta, then the original placement was restored.
- Minimize behavior: zero visible HUD windows while Codex was minimized; one returned after restore.
- Baseline visual capture: the original HUD and both popovers rendered in the intended title-bar location at the active display scale. The current typography/popover refinement builds cleanly; a fresh interactive capture was unavailable from the non-interactive validation session.
- Resolver harness: six cases pass (offline, announced, confirmed, natural-reset exclusion, stale event, strong watch).

## Documented vs. packaging-specific behavior

The app-server method, notification, and rate-limit fields are documented by OpenAI. The codex-resets.com endpoint and payload are documented by its OpenAPI file. Windows positioning uses documented Win32/DWM APIs.

The one packaging-specific dependency is Windows Codex window identification: the current Microsoft Store package runs its UI as `ChatGPT.exe` under a path containing `OpenAI.Codex_`. That executable/package naming is not an app-server contract and may change in a future Codex release. It is isolated to `WindowsCodexWindowTracker`, so updating it does not affect shared code or either data source.
