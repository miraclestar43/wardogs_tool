# wardogs_tool · 战狗小工具

A small Windows desktop tool for **WARDOGS (战狗)**. It does three things: fast hammering (auto hammer), anti-AFK key presses, and mortar direction/range calculation.
It's one Python file and uses only the standard library (tkinter + ctypes), so there's nothing to install.

**WARDOGS（战狗）** 的 Windows 桌面小工具，有三个功能：**快速敲锤**（自动敲锤、速敲、光速敲锤，支持小锤 / 中锤 / 大锤）、**防挂机**（定时自动按键），以及**迫击炮计算**（方向和距离）。单个 Python 文件，只用标准库，无需安装任何依赖。

> Keywords / 关键词：WARDOGS, 战狗, 敲锤, 快速敲锤, 速敲, 光速敲锤, 自动敲锤, 小锤, 中锤, 大锤, 防挂机, anti-AFK, 迫击炮, mortar calculator, auto hammer

---

## Run · 运行

Requires Windows and Python 3.8+. / 需要 Windows 和 Python 3.8 以上。

```powershell
python wardogs_tool.py
```

Self-test (mortar calculation) / 自检（迫击炮计算）：

```powershell
python wardogs_tool.py --test
```

## Hotkeys · 快捷键

Hotkeys are global, so they work while the game window is focused.
快捷键是全局的，游戏窗口在前台时也有效。

| Key 键 | Action | 功能 |
|---|---|---|
| **F8** | Start anti-AFK | 开始防挂机 |
| **F9** | Start fast hammering | 开始快速敲锤 |
| **Esc** | Stop everything | 全部停止 |
| **F12** | Stop and quit | 停止并退出 |

## Features · 功能

### Fast hammering · 快速敲锤（F9）

Hammers automatically in a loop: hold the left mouse button → release for 40 ms → hold again. There are two hold durations:

自动循环敲锤，也就是"速敲 / 光速敲锤"：按住鼠标左键 → 抬起 40 ms → 再按下。按住时长有两档：

| Hold 按住时长 | For | 适用 |
|---|---|---|
| 310 ms | Small / medium hammer | 小锤 / 中锤 |
| 510 ms | Large hammer | 大锤 |

### Anti-AFK · 防挂机（F8）

Presses a key a few times every period so you don't get kicked for being idle. By default it presses `c` twice, 500 ms apart, every 180 seconds. You can change the key, count, gap and period on the 按键 (Keys) tab. The first press comes one full period after you press F8, and the status bar counts down to it. The settings are locked while it runs, so press Esc first to change them.

每隔一个周期自动按几下指定按键，防止挂机被踢。默认每 180 秒按 2 下 `c`，两下间隔 500 ms。按键、次数、间隔和周期都可以在「按键」页修改。按 F8 后，第一次按键在一个周期之后，状态栏会显示倒计时。运行时设置不能改，先按 Esc 停止再改。

### Mortar calculator · 迫击炮计算（「迫击炮」tab）

1. Enter the mortar position in 迫击炮 X Y, e.g. `100.32 59.45`, then press Enter.
   在「迫击炮 X Y」里输入迫击炮坐标，例如 `100.32 59.45`，按回车。
2. Enter the target in 目标 X Y, e.g. `104.39 63.59`, then press Enter.
   在「目标 X Y」里输入目标坐标，例如 `104.39 63.59`，按回车。
3. The direction and range appear in large text, with the exact values below.
   结果以大字显示方向和距离，下面是精确值。

```
DIRECTION: 045°
RANGE:     581 m
Bearing exact: 44.51°   Range exact: 580.56 m
```

After each calculation the target field is selected, so you can type the next target straight away while the mortar position stays the same. To move the mortar, edit its field. You can separate X and Y with a space or a comma. The last 20 results are listed in the history below.

算完后目标框会自动全选，直接输入下一个目标即可，迫击炮坐标保持不变。要换迫击炮位置，直接修改那一栏。坐标用空格或逗号分隔都可以。最近 20 次结果显示在下方历史中。

**Coordinate convention · 坐标约定**

- X increases eastward, Y increases northward. / X 向东增加，Y 向北增加。
- 1.00 coordinate unit = 100 m. / 1.00 坐标单位 = 100 米。
- Direction is a compass bearing: North 0°, East 90°, South 180°, West 270°. / 方向是罗盘方位：北 0°、东 90°、南 180°、西 270°。
- Direction is rounded to the nearest degree and range to the nearest meter. / 方向四舍五入到整度，距离四舍五入到整米。

```python
dx = target_x - mortar_x
dy = target_y - mortar_y
distance_m = math.hypot(dx, dy) * 100
bearing_deg = math.degrees(math.atan2(dx, dy)) % 360
```

## Notes · 其他

- Tick 窗口置顶 (Always on top) at the bottom of the window to keep the tool above the game.
  勾选窗口底部的「窗口置顶」，工具窗口会保持在游戏上面。
- If the game runs as administrator, run this tool as administrator too, or the simulated keys and clicks may be ignored.
  如果游戏以管理员身份运行，工具也要用管理员身份运行，否则模拟的按键和鼠标可能不起作用。
