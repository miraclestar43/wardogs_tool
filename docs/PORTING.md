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
| Wait precision | `time.sleep` — on CPython 3.11+ a high-resolution waitable timer; also `timeBeginPeriod(1)` for the app's lifetime | `HighResolutionTimeSource`: high-resolution waitable timer + cancel handle; also `timeBeginPeriod(1)` |
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
2. **Inputs that break Python's scheduler are rejected with a message.** Python's `read_afk_settings` accepts an anti-AFK period of `inf`/`nan`, or one so large that `int(period * 1000)` overflows (`1e308`), then crashes in `schedule_cycle` and leaves the UI stuck in "running" with nothing scheduled. Periods beyond what can be scheduled (≳ 4.6 × 10¹¹ s, e.g. `1e12`) and counts above 2³¹−1 (millions of `after()` calls) are rejected too. A mortar coordinate of `inf`/`nan` makes Python print a traceback and show nothing; a range beyond 2⁶³ m (coordinates ~10¹⁷) makes Python print the big integer. The port shows a red error message in each case.
3. **Integers are otherwise unbounded, as in Python** (`BigInteger`), so validation order and messages match even for absurd values; a huge gap with count 1 is accepted, as in Python.
4. **Settings persist** in `%AppData%\WardogsTool\settings.json` (schema version 1; corrupt files are kept as `settings.json.bad` and never block startup).
5. **Worker exceptions are reported** in a message box (Python prints them to stderr). The button/key is released first in either case.

Two things that look like differences but are deliberate parity measures:

- **Timer.** `timeBeginPeriod(1)` alone does not reproduce Python's timing: from Windows 11 on, a window-owning process that is minimized or fully covered (the tool behind the game) loses the raised resolution. Measured with the tool minimized, a plain wait gave 317.6 ms holds and 46.6 ms gaps; Python stays at 310.7 / 40.7 ms because CPython 3.11+'s `time.sleep` uses a high-resolution waitable timer. The port now waits on the same kind of timer (310.7 / 40.6 ms minimized).
- **Stop-all order.** Python's `stop_afk()` returns immediately, then `stop_hammer()` runs. The port cancels anti-AFK, stops the hammer, and only then waits for any owed anti-AFK key-up, so Esc can never let the hammer send one more click or release late.
- **Scan code thread.** `MapVirtualKeyW` is called during validation on the UI thread, as Python's `tap()` calls it on the Tk thread (keyboard layouts are per thread).
6. **UI layout**: bilingual labels; tabs Hammer (landing) / Anti-AFK / Mortar / Magnifier / Settings; RUNNING/STOPPED badges; always-on-top moved to the Settings tab.
7. **Per-feature stop buttons.** The Hammer tab's 停止敲锤 stops only hammering and the Anti-AFK tab's 停止防挂机 only anti-AFK, so stopping one never stops the other. Esc is unchanged from Python: it stops both.

## New feature (not in Python): centre-screen magnifier

F10 toggles a 600×400 lens centred on the monitor that holds the foreground window, zoom 1.5/2.0/2.5/3.0/4.0x (default 2.0x, persisted). Implementation: the documented Windows Magnification API (`Magnification.dll`, a `WC_MAGNIFIER` control) in a borderless host window with `WS_EX_TOPMOST | WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`; the host is on the control's exclude list; a ~60 Hz invalidate (no screenshot polling) keeps it live. Geometry is pure Core code (`MagnifierGeometry`). No game process access, no image analysis, no fallback to anything in-process. Windows Graphics Capture was not needed: the Magnification API worked on the first try on this machine (see the probe report). Exclusive fullscreen cannot be overlaid by any external window — borderless windowed is required.

## Known limitations kept from Python

- A hard kill (Task Manager → End task) cannot run cleanup; if it happens mid-hold, click once to release the button.
- If the game runs elevated, the tool must run elevated too (UIPI blocks lower-integrity input).
- Anti-AFK with a period that truncates to 0 ms and a valid round (e.g. count 1, period `0.0004`) passes Python's validation and floods key presses with no wait. The port keeps that validation for parity; tightening it is a candidate follow-up.

## Validation

### Unit tests — `test.cmd`

156 tests. Highlights:

- `PythonParityTests`: 4148 mortar cases (three known in-game cases, all eight compass directions, the 359.5/0.5 boundaries, tiny negative angles, 3000 random two-decimal and 1000 full-precision cases), 58 `parse_xy` inputs (incl. `repr()` escaping and astral Unicode digits) and 34 `read_afk_settings` inputs, all recorded from the unmodified Python by `tools/parity/gen_python_reference.py`. Every number, display string and error message matches, except the six intended rejections listed above (one huge range, five anti-AFK inputs), which the test asserts explicitly.
- `HammerEngineTests` / `AntiAfkEngineTests`: exact event sequences against a fake clock — immediate first down, relative deadlines with no catch-up (7 ms input latency shifts every later deadline), stop during hold / gap, overlapping anti-AFK presses when gap < 50 ms, key-up after stop, exceptions still releasing.
- Mortar cardinal directions, four quadrants, the 359.49/359.50 boundary, `-0.0`, scientific notation, non-finite rejection, CPython float modulo, `.2f`/`:g` formatting.
- Magnifier geometry (centring, negative multi-monitor origins, small monitors, zoom snapping), hotkey edge detection, settings round-trip, schema v1 → v2, corrupt files.

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

Latest run: [validation/input-probe-report.md](validation/input-probe-report.md) — 24/24 scenarios pass, including per-feature stop and the magnifier (lens 600×400 centred on the 2560×1440 monitor, styles correct, click-through, focus kept, 12 px test stripes shown at exactly 24 px at 2.0x and 48 px at 4.0x with no recursion, 0.2 % CPU, gone 150 ms after F10 and after exit). Mouse events (`LBUTTONDOWN/UP flags=0x1 mouseData=0 extra=0`) and anti-AFK key events (`vk=0x43 scan=0x2E flags=0x10/0x90`) are field-for-field identical between Python and C#. Holds measure 310.7 vs 310.8 ms and 510.7 vs 510.7 ms, gaps 41.3 vs 40.7 ms; with the tool window minimized 310.7 vs 310.7 ms and 40.7 vs 40.6 ms.

Run-to-run variation: both tools occasionally stretch a single 40 ms gap to ~46–48 ms (Windows thread scheduling); means stay within 1–2 ms of nominal. The probe fails a target whose mean drifts more than 3 ms — one earlier run failed on the **Python** side for exactly this (510 ms scenario, gap mean 43.1 ms, max 48.2 ms; C# 41.1 / 42.1 ms in the same run); the rerun that produced the committed report passed 24/24.

What the probe cannot prove: that WARDOGS itself accepts this input. It shows the C# tool sends exactly what the Python tool sends; the in-game check is in [ACCEPTANCE.md](ACCEPTANCE.md).
