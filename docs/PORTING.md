# Python → C# port: behaviour and validation

The C# app is a port of `wardogs_tool.py` with behavioural parity as the priority. `wardogs_tool.py` is unchanged on this branch and is the reference: unit tests compare against outputs recorded from it, and the input probe runs it side by side with the C# build.

## Behaviour inventory

| Area | Python (`wardogs_tool.py`) | C# |
|---|---|---|
| Hammer loop | `hammer_loop`: left down → `sleep_or_stop(hold)` → left up → `sleep_or_stop(0.040)` → repeat, on a daemon thread | `HammerEngine.Run`, dedicated background thread, same order |
| First action | Immediate left down | Same |
| Timing | Each wait is `perf_counter() + seconds` taken after the input call; no absolute schedule, no catch-up | `ITimeSource.WaitUntil(Now + d)` after the input call; same |
| Presets | 310 ms (小/中锤), 510 ms (大锤); gap fixed 40 ms | Same; gap fixed 40 ms (not configurable in this version) |
| Hammer stop | Stop event set; `join(timeout=1)`; mouse-up in `finally` only if the tool's down is outstanding | Cancellation token; `Join(1 s)`; same `finally` |
| Hammer start while running | Ignored | Ignored |
| Anti-AFK schedule | Tk `after()`: first round one period after F8; presses at `i × gap`; next round `period` after the round ran | Event-queue worker replaying the same timeline |
| Anti-AFK key | `keybd_event(vk, MapVirtualKeyW(vk,0), 0/KEYUP, 0)`, held 50 ms; key-up still fires after stop | Same call, same 50 ms, same key-up guarantee |
| Anti-AFK validation | `read_afk_settings`: key/int/int/float checks, messages | Same order and messages (CPython `int()`/`float()` rules re-implemented) |
| Mouse injection | `user32.mouse_event(LEFTDOWN/LEFTUP, 0,0,0,0)` | Same call |
| Timer resolution | `timeBeginPeriod(1)` for the app's lifetime | Same |
| Hotkeys | `GetAsyncKeyState` polled every 20 ms, edge on not-down → down; keys not swallowed | Raw Input `RIDEV_INPUTSINK`, make/break tracked, auto-repeat ignored; keys not swallowed |
| F8 / F9 / Esc / F12 | anti-AFK start / hammer start / stop all / stop and quit | Same |
| Status line | `运行中   防挂机 N 秒后按键 \| 敲锤 310 ms` or the idle text | Same strings |
| Mortar maths | `atan2(dx, dy)`, `% 360`, `floor(x + 0.5)`, `hypot × 100` | Same, including CPython float `%` (`-1e-17 % 360 == 360.0`) |
| Mortar parsing | `parse_xy`: commas → spaces, `split()`, exactly two `float()`s | Same, incl. Unicode digits, `_`, Python whitespace; `，` is not a separator |
| Mortar display | `DIRECTION: {d:03d}°`, `RANGE:     {r} m`, `.2f` exact, `:g` history | Same strings, exact half-to-even `.2f` |
| Mortar UX | Enter in mortar box → target box; Enter in target box → calculate, select target text; errors in red, previous result kept; history 20 | Same |
| Self-test | `python wardogs_tool.py --test` | `WardogsTool.exe --test` (same output) |

## Intended differences

1. **Hotkeys are event-driven.** No 20 ms poll, so reaction is faster (see the probe report: stop releases the button in ~1–3 ms vs ~5–18 ms). A key whose break was lost (e.g. taken by the secure desktop) re-arms after 1.5 s; Python's polling never had that problem.
2. **Non-finite input is rejected with a message.** Python accepts an anti-AFK period of `inf`/`nan` and then crashes in `schedule_cycle`, leaving the UI stuck in "running" with nothing scheduled; a mortar coordinate of `inf`/`nan` prints a traceback and shows nothing. The port shows an error instead.
3. **Integers beyond the 64-bit range are rejected** (Python's unbounded `int()` would accept them and then hang or overflow).
4. **Settings persist** in `%AppData%\WardogsTool\settings.json` (schema version 1; corrupt files are kept as `settings.json.bad` and never block startup).
5. **Worker exceptions are reported** in a message box (Python prints them to stderr). The button/key is released first in either case.
6. **UI layout**: bilingual labels, hammer as the landing tab, RUNNING/STOPPED badges, a stop button on each tab (each calls stop-all, like Python's single 停止 button).

## Known limitations kept from Python

- A hard kill (Task Manager → End task) cannot run cleanup; if it happens mid-hold, click once to release the button.
- If the game runs elevated, the tool must run elevated too (UIPI blocks lower-integrity input).
- Anti-AFK with a period that truncates to 0 ms and a valid round (e.g. count 1, period `0.0004`) passes Python's validation and floods key presses with no wait. The port keeps that validation for parity; tightening it is a candidate follow-up.

## Validation

### Unit tests — `test.cmd`

116 tests. Highlights:

- `PythonParityTests`: 4147 mortar cases (three known in-game cases, all eight compass directions, the 359.5/0.5 boundaries, tiny negative angles, 3000 random two-decimal and 1000 full-precision cases), 51 `parse_xy` inputs and 28 `read_afk_settings` inputs, all recorded from the unmodified Python by `tools/parity/gen_python_reference.py`. Every number and every display string matches; the only difference is the intended `inf`/`nan` period rejection.
- `HammerEngineTests` / `AntiAfkEngineTests`: exact event sequences against a fake clock — immediate first down, relative deadlines with no catch-up (7 ms input latency shifts every later deadline), stop during hold / gap, overlapping anti-AFK presses when gap < 50 ms, key-up after stop, exceptions still releasing.
- Mortar boundaries, non-finite rejection, CPython float modulo, `.2f`/`:g` formatting, hotkey edge detection, settings round-trip and corrupt files.

Regenerate the fixture after any change to `wardogs_tool.py`:

```powershell
python tools\parity\gen_python_reference.py
```

### Integration probe — real input, both tools

`tools/WardogsTool.InputProbe` opens a topmost target window, clicks it so it has focus, and drives each tool through its global hotkeys. Low-level hooks record every event the tools inject. Everything the probe injects itself is tagged in `dwExtraInfo`, so it never counts as the tool's input. Moving the mouse by hand aborts the run.

```powershell
publish.cmd
tools\WardogsTool.InputProbe\bin\Release\net10.0-windows\WardogsTool.InputProbe.exe ^
  --exe dist\WardogsTool-portable\WardogsTool.exe --python <path to python.exe> --out docs\validation
```

Latest run: [validation/input-probe-report.md](validation/input-probe-report.md) — 20/20 scenarios pass. Mouse events (`LBUTTONDOWN/UP flags=0x1 mouseData=0 extra=0`) and anti-AFK key events (`vk=0x43 scan=0x2E flags=0x10/0x90`) are field-for-field identical between Python and C#; holds measure 310.7 vs 311.1 ms and 510.6 vs 510.9 ms, gaps 41.5 vs 41.0 ms.

What the probe cannot prove: that WARDOGS itself accepts this input. It shows the C# tool sends exactly what the Python tool sends; the in-game check is in [ACCEPTANCE.md](ACCEPTANCE.md).
