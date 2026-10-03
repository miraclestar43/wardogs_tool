# Input probe report

Run: 2026-10-03 14:59 · Microsoft Windows NT 10.0.26100.0 · C# build: `WardogsTool.exe`

Result: **22/22 scenarios passed**. Left-button presses that reached the probe window: 138.

## Hammer 310 ms, tool window minimized

| | Python | C# |
|---|---|---|
| **result** | PASS | PASS |
| first mouse-down after F9 ms | 37.7 | 10.4 |
| hold n | 17 | 17 |
| hold mean/min/max ms | 310.7 / 310.4 / 310.9 | 310.7 / 310.4 / 311.3 |
| gap n | 17 | 17 |
| gap mean/min/max ms | 40.7 / 40.4 / 41.6 | 40.6 / 40.4 / 41.2 |
| mouse-up after Esc ms | 35.8 | 3.8 |
| downs / ups | 18 / 18 | 18 / 18 |
| mouse event shapes | LBUTTONDOWN flags=0x1 mouseData=0x0 extra=0x0; LBUTTONUP flags=0x1 mouseData=0x0 extra=0x0 | LBUTTONDOWN flags=0x1 mouseData=0x0 extra=0x0; LBUTTONUP flags=0x1 mouseData=0x0 extra=0x0 |
| tool window minimized | True | True |

## Hammer 310 ms, stop during hold

| | Python | C# |
|---|---|---|
| **result** | PASS | PASS |
| first mouse-down after F9 ms | 9.0 | 12.8 |
| hold n | 8 | 8 |
| hold mean/min/max ms | 310.7 / 310.5 / 310.9 | 310.8 / 310.3 / 311.3 |
| gap n | 8 | 8 |
| gap mean/min/max ms | 41.3 / 40.4 / 45.9 | 40.7 / 40.4 / 41.0 |
| mouse-up after Esc ms | 20.2 | 2.1 |
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
| press times after F8 ms | 3001.6, 3502.5, 6001.6 | 3010.7, 3511.2, 6010.3 |
| key held ms | 50.5, 51.2, 50.5 | 51.0, 51.5, 50.6 |
| 2nd press after 1st ms | 500.9 | 500.5 |
| round 2 after round 1 ms | 3000.0 | 2999.6 |
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
| mouse-up after quit ms | 15.1 | 1.4 |

## Hammer 510 ms, stop during hold

| | Python | C# |
|---|---|---|
| **result** | PASS | PASS |
| first mouse-down after F9 ms | 5.3 | 15.5 |
| hold n | 5 | 5 |
| hold mean/min/max ms | 510.7 / 510.4 / 511.0 | 510.7 / 510.4 / 511.3 |
| gap n | 5 | 5 |
| gap mean/min/max ms | 40.8 / 40.4 / 41.1 | 40.7 / 40.3 / 41.5 |
| mouse-up after Esc ms | 19.2 | 1.6 |
| downs / ups | 6 / 6 | 6 / 6 |
| mouse event shapes | LBUTTONDOWN flags=0x1 mouseData=0x0 extra=0x0; LBUTTONUP flags=0x1 mouseData=0x0 extra=0x0 | LBUTTONDOWN flags=0x1 mouseData=0x0 extra=0x0; LBUTTONUP flags=0x1 mouseData=0x0 extra=0x0 |

## Closing the window during hold releases and exits

| | Python | C# |
|---|---|---|
| **result** | PASS | PASS |
| process exited | True | True |
| mouse-up after quit ms | 1.2 | 0.9 |

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
| saved settings | {"schemaVersion":1,"hammer":{"holdMs":310},"antiAfk":{"key":"c","count":"2","gapMs":"500","periodSeconds":"180"},"mortar":{"position":"78.49 71.84"},"window":{"alwaysOnTop":true,"left":100,"top":80,"selectedTab":2}} |
| topmost after relaunch | True |
| window position before close / after relaunch | (150,120) / (150,120) |
| mortar position after relaunch | 78.49 71.84 |

## Corrupt settings.json does not block startup

| | C# |
|---|---|
| **result** | PASS |
| window appeared | True |
| settings.json.bad kept | True |

