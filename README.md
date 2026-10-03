# WARDOGS Tool · 战狗土木 / 建造辅助工具

**Fast hammering for building in WARDOGS, plus anti-AFK and a mortar calculator.**
**WARDOGS（战狗）土木 / 建造辅助：快速敲锤，附带防挂机和迫击炮计算。**

WARDOGS Tool takes the clicking out of construction: press **F9** and it hammers for you, with the right hold time for small, medium and large hammers. It is a small native Windows app — one `WardogsTool.exe`, no Python, no .NET install, no administrator rights.

做工兵、修建、盖建筑时不用再狂点鼠标：按 **F9** 自动敲锤（速敲 / 光速敲锤），小锤、中锤、大锤各有合适的按住时长。单个 `WardogsTool.exe`，双击即用，不需要 Python，不需要安装 .NET，不需要管理员权限。

| Feature 功能 | Hotkey 快捷键 |
|---|---|
| **Fast hammer / 快速敲锤** — builder & engineer construction | **F9** |
| Anti-AFK / 防挂机 | F8 |
| Mortar calculator / 迫击炮计算 | (Mortar tab) |
| Stop everything / 全部停止 | **Esc** |
| Stop and quit / 停止并退出 | F12 |

Hotkeys are global (they work while the game has focus) and are only observed, never swallowed: the game still receives F8, F9 and Esc.
快捷键全局有效（游戏在前台时也能用），只监听、不拦截：游戏照常收到 F8、F9、Esc。

---

## 1. Fast hammer · 快速敲锤（F9）

The main feature. Hold the hammer, let go, hit again — automatically:

主功能。自动循环：按住左键 → 松开 → 再敲：

```
mouse left down ──hold──▶ mouse left up ──40 ms──▶ repeat
左键按下 ──按住时长──▶ 左键抬起 ──40 ms──▶ 循环
```

| Hold 按住时长 | Hammer 锤子 |
|---|---|
| **310 ms** | Small / medium hammer 小锤 / 中锤 |
| **510 ms** | Large hammer 大锤 |

- Choose the preset on the **Hammer / 敲锤** tab, press **F9** in game, press **Esc** to stop.
  在「敲锤」页选好档位，进游戏按 **F9** 开始，按 **Esc** 停止。
- The first hit happens immediately. A big **RUNNING / STOPPED** badge shows the state.
  第一下立即敲下；页面上的 **RUNNING / STOPPED** 标志显示是否在敲。
- Stopping, pressing F12, or closing the window always releases the left mouse button — the tool never leaves it held down.
  停止、按 F12 或关闭窗口时都会松开左键，不会让鼠标卡在按下状态。

## 2. Anti-AFK · 防挂机（F8）

Presses a key on a timer so you are not kicked for being idle. Default: press `c` twice, 500 ms apart, every 180 seconds; each press is held 50 ms. Key, count, gap and period can be changed on the **Anti-AFK / 防挂机** tab (stop with Esc first). The first press comes one full period after F8; the tab shows the countdown.

定时自动按键，避免挂机被踢。默认每 180 秒按 2 下 `c`，间隔 500 ms，每下按住 50 ms。按键、次数、间隔、周期可在「防挂机」页修改（先按 Esc 停止）。按 F8 后第一次按键在一个周期之后，页面会显示倒计时。

Hammer and anti-AFK can run at the same time; Esc stops both.
敲锤和防挂机可以同时运行，Esc 一起停止。

## 3. Mortar calculator · 迫击炮计算

A secondary utility on the **Mortar / 迫击炮** tab. Type the mortar position once (`100.32 59.45`, Enter), then each target (`104.39 63.59` or `104.39, 63.59`, Enter):

附带的小工具，在「迫击炮」页。先输入一次炮位（`100.32 59.45`，回车），再逐个输入目标（`104.39 63.59` 或 `104.39, 63.59`，回车）：

```
DIRECTION: 045°
RANGE:     581 m
Bearing exact: 44.51°   Range exact: 580.56 m
```

The target box is selected after each result, so you can type the next target straight away; the mortar position is kept. The last 20 results are listed.
算完后目标框自动全选，直接输入下一个目标；炮位保持不变；保留最近 20 条结果。

Convention 坐标约定: +X east 东, +Y north 北, 1.00 = 100 m; bearing north 0°, east 90°, south 180°, west 270°; direction rounded to whole degrees (359.5° → 000°), range to whole metres.

```text
dx = target_x - mortar_x
dy = target_y - mortar_y
range   = sqrt(dx² + dy²) × 100
bearing = atan2(dx, dy) in degrees, normalised to [0, 360)
```

---

## Download & run · 下载与运行

Windows 10/11 x64. Build the portable EXE yourself (below), then double-click `dist\WardogsTool-portable\WardogsTool.exe`. Settings (preset, anti-AFK values, mortar position, always-on-top, window position, tab) are saved to `%AppData%\WardogsTool\settings.json`.

Windows 10/11 x64。按下面的命令生成便携版，双击 `dist\WardogsTool-portable\WardogsTool.exe` 即可。设置保存在 `%AppData%\WardogsTool\settings.json`。

- Tick **窗口置顶 / Always on top** to keep the tool above the game. 勾选「窗口置顶」让工具浮在游戏上面。
- If the game runs as administrator, run the tool as administrator too, or Windows blocks its input. 游戏以管理员身份运行时，工具也要以管理员身份运行，否则按键和点击会被系统拦截。
- The tool only sends normal Windows mouse/keyboard input. It does not touch the game process, its memory or its files. 本工具只发送普通的 Windows 键鼠输入，不注入、不读写游戏内存、不修改游戏文件。

## Build · 构建

Requires the .NET 10 SDK. The scripts use `C:\dotnet10\dotnet.exe` (set `WARDOGS_DOTNET` to use another one) and stop with an error if it is missing; they never fall back to a system `dotnet` without the SDK.

```powershell
# one-time: install the SDK user-locally (no admin)
powershell -ExecutionPolicy Bypass -File dotnet-install.ps1 -Channel 10.0 -InstallDir C:\dotnet10 -NoPath

build.cmd      # Release build
test.cmd       # unit tests
publish.cmd    # → dist\WardogsTool-portable\WardogsTool.exe (self-contained, single file, ~59 MB)
```

`WardogsTool.exe --test` runs the mortar self-test (exit code 0 = pass).

## Project layout

```text
src/WardogsTool.Core/          game-independent logic, no Windows/WPF dependency
  Hammer/ AntiAfk/             state machines behind IMouseInput / IKeyboardInput / ITimeSource
  Mortar/ Parsing/             mortar maths, display strings, coordinate parsing
  Input/ Settings/ Timing/
src/WardogsTool.App/           WPF app
  Platform/Windows/            the only Win32 interop (input, Raw Input hotkeys, timer resolution)
  ViewModels/ Views/
tests/WardogsTool.Core.Tests/  xUnit, incl. parity fixtures recorded from the Python tool
tools/WardogsTool.InputProbe/  integration probe (drives both tools, records injected input)
tools/parity/                  Python fixture generator and driver
wardogs_tool.py                original Python/Tkinter version — behavioural reference, kept during the migration
```

See [docs/PORTING.md](docs/PORTING.md) for the behaviour inventory and validation, and [docs/ACCEPTANCE.md](docs/ACCEPTANCE.md) for the in-game checklist.

### Python version · Python 版

The original single-file Python/Tkinter tool is still here as the reference: `python wardogs_tool.py` (Python 3.8+, standard library only). 原来的 Python 单文件版仍保留，作为行为基准。
