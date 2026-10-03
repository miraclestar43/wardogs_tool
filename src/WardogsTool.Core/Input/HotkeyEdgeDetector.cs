namespace WardogsTool.Core.Input;

/// <summary>
/// Turns a raw stream of key make/break events into "fresh press" edges.
/// </summary>
/// <remarks>
/// The Python tool polls GetAsyncKeyState every 20 ms and fires on a not-down → down transition.
/// Raw Input instead reports every make, including auto-repeat makes while a key is held, and the
/// break when it is released. This detector keeps per-key down state so that:
/// <list type="bullet">
/// <item>the first make of a press fires once;</item>
/// <item>auto-repeat makes while the key is held are ignored;</item>
/// <item>a break re-arms the key.</item>
/// </list>
/// If a break is ever lost (e.g. the secure desktop took the key-up), a key would stay "down"
/// forever and never fire again. Auto-repeat makes arrive at most ~1 s apart (the longest Windows
/// repeat delay), so a make arriving more than <see cref="StaleAfter"/> after the previous event
/// for that key is treated as a new press.
/// </remarks>
public sealed class HotkeyEdgeDetector
{
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMilliseconds(1500);

    private readonly Dictionary<int, TimeSpan> _down = new();

    /// <summary>Feeds one key event; returns true when it is a fresh press of the key.</summary>
    public bool OnKey(int virtualKey, bool isBreak, TimeSpan timestamp)
    {
        if (isBreak)
        {
            _down.Remove(virtualKey);
            return false;
        }
        if (_down.TryGetValue(virtualKey, out var last) && timestamp - last <= StaleAfter)
        {
            _down[virtualKey] = timestamp; // auto-repeat: keep the key alive, no edge
            return false;
        }
        _down[virtualKey] = timestamp;
        return true;
    }

    /// <summary>Forgets all key state (e.g. after the listener is re-registered).</summary>
    public void Reset() => _down.Clear();
}
