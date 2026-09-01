# macOS USB test

This is an unsigned development build. It is self-contained: the target Mac does not need Git, Visual Studio, the .NET SDK, or a separately installed .NET runtime.

1. Copy `CodexUsage-*-usb.zip` to the Mac and unzip it to a local folder such as `~/Applications/CodexUsage`.
2. In Terminal, install the local plugin with one command:

   ```sh
   sh ~/Applications/CodexUsage/install-macos.sh
   ```

   The installer uses `codex` on PATH, or the bundled ChatGPT path at `/Applications/ChatGPT.app/Contents/Resources/codex`. It safely reinstalls only this local test marketplace and plugin.

3. Open a new Codex task and use `@Codex Usage Start!`. The installer prepares the matching bundled `.app`; the launcher selects the correct Mac architecture.
4. When Codex Usage asks for Accessibility access, enable **Codex Usage** in **System Settings → Privacy & Security → Accessibility**. It will continue automatically when access is granted.
5. If Gatekeeper blocks the unsigned app, use **System Settings → Privacy & Security → Open Anyway**, or remove quarantine only from the copied local app bundle:

   ```sh
   xattr -dr com.apple.quarantine ~/Applications/CodexUsage/plugins/codex-usage/bin/osx-arm64/Codex\ Usage.app
   ```

   Do not disable Gatekeeper globally.

Use `sh ~/Applications/CodexUsage/plugins/codex-usage/scripts/hud.sh Status` or `Stop` to manage the single helper instance.

Real-Mac verification remains required for the Accessibility prompt, Codex process identity, window geometry, Retina placement, and unsigned-helper behavior.
