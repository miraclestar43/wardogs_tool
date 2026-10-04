# WARDOGS Tool · 战狗土木 / 建造辅助工具

**战狗（WARDOGS）土木、建造辅助：快速敲锤，附带防挂机、迫击炮计算和屏幕中心放大镜。**
**Fast hammering for building in WARDOGS, plus anti-AFK, a mortar calculator and a centre-screen magnifier.**

做工兵、修建工事、盖建筑时不用再狂点鼠标：按 **F9** 自动敲锤（速敲 / 光速敲锤），小锤、中锤、大锤各有合适的按住时长。单个 `WardogsTool.exe`，双击即用，不需要安装 Python 或 .NET，不需要管理员权限。

WardogsTool takes the clicking out of construction: press **F9** and it hammers for you, with the right hold time for small, medium and large hammers. One `WardogsTool.exe` — no Python, no .NET install, no administrator rights.

| 功能 Feature | 快捷键 Hotkey |
|---|---|
| **快速敲锤 Fast hammer**（土木 / 建造 / 工兵 builder & engineer） | **F9** |
| 防挂机 Anti-AFK | F8 |
| 放大镜 Magnifier（屏幕中心放大 centre-screen zoom） | F10 |
| 迫击炮计算 Mortar calculator | 「迫击炮」页 Mortar tab |
| 停止敲锤和防挂机 Stop hammer + anti-AFK | **Esc** |
| 停止并退出 Stop and quit | F12 |

快捷键全局有效（游戏在前台时也能用），只监听、不拦截：游戏照常收到 F8、F9、F10、Esc。
Hotkeys are global and only observed, never swallowed: the game still receives F8, F9, F10 and Esc.

---

## 1. 快速敲锤 · Fast hammer（F9）

主功能。自动循环敲锤：按住左键 → 松开 → 40 ms 后再敲。
The main feature — hold, release, hit again, automatically:

```
左键按下 ──按住时长──▶ 左键抬起 ──40 ms──▶ 循环
mouse down ──hold──▶ mouse up ──40 ms──▶ repeat
```

| 按住时长 Hold | 锤子 Hammer |
|---|---|
| **310 ms** | 小锤 / 中锤 Small / medium hammer |
| **510 ms** | 大锤 Large hammer |

- 在「敲锤」页选好档位，进游戏按 **F9** 开始；按 **Esc** 或「停止敲锤」停止。第一下立即敲下，页面上的 **RUNNING / STOPPED** 标志显示状态。
  Pick the preset on the **Hammer** tab, press **F9** in game; **Esc** or the stop button stops it. The first hit is immediate.
- 停止、按 F12 或关闭窗口时一定会松开左键，不会让鼠标卡在按下状态。工具只松开它自己按下的键。
  Stopping, F12 or closing the window always releases the left button; the tool only releases a button it pressed itself.
- 工具窗口最小化或被游戏挡住时，敲击节奏不变。
  Timing stays exact while the tool is minimized or behind the game.

## 2. 防挂机 · Anti-AFK（F8）

定时自动按键，避免挂机被踢。默认每 180 秒按 2 下 `c`，间隔 500 ms，每下按住 50 ms。按键、次数、间隔、周期可在「防挂机」页修改。按 F8 后第一次按键在一个周期之后，页面显示倒计时。

Presses a key on a timer so you are not kicked for being idle. Default: `c` twice, 500 ms apart, every 180 s, each held 50 ms. The first press comes one full period after F8.

敲锤和防挂机可以同时运行。各自页面的「停止」按钮只停自己那一项；**Esc 两个都停**。
Hammer and anti-AFK can run together. Each tab's stop button stops only that feature; **Esc stops both**.

## 3. 迫击炮计算 · Mortar calculator

在「迫击炮」页：先输入一次炮位（如 `100.32 59.45`，回车），再逐个输入目标（`104.39 63.59` 或 `104.39, 63.59`，回车）：
On the **Mortar** tab: type the mortar position once, then each target:

```
DIRECTION: 045°
RANGE:     581 m
Bearing exact: 44.51°   Range exact: 580.56 m
```

算完后目标框自动全选，直接输入下一个目标；炮位保持不变；输入有误时上一次结果保留，错误用红字显示；保留最近 20 条结果。
After each result the target box is selected for the next one; the mortar position is kept; errors show in red and keep the last result; the last 20 results are listed.

坐标约定 Convention：X 向东 east、Y 向北 north，1.00 = 100 m；方向 bearing 北 0°、东 90°、南 180°、西 270°；方向取整到度（359.5° → 000°），距离取整到米。

```text
dx = target_x - mortar_x
dy = target_y - mortar_y
range   = sqrt(dx² + dy²) × 100
bearing = atan2(dx, dy) in degrees, normalised to [0, 360)
```

## 4. 放大镜 · Magnifier（F10）

给没有倍镜的武器用：按 **F10** 在屏幕中心显示一个 600×400 的放大镜头，倍率 1.5x / 2.0x / 2.5x / 3.0x / 4.0x（默认 2.0x，会记住）。镜头在游戏所在显示器的中心，鼠标可以穿透，不抢焦点，不出现在任务栏，不画准星。再按 F10 立即关闭。

For weapons without optics: **F10** shows a 600×400 lens at the centre of the game's monitor, zoom 1.5x–4.0x (default 2.0x, remembered). Click-through, never takes focus, no taskbar entry, no crosshair drawn. F10 again removes it immediately.

放大镜使用 Windows 自带的放大 API（Magnification API），只放大屏幕上已经显示出来的画面。请用 **无边框窗口** 模式运行游戏；独占全屏下 Windows 无法合成叠加窗口。
It uses the documented Windows Magnification API and only enlarges pixels already on screen. Run the game in **borderless windowed** mode; exclusive fullscreen cannot be overlaid.

---

## 下载与运行 · Download & run

Windows 10/11 x64。按下面「构建」一节生成 `dist\WardogsTool-portable\WardogsTool.exe`，双击运行。设置（档位、防挂机参数、炮位、放大倍率、窗口置顶、窗口位置）保存在 `%AppData%\WardogsTool\settings.json`。

Build the portable EXE (below) and double-click it. Settings are saved to `%AppData%\WardogsTool\settings.json`.

- 在「设置」页勾选「窗口置顶」可以让工具浮在游戏上面。Tick **Always on top** on the Settings tab to keep the tool above the game.
- 游戏以管理员身份运行时，工具也要以管理员身份运行，否则按键和点击会被 Windows 拦截。If the game runs as administrator, run the tool as administrator too.

## 免责声明 · Disclaimer

WardogsTool 是独立的非官方工具，与 WARDOGS 及其开发商、发行商没有任何关联，也未获其认可。

WardogsTool 在游戏进程之外运行：不读取或修改游戏内存，不注入代码或 DLL，不修改游戏文件，不检查游戏网络流量，不调用游戏内部接口，不绕过反作弊，也不隐藏自身运行。

它与游戏有关的功能仅限于：敲锤和防挂机所用的普通 Windows 键鼠输入；根据用户手动输入的坐标计算迫击炮方向和距离；通过 Windows API 检测全局快捷键；以及使用 Windows 公开的显示 / 截取接口进行可选的屏幕放大。放大镜不读取游戏状态、不识别目标、不瞄准、不做图像识别，也不改变游戏。

使用自动化或外部工具仍可能受游戏规则或服务条款限制，是否允许使用由用户自行判断并承担责任。

WardogsTool is an independent, unofficial utility and is not affiliated with or endorsed by WARDOGS or its developers/publishers.

WardogsTool operates outside the game process. It does not read or modify game memory, inject code or DLLs, modify game files, inspect game network traffic, access internal game APIs, bypass anti-cheat systems, or attempt to conceal its operation.

Its game-related functionality is limited to ordinary Windows mouse/keyboard input for Hammer and Anti-AFK, mortar calculations from user-entered coordinates, global hotkey detection through Windows APIs, and optional screen magnification using documented Windows display/capture APIs. The Magnifier does not inspect game state, detect targets, aim weapons, perform computer vision, or alter the game.

Use of automation/external utilities may still be restricted by a game's rules or Terms of Service. Users are responsible for determining whether their use is permitted.

WardogsTool 是独立实现。MortarHUD 仅作为项目 / 设计参考，WardogsTool 中没有复制任何 MortarHUD 源代码。
WardogsTool is an independent implementation. MortarHUD was consulted only as a project/design reference; no MortarHUD source code is copied into WardogsTool.

---

## 构建 · Build

需要 .NET 10 SDK。脚本固定使用 `C:\dotnet10\dotnet.exe`（可用环境变量 `WARDOGS_DOTNET` 指定其他路径），找不到就报错，不会退回到系统里没有 SDK 的 `dotnet`。
Requires the .NET 10 SDK; the scripts use `C:\dotnet10\dotnet.exe` (or `WARDOGS_DOTNET`) and stop if it is missing.

```powershell
# 一次性：用户目录安装 SDK（不需要管理员） one-time, no admin
powershell -ExecutionPolicy Bypass -File dotnet-install.ps1 -Channel 10.0 -InstallDir C:\dotnet10 -NoPath

build.cmd      # Release 构建
test.cmd       # 单元测试 unit tests
publish.cmd    # → dist\WardogsTool-portable\WardogsTool.exe（自包含单文件 self-contained single file，约 59 MB）
```

`WardogsTool.exe --test` 运行迫击炮自检（退出码 0 = 通过）。

## 项目结构 · Project layout

```text
src/WardogsTool.Core/          与游戏无关的逻辑，不依赖 Windows/WPF   game-independent logic
  Hammer/ AntiAfk/             状态机 state machines (IMouseInput / IKeyboardInput / ITimeSource)
  Mortar/ Parsing/ Magnifier/  迫击炮计算、坐标解析、放大镜几何
  Input/ Settings/ Timing/
src/WardogsTool.App/           WPF 程序
  Platform/Windows/            所有 Win32 调用：键鼠输入、Raw Input 快捷键、高精度计时、放大镜
  ViewModels/ Views/ Services/
tests/WardogsTool.Core.Tests/  xUnit，包含从 Python 版录制的对照数据 parity fixtures
tools/WardogsTool.InputProbe/  集成测试探针：驱动两个版本并记录实际发出的输入
tools/parity/                  Python 对照数据生成器 fixture generator
wardogs_tool.py                原 Python/Tkinter 版，作为行为基准保留 original version, kept as the reference
```

详见 [docs/PORTING.md](docs/PORTING.md)（行为对照与验证）和 [docs/ACCEPTANCE.md](docs/ACCEPTANCE.md)（游戏内验收清单）。

### Python 版 · Python version

原来的 Python 单文件版仍保留：`python wardogs_tool.py`（Python 3.8+，只用标准库）。它没有放大镜。
The original single-file Python tool is still here as the reference (no magnifier).
