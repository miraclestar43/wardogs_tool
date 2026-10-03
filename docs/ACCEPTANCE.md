# Manual acceptance checklist

Run these with the published `dist\WardogsTool-portable\WardogsTool.exe`. Items marked **(in game)** can only be checked inside WARDOGS; the rest were also covered by the automated probe ([validation/input-probe-report.md](validation/input-probe-report.md)) but are worth a quick look on your own setup.

Setup: WARDOGS in borderless windowed mode, the tool started normally (not as administrator, unless the game is).

## Global hotkeys

- [ ] **(in game)** With the game focused, **F9** starts hammering; the tool's badge turns green RUNNING.
- [ ] **(in game)** **Esc** stops it — and the game still reacts to that Esc (menu opens/closes as usual): the key was not swallowed.
- [ ] **(in game)** **F8** starts anti-AFK; the status line counts down.
- [ ] **(in game)** F8 and F9 still do whatever the game binds them to (if anything).
- [ ] Holding F9 down does not restart hammering after Esc; only a new F9 press does.
- [ ] **F12** stops everything and closes the tool.

## Fast hammer

- [ ] **(in game)** 310 ms (小/中锤) builds with a small/medium hammer at the expected rate.
- [ ] **(in game)** 510 ms (大锤) builds with the large hammer at the expected rate.
- [ ] **(in game)** Compare with the Python tool on the same structure: same hits per minute, same building progress.
- [ ] The preset radios are disabled while running and re-enabled after Esc.

## Simultaneous hammer + anti-AFK

- [ ] **(in game)** F8, then F9: hammering continues normally while the anti-AFK key fires (set the period to e.g. 10 s to see it).
- [ ] **(in game)** The anti-AFK key (`c` by default) does what you expect in game and does not interrupt hammering in a harmful way.
- [ ] One Esc stops both.

## Stop cleanup (no stuck mouse button)

- [ ] **(in game)** Esc in the middle of a 510 ms hold: the hammer stops at once and the left button is released (you can click/look normally).
- [ ] **(in game)** F12 mid-hold: the tool closes and the button is released.
- [ ] **(in game)** Closing the window with ✕ mid-hold: same.
- [ ] Anti-AFK stopped right after a press: the key is not left held.

## Always on top

- [ ] Tick **窗口置顶 / Always on top**: the tool stays above the game window (borderless windowed).
- [ ] Untick it: the game can cover the tool again.
- [ ] Restart the tool: the setting is remembered, and so are the window position, tab, hammer preset, anti-AFK values and mortar position.

## Python-vs-C# mortar parity

Enter the same values in both tools (`python wardogs_tool.py` and `WardogsTool.exe`) and compare all three lines:

| Mortar | Target | Expected |
|---|---|---|
| `100.32 59.45` | `104.39 63.59` | `DIRECTION: 045°` · `RANGE:     581 m` · `Bearing exact: 44.51°   Range exact: 580.56 m` |
| `78.49 71.84` | `81.44, 70.78` | `DIRECTION: 110°` · `RANGE:     313 m` |
| `78.49 71.84` | `83.60 72.96` | `DIRECTION: 078°` · `RANGE:     523 m` |
| `78.49 71.84` | `abc 1` | red `目标坐标: could not convert string to float: 'abc'`, previous result stays |
| a few real in-game readings of your own | | identical in both |

- [ ] After Enter, the target text is selected and typing replaces it; the mortar position stays.
- [ ] **(in game)** Firing with the shown direction/range hits as it did with the Python tool.

## Distribution

- [ ] Copy `WardogsTool.exe` to a machine (or user account) with no Python and no .NET installed: it starts by double-click, no console window, no UAC prompt.
