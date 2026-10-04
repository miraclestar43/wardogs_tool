# Input probe report

Run: 2026-10-03 17:24 · Microsoft Windows NT 10.0.26100.0 · C# build: `WardogsTool.exe`

Result: **24/24 scenarios passed**. Left-button presses that reached the probe window: 156.

## Hammer 310 ms, tool window minimized

| | Python | C# |
|---|---|---|
| **result** | PASS | PASS |
| first mouse-down after F9 ms | 36.0 | 11.2 |
| hold n | 17 | 17 |
| hold mean/min/max ms | 310.6 / 310.4 / 311.0 | 310.8 / 310.3 / 311.4 |
| gap n | 17 | 17 |
| gap mean/min/max ms | 41.0 / 40.4 / 46.1 | 41.3 / 40.4 / 47.6 |
| mouse-up after Esc ms | 33.5 | 2.5 |
| downs / ups | 18 / 18 | 18 / 18 |
| mouse event shapes | LBUTTONDOWN flags=0x1 mouseData=0x0 extra=0x0; LBUTTONUP flags=0x1 mouseData=0x0 extra=0x0 | LBUTTONDOWN flags=0x1 mouseData=0x0 extra=0x0; LBUTTONUP flags=0x1 mouseData=0x0 extra=0x0 |
| tool window minimized | True | True |

## Hammer 310 ms, stop during hold

| | Python | C# |
|---|---|---|
| **result** | PASS | PASS |
| first mouse-down after F9 ms | 16.9 | 11.1 |
| hold n | 8 | 8 |
| hold mean/min/max ms | 310.6 / 310.4 / 310.9 | 310.8 / 310.4 / 311.2 |
| gap n | 8 | 8 |
| gap mean/min/max ms | 42.0 / 40.4 / 46.5 | 40.6 / 40.4 / 40.9 |
| mouse-up after Esc ms | 12.9 | 1.5 |
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
| press times after F8 ms | 3021.3, 3521.2, 6021.3 | 3011.2, 3511.8, 6010.7 |
| key held ms | 50.5, 50.8, 50.6 | 51.1, 50.9, 51.0 |
| 2nd press after 1st ms | 500.0 | 500.6 |
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
| mouse-up after quit ms | 17.8 | 3.0 |

## Hammer 510 ms, stop during hold

| | Python | C# |
|---|---|---|
| **result** | PASS | PASS |
| first mouse-down after F9 ms | 12.7 | 14.6 |
| hold n | 5 | 5 |
| hold mean/min/max ms | 510.8 / 510.5 / 511.4 | 510.7 / 510.4 / 510.9 |
| gap n | 5 | 5 |
| gap mean/min/max ms | 41.9 / 40.5 / 46.3 | 40.7 / 40.5 / 41.0 |
| mouse-up after Esc ms | 9.2 | 3.0 |
| downs / ups | 6 / 6 | 6 / 6 |
| mouse event shapes | LBUTTONDOWN flags=0x1 mouseData=0x0 extra=0x0; LBUTTONUP flags=0x1 mouseData=0x0 extra=0x0 | LBUTTONDOWN flags=0x1 mouseData=0x0 extra=0x0; LBUTTONUP flags=0x1 mouseData=0x0 extra=0x0 |

## Closing the window during hold releases and exits

| | Python | C# |
|---|---|---|
| **result** | PASS | PASS |
| process exited | True | True |
| mouse-up after quit ms | 2.9 | 0.9 |

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

## Stopping one feature leaves the other running

| | C# |
|---|---|
| **result** | PASS |
| after 停止敲锤: mouse downs / anti-AFK presses | 0 / 4 |
| after 停止防挂机: anti-AFK presses / mouse downs | 0 / 7 |

## Magnifier (F10): centred, click-through, no focus steal, no recursion

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
| WardogsTool CPU with lens on (% of machine) | 0.23 |
| zoom items | 1.5, 2, 2.5, 3, 4 |
| stripe width with lens at 4.0x px | 48.0 |
| status line | Magnifier: ON   ·   Zoom: 4.0x   ·   \\.\DISPLAY1 · 2560×1440 |
| lens visible 150 ms after F10 | False |
| lens window after app exit | gone |
| saved zoom | 4 |

