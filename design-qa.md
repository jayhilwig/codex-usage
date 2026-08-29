# Design QA

## Source of truth

- Title-bar menu typography: `C:\Users\JAYHIL~2\AppData\Local\Temp\codex-clipboard-abd76483-df19-41e3-b922-41137a05e85f.png` (193 x 30 px).
- Codex popover treatment: `C:\Users\JAYHIL~2\AppData\Local\Temp\codex-clipboard-85865063-02f6-4dab-a01f-034589f19103.png` (321 x 249 px).
- Recent reset-announcement example: `C:\Users\JAYHIL~2\AppData\Local\Temp\codex-clipboard-bb3cb746-2df3-4545-a0aa-1f1fa28adc3b.png` (874 x 281 px).
- Typography uses Avalonia's platform-default UI family, equivalent to the requested CSS system stack: Segoe UI on Windows and the native system UI face on macOS. No font files are packaged.

## Implementation target

- Native Windows desktop overlay at the active display scale.
- HUD state: usage values plus reset indicator in the Codex title-bar area.
- Popover states: usage card, neutral recent-announcement card, announced card, confirmed card, and unavailable card.

## Visual acceptance checks

- HUD uses the platform-default UI face at 14 px and normal weight. At rest the overlay and both click targets are fully transparent, leaving only muted text and the reset glyph visible; a subtle four-pixel-radius highlight appears only on hover or press.
- Popovers are white with a 1 px `#E6E6E7` border, 12 px radius, compact 13 x 11 px internal padding, and low-opacity layered shadows.
- Usage popover has a fixed 248 px outer width and height-to-content sizing.
- Reset popover has a fixed 232 px outer width and height-to-content sizing.
- The neutral reset card displays the latest returned public announcement age even when it is outside the eight-hour amber alert window.
- The reset link label is `View reset announcements →` and opens `https://codex-resets.com/`.
- Each popover hides on window deactivation, covering clicks in Codex or elsewhere outside the card.

## Validation history

1. Static implementation review: passed. Typography, colors, compact dimensions, external link, and deactivation behavior are explicit in the Avalonia controls.
2. Debug desktop build: passed with zero warnings and zero errors.
3. Interactive screenshot/click pass: blocked. During this validation run, the command session could not enumerate any interactive desktop windows, including the running Codex window. The automated capture therefore could not locate or click either popover; the resulting title-bar image was unrelated screen content and was discarded.

## Final result

**Blocked for visual sign-off.** No implementation defect was observed in build or static checks, but the current styling and outside-click behavior still need one fresh interactive-desktop pass before visual QA can be marked passed.
