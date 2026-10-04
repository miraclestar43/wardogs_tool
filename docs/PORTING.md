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
| F8 / F9 / Esc / F12 | anti-AFK start / hammer start (selected preset; ignored while running) / stop all / stop and quit | F8, Esc, F12 as in Python (Esc and F12 also hide the magnifier); **F9 cycles OFF → 510 → 310 → OFF** (new); F10 cycles the magnifier (new) |
| Status line | `运行中   防挂机 N 秒后按键 \| 敲锤 310 ms` or `已停止   F8 防挂机 \| F9 敲锤 \| Esc 停止 \| F12 退出` | The F9 state is always shown: `运行中   F9 敲锤：大锤 510 ms \| 防挂机 N 秒后按键 \| 放大镜 2.0x`, idle `已停止   F9 敲锤：关（→ 大锤 → 小/中锤）\| F8 防挂机 \| F10 放大镜 \| Esc 全部停止 \| F12 退出` |
| Mortar maths | `atan2(dx, dy)`, `% 360`, `floor(x + 0.5)`, `hypot × 100` | Same, including CPython float `%` (`-1e-17 % 360 == 360.0`) |
| Mortar parsing | `parse_xy`: commas → spaces, `split()`, exactly two `float()`s | Same, incl. Unicode digits, `_`, Python whitespace; `，` is not a separator |
| Mortar display | `DIRECTION: {d:03d}°`, `RANGE:     {r} m`, `.2f` exact, `:g` history | Same strings, exact half-to-even `.2f` |
| Mortar UX | Enter in mortar box → target box; Enter in target box → calculate, select target text; errors in red, previous result kept; history 20 | Same |
| Self-test | `python wardogs_tool.py --test` | `WardogsTool.exe --test` (same output) |

## Intended differences

1. **Hotkeys are event-driven.** No 20 ms poll, so reaction is faster (see the probe report: stop releases the button in ~1–3 ms vs ~5–18 ms). A key whose break was lost (e.g. taken by the secure desktop) re-arms after 1.5 s; Python's polling never had that problem.
2. **Inputs that break Python's scheduler are rejected with a message.** Python's `read_afk_settings` accepts an anti-AFK period of `inf`/`nan`, or one so large that `int(period * 1000)` overflows (`1e308`), then crashes in `schedule_cycle` and leaves the UI stuck in "running" with nothing scheduled. Periods beyond what can be scheduled (≳ 4.6 × 10¹¹ s, e.g. `1e12`) and counts above 2³¹−1 (millions of `after()` calls) are rejected too. A mortar coordinate of `inf`/`nan` makes Python print a traceback and show nothing; a range beyond 2⁶³ m (coordinates ~10¹⁷) makes Python print the big integer. The port shows a red error message in each case.
3. **Integers are otherwise unbounded, as in Python** (`BigInteger`), so validation order and messages match even for absurd values; a huge gap with count 1 is accepted, as in Python.
4. **Settings persist** in `%AppData%\WardogsTool\settings.json`, schema version **2** (version 2 added `magnifier.zoom`; version-1 files still load, with the default zoom). Corrupt or newer-version files fall back to defaults (a corrupt file is kept as `settings.json.bad`) and never block startup. Running state is never saved.
5. **Worker exceptions are reported** in a message box (Python prints them to stderr). The button/key is released first in either case.

Two things that look like differences but are deliberate parity measures:

- **Timer.** `timeBeginPeriod(1)` alone does not reproduce Python's timing: from Windows 11 on, a window-owning process that is minimized or fully covered (the tool behind the game) loses the raised resolution. Measured with the tool minimized, a plain wait gave 317.6 ms holds and 46.6 ms gaps; Python stays at 310.7 / 40.7 ms because CPython 3.11+'s `time.sleep` uses a high-resolution waitable timer. The port now waits on the same kind of timer (310.7 / 40.6 ms minimized).
- **Stop-all order.** Python's `stop_afk()` returns immediately, then `stop_hammer()` runs. The port cancels anti-AFK, stops the hammer, and only then waits for any owed anti-AFK key-up, so Esc can never let the hammer send one more click or release late.
- **Scan code thread.** `MapVirtualKeyW` is called during validation on the UI thread, as Python's `tap()` calls it on the Tk thread (keyboard layouts are per thread).
6. **UI layout**: bilingual labels; tabs Hammer (landing) / Anti-AFK / Mortar / Magnifier / Settings; RUNNING/STOPPED badges; always-on-top moved to the Settings tab.
7. **Esc is a global emergency stop, and every Stop button is the same action.** Esc stops hammer and anti-AFK (each releases whatever it holds: the hammer's mouse button, any anti-AFK key still down), hides the magnifier, and leaves the app open. Both tabs' 停止 STOP buttons run exactly the same stop. F12 (and closing the window) does the same cleanup, then exits. F8 and F9 still start independently and may run together; there is no mutual exclusion. In Python, Esc and its single 停止 button also stopped both — the magnifier part is new.
8. **F9 is a three-state cycle** (Python: F9 starts the selected preset and is ignored while running). Each fresh F9 key-down advances one step, OFF → Large 510 ms → Small/Medium 310 ms → OFF (`HammerPresets.NextHotkeyState`); auto-repeat is filtered by the hotkey service. The step follows what the engine is actually running. A switch stops the running cycle first — `HammerEngine.Stop()` returns only after the worker has sent the mouse-up for a button it holds — and only then starts the new timing. Esc always returns the hammer to OFF. The Hammer tab's radio buttons and 开始所选 START button still start the picked preset as before.

## New feature (not in Python): centre-screen magnifier

Each **F10** press advances one step: **OFF → 2.0x → 3.0x → 4.0x → OFF** (`MagnifierGeometry.NextHotkeyState`; no multi-click timing). The Magnifier tab additionally offers 1.5x and 2.5x through its list and on/off button; F10 from a UI-only zoom goes to the next larger cycle step (1.5 → 2.0, 2.5 → 3.0). The lens is 600×400, centred on the monitor that holds the foreground window; the zoom is persisted. Esc and F12 hide it. Implementation: the documented Windows Magnification API (`Magnification.dll`, a `WC_MAGNIFIER` control) in a borderless host window with `WS_EX_TOPMOST | WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`; the host is on the control's exclude list and ignores `WM_CLOSE`; every show/zoom change re-asserts its topmost position (without activating it) in case another topmost window was raised above it; a ~60 Hz invalidate (no screenshot polling) keeps it live. Geometry is pure Core code (`MagnifierGeometry`). No game process access, no image analysis, no fallback to anything in-process. Windows Graphics Capture was not needed: the Magnification API worked on this machine (see the probe report). Exclusive fullscreen cannot be overlaid by any external window — borderless windowed is required.

## Known limitations kept from Python

- A hard kill (Task Manager → End task) cannot run cleanup; if it happens mid-hold, click once to release the button.
- If the game runs elevated, the tool must run elevated too (UIPI blocks lower-integrity input).
- Anti-AFK with a period that truncates to 0 ms and a valid round (e.g. count 1, period `0.0004`) passes Python's validation and floods key presses with no wait. The port keeps that validation for parity; tightening it is a candidate follow-up.

## .NET Framework 4.8 runtime notes

The tests run on the .NET Framework runtime, and found three ways it differs from .NET Core / CPython. All three are handled so the results match Python:

- `double.Parse` **throws** on overflow (`1e400`); the port returns ±inf like CPython's `float()`.
- `double.Parse("-0")` / `"-0.0"` returns **+0**; the port restores the sign, as CPython keeps it.
- `(-0.0).ToString()` prints `"0"`; the history line prints `-0` like Python.

**Parity guarantee scope for number parsing.** .NET Framework's parser is not always correctly rounded for long inputs. Measured against Python's `float()` on 200,000 strings: two-decimal coordinates (the in-game format, e.g. `104.39`) **0 / 50,000** differ; six-decimal values 28 / 50,000, 17-digit values 8 / 50,000 and values with large exponents 487 / 50,000 differ by one unit in the last place. Such a difference could only change a displayed mortar result at an exact rounding tie. Extreme long or exponent inputs are therefore outside the parity guarantee; normal short decimal coordinates are inside it. No custom parser is used.

## Validation

### Unit tests — `test.cmd`

168 tests, run on the .NET Framework 4.8 runtime itself (`test.cmd`), so its number parsing and formatting are what is tested. Highlights:

- `PythonParityTests`: 4148 mortar cases (three known in-game cases, all eight compass directions, the 359.5/0.5 boundaries, tiny negative angles, 3000 random two-decimal and 1000 full-precision cases), 58 `parse_xy` inputs (incl. `repr()` escaping and astral Unicode digits) and 34 `read_afk_settings` inputs, all recorded from the unmodified Python by `tools/parity/gen_python_reference.py`. Every number, display string and error message matches, except the six intended rejections listed above (one huge range, five anti-AFK inputs), which the test asserts explicitly.
- `HammerEngineTests` / `AntiAfkEngineTests`: exact event sequences against a fake clock — immediate first down, relative deadlines with no catch-up (7 ms input latency shifts every later deadline), stop during hold / gap, overlapping anti-AFK presses when gap < 50 ms, key-up after stop, exceptions still releasing.
- Mortar cardinal directions, four quadrants, the 359.49/359.50 boundary, `-0.0`, scientific notation, non-finite rejection, CPython float modulo, `.2f`/`:g` formatting.
- Magnifier geometry (centring, negative multi-monitor origins, small monitors, zoom snapping), the F10 cycle (OFF → 2x → 3x → 4x → OFF, and from UI-only zooms), hotkey edge detection, settings round-trip, schema v1 → v2, corrupt files.

Regenerate the fixture after any change to `wardogs_tool.py`:

```powershell
python tools\parity\gen_python_reference.py
```

### Integration probe — real input, both tools

`tools/WardogsTool.InputProbe` opens a topmost target window, clicks it so it has focus, and drives each tool through its global hotkeys. Low-level hooks record every event the tools inject. Everything the probe injects itself is tagged in `dwExtraInfo`, so it never counts as the tool's input. Moving the mouse by hand aborts the run.

```powershell
publish.cmd
tools\WardogsTool.InputProbe\bin\Release\net10.0-windows\WardogsTool.InputProbe.exe ^
  --exe dist\WardogsTool\WardogsTool.exe --python <path to python.exe> --out docs\validation
```

Latest run: [validation/input-probe-report.md](validation/input-probe-report.md) — 25/25 scenarios pass. C#-only scenarios include the **F9 cycle** (510 ms holds → mouse-up then 310 ms holds → single mouse-up and OFF; F9 held in auto-repeat = one step; Esc from 510 and from 310 = only a release, status back to `已停止   F9 敲锤：关`; status bar and Hammer badge checked at every step), the 510 → 310 **switch inside the shared timing scenarios** (release 1.4 ms after F9, before the first new mouse-down), **Esc as global stop** (with hammer, anti-AFK and the lens all on: nothing sent after Esc + 120 ms, every down has its up, no button/key held, lens hidden, app still running, Esc and F10 not swallowed, status back to idle; then F12 releases the held button, removes the lens and exits) and the **magnifier** (lens 600×400 centred on the 2560×1440 monitor, styles correct, click-through, focus kept; 12 px test stripes shown at exactly 24 / 36 / 48 px for the F10 steps 2.0x / 3.0x / 4.0x, then gone within 150 ms; 2.5x from the UI = 30 px, F10 from there = 36 px; no recursion; ≤ 0.4 % CPU; removed on exit). Mouse events (`LBUTTONDOWN/UP flags=0x1 mouseData=0 extra=0`) and anti-AFK key events (`vk=0x43 scan=0x2E flags=0x10/0x90`) are field-for-field identical between Python and C#. Holds measure 310.7 vs 310.7 ms and 510.8 vs 510.9 ms, gaps 41.0 vs 40.6 ms; with the tool window minimized 310.7 vs 310.6 ms and 41.0 vs 40.7 ms (Python vs the .NET Framework 4.8 build).

Run-to-run variation: both tools occasionally stretch a single 40 ms gap to ~46–48 ms (Windows thread scheduling); means stay within 1–2 ms of nominal. The probe fails a target whose mean drifts more than 3 ms — one earlier run failed on the **Python** side for exactly this (510 ms scenario, gap mean 43.1 ms, max 48.2 ms; C# 41.1 / 42.1 ms in the same run); the rerun that produced the committed report passed 24/24. The first full run after the F9 change failed this check 4 times while other applications were loading the machine — 3 on the **Python** side (gap means 43.5 / 44.6 / 45.9 ms) and 1 on C# (43.3 ms, minimized); the immediate rerun that produced the committed report passed 25/25 with C# gap means 40.6–40.8 ms.

What the probe cannot prove: that WARDOGS itself accepts this input. It shows the C# tool sends exactly what the Python tool sends; the in-game check is in [ACCEPTANCE.md](ACCEPTANCE.md).

## Distribution

| Script | Output | Size | Needs on the target machine |
|---|---|---|---|
| `publish.cmd` | `dist\WardogsTool\`: `WardogsTool.exe` (50,688 B), `WardogsTool.Core.dll` (46,592 B), `WardogsTool.exe.config` (174 B) | 97,454 bytes | nothing on Windows 10 1903+ (64-bit) and Windows 11 |

The app targets .NET Framework 4.8, which is part of Windows 10 1903+ and Windows 11 (11 ships 4.8.1). The three files must stay together; no ILMerge/Costura-style bundling is used. The settings use the built-in `DataContractJsonSerializer`, so no NuGet assemblies ship. Startup on this machine: ~250 ms to the main window (the earlier .NET 10 self-contained single-file build: ~625 ms, 58.9 MiB).
