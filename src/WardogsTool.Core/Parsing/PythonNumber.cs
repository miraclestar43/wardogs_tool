using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WardogsTool.Core.Parsing;

/// <summary>
/// Parses numbers exactly the way CPython's <c>float(str)</c> and <c>int(str)</c> do, because the
/// Python tool (wardogs_tool.py) is the behavioural reference for this port.
/// </summary>
/// <remarks>
/// What CPython accepts and .NET's double.Parse does not (or vice versa):
/// <list type="bullet">
/// <item>Any Unicode decimal digit counts as a digit, so full-width IME input such as "１０４.３９" parses.</item>
/// <item>Underscores are allowed, but only between two digits ("1_0" yes, "_1", "1_", "1__0" no).</item>
/// <item>"inf", "infinity" and "nan" (any case, optional sign) are accepted by float().</item>
/// <item>No thousands separators and no culture: "." is the only decimal point.</item>
/// <item>Python's whitespace set is char.IsWhiteSpace plus U+001C..U+001F.</item>
/// </list>
/// Callers decide what to do with non-finite results; this class only mirrors the parse.
/// </remarks>
public static partial class PythonNumber
{
    [GeneratedRegex(@"^[+-]?(?:(?:\d(?:_?\d)*)?\.\d(?:_?\d)*|\d(?:_?\d)*\.?)(?:[eE][+-]?\d(?:_?\d)*)?$")]
    private static partial Regex FloatGrammar();

    [GeneratedRegex(@"^[+-]?(?:inf|infinity|nan)$", RegexOptions.IgnoreCase)]
    private static partial Regex FloatSpecial();

    [GeneratedRegex(@"^[+-]?\d(?:_?\d)*$")]
    private static partial Regex IntGrammar();

    /// <summary>Python's str.isspace() for a single UTF-16 code unit.</summary>
    public static bool IsPythonSpace(char c) => char.IsWhiteSpace(c) || (c >= '\x1c' && c <= '\x1f');

    /// <summary>Python's <c>text.split()</c> with no arguments: split on whitespace runs, drop empties.</summary>
    public static IReadOnlyList<string> SplitWhitespace(string text)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        foreach (var c in text)
        {
            if (IsPythonSpace(c))
            {
                if (current.Length > 0)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }
        if (current.Length > 0)
            parts.Add(current.ToString());
        return parts;
    }

    /// <summary>Python <c>float(text)</c>. Returns false with Python's error message on failure.</summary>
    public static bool TryParseFloat(string text, out double value, out string error)
    {
        value = 0;
        error = $"could not convert string to float: {PythonRepr(text)}";
        var ascii = ToAsciiDigits(text);
        if (ascii is null)
            return false;

        if (FloatSpecial().IsMatch(ascii))
        {
            var negative = ascii.StartsWith('-');
            var body = ascii.TrimStart('+', '-').ToLowerInvariant();
            value = body == "nan" ? double.NaN : negative ? double.NegativeInfinity : double.PositiveInfinity;
            error = "";
            return true;
        }
        if (!FloatGrammar().IsMatch(ascii))
            return false;

        // .NET Core 3.0+ parses IEEE-correctly and returns ±Infinity on overflow, like CPython.
        value = double.Parse(ascii.Replace("_", ""), NumberStyles.Float, CultureInfo.InvariantCulture);
        error = "";
        return true;
    }

    /// <summary>
    /// Python <c>int(text)</c> in base 10. Python ints are unbounded; values outside the long range
    /// are rejected here (they would make the Python tool hang or overflow anyway).
    /// </summary>
    public static bool TryParseInt(string text, out long value, out string error)
    {
        value = 0;
        error = $"invalid literal for int() with base 10: {PythonRepr(text)}";
        var ascii = ToAsciiDigits(text);
        if (ascii is null || !IntGrammar().IsMatch(ascii))
            return false;
        if (!long.TryParse(ascii.Replace("_", ""), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value))
        {
            error = $"number is too large: {PythonRepr(text)}";
            return false;
        }
        error = "";
        return true;
    }

    /// <summary>
    /// CPython's _PyUnicode_TransformDecimalAndSpaceToASCII followed by strip(): Unicode decimal
    /// digits become ASCII digits, whitespace is trimmed, any other non-ASCII character is invalid.
    /// </summary>
    private static string? ToAsciiDigits(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (IsPythonSpace(c))
            {
                sb.Append(' ');
                continue;
            }
            var digit = CharUnicodeInfo.GetDecimalDigitValue(c);
            if (c > 127 && digit >= 0 && CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.DecimalDigitNumber)
                sb.Append((char)('0' + digit));
            else if (c <= 127)
                sb.Append(c);
            else
                return null;
        }
        var stripped = sb.ToString().Trim(' ');
        // Interior whitespace ("1 0") is not part of either grammar, so the regexes reject it.
        return stripped;
    }

    /// <summary>Close enough to Python's repr() of a str for error messages.</summary>
    private static string PythonRepr(string s) => s.Contains('\'') && !s.Contains('"') ? $"\"{s}\"" : $"'{s.Replace("'", "\\'")}'";
}
