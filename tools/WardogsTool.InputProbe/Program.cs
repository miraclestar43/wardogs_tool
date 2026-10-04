using System.IO;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace WardogsTool.InputProbe;

/// <summary>
/// Usage:
///   WardogsTool.InputProbe --exe dist\WardogsTool-portable\WardogsTool.exe --python C:\...\python.exe --out docs\validation
/// Opens a topmost target window, clicks it so it has focus, then drives the Python tool and the
/// C# tool through the same hotkey scenarios. Do not touch the mouse while it runs; moving it
/// aborts the run.
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        string Arg(string name, string? fallback = null)
        {
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback ?? throw new ArgumentException($"missing {name}");
        }

        var exe = Path.GetFullPath(Arg("--exe"));
        var python = Arg("--python", "");
        var outDir = Path.GetFullPath(Arg("--out", "."));
        var only = Arg("--only", "all");
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        if (!File.Exists(Path.Combine(repoRoot, "wardogs_tool.py")))
            repoRoot = Path.GetFullPath(Arg("--repo"));

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var keysAtWindow = new ConcurrentQueue<(long, int)>();
        var mouseAtWindow = 0;
        var window = new Window
        {
            Title = "WardogsTool input probe — do not touch the mouse",
            Width = 640, Height = 420, Topmost = true,
            // Centred on the monitor (not the work area), where the magnifier lens goes.
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = (SystemParameters.PrimaryScreenWidth - 640) / 2,
            Top = (SystemParameters.PrimaryScreenHeight - 420) / 2,
            Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x37)),
            Content = new System.Windows.Controls.TextBlock
            {
                Text = "WardogsTool input probe\n\nClicks and keys from the tools under test land here.\nDo not move the mouse — that aborts the run.",
                Foreground = Brushes.White, FontSize = 18, Margin = new Thickness(30), TextWrapping = TextWrapping.Wrap,
            },
        };
        window.PreviewKeyDown += (_, e) =>
        {
            // With a Chinese IME active, letter keys arrive as ImeProcessed; F-keys bypass the IME.
            var key = e.Key switch
            {
                Key.System => e.SystemKey,
                Key.ImeProcessed => e.ImeProcessedKey,
                Key.DeadCharProcessed => e.DeadCharProcessedKey,
                _ => e.Key,
            };
            keysAtWindow.Enqueue((Recorder.Now, KeyInterop.VirtualKeyFromKey(key)));
            e.Handled = true; // the probe window itself does nothing with keys
        };
        window.PreviewMouseLeftButtonDown += (_, _) => Interlocked.Increment(ref mouseAtWindow);

        var recorder = new Recorder();
        var results = new List<ScenarioResult>();
        string? abort = null;
        var exitCode = 1;

        window.Loaded += (_, _) =>
        {
            recorder.Install();
            var hwnd = new WindowInteropHelper(window).Handle;
            var topLeft = window.PointToScreen(new Point(0, 0));
            var bottomRight = window.PointToScreen(new Point(window.ActualWidth, window.ActualHeight));
            var centre = new Native.POINT { X = (int)((topLeft.X + bottomRight.X) / 2), Y = (int)((topLeft.Y + bottomRight.Y) / 2) };
            var probe = new Probe(recorder, hwnd, centre, keysAtWindow);
            var defaultContent = window.Content;
            void ShowStripes(bool on) => app.Dispatcher.Invoke(() => window.Content = on ? Stripes() : defaultContent);
            int ClicksAtWindow() => Volatile.Read(ref mouseAtWindow);

            new Thread(() =>
            {
                using var backup = new SettingsBackup();
                try
                {
                    if (only == "f9")
                        results.Add(F9Scenarios.Cycle(probe, exe));
                    if (only == "minimized")
                    {
                        results.Add(Scenarios.HammerMinimized(probe, new PythonTarget(python, repoRoot), 310));
                        results.Add(Scenarios.HammerMinimized(probe, new CSharpTarget(exe), 310));
                    }
                    if (only is "all" or "python")
                    {
                        if (string.IsNullOrEmpty(python)) throw new AbortException("--python is required");
                        results.AddRange(Scenarios.RunInputSuite(probe, new PythonTarget(python, repoRoot)));
                    }
                    if (only is "all" or "csharp")
                    {
                        results.AddRange(Scenarios.RunInputSuite(probe, new CSharpTarget(exe)));
                        results.AddRange(UiScenarios.Run(probe, exe));
                        results.Add(MagnifierScenarios.EscGlobalStop(probe, exe));
                        results.Add(F9Scenarios.Cycle(probe, exe));
                    }
                    if (only is "all" or "csharp" or "magnifier")
                        results.Add(MagnifierScenarios.Magnifier(probe, exe, centre, ShowStripes, ClicksAtWindow));
                    exitCode = results.All(r => r.Pass) ? 0 : 2;
                }
                catch (AbortException ex)
                {
                    abort = ex.Message;
                    probe.KeyTap(Probe.VkEsc);
                }
                catch (Exception ex)
                {
                    abort = ex.ToString();
                    probe.KeyTap(Probe.VkEsc);
                }
                finally
                {
                    app.Dispatcher.Invoke(() =>
                    {
                        recorder.Dispose();
                        window.Close();
                        app.Shutdown();
                    });
                }
            }) { IsBackground = true, Name = "scenarios" }.Start();
        };

        app.Run(window);

        Directory.CreateDirectory(outDir);
        var report = Report(results, abort, mouseAtWindow, exe);
        var path = Path.Combine(outDir, "input-probe-report.md");
        File.WriteAllText(path, report, new UTF8Encoding(false));
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine(report);
        Console.WriteLine($"Report: {path}");
        return abort is null ? exitCode : 3;
    }

    /// <summary>Vertical black/white stripes, 8 DIP each, for measuring magnification.</summary>
    private static UIElement Stripes()
    {
        var tile = new DrawingGroup();
        tile.Children.Add(new GeometryDrawing(Brushes.White, null, new RectangleGeometry(new Rect(0, 0, 16, 16))));
        tile.Children.Add(new GeometryDrawing(Brushes.Black, null, new RectangleGeometry(new Rect(0, 0, 8, 16))));
        var brush = new DrawingBrush(tile)
        {
            TileMode = TileMode.Tile, Stretch = Stretch.None,
            Viewport = new Rect(0, 0, 16, 16), ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, 16, 16), ViewboxUnits = BrushMappingMode.Absolute,
        };
        return new System.Windows.Controls.Border { Background = brush, SnapsToDevicePixels = true };
    }

    private static string Report(List<ScenarioResult> results, string? abort, int mouseAtWindow, string exe)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Input probe report");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Run: {DateTime.Now:yyyy-MM-dd HH:mm} · {Environment.OSVersion} · C# build: `{Path.GetFileName(exe)}`");
        sb.AppendLine();
        if (abort is not null)
        {
            sb.AppendLine($"**ABORTED:** {abort}");
            sb.AppendLine();
        }
        sb.AppendLine(CultureInfo.InvariantCulture, $"Result: **{results.Count(r => r.Pass)}/{results.Count} scenarios passed**. Left-button presses that reached the probe window: {mouseAtWindow}.");
        sb.AppendLine();
        foreach (var group in results.GroupBy(r => r.Name))
        {
            sb.AppendLine($"## {group.Key}");
            sb.AppendLine();
            var targets = group.ToList();
            sb.AppendLine("| | " + string.Join(" | ", targets.Select(t => t.Target)) + " |");
            sb.AppendLine("|---|" + string.Concat(targets.Select(_ => "---|")));
            sb.AppendLine("| **result** | " + string.Join(" | ", targets.Select(t => t.Pass ? "PASS" : "**FAIL**")) + " |");
            foreach (var key in targets.SelectMany(t => t.Metrics.Select(m => m.Key)).Distinct())
                sb.AppendLine($"| {key} | " + string.Join(" | ", targets.Select(t => t.Metrics.FirstOrDefault(m => m.Key == key).Value?.Replace("|", "\\|") ?? "")) + " |");
            foreach (var t in targets.Where(t => !t.Pass))
                sb.AppendLine().AppendLine($"{t.Target} failures: " + string.Join("; ", t.Failures));
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
