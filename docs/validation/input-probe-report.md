# Input probe report

Run: 2026-10-03 17:44 · Microsoft Windows NT 10.0.26100.0 · C# build: `WardogsTool.exe`

Result: **24/24 scenarios passed**. Left-button presses that reached the probe window: 154.

## Hammer 310 ms, tool window minimized

| | Python | C# |
|---|---|---|
| **result** | PASS | PASS |
| first mouse-down after F9 ms | 39.1 | 6.4 |
| hold n | 17 | 17 |
| hold mean/min/max ms | 310.7 / 310.4 / 311.1 | 310.7 / 310.4 / 311.8 |
| gap n | 17 | 17 |
| gap mean/min/max ms | 40.7 / 40.4 / 41.3 | 40.7 / 40.4 / 40.9 |
| mouse-up after Esc ms | 16.0 | 2.6 |
| downs / ups | 18 / 18 | 18 / 18 |
| mouse event shapes | LBUTTONDOWN flags=0x1 mouseData=0x0 extra=0x0; LBUTTONUP flags=0x1 mouseData=0x0 extra=0x0 | LBUTTONDOWN flags=0x1 mouseData=0x0 extra=0x0; LBUTTONUP flags=0x1 mouseData=0x0 extra=0x0 |
| tool window minimized | True | True |

## Hammer 310 ms, stop during hold

| | Python | C# |
|---|---|---|
| **result** | PASS | PASS |
| first mouse-down after F9 ms | 7.0 | 8.7 |
| hold n | 8 | 8 |
| hold mean/min/max ms | 310.8 / 310.5 / 311.2 | 310.7 / 310.4 / 311.2 |
| gap n | 8 | 8 |
| gap mean/min/max ms | 42.1 / 40.4 / 47.3 | 40.7 / 40.4 / 41.4 |
| mouse-up after Esc ms | 19.2 | 2.2 |
| downs / ups | 9 / 9 | 9 / 9 |
| mouse event shapes | LBUTTONDOWN flags=0x1 mouseData=0x0 extra=0x0; LBUTTONUP flags=0x1 mouseData=0x0 extra=0x0 | LBUTTONDOWN flags=0x1 mouseData=0x0 extra=0x0; LBUTTONUP flags=0x1 mouseData=0x0 extra=0x0 |

## Hammer, stop during gap

| | Python | C# |
|---|---|---|
| **result** | PASS | PASS |
| events after Esc | 0 | 0 |
| downs / ups | 4 / 4 | 4 / 4 |

## Anti-AFK c ×2 / 500 ms / 3 s, stop while a key is held

| | Python | C# |
|---|---|---|
| **result** | PASS | PASS |
| press times after F8 ms | 3010.9, 3510.9, 6010.9 | 3012.1, 3512.0, 6011.6 |
| key held ms | 50.5, 50.6, 50.5 | 50.8, 50.9, 50.5 |
| 2nd press after 1st ms | 499.9 | 499.9 |
| round 2 after round 1 ms | 3000.0 | 2999.5 |
| key event shapes | KEYDOWN vk=0x43 scan=0x2E flags=0x10 extra=0x0; KEYUP vk=0x43 scan=0x2E flags=0x90 extra=0x0 | KEYDOWN vk=0x43 scan=0x2E flags=0x10 extra=0x0; KEYUP vk=0x43 scan=0x2E flags=0x90 extra=0x0 |

## Hammer + anti-AFK together, one Esc stops both

| | Python | C# |
|---|---|---|
| **result** | PASS | PASS |
| mouse downs before / after first c | 8 / 3 | 8 / 3 |
| c downs / ups | 2 / 2 | 2 / 2 |
| events later than Esc + 120 ms | 0 | 0 |

## F9 auto-repeat is not a new press

| | Python | C# |
|---|---|---|
| **result** | PASS | PASS |
| mouse downs while F9 held, before Esc | 3 | 3 |
| mouse downs after Esc while F9 still repeating | 0 | 0 |

## F12 during hold releases and exits

| | Python | C# |
|---|---|---|
| **result** | PASS | PASS |
| process exited | True | True |
| mouse-up after quit ms | 17.7 | 1.4 |

## Hammer 510 ms, stop during hold

| | Python | C# |
|---|---|---|
| **result** | PASS | PASS |
| first mouse-down after F9 ms | 19.0 | 16.8 |
| hold n | 5 | 5 |
| hold mean/min/max ms | 510.7 / 510.4 / 511.1 | 510.9 / 510.8 / 511.2 |
| gap n | 5 | 5 |
| gap mean/min/max ms | 41.1 / 40.5 / 42.6 | 41.0 / 40.6 / 41.6 |
| mouse-up after Esc ms | 16.6 | 2.0 |
| downs / ups | 6 / 6 | 6 / 6 |
| mouse event shapes | LBUTTONDOWN flags=0x1 mouseData=0x0 extra=0x0; LBUTTONUP flags=0x1 mouseData=0x0 extra=0x0 | LBUTTONDOWN flags=0x1 mouseData=0x0 extra=0x0; LBUTTONUP flags=0x1 mouseData=0x0 extra=0x0 |

## Closing the window during hold releases and exits

| | Python | C# |
|---|---|---|
| **result** | PASS | PASS |
| process exited | True | True |
| mouse-up after quit ms | 0.7 | 0.9 |

## No errors printed by the tool (stderr)

| | Python |
|---|---|
| **result** | PASS |
| stderr lines | 0 |

## Mortar workflow in the window (three known cases)

| | C# |
|---|---|
| **result** | PASS |
| 100.32 59.45 -> 104.39 63.59 | DIRECTION: 045° \| RANGE:     581 m |
| 78.49 71.84 -> 81.44, 70.78 | DIRECTION: 110° \| RANGE:     313 m |
| 78.49 71.84 -> 83.60 72.96 | DIRECTION: 078° \| RANGE:     523 m |
| history rows | 3 |
| error line | 目标坐标: could not convert string to float: 'abc' |

## Always on top + settings persistence

| | C# |
|---|---|
| **result** | PASS |
| elevated | False |
| topmost at start (setting false) | False |
| topmost after ticking the box | True |
| saved settings | {"schemaVersion":2,"hammer":{"holdMs":310},"antiAfk":{"key":"c","count":"2","gapMs":"500","periodSeconds":"180"},"mortar":{"position":"78.49 71.84"},"magnifier":{"zoom":2},"window":{"alwaysOnTop":true,"left":100,"top":80,"selectedTab":2}} |
| topmost after relaunch | True |
| window position before close / after relaunch | (150,120) / (150,120) |
| mortar position after relaunch | 78.49 71.84 |

## Corrupt settings.json does not block startup

| | C# |
|---|---|
| **result** | PASS |
| window appeared | True |
| settings.json.bad kept | True |

## Esc = global stop (hammer + anti-AFK + magnifier), F12 = same + exit

| | C# |
|---|---|
| **result** | PASS |
| before Esc: mouse downs / anti-AFK presses | 6 / 2 |
| events later than Esc + 120 ms | 0 |
| lens visible after Esc | False |
| app still running after Esc | True |
| status line after Esc | 已停止   F9 敲锤 \| F8 防挂机 \| F10 放大镜 \| Esc 全部停止 \| F12 退出 |
| F12: exited / lens window after exit | True / gone |

## Magnifier: F10 cycle OFF→2x→3x→4x→OFF, centred, click-through, no focus steal, no recursion

| | C# |
|---|---|
| **result** | PASS |
| stripe width without lens px | 12.0 |
| lens ex-styles | topmost=True click-through=True layered=True no-activate=True toolwindow(no taskbar)=True |
| lens rect | (980,520)-(1580,920) |
| monitor centre | (1280,720) |
| stripe width with lens at 2.0x px | 24.0 |
| stripes at exactly 2x width | 100% |
| click at centre reached the window under the lens | True |
| WardogsTool CPU with lens on (% of machine) | 0.49 |
| stripe width after 2nd F10 (3.0x) px | 36.0 |
| stripe width after 3rd F10 (4.0x) px | 48.0 |
| lens visible 150 ms after 4th F10 | False |
| zoom items | 1.5, 2, 2.5, 3, 4 |
| stripe width UI 2.5x px | 30.0 |
| magnifier status | Magnifier: ON   ·   Zoom: 2.5x   ·   \\.\DISPLAY1 · 2560×1440 |
| stripe width F10 from 2.5x (3.0x) px | 36.0 |
| lens visible after F10 from 2.5x | True |
| lens window after app exit | gone |
| saved zoom | 3 |

