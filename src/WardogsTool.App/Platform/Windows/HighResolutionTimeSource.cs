using System.Diagnostics;
using Microsoft.Win32.SafeHandles;
using WardogsTool.Core.Timing;

namespace WardogsTool.App.Platform.Windows;

/// <summary>
/// Waits on a high-resolution waitable timer together with the cancellation handle.
/// </summary>
/// <remarks>
/// This is the mechanism CPython 3.11+ uses for time.sleep — and therefore what the Python tool's
/// hammer timing really rests on. timeBeginPeriod(1) alone is not enough: from Windows 11 on, a
/// process that owns windows loses the raised timer resolution while those windows are minimized
/// or fully covered (i.e. whenever the game is in front). Measured with the tool minimized, a
/// WaitOne-based wait stretched the 310 ms hold to ~318 ms and the 40 ms gap to ~47 ms; Python
/// stayed at 310.7 / 40.6 ms. CREATE_WAITABLE_TIMER_HIGH_RESOLUTION timers are not throttled
/// that way. Falls back to <see cref="SystemTimeSource"/> where the flag is unsupported
/// (Windows 10 before 1803).
/// </remarks>
internal sealed class HighResolutionTimeSource : ITimeSource
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly SystemTimeSource _fallback = new();
    private readonly bool _supported;

    public HighResolutionTimeSource()
    {
        using var probe = CreateTimer();
        _supported = probe is not null;
    }

    public TimeSpan Now => _clock.Elapsed;

    public bool WaitUntil(TimeSpan deadline, CancellationToken cancellation)
    {
        if (!_supported)
            return WaitWithFallback(deadline, cancellation);

        using var timer = CreateTimer();
        if (timer is null)
            return WaitWithFallback(deadline, cancellation);

        WaitHandle[] handles = cancellation.CanBeCanceled ? [timer, cancellation.WaitHandle] : [timer];
        while (true)
        {
            if (cancellation.IsCancellationRequested)
                return false;
            var left = deadline - Now;
            if (left <= TimeSpan.Zero)
                return true;
            var due = -left.Ticks; // negative = relative, in 100 ns units (TimeSpan ticks are 100 ns)
            if (!NativeMethods.SetWaitableTimer(timer.SafeWaitHandle, ref due, 0, 0, 0, false))
                return WaitWithFallback(deadline, cancellation);
            if (WaitHandle.WaitAny(handles) == 1)
                return false;
        }
    }

    // The fallback has its own clock; translate the deadline onto it.
    private bool WaitWithFallback(TimeSpan deadline, CancellationToken cancellation) =>
        _fallback.WaitUntil(_fallback.Now + (deadline - Now), cancellation);

    private static TimerHandle? CreateTimer()
    {
        var handle = NativeMethods.CreateWaitableTimerExW(0, null,
            NativeMethods.CREATE_WAITABLE_TIMER_HIGH_RESOLUTION, NativeMethods.TIMER_ALL_ACCESS);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            return null;
        }
        return new TimerHandle(handle);
    }

    private sealed class TimerHandle : WaitHandle
    {
        public TimerHandle(SafeWaitHandle handle) => SafeWaitHandle = handle;
    }
}
