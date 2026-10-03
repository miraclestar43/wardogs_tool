"""WARDOGS 小工具（GUI 版）。

「按键」页:
    F8   防挂机：每隔一段时间自动按一下键盘（默认每 180 秒按 2 下 c）
    F9   快速敲锤：左键按下 310 ms（小/中锤）或 510 ms（大锤）→ 抬起 40 ms → 循环
    Esc  全部停止
    F12  停止并退出
「迫击炮」页: 迫击炮方向/距离计算（X 向东、Y 向北、1.00 = 100 m）

快捷键是全局的，窗口不在前台也有效。只用标准库（tkinter + ctypes），无需安装依赖。
用法: python wardogs_tool.py
自检: python wardogs_tool.py --test
"""
import ctypes
import math
import sys
import threading
import time
import tkinter as tk
from tkinter import messagebox, ttk

user32 = ctypes.windll.user32
winmm = ctypes.windll.winmm

MOUSEEVENTF_LEFTDOWN = 0x0002
MOUSEEVENTF_LEFTUP = 0x0004
KEYEVENTF_KEYUP = 0x0002
VK_AFK = 0x77     # F8
VK_HAMMER = 0x78  # F9
VK_STOP = 0x1B    # Esc
VK_QUIT = 0x7B    # F12
KEY_HOLD_MS = 50  # 每次按键按下的时长，太短有些游戏读不到
HAMMER_UP_S = 0.040
HAMMER_HOLDS = ((310, "小/中锤"), (510, "大锤"))
POLL_MS = 20
IDLE_TEXT = "已停止   F8 防挂机 | F9 敲锤 | Esc 停止 | F12 退出"


def key_down(vk):
    return bool(user32.GetAsyncKeyState(vk) & 0x8000)


def char_to_vk(ch):
    res = user32.VkKeyScanW(ord(ch))
    return None if res == -1 else res & 0xFF


# ---------- 迫击炮计算 ----------
def mortar_solution(mx, my, tx, ty):
    """返回 (方位角 °, 距离 m)。方位角: 北 0°、东 90°。"""
    dx = tx - mx
    dy = ty - my
    distance_m = math.hypot(dx, dy) * 100
    bearing_deg = math.degrees(math.atan2(dx, dy)) % 360
    return bearing_deg, distance_m


def firing_solution(mx, my, tx, ty):
    """四舍五入到整度、整米（不用银行家舍入）。"""
    bearing, dist = mortar_solution(mx, my, tx, ty)
    return int(math.floor(bearing + 0.5)) % 360, int(math.floor(dist + 0.5))


def parse_xy(text):
    parts = text.replace(",", " ").split()
    if len(parts) != 2:
        raise ValueError("请输入两个数，例如 104.39 63.59")
    return float(parts[0]), float(parts[1])


def run_self_tests():
    cases = [
        ((100.32, 59.45), (104.39, 63.59), 45, 581),
        ((78.49, 71.84), (81.44, 70.78), 110, 313),
        ((78.49, 71.84), (83.60, 72.96), 78, 523),
    ]
    for (mx, my), (tx, ty), deg, rng in cases:
        got = firing_solution(mx, my, tx, ty)
        status = "OK " if got == (deg, rng) else "FAIL"
        print(f"{status} ({mx}, {my}) -> ({tx}, {ty}): {got[0]:03d}°, {got[1]} m "
              f"(期望 {deg:03d}°, {rng} m)")
        assert got == (deg, rng)
    print("全部通过")


# ---------- F9 快速敲锤（单独线程，计时更准） ----------
def sleep_or_stop(stop, seconds):
    """睡 seconds 秒；被要求停止时返回 False。"""
    end = time.perf_counter() + seconds
    while not stop.is_set():
        left = end - time.perf_counter()
        if left <= 0:
            return True
        time.sleep(min(left, 0.005))
    return False


def hammer_loop(stop, hold_s):
    down = False
    try:
        while not stop.is_set():
            user32.mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0)
            down = True
            if not sleep_or_stop(stop, hold_s):
                break
            user32.mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0)
            down = False
            if not sleep_or_stop(stop, HAMMER_UP_S):
                break
    finally:
        if down:
            user32.mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0)


class App:
    def __init__(self, root):
        self.root = root
        root.title("WARDOGS 小工具")
        root.resizable(False, False)

        nb = ttk.Notebook(root)
        nb.grid(row=0, column=0, padx=6, pady=6)
        keys_tab = ttk.Frame(nb, padding=12)
        mortar_tab = ttk.Frame(nb, padding=12)
        nb.add(keys_tab, text="按键")
        nb.add(mortar_tab, text="迫击炮")
        self.build_keys_tab(keys_tab)
        self.build_mortar_tab(mortar_tab)

        self.topmost_var = tk.BooleanVar(value=False)
        ttk.Checkbutton(root, text="窗口置顶", variable=self.topmost_var,
                        command=lambda: root.attributes("-topmost", self.topmost_var.get())
                        ).grid(row=1, column=0, sticky="w", padx=10, pady=(0, 8))

        self.afk_running = False
        self.gen = 0          # stop_afk() 时递增，让已排队的按键回调失效
        self.next_press = None
        self.hammer_stop = None
        self.hammer_thread = None
        self.prev = {VK_AFK: False, VK_HAMMER: False, VK_STOP: False, VK_QUIT: False}

        winmm.timeBeginPeriod(1)  # 系统计时器精度提到 1 ms
        root.protocol("WM_DELETE_WINDOW", self.quit)
        self.poll()

    # ---------- 界面 ----------
    def build_keys_tab(self, frm):
        self.key_var = tk.StringVar(value="c")
        self.count_var = tk.StringVar(value="2")
        self.gap_var = tk.StringVar(value="500")
        self.period_var = tk.StringVar(value="180")
        self.hammer_var = tk.IntVar(value=HAMMER_HOLDS[0][0])
        self.status_var = tk.StringVar(value=IDLE_TEXT)

        # 防挂机：每隔一个周期自动按几下键盘
        f8 = ttk.LabelFrame(frm, text="F8  防挂机（定时按键）", padding=8)
        f8.grid(row=0, column=0, sticky="ew")
        self.entries = []
        fields = [("按键", self.key_var), ("每次按几下", self.count_var),
                  ("两下间隔 (ms)", self.gap_var), ("周期 (秒)", self.period_var)]
        for i, (label, var) in enumerate(fields):
            ttk.Label(f8, text=label).grid(row=i, column=0, sticky="w", padx=(0, 8), pady=2)
            e = ttk.Entry(f8, textvariable=var, width=10)
            e.grid(row=i, column=1, sticky="w", pady=2)
            self.entries.append(e)

        # 快速敲锤：左键按住一段时间再抬起，循环
        f9 = ttk.LabelFrame(frm, text="F9  快速敲锤（左键）", padding=8)
        f9.grid(row=1, column=0, sticky="ew", pady=(8, 0))
        self.hammer_radios = []
        for i, (ms, name) in enumerate(HAMMER_HOLDS):
            r = ttk.Radiobutton(f9, text=f"{ms} ms  {name}", variable=self.hammer_var, value=ms)
            r.grid(row=i, column=0, sticky="w")
            self.hammer_radios.append(r)
        ttk.Label(f9, text=f"按下上面的时长 → 抬起 {HAMMER_UP_S * 1000:.0f} ms → 循环").grid(
            row=2, column=0, sticky="w", pady=(4, 0))

        btns = ttk.Frame(frm)
        btns.grid(row=2, column=0, pady=(10, 4))
        ttk.Button(btns, text="防挂机 (F8)", command=self.start_afk).grid(row=0, column=0, padx=4)
        ttk.Button(btns, text="敲锤 (F9)", command=self.start_hammer).grid(row=0, column=1, padx=4)
        ttk.Button(btns, text="停止 (Esc)", command=self.stop_all).grid(row=0, column=2, padx=4)
        ttk.Label(frm, textvariable=self.status_var).grid(row=3, column=0, sticky="w")

    def build_mortar_tab(self, frm):
        self.mortar_var = tk.StringVar()
        self.target_var = tk.StringVar()
        self.dir_var = tk.StringVar(value="DIRECTION: ---°")
        self.range_var = tk.StringVar(value="RANGE:     --- m")
        self.exact_var = tk.StringVar()

        ttk.Label(frm, text="迫击炮 X Y").grid(row=0, column=0, sticky="w", padx=(0, 8), pady=2)
        me = ttk.Entry(frm, textvariable=self.mortar_var, width=20)
        me.grid(row=0, column=1, sticky="w", pady=2)
        ttk.Label(frm, text="目标 X Y").grid(row=1, column=0, sticky="w", padx=(0, 8), pady=2)
        self.target_entry = ttk.Entry(frm, textvariable=self.target_var, width=20)
        self.target_entry.grid(row=1, column=1, sticky="w", pady=2)
        ttk.Label(frm, text="例: 104.39 63.59，回车计算", foreground="gray").grid(
            row=2, column=0, columnspan=2, sticky="w")

        me.bind("<Return>", lambda e: self.target_entry.focus_set())
        self.target_entry.bind("<Return>", lambda e: self.calc_mortar())

        big = ("Consolas", 22, "bold")
        ttk.Label(frm, textvariable=self.dir_var, font=big).grid(
            row=3, column=0, columnspan=2, sticky="w", pady=(12, 0))
        ttk.Label(frm, textvariable=self.range_var, font=big).grid(
            row=4, column=0, columnspan=2, sticky="w")
        self.exact_label = ttk.Label(frm, textvariable=self.exact_var, font=("Consolas", 10))
        self.exact_label.grid(row=5, column=0, columnspan=2, sticky="w", pady=(4, 8))

        ttk.Label(frm, text="历史").grid(row=6, column=0, sticky="w")
        self.history = tk.Listbox(frm, height=6, width=42, font=("Consolas", 9))
        self.history.grid(row=7, column=0, columnspan=2, sticky="w")

    # ---------- 迫击炮 ----------
    def calc_mortar(self):
        try:
            mx, my = parse_xy(self.mortar_var.get())
        except ValueError as e:
            self.show_mortar_error(f"迫击炮坐标: {e}")
            return
        try:
            tx, ty = parse_xy(self.target_var.get())
        except ValueError as e:
            self.show_mortar_error(f"目标坐标: {e}")
            return
        bearing, dist = mortar_solution(mx, my, tx, ty)
        deg, rng = firing_solution(mx, my, tx, ty)
        self.dir_var.set(f"DIRECTION: {deg:03d}°")
        self.range_var.set(f"RANGE:     {rng} m")
        self.exact_label.configure(foreground="")
        self.exact_var.set(f"Bearing exact: {bearing:.2f}°   Range exact: {dist:.2f} m")
        self.history.insert(0, f"({tx:g}, {ty:g})  {deg:03d}°  {rng} m")
        self.history.delete(20, "end")
        # 选中目标框内容，直接输入下一个目标就会覆盖
        self.target_entry.select_range(0, "end")
        self.target_entry.icursor("end")

    def show_mortar_error(self, msg):
        self.exact_label.configure(foreground="red")
        self.exact_var.set(msg)

    # ---------- 全局快捷键 ----------
    def poll(self):
        for vk in self.prev:
            now = key_down(vk)
            if now and not self.prev[vk]:
                if vk == VK_AFK:
                    self.start_afk()
                elif vk == VK_HAMMER:
                    self.start_hammer()
                elif vk == VK_STOP:
                    self.stop_all()
                elif vk == VK_QUIT:
                    self.quit()
                    return
            self.prev[vk] = now
        self.update_status()
        self.root.after(POLL_MS, self.poll)

    def update_status(self):
        parts = []
        if self.afk_running and self.next_press is not None:
            left = max(0, self.next_press - time.monotonic())
            parts.append(f"防挂机 {left:.0f} 秒后按键")
        if self.hammer_running():
            parts.append(f"敲锤 {self.hammer_var.get()} ms")
        self.status_var.set("运行中   " + " | ".join(parts) if parts else IDLE_TEXT)

    # ---------- F8: 防挂机 ----------
    def read_afk_settings(self):
        key = self.key_var.get()
        if len(key) != 1 or char_to_vk(key) is None:
            raise ValueError("按键请填一个字符，例如 c")
        count = int(self.count_var.get())
        gap = int(self.gap_var.get())
        period = float(self.period_var.get())
        if count < 1 or gap < 0 or period <= 0:
            raise ValueError("次数 ≥ 1，间隔 ≥ 0，周期 > 0")
        if (count - 1) * gap / 1000 >= period:
            raise ValueError("一轮按键的总时长超过了周期")
        return char_to_vk(key), count, gap, period

    def start_afk(self):
        if self.afk_running:
            return
        try:
            self.afk_settings = self.read_afk_settings()
        except ValueError as e:
            messagebox.showerror("设置有误", str(e))
            return
        self.afk_running = True
        for e in self.entries:
            e.state(["disabled"])
        self.schedule_cycle(self.gen)

    def stop_afk(self):
        if not self.afk_running:
            return
        self.gen += 1
        self.afk_running = False
        self.next_press = None
        for e in self.entries:
            e.state(["!disabled"])

    def schedule_cycle(self, gen):
        period_ms = int(self.afk_settings[3] * 1000)
        self.next_press = time.monotonic() + period_ms / 1000
        self.root.after(period_ms, lambda: self.cycle(gen))

    def cycle(self, gen):
        if gen != self.gen:
            return
        vk, count, gap, _ = self.afk_settings
        for i in range(count):
            self.root.after(i * gap, lambda: self.tap(vk, gen))
        self.schedule_cycle(gen)

    def tap(self, vk, gen):
        if gen != self.gen:
            return
        scan = user32.MapVirtualKeyW(vk, 0)
        user32.keybd_event(vk, scan, 0, 0)
        # 松开不受 stop 影响，避免按键卡住
        self.root.after(KEY_HOLD_MS, lambda: user32.keybd_event(vk, scan, KEYEVENTF_KEYUP, 0))

    # ---------- F9: 快速敲锤 ----------
    def hammer_running(self):
        return self.hammer_thread is not None and self.hammer_thread.is_alive()

    def start_hammer(self):
        if self.hammer_running():
            return
        self.hammer_stop = threading.Event()
        self.hammer_thread = threading.Thread(
            target=hammer_loop, args=(self.hammer_stop, self.hammer_var.get() / 1000), daemon=True)
        self.hammer_thread.start()
        for r in self.hammer_radios:
            r.state(["disabled"])

    def stop_hammer(self):
        if self.hammer_stop is not None:
            self.hammer_stop.set()
        if self.hammer_thread is not None:
            self.hammer_thread.join(timeout=1)  # 等线程把左键松开
        for r in self.hammer_radios:
            r.state(["!disabled"])

    # ---------- 停止 / 退出 ----------
    def stop_all(self):
        self.stop_afk()
        self.stop_hammer()

    def quit(self):
        self.stop_all()
        winmm.timeEndPeriod(1)
        self.root.destroy()


if __name__ == "__main__":
    if "--test" in sys.argv:
        run_self_tests()
    else:
        root = tk.Tk()
        App(root)
        root.mainloop()
