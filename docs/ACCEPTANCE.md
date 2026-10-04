# Manual acceptance checklist · 手动验收清单

Run these with the published `dist\WardogsTool-portable\WardogsTool.exe`, WARDOGS in **borderless windowed** mode, the tool started normally (not as administrator unless the game is). Items marked **(in game)** can only be checked inside WARDOGS; the others are also covered by the automated probe ([validation/input-probe-report.md](validation/input-probe-report.md)) but worth a quick look on your own setup.

## Hammer · 敲锤

- [ ] **(in game)** F9 starts hammering while WARDOGS is focused; the badge turns green RUNNING.
- [ ] **(in game)** A second F9 while running does nothing.
- [ ] **(in game)** Esc stops it — and the game still reacts to that Esc.
- [ ] **(in game)** 310 ms builds with the small/medium hammer, 510 ms with the large hammer, at the same rate as the Python tool on the same structure.
- [ ] **(in game)** Same rate with the tool window behind the game or minimized.
- [ ] **(in game)** Stopping in the middle of a 510 ms hold releases the button at once (you can look/click normally).
- [ ] **(in game)** F12 and closing the window mid-hold: no mouse button stays stuck.

## Anti-AFK · 防挂机

- [ ] **(in game)** F8 starts it (use a short period such as 10 s to watch); the countdown runs.
- [ ] **(in game)** Every generated key-down gets its key-up; no key stays stuck after stopping.
- [ ] Stop button / Esc stop it.

## Simultaneous operation · 同时运行

- [ ] **(in game)** Hammer + anti-AFK run together; hammering continues while the key fires.
- [ ] The Hammer tab's 停止敲锤 stops only hammering; the Anti-AFK tab's 停止防挂机 stops only anti-AFK.
- [ ] Esc stops both.

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

- [ ] **(in game)** F10 toggles the lens while WARDOGS is focused, and the game still receives F10.
- [ ] **(in game)** The centre of the screen is magnified (the lens shows the game, not a black or frozen image).
- [ ] **(in game)** Each zoom (1.5x–4.0x) looks right; the choice is remembered after restart.
- [ ] **(in game)** The lens is click-through and never takes focus: shooting/aiming through it works, the game keeps keyboard input.
- [ ] No recursive "mirror" effect inside the lens.
- [ ] **(in game)** The game stays responsive with the lens on (no stutter or noticeable input lag).
- [ ] F10 again removes it immediately; quitting the tool (F12 / ✕) removes it too.
- [ ] Multi-monitor: the lens appears on the monitor the game is on.

## Window · 窗口

- [ ] Settings tab → 窗口置顶 / Always on top keeps the tool above the game; unticking lets the game cover it.
- [ ] Restart: always-on-top, window position, tab, hammer preset, anti-AFK values, mortar position and zoom are remembered.
- [ ] Copy `WardogsTool.exe` to a machine without Python/.NET: it starts by double-click, no console window, **no administrator prompt**.
