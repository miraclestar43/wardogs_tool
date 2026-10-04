# Manual acceptance checklist · 手动验收清单

Run these with the published `dist\WardogsTool-portable\WardogsTool.exe`, WARDOGS in **borderless windowed** mode, the tool started normally (not as administrator unless the game is). Items marked **(in game)** can only be checked inside WARDOGS; the others are also covered by the automated probe ([validation/input-probe-report.md](validation/input-probe-report.md)) but worth a quick look on your own setup.

## Hammer · 敲锤

- [ ] **(in game)** F9 starts hammering while WARDOGS is focused; the badge turns green RUNNING.
- [ ] **(in game)** A second F9 while running does nothing.
- [ ] **(in game)** 310 ms builds with the small/medium hammer, 510 ms with the large hammer, at the same rate as the Python tool on the same structure.
- [ ] **(in game)** Same rate with the tool window behind the game or minimized.

## Anti-AFK · 防挂机

- [ ] **(in game)** F8 starts it (use a short period such as 10 s to watch); the countdown runs.
- [ ] **(in game)** Every generated key-down gets its key-up; no key stays stuck.

## Stop: Esc, Stop buttons, F12 · 停止

Every Stop is a global stop. Start hammer (F9), anti-AFK (F8) and the magnifier (F10) together, then:

- [ ] **(in game)** **Esc** — hammering stops, anti-AFK stops, the lens disappears, no mouse button or key stays held, **the tool stays open**, and the game still reacts to that Esc (not swallowed).
- [ ] **(in game)** Esc in the middle of a 510 ms hold releases the button at once (you can look/click normally).
- [ ] The Hammer tab's **停止 STOP** button does exactly the same as Esc (everything stops, lens hidden).
- [ ] The Anti-AFK tab's **停止 STOP** button does exactly the same as Esc.
- [ ] After any stop the status line reads `已停止   F9 敲锤 | F8 防挂机 | F10 放大镜 | Esc 全部停止 | F12 退出`.
- [ ] **(in game)** **F12** — the same cleanup, then the tool exits; no lens, no stuck button. Closing the window with ✕ behaves the same.

## Hotkeys · 快捷键

- [ ] **(in game)** F8 / F9 / F10 / Esc work while WardogsTool is unfocused and after Alt-Tab.
- [ ] **(in game)** Holding a hotkey down does not retrigger it.
- [ ] **(in game)** The keys are not swallowed: the game still gets F8 / F9 / F10 / Esc (if it binds them).

## Mortar · 迫击炮

Enter the same values in `python wardogs_tool.py` and `WardogsTool.exe`:

| Mortar | Target | Expected (both tools) |
|---|---|---|
| `100.32 59.45` | `104.39 63.59` | `DIRECTION: 045°` · `RANGE:     581 m` · `Bearing exact: 44.51°   Range exact: 580.56 m` |
| `78.49 71.84` | `81.44, 70.78` | `DIRECTION: 110°` · `RANGE:     313 m` |
| `78.49 71.84` | `83.60 72.96` | `DIRECTION: 078°` · `RANGE:     523 m` |
| `78.49 71.84` | `abc 1` | red `目标坐标: could not convert string to float: 'abc'`; previous result stays |

- [ ] A few real in-game readings of your own give identical results in both tools.
- [ ] **(in game)** Firing with the shown direction/range hits as before.

## Magnifier · 放大镜

- [ ] **(in game)** Each F10 press advances exactly one step: **OFF → 2.0x → 3.0x → 4.0x → OFF → 2.0x …**; the game still receives F10.
- [ ] **(in game)** The centre of the screen is magnified (the lens shows the live game, not a black or frozen image).
- [ ] The Magnifier tab's list + 开 / 关 ON / OFF button shows 1.5x and 2.5x; F10 from 2.5x goes to 3.0x.
- [ ] **(in game)** The lens is click-through and never takes focus: shooting/aiming through it works, the game keeps keyboard input.
- [ ] **(in game)** The lens stays above the game after clicking into the game.
- [ ] No recursive "mirror" effect inside the lens.
- [ ] **(in game)** The game stays responsive with the lens on (no stutter or noticeable input lag).
- [ ] Multi-monitor: the lens appears on the monitor the game is on.

## Window and builds · 窗口与版本

- [ ] Settings tab → 窗口置顶 / Always on top keeps the tool above the game; unticking lets the game cover it.
- [ ] Restart: always-on-top, window position, tab, hammer preset, anti-AFK values, mortar position and zoom are remembered.
- [ ] Portable `dist\WardogsTool-portable\WardogsTool.exe` on a machine without Python/.NET: starts by double-click, no console window, **no administrator prompt**.
- [ ] Slim `dist\WardogsTool-slim\WardogsTool.exe`: starts where the **.NET 10 Desktop Runtime (x64)** is installed; where it is not, Windows shows its "install .NET" prompt (the app does not start).
