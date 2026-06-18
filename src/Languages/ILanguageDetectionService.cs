namespace Aero.Languages;

public interface ILanguageDetectionService
{
    LanguageInfo Detect(string? filePath);
}
