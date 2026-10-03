using System.Globalization;
using System.Numerics;
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

    /// <summary>Python <c>int(text)</c> in base 10. Unbounded, like Python's int.</summary>
    public static bool TryParseInt(string text, out BigInteger value, out string error)
    {
        value = BigInteger.Zero;
        // CPython: "invalid literal for int() with base 10: %.200R" — the repr is cut at 200 characters.
        var repr = PythonRepr(text);
        error = $"invalid literal for int() with base 10: {(repr.Length > 200 ? repr[..200] : repr)}";
        var ascii = ToAsciiDigits(text);
        if (ascii is null || !IntGrammar().IsMatch(ascii))
            return false;
        value = BigInteger.Parse(ascii.Replace("_", ""), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        error = "";
        return true;
    }

    /// <summary>
    /// CPython's _PyUnicode_TransformDecimalAndSpaceToASCII followed by strip(): Unicode decimal
    /// digits (by code point, so astral digits such as U+1D7CE count) become ASCII digits,
    /// whitespace is trimmed, any other non-ASCII character is invalid.
    /// </summary>
    private static string? ToAsciiDigits(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.IsBmp && IsPythonSpace((char)rune.Value))
                sb.Append(' ');
            else if (rune.IsAscii)
                sb.Append((char)rune.Value);
            else if (Rune.GetUnicodeCategory(rune) == UnicodeCategory.DecimalDigitNumber)
                sb.Append((char)('0' + (int)Rune.GetNumericValue(rune)));
            else
                return null;
        }
        // Interior whitespace ("1 0") is not part of either grammar, so the regexes reject it.
        return sb.ToString().Trim(' ');
    }

    /// <summary>Python's <c>repr()</c> of a str, for error messages.</summary>
    public static string PythonRepr(string s)
    {
        var quote = s.Contains('\'') && !s.Contains('"') ? '"' : '\'';
        var sb = new StringBuilder(s.Length + 2).Append(quote);
        for (var i = 0; i < s.Length; i++)
        {
            int cp = s[i];
            if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                cp = char.ConvertToUtf32(s[i], s[++i]);

            if (cp == quote || cp == '\\') sb.Append('\\').Append((char)cp);
            else if (cp == '\t') sb.Append("\\t");
            else if (cp == '\n') sb.Append("\\n");
            else if (cp == '\r') sb.Append("\\r");
            else if (cp < 0x20 || cp == 0x7F) sb.Append($"\\x{cp:x2}");
            else if (cp < 0x7F) sb.Append((char)cp);
            else if (IsPythonPrintable(cp)) sb.Append(char.ConvertFromUtf32(cp is >= 0xD800 and <= 0xDFFF ? 0xFFFD : cp));
            else if (cp <= 0xFF) sb.Append($"\\x{cp:x2}");
            else if (cp <= 0xFFFF) sb.Append($"\\u{cp:x4}");
            else sb.Append($"\\U{cp:x8}");
        }
        return sb.Append(quote).ToString();
    }

    /// <summary>Python's str.isprintable() for one non-ASCII code point.</summary>
    private static bool IsPythonPrintable(int cp)
    {
        if (cp is >= 0xD800 and <= 0xDFFF)
            return false; // lone surrogate
        return CharUnicodeInfo.GetUnicodeCategory(cp) switch
        {
            UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.Surrogate or UnicodeCategory.PrivateUse
                or UnicodeCategory.OtherNotAssigned or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator
                or UnicodeCategory.SpaceSeparator => false,
            _ => true,
        };
    }
}