using System.Text;

namespace RomanCalc.Core;

/// <summary>Converts between integers and standard (subtractive) Roman numerals, range 1..3999.</summary>
public static class RomanNumeral
{
    public const int Min = 1;
    public const int Max = 3999;

    private static readonly (int Value, string Symbol)[] Table =
    {
        (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"),
        (100, "C"), (90, "XC"), (50, "L"), (40, "XL"),
        (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I"),
    };

    public static string ToRoman(int value)
    {
        if (value < Min || value > Max)
            throw new ArgumentOutOfRangeException(nameof(value), value, $"Roman numerals cover {Min}..{Max}.");

        var sb = new StringBuilder();
        foreach (var (v, s) in Table)
        {
            while (value >= v)
            {
                sb.Append(s);
                value -= v;
            }
        }
        return sb.ToString();
    }

    /// <summary>Parses a canonical Roman numeral (case-insensitive). Rejects non-canonical forms like IIII or IC.</summary>
    public static bool TryParse(string? text, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var s = text.Trim().ToUpperInvariant();
        int total = 0, i = 0;
        foreach (var (v, sym) in Table)
        {
            while (i + sym.Length <= s.Length && string.CompareOrdinal(s, i, sym, 0, sym.Length) == 0)
            {
                total += v;
                i += sym.Length;
            }
        }
        if (i != s.Length || total < Min || total > Max) return false;

        // Round-trip check guarantees canonical form (rejects "IIII", "VV", "XM", ...).
        if (ToRoman(total) != s) return false;

        value = total;
        return true;
    }

    public static int Parse(string text) =>
        TryParse(text, out var v) ? v : throw new FormatException($"'{text}' is not a valid Roman numeral ({Min}..{Max}).");
}
