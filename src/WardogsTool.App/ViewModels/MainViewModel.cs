using System.Collections.ObjectModel;
using System.Windows.Input;
using WardogsTool.App.Services;
using WardogsTool.Core.AntiAfk;
using WardogsTool.Core.Hammer;
using WardogsTool.Core.Input;
using WardogsTool.Core.Magnifier;
using WardogsTool.Core.Mortar;
using WardogsTool.Core.Parsing;
using WardogsTool.Core.Settings;
using WardogsTool.Core.Timing;

namespace WardogsTool.App.ViewModels;

/// <summary>
/// Everything the window shows and does. The behaviour is wardogs_tool.py's App class; the
/// comments name the Python method each part comes from.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    public const int VkF8 = 0x77;
    public const int VkF9 = 0x78;
    public const int VkEscape = 0x1B;
    public const int VkF10 = 0x79;
    public const int VkF12 = 0x7B;
    public static readonly int[] Hotkeys = [VkF8, VkF9, VkF10, VkEscape, VkF12];

    /// <summary>Tabs: 0 Hammer, 1 Anti-AFK, 2 Mortar, 3 Magnifier, 4 Settings.</summary>
    public const int TabCount = 5;

    /// <summary>Based on IDLE_TEXT in wardogs_tool.py, plus F10 and the global meaning of Esc.</summary>
    public const string IdleText = "已停止   F9 敲锤 | F8 防挂机 | F10 放大镜 | Esc 全部停止 | F12 退出";

    /// <summary>Python keeps the latest 20 mortar results (history.delete(20, "end")).</summary>
    public const int HistoryLimit = 20;

    private readonly HammerEngine _hammer;
    private readonly AntiAfkEngine _afk;
    private readonly IKeyboardInput _keyboard;
    private readonly ITimeSource _time;
    private readonly AppSettings _settings;
    private readonly IMagnifierOverlay _magnifier;
    private readonly Action<string, string> _showError;

    public MainViewModel(HammerEngine hammer, AntiAfkEngine afk, IKeyboardInput keyboard, ITimeSource time,
        IMagnifierOverlay magnifier, AppSettings settings, string settingsPath, Action<string, string> showError)
    {
        _hammer = hammer;
        _afk = afk;
        _keyboard = keyboard;
        _time = time;
        _magnifier = magnifier;
        _settings = settings;
        _showError = showError;
        SettingsPath = settingsPath;
        _zoom = MagnifierGeometry.NormalizeZoom(settings.Magnifier.Zoom);

        _holdMs = settings.Hammer.HoldMs;
        _afkKey = settings.AntiAfk.Key;
        _afkCount = settings.AntiAfk.Count;
        _afkGap = settings.AntiAfk.GapMs;
        _afkPeriod = settings.AntiAfk.PeriodSeconds;
        _mortarPosition = settings.Mortar.Position;
        _alwaysOnTop = settings.Window.AlwaysOnTop;
        _selectedTab = Math.Max(0, Math.Min(settings.Window.SelectedTab, TabCount - 1));

        StartHammerCommand = new RelayCommand(StartHammer);
        StartAfkCommand = new RelayCommand(StartAfk);
        StopAllCommand = new RelayCommand(StopAll);
        ToggleMagnifierCommand = new RelayCommand(ToggleMagnifier);
        Refresh();
        RefreshMagnifier();
    }

    public ICommand StartHammerCommand { get; }
    public ICommand StartAfkCommand { get; }
    /// <summary>Every Stop button and Esc.</summary>
    public ICommand StopAllCommand { get; }
    public ICommand ToggleMagnifierCommand { get; }

    public string SettingsPath { get; }

    /// <summary>
    /// F12 (Python: quit()). The window closes itself, which runs <see cref="Shutdown"/>: the same
    /// global stop as Esc, then exit.
    /// </summary>
    public event EventHandler? QuitRequested;

    /// <summary>A mortar result was shown; the view selects the target box for the next entry.</summary>
    public event EventHandler? MortarCalculated;

    // ---------- global ----------

    private string _statusText = IdleText;
    public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }

    private bool _alwaysOnTop;
    public bool AlwaysOnTop { get => _alwaysOnTop; set => Set(ref _alwaysOnTop, value); }

    private int _selectedTab;
    public int SelectedTab { get => _selectedTab; set => Set(ref _selectedTab, value); }

    /// <summary>Python poll(): F8 / F9 / Esc / F12 fresh presses, plus F10.</summary>
    public void OnHotkey(int virtualKey)
    {
        switch (virtualKey)
        {
            case VkF8: StartAfk(); break;
            case VkF9: StartHammer(); break;
            case VkF10: CycleMagnifier(); break;
            case VkEscape: StopAll(); break;
            case VkF12: QuitRequested?.Invoke(this, EventArgs.Empty); break;
        }
    }

    /// <summary>
    /// Global stop — Esc and every Stop button; does not exit. Stops anti-AFK and the hammer,
    /// which release whatever they hold (the hammer's mouse button, any anti-AFK key still down),
    /// and hides the magnifier.
    /// </summary>
    /// <remarks>
    /// Order as in Python's stop_all(): anti-AFK, then the hammer. Python's stop_afk() returns at
    /// once, so the hammer is stopped before waiting for any anti-AFK key-up — otherwise that wait
    /// (up to 50 ms) could let the hammer send one more click or release late.
    /// </remarks>
    public void StopAll()
    {
        _afk.RequestStop();
        _hammer.Stop();
        _afk.Stop();
        RunMagnifierAction(_magnifier.Hide);
        RefreshMagnifier();
        Refresh();
    }

    /// <summary>F12 / closing the window: the global stop, then the settings to save before exit.</summary>
    public AppSettings Shutdown()
    {
        StopAll();
        _settings.Magnifier.Zoom = Zoom;
        _settings.Hammer.HoldMs = HoldMs;
        _settings.AntiAfk.Key = AfkKey;
        _settings.AntiAfk.Count = AfkCount;
        _settings.AntiAfk.GapMs = AfkGap;
        _settings.AntiAfk.PeriodSeconds = AfkPeriod;
        _settings.Mortar.Position = MortarPosition;
        _settings.Window.AlwaysOnTop = AlwaysOnTop;
        _settings.Window.SelectedTab = SelectedTab;
        return _settings;
    }

    /// <summary>Python update_status(), plus the per-feature state shown on each tab.</summary>
    public void Refresh()
    {
        IsHammerRunning = _hammer.IsRunning;
        IsAfkRunning = _afk.IsRunning;

        var parts = new List<string>();
        string? countdown = null;
        if (_afk.IsRunning && _afk.NextRoundAt is { } next)
        {
            var left = Math.Max(0, (next - _time.Now).TotalSeconds);
            countdown = MortarText.FixedPoint(left, 0); // Python f"{left:.0f}"
            parts.Add($"防挂机 {countdown} 秒后按键");
        }
        if (_hammer.IsRunning)
            parts.Add($"敲锤 {HoldMs} ms");
        if (_magnifier.IsShown)
            parts.Add($"放大镜 {Zoom:0.0}x");
        StatusText = parts.Count > 0 ? "运行中   " + string.Join(" | ", parts) : IdleText;
        AfkCountdown = countdown is null ? "" : $"{countdown} 秒后按键";

        if (_hammer.LastError is { } hammerError && !_hammer.IsRunning && _reportedHammerError != hammerError)
        {
            _reportedHammerError = hammerError;
            _showError("敲锤出错", hammerError.Message);
        }
        if (_afk.LastError is { } afkError && !_afk.IsRunning && _reportedAfkError != afkError)
        {
            _reportedAfkError = afkError;
            _showError("防挂机出错", afkError.Message);
        }
    }

    private Exception? _reportedHammerError;
    private Exception? _reportedAfkError;

    // ---------- F9 fast hammer ----------

    private int _holdMs;
    public int HoldMs
    {
        get => _holdMs;
        private set
        {
            if (Set(ref _holdMs, value))
            {
                OnPropertyChanged(nameof(IsSmallMedium));
                OnPropertyChanged(nameof(IsLarge));
            }
        }
    }

    public bool IsSmallMedium
    {
        get => HoldMs == HammerPresets.SmallMedium.HoldMs;
        set { if (value) HoldMs = HammerPresets.SmallMedium.HoldMs; }
    }

    public bool IsLarge
    {
        get => HoldMs == HammerPresets.Large.HoldMs;
        set { if (value) HoldMs = HammerPresets.Large.HoldMs; }
    }

    public int ReleaseGapMs => HammerPresets.ReleaseGapMs;

    private bool _isHammerRunning;
    public bool IsHammerRunning
    {
        get => _isHammerRunning;
        private set
        {
            if (Set(ref _isHammerRunning, value))
                OnPropertyChanged(nameof(CanEditHammer));
        }
    }

    /// <summary>Python disables the preset radios while hammering.</summary>
    public bool CanEditHammer => !IsHammerRunning;

    /// <summary>Python start_hammer(): ignored while running; the preset is read once at start.</summary>
    public void StartHammer()
    {
        _hammer.Start(HoldMs);
        Refresh();
    }

    // ---------- F8 anti-AFK ----------

    private string _afkKey;
    public string AfkKey { get => _afkKey; set => Set(ref _afkKey, value); }

    private string _afkCount;
    public string AfkCount { get => _afkCount; set => Set(ref _afkCount, value); }

    private string _afkGap;
    public string AfkGap { get => _afkGap; set => Set(ref _afkGap, value); }

    private string _afkPeriod;
    public string AfkPeriod { get => _afkPeriod; set => Set(ref _afkPeriod, value); }

    private bool _isAfkRunning;
    public bool IsAfkRunning
    {
        get => _isAfkRunning;
        private set
        {
            if (Set(ref _isAfkRunning, value))
                OnPropertyChanged(nameof(CanEditAfk));
        }
    }

    /// <summary>Python disables the anti-AFK entries while running.</summary>
    public bool CanEditAfk => !IsAfkRunning;

    private string _afkCountdown = "";
    public string AfkCountdown { get => _afkCountdown; private set => Set(ref _afkCountdown, value); }

    /// <summary>Python start_afk(): ignored while running; invalid settings show "设置有误".</summary>
    public void StartAfk()
    {
        if (_afk.IsRunning)
            return;
        if (!AntiAfkSettings.TryValidate(AfkKey, AfkCount, AfkGap, AfkPeriod, _keyboard, out var plan, out var error))
        {
            _showError("设置有误", error);
            return;
        }
        _afk.Start(plan!);
        Refresh();
    }

    // ---------- F10 magnifier ----------

    public IReadOnlyList<double> ZoomChoices => MagnifierGeometry.ZoomChoices;

    private double _zoom;
    public double Zoom
    {
        get => _zoom;
        set
        {
            if (Set(ref _zoom, MagnifierGeometry.NormalizeZoom(value)))
            {
                RunMagnifierAction(() => _magnifier.SetZoom(_zoom));
                RefreshMagnifier();
            }
        }
    }

    private bool _isMagnifierOn;
    public bool IsMagnifierOn { get => _isMagnifierOn; private set => Set(ref _isMagnifierOn, value); }

    private string _magnifierStatus = "";
    public string MagnifierStatus { get => _magnifierStatus; private set => Set(ref _magnifierStatus, value); }

    /// <summary>
    /// F10: one step of OFF → 2.0x → 3.0x → 4.0x → OFF per fresh key-down. Esc and F12 also turn
    /// the lens off (<see cref="StopAll"/>).
    /// </summary>
    public void CycleMagnifier()
    {
        var (on, zoom) = MagnifierGeometry.NextHotkeyState(_magnifier.IsShown, Zoom);
        RunMagnifierAction(() =>
        {
            if (!on)
            {
                _magnifier.Hide();
                return;
            }
            Set(ref _zoom, zoom, nameof(Zoom));
            if (_magnifier.IsShown) _magnifier.SetZoom(zoom);
            else _magnifier.Show(zoom);
        });
        RefreshMagnifier();
    }

    /// <summary>The Magnifier tab's on/off button: shows the lens at the zoom picked in the list.</summary>
    public void ToggleMagnifier()
    {
        RunMagnifierAction(() =>
        {
            if (_magnifier.IsShown) _magnifier.Hide();
            else _magnifier.Show(Zoom);
        });
        RefreshMagnifier();
    }

    private void RunMagnifierAction(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            try { _magnifier.Hide(); } catch { /* already failing */ }
            _showError("放大镜出错", ex.Message);
        }
    }

    private void RefreshMagnifier()
    {
        IsMagnifierOn = _magnifier.IsShown;
        var display = _magnifier.IsShown && _magnifier.Display.Length > 0 ? $"   ·   {_magnifier.Display}" : "";
        MagnifierStatus = $"Magnifier: {(IsMagnifierOn ? "ON" : "OFF")}   ·   Zoom: {Zoom:0.0}x{display}";
    }

    // ---------- mortar ----------

    private string _mortarPosition;
    public string MortarPosition { get => _mortarPosition; set => Set(ref _mortarPosition, value); }

    private string _targetPosition = "";
    public string TargetPosition { get => _targetPosition; set => Set(ref _targetPosition, value); }

    private string _directionText = MortarText.EmptyDirection;
    public string DirectionText { get => _directionText; private set => Set(ref _directionText, value); }

    private string _rangeText = MortarText.EmptyRange;
    public string RangeText { get => _rangeText; private set => Set(ref _rangeText, value); }

    private string _exactText = "";
    public string ExactText { get => _exactText; private set => Set(ref _exactText, value); }

    private bool _exactIsError;
    public bool ExactIsError { get => _exactIsError; private set => Set(ref _exactIsError, value); }

    public ObservableCollection<string> History { get; } = [];

    /// <summary>
    /// Python calc_mortar(). On an error the previous direction/range stay on screen and only the
    /// line under them turns red.
    /// </summary>
    public void CalculateMortar()
    {
        if (!CoordinateParser.TryParse(MortarPosition, out var mortar, out var error))
        {
            ShowMortarError($"迫击炮坐标: {error}");
            return;
        }
        if (!CoordinateParser.TryParse(TargetPosition, out var target, out error))
        {
            ShowMortarError($"目标坐标: {error}");
            return;
        }
        if (!MortarCalculator.TrySolve(mortar, target, out var solution, out error))
        {
            // Python crashes here (floor of nan/inf) and shows nothing; report it instead.
            ShowMortarError(error);
            return;
        }

        DirectionText = MortarText.Direction(solution);
        RangeText = MortarText.Range(solution);
        ExactIsError = false;
        ExactText = MortarText.Exact(solution);
        History.Insert(0, MortarText.HistoryEntry(target, solution));
        while (History.Count > HistoryLimit)
            History.RemoveAt(History.Count - 1);
        MortarCalculated?.Invoke(this, EventArgs.Empty);
    }

    private void ShowMortarError(string message)
    {
        ExactIsError = true;
        ExactText = message;
    }
}
