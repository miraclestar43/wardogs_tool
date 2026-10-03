using System.Numerics;
using WardogsTool.Core.Input;
using WardogsTool.Core.Parsing;

namespace WardogsTool.Core.AntiAfk;

/// <summary>A validated anti-AFK configuration.</summary>
/// <param name="VirtualKey">Key to press (low byte of VkKeyScanW).</param>
/// <param name="ScanCode">MapVirtualKeyW(vk, 0), taken on the UI thread like Python's tap() does.</param>
/// <param name="Count">Presses per round.</param>
/// <param name="GapMs">Time between the presses of one round.</param>
/// <param name="PeriodMs">Time from one round's start to the next (truncated to whole ms, like Python's int(period * 1000)).</param>
public sealed record AntiAfkPlan(byte VirtualKey, byte ScanCode, int Count, long GapMs, long PeriodMs);

/// <summary>
/// Port of <c>read_afk_settings</c> in wardogs_tool.py: same checks, same order, same messages.
/// </summary>
public static class AntiAfkSettings
{
    public const string DefaultKey = "c";
    public const string DefaultCount = "2";
    public const string DefaultGapMs = "500";
    public const string DefaultPeriodSeconds = "180";

    public const string KeyMessage = "按键请填一个字符，例如 c";
    public const string RangeMessage = "次数 ≥ 1，间隔 ≥ 0，周期 > 0";
    public const string RoundTooLongMessage = "一轮按键的总时长超过了周期";

    // The messages below have no Python equivalent. In each case the Python tool passes
    // read_afk_settings and then fails while scheduling (OverflowError / ValueError from
    // int(period * 1000), or millions of after() calls), leaving its UI stuck in "running" with
    // nothing scheduled. The port rejects those inputs up front instead.
    public const string NonFinitePeriodMessage = "周期必须是有限数字（不支持 inf / nan）";
    public const string PeriodTooLargeMessage = "周期太大";
    public const string CountTooLargeMessage = "次数太大";

    /// <summary>Longest period the scheduler can represent (TimeSpan range), in ms.</summary>
    public static readonly long MaxPeriodMs = (long)TimeSpan.MaxValue.TotalMilliseconds / 2;

    public static bool TryValidate(string key, string count, string gapMs, string periodSeconds,
        IKeyboardInput keyboard, out AntiAfkPlan? plan, out string error)
    {
        plan = null;
        if (key.Length != 1 || !keyboard.TryGetVirtualKey(key[0], out var vk))
        {
            error = KeyMessage;
            return false;
        }
        if (!PythonNumber.TryParseInt(count, out var countValue, out error))
            return false;
        if (!PythonNumber.TryParseInt(gapMs, out var gapValue, out error))
            return false;
        if (!PythonNumber.TryParseFloat(periodSeconds, out var period, out error))
            return false;

        // Python: `if count < 1 or gap < 0 or period <= 0`. NaN compares false everywhere, so it
        // slips through this check in Python; it is caught by the finiteness check below.
        if (countValue < 1 || gapValue < 0 || period <= 0)
        {
            error = RangeMessage;
            return false;
        }
        if (!double.IsFinite(period))
        {
            error = NonFinitePeriodMessage;
            return false;
        }
        // Python: `(count - 1) * gap / 1000 >= period` — exact int product, then true division.
        if ((double)((countValue - 1) * gapValue) / 1000 >= period)
        {
            error = RoundTooLongMessage;
            return false;
        }
        // Python: period_ms = int(period * 1000) — truncation toward zero; period * 1000 can overflow.
        var periodMsExact = Math.Truncate(period * 1000);
        if (!double.IsFinite(periodMsExact) || periodMsExact > MaxPeriodMs)
        {
            error = PeriodTooLargeMessage;
            return false;
        }
        if (countValue > int.MaxValue)
        {
            error = CountTooLargeMessage;
            return false;
        }

        // The round check above bounds (count - 1) * gap, so gap fits in a long whenever it is
        // used; with count == 1 it is never used and may be arbitrarily large.
        var gap = gapValue <= long.MaxValue ? (long)gapValue : 0;
        plan = new AntiAfkPlan(vk, keyboard.GetScanCode(vk), (int)countValue, gap, (long)periodMsExact);
        error = "";
        return true;
    }
}
