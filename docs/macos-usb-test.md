# macOS USB test

This is an unsigned development build. It is self-contained: the target Mac does not need Git, Visual Studio, the .NET SDK, or a separately installed .NET runtime.

1. Copy `CodexUsage-*-usb.zip` to the Mac and unzip it to a local folder such as `~/Applications/CodexUsage`.
2. In Terminal, install the local plugin with one command:

   ```sh
   sh ~/Applications/CodexUsage/install-macos.sh
   ```

   The installer uses `codex` on PATH, or the bundled ChatGPT path at `/Applications/ChatGPT.app/Contents/Resources/codex`. It safely reinstalls only this local test marketplace and plugin.

3. Open a new Codex task and use `@Codex Usage Start!`. The installer prepares the matching bundled `.app`; the launcher selects the correct Mac architecture.
4. Codex Usage uses window metadata only. It should not request Accessibility or Screen Recording access.
5. If Gatekeeper blocks the unsigned app, use **System Settings → Privacy & Security → Open Anyway**, or remove quarantine only from the copied local app bundle:

   ```sh
   xattr -dr com.apple.quarantine ~/Applications/CodexUsage/plugins/codex-usage/bin/osx-arm64/Codex\ Usage.app
   ```

   Do not disable Gatekeeper globally.

Use `sh ~/Applications/CodexUsage/plugins/codex-usage/scripts/hud.sh Status` or `Stop` to manage the single helper instance.

Real-Mac verification covers Codex process identity, window geometry, Retina placement, popovers, Exit behavior, and unsigned-helper launch behavior.
