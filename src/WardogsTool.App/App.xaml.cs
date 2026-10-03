using System.Globalization;
using System.Windows;
using WardogsTool.App.Platform.Windows;
using WardogsTool.App.ViewModels;
using WardogsTool.App.Views;
using WardogsTool.Core.AntiAfk;
using WardogsTool.Core.Hammer;
using WardogsTool.Core.Mortar;
using WardogsTool.Core.Settings;
using WardogsTool.Core.Timing;

namespace WardogsTool.App;

public partial class App : Application
{
    private TimerResolution? _timerResolution;
    private HammerEngine? _hammer;
    private AntiAfkEngine? _afk;
    private MainViewModel? _vm;
    private SettingsStore? _store;
    private MainWindow? _window;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Python: `python wardogs_tool.py --test` runs the mortar self-tests.
        if (e.Args.Contains("--test"))
        {
            Shutdown(SelfTest.Run());
            return;
        }

        // Last-resort cleanup so the left button / anti-AFK key is never left held down when
        // the process goes away. (A hard kill from Task Manager cannot be intercepted.)
        AppDomain.CurrentDomain.UnhandledException += (_, _) => EmergencyRelease();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => EmergencyRelease();
        SessionEnding += (_, _) => EmergencyRelease();
        // Like a Tk callback error in the Python tool: report it and keep running.
        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;
            MessageBox.Show(args.Exception.Message, "WARDOGS Tool 出错", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        _timerResolution = TimerResolution.Begin();
        var time = new SystemTimeSource();
        var keyboard = new WindowsKeyboardInput();
        _hammer = new HammerEngine(new WindowsMouseInput(), time);
        _afk = new AntiAfkEngine(keyboard, time);

        _store = new SettingsStore(SettingsStore.DefaultDirectory);
        var (settings, warning) = _store.Load();

        _vm = new MainViewModel(_hammer, _afk, keyboard, time, settings,
            (title, message) => MessageBox.Show(_window!, message, title, MessageBoxButton.OK, MessageBoxImage.Error));
        _window = new MainWindow(_vm, settings.Window);
        _window.Closing += (_, _) => SaveAndStop();
        MainWindow = _window;
        _window.Show();

        if (warning is not null)
            MessageBox.Show(_window, warning, "WARDOGS Tool", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void SaveAndStop()
    {
        if (_vm is null || _store is null || _window is null)
            return;
        var settings = _vm.Shutdown(); // stops hammer and anti-AFK first
        (settings.Window.Left, settings.Window.Top) = _window.Placement;
        try
        {
            _store.Save(settings);
        }
        catch (Exception ex)
        {
            MessageBox.Show(_window, $"设置保存失败：{ex.Message}", "WARDOGS Tool", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void EmergencyRelease()
    {
        try { _afk?.Stop(); } catch { /* exiting anyway */ }
        try { _hammer?.Stop(); } catch { /* exiting anyway */ }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        EmergencyRelease();
        _timerResolution?.Dispose();
        base.OnExit(e);
    }
}

/// <summary>Port of run_self_tests() in wardogs_tool.py.</summary>
internal static class SelfTest
{
    public static int Run()
    {
        NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);
        (double, double, double, double, int, long)[] cases =
        [
            (100.32, 59.45, 104.39, 63.59, 45, 581),
            (78.49, 71.84, 81.44, 70.78, 110, 313),
            (78.49, 71.84, 83.60, 72.96, 78, 523),
        ];
        var failed = false;
        foreach (var (mx, my, tx, ty, deg, rng) in cases)
        {
            MortarCalculator.TrySolve(new(mx, my), new(tx, ty), out var s, out _);
            var ok = s.Direction == deg && s.Range == rng;
            failed |= !ok;
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{(ok ? "OK " : "FAIL")} ({mx:R}, {my:R}) -> ({tx:R}, {ty:R}): {s.Direction:000}°, {s.Range} m (期望 {deg:000}°, {rng} m)"));
        }
        Console.WriteLine(failed ? "失败" : "全部通过");
        return failed ? 1 : 0;
    }
}
