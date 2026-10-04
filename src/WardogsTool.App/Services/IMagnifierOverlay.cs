using WardogsTool.Core.Magnifier;

namespace WardogsTool.App.Services;

/// <summary>The centre-screen magnifier, as the view model sees it.</summary>
public interface IMagnifierOverlay : IDisposable
{
    bool IsShown { get; }

    /// <summary>Monitor name and resolution of the current lens, e.g. "\\.\DISPLAY1 · 2560×1440".</summary>
    string Display { get; }

    MagnifierLayout? Layout { get; }

    /// <summary>Shows the lens centred on the monitor holding the foreground window.</summary>
    void Show(double zoom);

    void SetZoom(double zoom);

    void Hide();
}
