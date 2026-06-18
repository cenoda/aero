namespace Aero.Languages;

public record LanguageInfo(string Id, string DisplayName)
{
    public static readonly LanguageInfo PlainText = new("plaintext", "Plain Text");
}
