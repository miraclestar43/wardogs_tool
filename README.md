# wardogs_tool

A small Windows desktop tool for WARDOGS: anti-AFK key presses, fast hammering, and a mortar calculator.
It's one Python file and uses only the standard library (tkinter + ctypes). Nothing to install.

WARDOGS 小工具：防挂机、快速敲锤、迫击炮计算。单文件，只用 Python 标准库，无需安装依赖。

## 运行

需要 Windows 和 Python 3.8+。

```powershell
python wardogs_tool.py
```

运行自检（迫击炮计算）：

```powershell
python wardogs_tool.py --test
```

## 快捷键

快捷键是全局的，游戏窗口在前台时也有效。

| 键 | 功能 |
|---|---|
| **F8** | 开始防挂机 |
| **F9** | 开始快速敲锤 |
| **Esc** | 全部停止 |
| **F12** | 停止并退出 |

## 功能

### 防挂机（F8）

每隔一个周期自动按几下指定按键，避免挂机被踢。默认每 180 秒按 2 下 `c`，两下间隔 500 ms。

按键、次数、间隔、周期都可以在「按键」页修改。按 F8 后，第一次按键在一个周期之后，状态栏会显示倒计时。运行时设置不能改，先按 Esc 停止再改。

### 快速敲锤（F9）

自动循环敲锤：按下鼠标左键 → 保持一段时间 → 抬起 40 ms → 再按下。按下时长有两档：

| 按下时长 | 适用 |
|---|---|
| 310 ms | 小锤 / 中锤 |
| 510 ms | 大锤 |

### 迫击炮计算（「迫击炮」页）

1. 在「迫击炮 X Y」里输入迫击炮坐标，例如 `100.32 59.45`，按回车。
2. 在「目标 X Y」里输入目标坐标，例如 `104.39 63.59`，按回车。
3. 结果以大字显示方向和距离，下面是精确值。

```
DIRECTION: 045°
RANGE:     581 m
Bearing exact: 44.51°   Range exact: 580.56 m
```

算完后目标框会自动全选，直接输入下一个目标即可，迫击炮坐标保持不变。要换迫击炮位置，直接修改那一栏。坐标用空格或逗号分隔都可以。最近 20 次结果显示在下方历史中。

坐标约定：

- X 向东增加，Y 向北增加。
- 1.00 坐标单位 = 100 米。
- 方向是罗盘方位：北 0°、东 90°、南 180°、西 270°。
- 方向四舍五入到整度，距离四舍五入到整米。

```python
dx = target_x - mortar_x
dy = target_y - mortar_y
distance_m = math.hypot(dx, dy) * 100
bearing_deg = math.degrees(math.atan2(dx, dy)) % 360
```

## 其他

- 勾选窗口底部的「窗口置顶」，工具窗口会保持在游戏上面。
- 如果游戏以管理员身份运行，工具也要用管理员身份运行，否则模拟的按键和鼠标可能不起作用。
