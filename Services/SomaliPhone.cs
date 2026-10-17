using System.Text.RegularExpressions;

namespace VehicleTax.Web.Services;

/// <summary>
/// Vehicle owner mobile numbers: stored as "+252" followed by 7–9 digits.
/// Accepts what people type: "615xxxxxx", "0615xxxxxx", "252615xxxxxx",
/// "+252 61 5xx xxxx".
/// </summary>
public static class SomaliPhone
{
    public const string Prefix = "+252";
    public const int MinDigits = 7;
    public const int MaxDigits = 9;
    public const string Error = "Mobile number must be 7 to 9 digits after +252 (numbers only).";

    /// <summary>"+252XXXXXXXXX", or null when the number is not valid.</summary>
    public static string? Normalize(string? input)
    {
        var s = Regex.Replace(input ?? "", @"[\s\-().]", "");
        if (s.StartsWith("+252")) s = s[4..];
        else if (s.StartsWith("00252")) s = s[5..];
        else if (s.StartsWith("252") && s.Length > MaxDigits) s = s[3..];
        if (s.StartsWith("0")) s = s[1..];
        return Regex.IsMatch(s, $@"^\d{{{MinDigits},{MaxDigits}}}$") ? Prefix + s : null;
    }

    /// <summary>Partly hidden for public pages: +25261•••456.</summary>
    public static string? Mask(string? number)
    {
        if (string.IsNullOrWhiteSpace(number)) return number;
        var n = number.Trim();
        return n.Length <= 7 ? new string('•', n.Length) : n[..Math.Min(6, n.Length - 3)] + "•••" + n[^3..];
    }

    /// <summary>The digits after +252, for the input box.</summary>
    public static string Local(string? stored) =>
        Normalize(stored) is { } n ? n[Prefix.Length..] : Regex.Replace(stored ?? "", @"\D", "");
}
