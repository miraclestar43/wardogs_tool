using System.Diagnostics;

namespace WardogsTool.Core.Timing;

/// <summary>
/// A monotonic clock plus a cancellable wait. Abstracted so the hammer and anti-AFK state machines
/// can run against a fake clock in tests.
/// </summary>
public interface ITimeSource
{
    TimeSpan Now { get; }

    /// <summary>
    /// Waits until <paramref name="deadline"/>. Returns false if <paramref name="cancellation"/> is
    /// (or becomes) cancelled — checked before returning true, exactly like <c>sleep_or_stop</c> in
    /// wardogs_tool.py, which returns False once stop is set even if the time is already up.
    /// Pass CancellationToken.None for a wait that must not be interrupted.
    /// </summary>
    bool WaitUntil(TimeSpan deadline, CancellationToken cancellation);
}

/// <summary>
/// Stopwatch clock; waits block on the cancellation handle so a stop wakes the worker immediately.
/// </summary>
/// <remarks>
/// Wait precision depends on the system timer resolution. The app raises it to 1 ms for its
/// lifetime (timeBeginPeriod(1)), as the Python tool does; without that a 40 ms wait can take ~46 ms.
/// The Python tool sleeps in 5 ms slices and polls the stop flag; waiting on the handle gives the
/// same deadline without polling.
/// </remarks>
public sealed class SystemTimeSource : ITimeSource
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    public TimeSpan Now => _clock.Elapsed;

    public bool WaitUntil(TimeSpan deadline, CancellationToken cancellation)
    {
        while (true)
        {
            if (cancellation.IsCancellationRequested)
                return false;
            var left = deadline - Now;
            if (left <= TimeSpan.Zero)
                return true;
            var ms = (int)Math.Ceiling(left.TotalMilliseconds);
            if (cancellation.CanBeCanceled)
            {
                if (cancellation.WaitHandle.WaitOne(ms))
                    return false;
            }
            else
            {
                Thread.Sleep(ms);
            }
        }
    }
}
