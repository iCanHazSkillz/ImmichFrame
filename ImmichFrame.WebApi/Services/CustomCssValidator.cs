using System.Text.RegularExpressions;
using ExCSS;

namespace ImmichFrame.WebApi.Services;

public interface ICustomCssValidator
{
    string ValidateAndSanitize(string? css);
}

public sealed class CustomCssValidationException(string message) : Exception(message);

public sealed partial class CustomCssValidator : ICustomCssValidator
{
    private const int MaxCssLength = 20_000;
    private const int MaxRuleCount = 200;
    private const int MaxSelectorCount = 400;
    private const int MaxSelectorLength = 200;
    private const int MaxCombinatorsPerSelector = 6;

    private readonly StylesheetParser _parser = new();

    public string ValidateAndSanitize(string? css)
    {
        var normalized = Normalize(css);
        if (string.IsNullOrEmpty(normalized))
        {
            return string.Empty;
        }

        if (normalized.Length > MaxCssLength)
        {
            throw new CustomCssValidationException($"Custom CSS must be {MaxCssLength} characters or fewer.");
        }

        RejectDangerousConstructs(normalized);

        var stylesheet = _parser.Parse(normalized);
        var sanitized = stylesheet.ToCss().Trim();
        RejectDangerousConstructs(sanitized);
        ValidateComplexity(stylesheet);

        return sanitized;
    }

    private static string Normalize(string? css)
    {
        return (css ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
    }

    private static void RejectDangerousConstructs(string css)
    {
        if (ExpressionPattern().IsMatch(css))
        {
            throw new CustomCssValidationException("Custom CSS cannot use expression() functions.");
        }

        if (ImportPattern().IsMatch(css))
        {
            throw new CustomCssValidationException("Custom CSS cannot use @import rules.");
        }

        if (DataUrlPattern().IsMatch(css))
        {
            throw new CustomCssValidationException("Custom CSS cannot embed data: URLs.");
        }
    }

    private static void ValidateComplexity(Stylesheet stylesheet)
    {
        var styleRules = stylesheet.StyleRules.ToList();
        if (styleRules.Count > MaxRuleCount)
        {
            throw new CustomCssValidationException($"Custom CSS cannot contain more than {MaxRuleCount} style rules.");
        }

        var selectorTexts = styleRules
            .SelectMany(rule => rule.SelectorText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .ToList();

        if (selectorTexts.Count > MaxSelectorCount)
        {
            throw new CustomCssValidationException($"Custom CSS cannot contain more than {MaxSelectorCount} selectors.");
        }

        foreach (var selector in selectorTexts)
        {
            if (selector.Length > MaxSelectorLength)
            {
                throw new CustomCssValidationException($"Custom CSS selectors must be {MaxSelectorLength} characters or fewer.");
            }

            if (ExpensiveSelectorPattern().IsMatch(selector))
            {
                throw new CustomCssValidationException("Custom CSS contains selectors that are too expensive to allow.");
            }

            if (CombinatorPattern().Matches(selector).Count > MaxCombinatorsPerSelector)
            {
                throw new CustomCssValidationException("Custom CSS selectors are too deeply chained.");
            }
        }
    }

    [GeneratedRegex(@"expression\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ExpressionPattern();

    [GeneratedRegex(@"@import\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ImportPattern();

    [GeneratedRegex(@"url\s*\(\s*(['""])?\s*data:", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DataUrlPattern();

    [GeneratedRegex(@":has\s*\(|\[[^\]]*[\*\^\$]=[^\]]*\]", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ExpensiveSelectorPattern();

    [GeneratedRegex(@"\s*[>+~]\s*|\s{2,}|(?<=\S)\s(?=\S)", RegexOptions.Compiled)]
    private static partial Regex CombinatorPattern();
}
