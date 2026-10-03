"""Record the Python tool's exact outputs as a fixture for the C# parity tests.

wardogs_tool.py is the behavioural reference and is imported unmodified. The fixture covers
mortar solving and display strings, parse_xy, and read_afk_settings.

Usage (from the repo root):
    python tools/parity/gen_python_reference.py
Writes tests/WardogsTool.Core.Tests/Fixtures/python_reference.json.
"""
import json
import math
import os
import random
import sys
from types import SimpleNamespace

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
sys.path.insert(0, ROOT)
import wardogs_tool as wt  # noqa: E402  (the reference implementation, untouched)

OUT = os.path.join(ROOT, "tests", "WardogsTool.Core.Tests", "Fixtures", "python_reference.json")


def mortar_case(mx, my, tx, ty):
    bearing, dist = wt.mortar_solution(mx, my, tx, ty)
    deg, rng = wt.firing_solution(mx, my, tx, ty)
    # The strings below are copied from App.calc_mortar.
    return {
        "mortar": [mx, my],
        "target": [tx, ty],
        "bearing": bearing,
        "distance": dist,
        "direction": deg,
        "range": rng,
        "directionText": f"DIRECTION: {deg:03d}°",
        "rangeText": f"RANGE:     {rng} m",
        "exactText": f"Bearing exact: {bearing:.2f}°   Range exact: {dist:.2f} m",
        "historyText": f"({tx:g}, {ty:g})  {deg:03d}°  {rng} m",
    }


def mortar_cases():
    cases = [
        (100.32, 59.45, 104.39, 63.59),
        (78.49, 71.84, 81.44, 70.78),
        (78.49, 71.84, 83.60, 72.96),
        (50, 50, 50, 50),  # zero distance
    ]
    # Cardinal and intercardinal directions, several distances, from a few origins.
    for ox, oy in [(0, 0), (100, 100), (-12.5, 37.25)]:
        for r in (0.01, 1, 3.17, 150):
            for ddx, ddy in [(0, 1), (1, 1), (1, 0), (1, -1), (0, -1), (-1, -1), (-1, 0), (-1, 1)]:
                cases.append((ox, oy, ox + ddx * r, oy + ddy * r))
    # Around the 359.5 -> 000 wrap and other .5 boundaries.
    for centre in (359.5, 0.5, 44.5, 89.5, 179.5, 269.5):
        for off in (-1e-6, -1e-9, 0.0, 1e-9, 1e-6, -0.01, 0.01):
            a = math.radians(centre + off)
            cases.append((10, 20, 10 + 5 * math.sin(a), 20 + 5 * math.cos(a)))
    # Tiny negative angles (dx = -tiny, dy > 0): Python's % can return exactly 360.0 here.
    cases.append((0, 0, -1e-300, 1))
    cases.append((0, 0, -0.0, 1))
    cases.append((0, 0, -1e-17, 1))
    # Range beyond 64 bits (Python prints the big int).
    cases.append((0, 0, 1e17, 0))
    # Large coordinates and history formatting with exponents.
    cases.append((0, 0, 1234567.0, 0.0001))
    cases.append((0, 0, 1e-05, 2.5e-07))
    # Realistic in-game input: two decimals in 0..200.
    rnd = random.Random(20261003)
    for _ in range(3000):
        mx, my, tx, ty = (round(rnd.uniform(0, 200), 2) for _ in range(4))
        cases.append((mx, my, tx, ty))
    # Full double precision.
    for _ in range(1000):
        cases.append(tuple(rnd.uniform(-500, 500) for _ in range(4)))
    return [mortar_case(*c) for c in cases]


def encode_float(x):
    if math.isnan(x):
        return "nan"
    if math.isinf(x):
        return "inf" if x > 0 else "-inf"
    return repr(x)


PARSE_INPUTS = [
    "104.39 63.59", "104.39, 63.59", "104.39,63.59", "  104.39   63.59  ", "104.39\t63.59",
    "104.39　63.59", "104.39 63.59", "104.39\x1c63.59",
    "１０４.３９ ６３.５９", "١٢ 3", "104.39，63.59", "104,39 63,59", "104.39 63.59 1",
    "104.39", "", "   ", ",", ",,", "1,2", "1, 2", ".5 5.", "+1 -2", "1e2 3E-1", "1E+05 0",
    "1_0 2", "1__0 2", "_1 2", "1_ 2", "1_000.5 2", "1._5 2", "1e1_0 2",
    "inf 1", "-inf 1", "nan 1", "Infinity 1", "+NaN 1", "1e400 0", "-1e400 0",
    "0x10 1", "1.2.3 4", "abc 1", "1 abc", "1 2abc", "１０４．３９ 1", "- 1", ". 1", "e5 1",
    "0 0", "-0 -0.0", "007 08", "1e-400 1",
    # repr() escaping in error messages, astral digits
    "1\\2 3", "104.39​ 63.59", "1\x7f 2", "it's 1", "'\"x 1", "\U0001d7cf\U0001d7ce 5", "\U0001d7cfx 5",
]


def parse_cases():
    out = []
    for text in PARSE_INPUTS:
        try:
            x, y = wt.parse_xy(text)
            out.append({"input": text, "ok": True, "x": encode_float(x), "y": encode_float(y)})
        except ValueError as e:
            out.append({"input": text, "ok": False, "error": str(e)})
    return out


AFK_INPUTS = [
    ("c", "2", "500", "180"), ("C", "2", "500", "180"), ("1", "1", "0", "1"), ("?", "1", "0", "1"),
    ("", "2", "500", "180"), ("cc", "2", "500", "180"), ("中", "2", "500", "180"),
    ("c", "0", "500", "180"), ("c", "-1", "500", "180"), ("c", "2", "-1", "180"), ("c", "2", "500", "0"),
    ("c", "2", "500", "-5"), ("c", "2", "500", "0.5"), ("c", "2", "500", "0.501"), ("c", "3", "500", "1"),
    ("c", "2.0", "500", "180"), ("c", " 2 ", " 500 ", " 180 "), ("c", "+2", "5_0_0", "1_8_0"),
    ("c", "２", "５００", "１８０"), ("c", "2", "500", "1e2"), ("c", "2", "500", "0.0004"),
    ("c", "2", "500", "180.0009"), ("c", "x", "500", "180"), ("c", "2", "x", "180"), ("c", "2", "500", "x"),
    ("c", "2", "500", "-inf"), ("c", "2", "500", "inf"), ("c", "2", "500", "nan"),
    # beyond what can be scheduled; unbounded ints
    ("c", "2", "500", "1e308"), ("c", "2", "500", "1e12"), ("c", "1", "10000000000000000000", "180"),
    ("c", "99999999999999999999", "x", "180"), ("c", "99999999999999999999", "0", "180"), ("c", "x" * 250, "1", "1"),
]


def afk_cases():
    out = []
    for key, count, gap, period in AFK_INPUTS:
        fake_self = SimpleNamespace(
            key_var=SimpleNamespace(get=lambda v=key: v),
            count_var=SimpleNamespace(get=lambda v=count: v),
            gap_var=SimpleNamespace(get=lambda v=gap: v),
            period_var=SimpleNamespace(get=lambda v=period: v),
        )
        case = {"key": key, "count": count, "gap": gap, "period": period}
        try:
            vk, c, g, p = wt.App.read_afk_settings(fake_self)
            # Python later uses int(p * 1000); record it, or the error that line would raise.
            try:
                period_ms = int(p * 1000)
                case.update(ok=True, vk=vk, countValue=c, gapValue=g, periodMs=period_ms)
            except (OverflowError, ValueError) as e:
                case.update(ok=True, vk=vk, countValue=c, gapValue=g, periodMs=None,
                            pythonSchedulerError=f"{type(e).__name__}: {e}")
        except ValueError as e:
            case.update(ok=False, error=str(e))
        out.append(case)
    return out


def main():
    data = {
        "generatedBy": "tools/parity/gen_python_reference.py",
        "python": sys.version.split()[0],
        "mortar": mortar_cases(),
        "parse": parse_cases(),
        "antiAfk": afk_cases(),
    }
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, ensure_ascii=False, indent=1)
        f.write("\n")
    print(f"wrote {OUT}: {len(data['mortar'])} mortar, {len(data['parse'])} parse, {len(data['antiAfk'])} anti-AFK cases")


if __name__ == "__main__":
    main()
