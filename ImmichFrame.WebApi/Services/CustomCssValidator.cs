using System.Text;
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
        ValidateSyntax(normalized);

        var stylesheet = _parser.Parse(normalized);
        ValidateComplexity(stylesheet);

        return normalized;
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

    private static void ValidateSyntax(string css)
    {
        var blockStack = new Stack<BlockContext>();
        var segmentStart = 0;
        var parenthesesDepth = 0;
        var bracketDepth = 0;
        var inComment = false;
        var inSingleQuotedString = false;
        var inDoubleQuotedString = false;
        var isEscaped = false;

        for (var i = 0; i < css.Length; i++)
        {
            var current = css[i];
            if (inComment)
            {
                if (current == '*' && i + 1 < css.Length && css[i + 1] == '/')
                {
                    inComment = false;
                    i++;
                }

                continue;
            }

            if (inSingleQuotedString)
            {
                if (isEscaped)
                {
                    isEscaped = false;
                    continue;
                }

                if (current == '\\')
                {
                    isEscaped = true;
                    continue;
                }

                if (current == '\'')
                {
                    inSingleQuotedString = false;
                }

                continue;
            }

            if (inDoubleQuotedString)
            {
                if (isEscaped)
                {
                    isEscaped = false;
                    continue;
                }

                if (current == '\\')
                {
                    isEscaped = true;
                    continue;
                }

                if (current == '"')
                {
                    inDoubleQuotedString = false;
                }

                continue;
            }

            if (current == '/' && i + 1 < css.Length && css[i + 1] == '*')
            {
                inComment = true;
                i++;
                continue;
            }

            switch (current)
            {
                case '\'':
                    inSingleQuotedString = true;
                    break;
                case '"':
                    inDoubleQuotedString = true;
                    break;
                case '(':
                    parenthesesDepth++;
                    break;
                case ')':
                    if (parenthesesDepth == 0)
                    {
                        throw InvalidSyntax();
                    }

                    parenthesesDepth--;
                    break;
                case '[':
                    bracketDepth++;
                    break;
                case ']':
                    if (bracketDepth == 0)
                    {
                        throw InvalidSyntax();
                    }

                    bracketDepth--;
                    break;
                case '{' when parenthesesDepth == 0 && bracketDepth == 0:
                    var prelude = css.Substring(segmentStart, i - segmentStart).Trim();
                    blockStack.Push(new BlockContext(i + 1, IsDeclarationBlock(prelude)));
                    segmentStart = i + 1;
                    break;
                case '}' when parenthesesDepth == 0 && bracketDepth == 0:
                    if (blockStack.Count == 0)
                    {
                        throw InvalidSyntax();
                    }

                    var blockContext = blockStack.Pop();
                    if (blockContext.IsDeclarationBlock)
                    {
                        ValidateDeclarationBlock(css.Substring(blockContext.ContentStart, i - blockContext.ContentStart));
                    }

                    segmentStart = i + 1;
                    break;
            }
        }

        if (inComment || inSingleQuotedString || inDoubleQuotedString || parenthesesDepth != 0 || bracketDepth != 0 || blockStack.Count != 0)
        {
            throw InvalidSyntax();
        }
    }

    private static bool IsDeclarationBlock(string prelude)
    {
        if (string.IsNullOrWhiteSpace(prelude))
        {
            return false;
        }

        if (!prelude.StartsWith('@'))
        {
            return true;
        }

        return prelude.StartsWith("@font-face", StringComparison.OrdinalIgnoreCase)
            || prelude.StartsWith("@page", StringComparison.OrdinalIgnoreCase)
            || prelude.StartsWith("@counter-style", StringComparison.OrdinalIgnoreCase)
            || prelude.StartsWith("@property", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateDeclarationBlock(string blockContent)
    {
        foreach (var declaration in SplitTopLevelStatements(blockContent))
        {
            var trimmed = declaration.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            var colonIndex = IndexOfTopLevelColon(trimmed);
            if (colonIndex <= 0 || colonIndex == trimmed.Length - 1)
            {
                throw InvalidSyntax();
            }

            var propertyName = trimmed[..colonIndex].Trim();
            var value = trimmed[(colonIndex + 1)..].Trim();
            if (propertyName.Length == 0 || value.Length == 0 || !PropertyNamePattern().IsMatch(propertyName))
            {
                throw InvalidSyntax();
            }
        }
    }

    private static List<string> SplitTopLevelStatements(string blockContent)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        var parenthesesDepth = 0;
        var bracketDepth = 0;
        var inComment = false;
        var inSingleQuotedString = false;
        var inDoubleQuotedString = false;
        var isEscaped = false;

        for (var i = 0; i < blockContent.Length; i++)
        {
            var character = blockContent[i];

            if (inComment)
            {
                current.Append(character);
                if (character == '*' && i + 1 < blockContent.Length && blockContent[i + 1] == '/')
                {
                    current.Append(blockContent[i + 1]);
                    inComment = false;
                    i++;
                }

                continue;
            }

            if (inSingleQuotedString)
            {
                current.Append(character);
                if (isEscaped)
                {
                    isEscaped = false;
                    continue;
                }

                if (character == '\\')
                {
                    isEscaped = true;
                    continue;
                }

                if (character == '\'')
                {
                    inSingleQuotedString = false;
                }

                continue;
            }

            if (inDoubleQuotedString)
            {
                current.Append(character);
                if (isEscaped)
                {
                    isEscaped = false;
                    continue;
                }

                if (character == '\\')
                {
                    isEscaped = true;
                    continue;
                }

                if (character == '"')
                {
                    inDoubleQuotedString = false;
                }

                continue;
            }

            if (character == '/' && i + 1 < blockContent.Length && blockContent[i + 1] == '*')
            {
                current.Append(character);
                current.Append(blockContent[i + 1]);
                inComment = true;
                i++;
                continue;
            }

            switch (character)
            {
                case '\'':
                    current.Append(character);
                    inSingleQuotedString = true;
                    break;
                case '"':
                    current.Append(character);
                    inDoubleQuotedString = true;
                    break;
                case '(':
                    current.Append(character);
                    parenthesesDepth++;
                    break;
                case ')':
                    if (parenthesesDepth == 0)
                    {
                        throw InvalidSyntax();
                    }

                    current.Append(character);
                    parenthesesDepth--;
                    break;
                case '[':
                    current.Append(character);
                    bracketDepth++;
                    break;
                case ']':
                    if (bracketDepth == 0)
                    {
                        throw InvalidSyntax();
                    }

                    current.Append(character);
                    bracketDepth--;
                    break;
                case '{':
                case '}':
                    throw InvalidSyntax();
                case ';' when parenthesesDepth == 0 && bracketDepth == 0:
                    parts.Add(current.ToString());
                    current.Clear();
                    break;
                default:
                    current.Append(character);
                    break;
            }
        }

        if (inComment || inSingleQuotedString || inDoubleQuotedString || parenthesesDepth != 0 || bracketDepth != 0)
        {
            throw InvalidSyntax();
        }

        if (current.Length > 0)
        {
            parts.Add(current.ToString());
        }

        return parts;
    }

    private static int IndexOfTopLevelColon(string declaration)
    {
        var parenthesesDepth = 0;
        var bracketDepth = 0;
        var inComment = false;
        var inSingleQuotedString = false;
        var inDoubleQuotedString = false;
        var isEscaped = false;

        for (var i = 0; i < declaration.Length; i++)
        {
            var character = declaration[i];

            if (inComment)
            {
                if (character == '*' && i + 1 < declaration.Length && declaration[i + 1] == '/')
                {
                    inComment = false;
                    i++;
                }

                continue;
            }

            if (inSingleQuotedString)
            {
                if (isEscaped)
                {
                    isEscaped = false;
                    continue;
                }

                if (character == '\\')
                {
                    isEscaped = true;
                    continue;
                }

                if (character == '\'')
                {
                    inSingleQuotedString = false;
                }

                continue;
            }

            if (inDoubleQuotedString)
            {
                if (isEscaped)
                {
                    isEscaped = false;
                    continue;
                }

                if (character == '\\')
                {
                    isEscaped = true;
                    continue;
                }

                if (character == '"')
                {
                    inDoubleQuotedString = false;
                }

                continue;
            }

            if (character == '/' && i + 1 < declaration.Length && declaration[i + 1] == '*')
            {
                inComment = true;
                i++;
                continue;
            }

            switch (character)
            {
                case '\'':
                    inSingleQuotedString = true;
                    break;
                case '"':
                    inDoubleQuotedString = true;
                    break;
                case '(':
                    parenthesesDepth++;
                    break;
                case ')':
                    if (parenthesesDepth == 0)
                    {
                        throw InvalidSyntax();
                    }

                    parenthesesDepth--;
                    break;
                case '[':
                    bracketDepth++;
                    break;
                case ']':
                    if (bracketDepth == 0)
                    {
                        throw InvalidSyntax();
                    }

                    bracketDepth--;
                    break;
                case ':':
                    if (parenthesesDepth == 0 && bracketDepth == 0)
                    {
                        return i;
                    }

                    break;
            }
        }

        if (inComment || inSingleQuotedString || inDoubleQuotedString || parenthesesDepth != 0 || bracketDepth != 0)
        {
            throw InvalidSyntax();
        }

        return -1;
    }

    private static CustomCssValidationException InvalidSyntax()
    {
        return new CustomCssValidationException("Custom CSS contains invalid syntax.");
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

    [GeneratedRegex(@"^(--[A-Za-z0-9_-]+|-?[A-Za-z_][A-Za-z0-9_-]*)$", RegexOptions.Compiled)]
    private static partial Regex PropertyNamePattern();

    private readonly record struct BlockContext(int ContentStart, bool IsDeclarationBlock);
}
