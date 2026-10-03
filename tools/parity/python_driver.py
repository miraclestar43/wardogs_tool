"""Run the unmodified Python tool with chosen settings, for tools/WardogsTool.InputProbe.

Same code path as `python wardogs_tool.py`: it builds the real App class and runs Tk's main loop.
The only difference is that the entry/radio values are set first, which is what a user would do
by clicking 510 ms or typing a shorter anti-AFK period.

    python python_driver.py --hold 510 --key c --count 2 --gap 500 --period 3 --x 20 --y 20
"""
import argparse
import os
import sys
import tkinter as tk

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
sys.path.insert(0, ROOT)
import wardogs_tool as wt  # noqa: E402

parser = argparse.ArgumentParser()
parser.add_argument("--hold", type=int, default=310)
parser.add_argument("--key", default="c")
parser.add_argument("--count", default="2")
parser.add_argument("--gap", default="500")
parser.add_argument("--period", default="180")
parser.add_argument("--x", type=int, default=20)
parser.add_argument("--y", type=int, default=20)
args = parser.parse_args()

root = tk.Tk()
app = wt.App(root)
app.hammer_var.set(args.hold)
app.key_var.set(args.key)
app.count_var.set(args.count)
app.gap_var.set(args.gap)
app.period_var.set(args.period)
root.geometry(f"+{args.x}+{args.y}")
root.mainloop()
