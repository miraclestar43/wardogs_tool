using System.Text.RegularExpressions;
using WardogsTool.Core.Mortar;

namespace WardogsTool.Core.Parsing;

/// <summary>
/// Parses a map coordinate typed by the user. Two kinds of input are accepted:
/// <list type="bullet">
/// <item><b>Labeled</b>, as the WARDOGS map shows it: <c>x134.98, y65.56</c>. Also
/// <c>x134.98 y65.56</c>, <c>X134.98, Y65.56</c>, <c>x=134.98, y=65.56</c>,
/// <c>X = 134.98 Y = 65.56</c>. Labels are case-insensitive and decide which value is X and
/// which is Y, so <c>y65.56, x134.98</c> is the same point.</item>
/// <item><b>Unlabeled</b>, exactly like <c>parse_xy</c> in wardogs_tool.py: commas become spaces,
/// the text is split on whitespace, and there must be exactly two numbers, X then Y
/// (<c>134.98 65.56</c>, <c>134.98, 65.56</c>).</item>
/// </list>
/// Each number is parsed with Python's float() rules (<see cref="PythonNumber"/>), so negative
/// and decimal values work in both kinds.
/// </summary>
/// <remarks>
/// Labeled mode is chosen only when a token starts with x or y. Unlabeled input that Python
/// accepts or rejects (e.g. "0x10 1", "Infinity 1") therefore behaves exactly as before.
/// In labeled mode anything that is not exactly one x and one y, each followed by one number and
/// separated by a comma or whitespace, is rejected rather than guessed — e.g. "x134.98" (no y),
/// "x1, x2" (two x), "x1y2" (no separator), "x1 2" (an unlabeled number).
/// Only the ASCII comma is a separator; the full-width "，" is not, as in Python.
/// Like the Python version this accepts "inf"/"nan"; rejecting non-finite coordinates is the
/// calculator's job (see <see cref="MortarCalculator.TrySolve"/>).
/// </remarks>
public static class CoordinateParser
{
    /// <summary>parse_xy's message in wardogs_tool.py, kept for parity on unlabeled input.</summary>
    public const string WrongCountMessage = "请输入两个数，例如 104.39 63.59";

    public const string LabeledFormatMessage = "坐标格式不对，请写成 x134.98, y65.56（或 134.98 65.56）";
    public const string DuplicateLabelMessage = "x 和 y 要各写一次，例如 x134.98, y65.56";

    private static readonly Regex LabeledPair = new(
        @"^\s*(?<l1>[xy])\s*=?\s*(?<v1>[^\s,=]+)\s*(?:,|\s)\s*(?<l2>[xy])\s*=?\s*(?<v2>[^\s,=]+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex TokenSeparators = new(@"[\s,=]+", RegexOptions.CultureInvariant);

    public static bool TryParse(string text, out MapPoint point, out string error)
    {
        point = default;
        return IsLabeled(text)
            ? TryParseLabeled(text, out point, out error)
            : TryParseUnlabeled(text, out point, out error);
    }

    /// <summary>True when any token starts with an x or y label.</summary>
    public static bool IsLabeled(string text)
    {
        foreach (var token in TokenSeparators.Split(text))
            if (token.Length > 0 && token[0] is 'x' or 'X' or 'y' or 'Y')
                return true;
        return false;
    }

    private static bool TryParseLabeled(string text, out MapPoint point, out string error)
    {
        point = default;
        var m = LabeledPair.Match(text);
        if (!m.Success)
        {
            error = LabeledFormatMessage;
            return false;
        }
        var first = char.ToLowerInvariant(m.Groups["l1"].Value[0]);
        var second = char.ToLowerInvariant(m.Groups["l2"].Value[0]);
        if (first == second)
        {
            error = DuplicateLabelMessage;
            return false;
        }
        if (!PythonNumber.TryParseFloat(m.Groups["v1"].Value, out var v1, out error))
            return false;
        if (!PythonNumber.TryParseFloat(m.Groups["v2"].Value, out var v2, out error))
            return false;
        point = first == 'x' ? new MapPoint(v1, v2) : new MapPoint(v2, v1);
        return true;
    }

    /// <summary>parse_xy from wardogs_tool.py.</summary>
    private static bool TryParseUnlabeled(string text, out MapPoint point, out string error)
    {
        point = default;
        var parts = PythonNumber.SplitWhitespace(text.Replace(",", " "));
        if (parts.Count != 2)
        {
            error = WrongCountMessage;
            return false;
        }
        if (!PythonNumber.TryParseFloat(parts[0], out var x, out error))
            return false;
        if (!PythonNumber.TryParseFloat(parts[1], out var y, out error))
            return false;
        point = new MapPoint(x, y);
        return true;
    }
}
