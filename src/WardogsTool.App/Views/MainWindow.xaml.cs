using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using WardogsTool.App.Platform.Windows;
using WardogsTool.App.ViewModels;
using WardogsTool.Core.Settings;

namespace WardogsTool.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly GlobalHotkeyService _hotkeys = new(MainViewModel.Hotkeys);
    private readonly DispatcherTimer _refresh;

    /// <param name="hotkeysEnabled">false only for the --test self-test path.</param>
    public MainWindow(MainViewModel vm, AppSettings.WindowSection placement, bool hotkeysEnabled = true)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        RestorePlacement(placement);

        vm.MortarCalculated += (_, _) =>
        {
            // Python: select the target text so typing the next target replaces it.
            TargetBox.Focus();
            TargetBox.SelectAll();
        };
        vm.QuitRequested += (_, _) => Close();

        if (hotkeysEnabled)
        {
            _hotkeys.Pressed += vm.OnHotkey;
            SourceInitialized += (_, _) => _hotkeys.Attach(this);
        }

        // Python polls every 20 ms; here hotkeys are event-driven and this only refreshes the
        // status line and countdown.
        _refresh = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Background, (_, _) => vm.Refresh(), Dispatcher);
        _refresh.Start();
        Closed += (_, _) =>
        {
            _refresh.Stop();
            _hotkeys.Dispose();
        };
    }

    /// <summary>Where the window was, for the settings file.</summary>
    public (double Left, double Top) Placement => (RestoreBounds.Left, RestoreBounds.Top);

    private void RestorePlacement(AppSettings.WindowSection placement)
    {
        if (placement is { Left: { } left, Top: { } top }
            && left >= SystemParameters.VirtualScreenLeft - 50
            && top >= SystemParameters.VirtualScreenTop - 10
            && left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 80
            && top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 60)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    // Python: Enter in the mortar box moves to the target box.
    private void MortarBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            TargetBox.Focus();
            e.Handled = true;
        }
    }

    // Python: Enter in the target box calculates.
    private void TargetBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _vm.CalculateMortar();
            e.Handled = true;
        }
    }
}
