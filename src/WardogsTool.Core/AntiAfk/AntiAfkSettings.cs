using WardogsTool.Core.Input;
using WardogsTool.Core.Parsing;

namespace WardogsTool.Core.AntiAfk;

/// <summary>A validated anti-AFK configuration.</summary>
/// <param name="VirtualKey">Key to press (low byte of VkKeyScanW).</param>
/// <param name="Count">Presses per round.</param>
/// <param name="GapMs">Time between the presses of one round.</param>
/// <param name="PeriodMs">Time from one round's start to the next (truncated to whole ms, like Python's int(period * 1000)).</param>
public sealed record AntiAfkPlan(byte VirtualKey, int Count, long GapMs, long PeriodMs);

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
    /// <summary>
    /// Not in the Python tool: there, "inf"/"nan" pass validation and then crash the scheduler,
    /// leaving the UI stuck in a running state with nothing scheduled. Rejecting them is the only
    /// deliberate difference here.
    /// </summary>
    public const string NonFinitePeriodMessage = "周期必须是有限数字（不支持 inf / nan）";

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
        if ((countValue - 1) * (double)gapValue / 1000 >= period)
        {
            error = RoundTooLongMessage;
            return false;
        }
        if (countValue > int.MaxValue)
        {
            error = $"number is too large: '{count}'";
            return false;
        }

        // Python: period_ms = int(period * 1000) — truncation toward zero.
        var periodMs = (long)Math.Truncate(period * 1000);
        plan = new AntiAfkPlan(vk, (int)countValue, gapValue, periodMs);
        error = "";
        return true;
    }
}
