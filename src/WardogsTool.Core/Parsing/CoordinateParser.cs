using WardogsTool.Core.Mortar;

namespace WardogsTool.Core.Parsing;

/// <summary>
/// Mirrors <c>parse_xy</c> in wardogs_tool.py: commas become spaces, the text is split on
/// whitespace, and there must be exactly two tokens, each parsed with Python's float().
/// </summary>
/// <remarks>
/// Like the Python version this accepts "inf"/"nan"; rejecting non-finite coordinates is the
/// calculator's job (see <see cref="MortarCalculator.TrySolve"/>). Only the ASCII comma is a
/// separator — the full-width "，" is not, exactly as in Python.
/// </remarks>
public static class CoordinateParser
{
    public const string WrongCountMessage = "请输入两个数，例如 104.39 63.59";

    public static bool TryParse(string text, out MapPoint point, out string error)
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
