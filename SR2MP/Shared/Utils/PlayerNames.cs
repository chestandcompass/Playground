using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SR2MP.Shared.Utils;

internal static class PlayerNames
{
    internal const int MaxCharacters = 24;
    internal static string Normalize(string? value, int maxCharacters = MaxCharacters)
    {
        var cleaned = new StringBuilder();
        foreach (var rune in (value ?? "").EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.Control or UnicodeCategory.Format || rune.Value == 0xfffd) continue;
            var text = Rune.IsWhiteSpace(rune) ? " " : rune.ToString();
            if (text == " " && (cleaned.Length == 0 || cleaned[cleaned.Length - 1] == ' ')) continue;
            cleaned.Append(text.Replace('<', '＜').Replace('>', '＞'));
            if (cleaned.Length >= 192) break;
        }
        var normalized = cleaned.ToString().Normalize(NormalizationForm.FormC).Trim();
        var elements = StringInfo.ParseCombiningCharacters(normalized);
        if (maxCharacters < 1 || maxCharacters > 80) throw new ArgumentOutOfRangeException(nameof(maxCharacters));
        if (elements.Length > maxCharacters) normalized = normalized.Substring(0, elements[maxCharacters]).TrimEnd();
        return normalized;
    }

    internal static string SanitizeWire(string? value)
    {
        var match = Regex.Match(value ?? "", "^<color=(#[a-fA-F0-9]{6}(?:[a-fA-F0-9]{2})?)>(.*?)</color>$", RegexOptions.Singleline);
        if (!match.Success) return Normalize(value);
        var name = Normalize(match.Groups[2].Value);
        return name.Length == 0 ? "" : "<color=" + match.Groups[1].Value + ">" + name + "</color>";
    }
}
