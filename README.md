# WARDOGS Tool · 战狗土木 / 建造辅助工具

**战狗（WARDOGS）土木、建造辅助工具：快速敲锤，附带迫击炮计算器（方向 / 距离）、屏幕中心放大镜（倍镜）和防挂机。**
**Fast hammering for building in WARDOGS, plus a mortar calculator, a centre-screen magnifier (zoom) and anti-AFK.**


做工兵、修建工事、盖建筑时不用再狂点鼠标：按 **F9** 自动敲锤（速敲 / 光速敲锤），小锤、中锤、大锤各有合适的按住时长。整个程序不到 100 KB，双击 `WardogsTool.exe` 即用：Windows 10 1903 及以上 / Windows 11 自带所需的运行环境，不需要另外安装 Python 或 .NET，不需要管理员权限。

WardogsTool takes the clicking out of construction: press **F9** and it hammers for you, with the right hold time for small, medium and large hammers. The whole tool is under 100 KB; double-click `WardogsTool.exe`. Windows 10 1903+ and Windows 11 already include what it needs — no Python, no separate .NET install, no administrator rights.

下载链接download：https://github.com/miraclestar43/wardogs_tool/releases/latest

**关键词 Keywords：** 战狗 · WARDOGS · 战狗工具 · 土木 · 建造 · 工兵 · 修建 · 敲锤 · 快速敲锤 · 自动敲锤 · 速敲 · 光速敲锤 · 大锤 · 中锤 · 小锤 · 迫击炮 · 迫击炮计算 · 迫击炮计算器 · 迫击炮方向 · 迫击炮距离 · 方位角 · 坐标计算 · 放大镜 · 倍镜 · 屏幕放大 · 中心放大 · 防挂机 · fast hammer · auto hammer · mortar calculator · magnifier · zoom · anti-AFK

| 功能 Feature | 快捷键 Hotkey |
|---|---|
| **快速敲锤 Fast hammer**（土木 / 建造 / 工兵）：关 → 大锤 510 → 小/中锤 310 → 关 · OFF → Large → Small/Medium → OFF | **F9** |
| 防挂机 Anti-AFK | F8 |
| 放大镜 Magnifier：关 → 2.0x → 3.0x → 4.0x → 关 OFF → 2x → 3x → 4x → OFF | F10 |
| 迫击炮计算 Mortar calculator | 「迫击炮」页 Mortar tab |
| **全部停止**（敲锤、防挂机、放大镜），不退出 · Stop everything (hammer, anti-AFK, magnifier), stay open | **Esc** |
| 全部停止并退出 Stop everything and quit | F12 |

快捷键全局有效（游戏在前台时也能用），只监听、不拦截：游戏照常收到 F8、F9、F10、Esc。Esc 是紧急停止：停下敲锤和防挂机、松开工具按下的鼠标键和按键、关掉放大镜，工具本身保持打开。
Hotkeys are global and only observed, never swallowed: the game still receives F8, F9, F10 and Esc. Esc is the emergency stop: it stops hammering and anti-AFK, releases anything the tool holds, hides the magnifier, and leaves the tool open.

---

## 1. 快速敲锤 · Fast hammer（F9）
<img width="588" height="813" alt="c78adc6d2cf9fdb4c7dc6c97f261552a" src="https://github.com/user-attachments/assets/eb40052a-c441-466a-9c37-2e0e6b0f37ec" />

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

- **F9 每按一次切换一档：关 → 大锤 510 ms → 小/中锤 310 ms → 关**，按住不放只算一次。换档时先松开左键，再按新的节奏开始。「敲锤」页高亮当前档位，底部状态栏也一直显示，例如 `运行中   F9 敲锤：大锤 510 ms`。**Esc** 不论在哪一档都立即回到「关」（同时全部停止）。也可以用页面上的单选框 + 「开始所选」按钮启动。第一下立即敲下。
  **Each F9 press advances one step: OFF → Large 510 ms → Small/Medium 310 ms → OFF**; holding the key counts once. Switching releases the left button before the new timing starts. The Hammer tab highlights the current step and the status bar always shows it. **Esc** returns to OFF at once from any step (and stops everything). The radio buttons + START button also start a preset. The first hit is immediate.
- 停止、按 F12 或关闭窗口时一定会松开左键，不会让鼠标卡在按下状态。工具只松开它自己按下的键。
  Stopping, F12 or closing the window always releases the left button; the tool only releases a button it pressed itself.
- 工具窗口最小化或被游戏挡住时，敲击节奏不变。
  Timing stays exact while the tool is minimized or behind the game.

## 2. 防挂机 · Anti-AFK（F8）

定时自动按键，避免挂机被踢。默认每 180 秒按 2 下 `c`，间隔 500 ms，每下按住 50 ms。按键、次数、间隔、周期可在「防挂机」页修改。按 F8 后第一次按键在一个周期之后，页面显示倒计时。

Presses a key on a timer so you are not kicked for being idle. Default: `c` twice, 500 ms apart, every 180 s, each held 50 ms. The first press comes one full period after F8.

<img width="591" height="734" alt="image" src="https://github.com/user-attachments/assets/71adaa7f-0363-422d-9c99-22b62bd741c3" />


F8 和 F9 各自启动，可以同时运行。任何「停止」按钮和 **Esc** 都是全部停止（敲锤、防挂机、放大镜）。
F8 and F9 start independently and can run together. Every Stop button and **Esc** stop everything (hammer, anti-AFK, magnifier).

## 3. 迫击炮计算器 · Mortar calculator（方向 / 距离 / 坐标计算）

用战狗地图上的坐标算出迫击炮要打的**方向（方位角）**和**距离**，不用手算。
Turns WARDOGS map coordinates into the mortar's firing **direction (bearing)** and **range** — no mental maths.

<img width="572" height="831" alt="image" src="https://github.com/user-attachments/assets/d2b59f61-b952-4fc0-a74b-371d200dc837" />

在「迫击炮」页直接照抄地图上显示的坐标：先输入一次炮位（如 `x100.32, y59.45`，回车），再逐个输入目标（如 `x104.39, y63.59`，回车）：
On the **Mortar** tab, type coordinates exactly as the WARDOGS map shows them: the mortar position once (e.g. `x100.32, y59.45`), then each target (e.g. `x104.39, y63.59`):

```
DIRECTION: 045°
RANGE:     581 m
Bearing exact: 44.51°   Range exact: 580.56 m
```

算完后目标框自动全选，直接输入下一个目标；炮位保持不变；输入有误时上一次结果保留，错误用红字显示；保留最近 20 条结果。
After each result the target box is selected for the next one; the mortar position is kept; errors show in red and keep the last result; the last 20 results are listed.

支持的坐标写法 Accepted formats（大小写都行，空格随意 · case-insensitive, spaces optional）：

| 写法 Format | 说明 Notes |
|---|---|
| `x134.98, y65.56` | **地图上的原样格式（推荐）** · as shown on the WARDOGS map (recommended) |
| `x134.98 y65.56` · `X134.98, Y65.56` · `x=134.98, y=65.56` · `X = 134.98 Y = 65.56` | 同上的变体 · variants |
| `y65.56, x134.98` | 有 x / y 标记时按标记读，顺序不限 · with labels, order does not matter |
| `134.98 65.56` · `134.98, 65.56` | 不带标记：先 X 后 Y · without labels: X first, then Y |

只写了一个、写了两个 x、数字和下一个标记连在一起（如 `x1y2`）、或者带标记和不带标记混用时，会提示格式不对，不会猜。
Input with a missing or repeated label, a number glued to the next label (`x1y2`), or a mix of labeled and unlabeled numbers is rejected rather than guessed.

坐标约定 Convention：X 向东 east、Y 向北 north，1.00 = 100 m；方向 bearing 北 0°、东 90°、南 180°、西 270°；方向取整到度（359.5° → 000°），距离取整到米。

```text
dx = target_x - mortar_x
dy = target_y - mortar_y
range   = sqrt(dx² + dy²) × 100
bearing = atan2(dx, dy) in degrees, normalised to [0, 360)
```

## 4. 放大镜 / 倍镜 · Magnifier（F10）

给没有倍镜的武器用，相当于一个屏幕中心的外置放大倍镜：在屏幕中心显示一个 600×400 的放大镜头。**F10 每按一次切换一档：关 → 2.0x → 3.0x → 4.0x → 关**，循环往复。「放大镜」页还可以选 1.5x 和 2.5x，再用「开 / 关」按钮打开。镜头在游戏所在显示器的中心，鼠标可以穿透，不抢焦点，不出现在任务栏，不画准星。**Esc** 也会立即关闭它。

For weapons without optics: a 600×400 lens at the centre of the game's monitor. **Each F10 press advances one step: OFF → 2.0x → 3.0x → 4.0x → OFF.** The Magnifier tab also offers 1.5x and 2.5x with its on/off button. Click-through, never takes focus, no taskbar entry, no crosshair drawn. **Esc** removes it immediately too.

<img width="586" height="731" alt="dca941bc92a476c15d5c1972fe7b11b2" src="https://github.com/user-attachments/assets/6ddc807c-68ab-4132-b800-5bfd92ff8f26" />

放大镜使用 Windows 自带的放大 API（Magnification API），只放大屏幕上已经显示出来的画面。请用 **无边框窗口** 模式运行游戏；独占全屏下 Windows 无法合成叠加窗口。
It uses the documented Windows Magnification API and only enlarges pixels already on screen. Run the game in **borderless windowed** mode; exclusive fullscreen cannot be overlaid.

---

## 下载与运行 · Download & run

**系统要求 Requirements：Windows 10 1903 或更新版本（64 位），或 Windows 11。**
**Windows 10 version 1903 or later (64-bit), or Windows 11.**

程序基于 .NET Framework 4.8，它已经内置在 Windows 10 1903+ 和 Windows 11 中（Windows 11 自带 4.8.1），**不需要另外安装任何 .NET 运行库**。
WardogsTool runs on .NET Framework 4.8, which is built into Windows 10 1903+ and Windows 11 (Windows 11 ships 4.8.1) — **no separate .NET runtime installation is needed.**

用下面「构建」一节生成 `dist\WardogsTool\`，里面三个文件要放在一起（可以打包成 zip 分发），双击 `WardogsTool.exe` 运行：
Build `dist\WardogsTool\` (see Build below); keep its three files together (e.g. zipped) and double-click `WardogsTool.exe`:

| 文件 File | 大小 Size |
|---|---|
| `WardogsTool.exe` | 约 50 KB |
| `WardogsTool.Core.dll` | 约 46 KB |
| `WardogsTool.exe.config` | < 1 KB |

设置（档位、防挂机参数、炮位、放大倍率、窗口置顶、窗口位置）保存在 `%AppData%\WardogsTool\settings.json`，格式版本 `schemaVersion` 为 **2**（旧的版本 1 文件会自动读取，放大倍率取默认 2.0x）；文件损坏时使用默认设置并把原文件备份为 `settings.json.bad`。
Settings are saved to `%AppData%\WardogsTool\settings.json`, schema version **2** (version-1 files still load, with the default 2.0x zoom). A corrupt file falls back to defaults and is kept as `settings.json.bad`.

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

只有开发者构建时才需要 .NET SDK（这里用 10.0 版 SDK 编译 .NET Framework 4.8 程序；用户运行时不需要）。脚本固定使用 `C:\dotnet10\dotnet.exe`（可用环境变量 `WARDOGS_DOTNET` 指定其他路径），找不到就报错。
Only building needs a .NET SDK (the 10.0 SDK is used here to compile the .NET Framework 4.8 app; users do not need it). The scripts use `C:\dotnet10\dotnet.exe` (or `WARDOGS_DOTNET`) and stop if it is missing.

```powershell
# 一次性：用户目录安装 SDK（不需要管理员） one-time, no admin
powershell -ExecutionPolicy Bypass -File dotnet-install.ps1 -Channel 10.0 -InstallDir C:\dotnet10 -NoPath

build.cmd      # Release 构建
test.cmd       # 单元测试（在 .NET Framework 4.8 上运行） unit tests, run on .NET Framework 4.8
publish.cmd    # → dist\WardogsTool\：WardogsTool.exe + WardogsTool.Core.dll + WardogsTool.exe.config（约 97 KB）
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
