using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ResumeAnalyzer.Domain;

namespace ResumeAnalyzer.Core;

internal static class TextRules
{
    internal const int MaxCharacters = 200_000;
    private static readonly ConcurrentDictionary<string, Regex> Patterns = new(StringComparer.Ordinal);
    internal static Regex Rx(string pattern) => Patterns.GetOrAdd(pattern,
        value => new Regex(value, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)));

    internal static string Input(string? text, string parameter)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Provide non-empty, selectable text before running an analysis.", parameter);
        if (text.Length > MaxCharacters)
            throw new ArgumentException($"Text exceeds the {MaxCharacters:N0}-character offline analysis limit. Use a shorter document.", parameter);
        if (!text.Any(char.IsLetterOrDigit))
            throw new ArgumentException("The document contains no readable letters or numbers. Extract selectable text or run OCR first.", parameter);
        return text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Normalize(NormalizationForm.FormC).Trim();
    }

    internal static string CleanLine(string line) => Rx(@"^\s*(?:[•●▪◦*\-–—]\s*|\d+[.)]\s+)").Replace(line, "").Trim();
    internal static string Key(string value) => Rx(@"[^\p{L}\p{M}\p{N}+#]+").Replace(value.ToLowerInvariant(), " ").Trim();
    internal static string PhrasePattern(string phrase)
    {
        var parts = Rx(@"[\s\-‐‑–]+").Split(phrase.Trim());
        // Symbols belong to a token too: C# is not C, and Java is not JavaScript.
        return @"(?<![\p{L}\p{M}\p{N}_+#])" + string.Join(@"[\s\-‐‑–]+", parts.Select(Regex.Escape))
            + @"(?![\p{L}\p{M}\p{N}_+#])";
    }

    internal static bool Negated(string text, int start, int length)
    {
        var before = text[..start];
        var cut = Math.Max(before.LastIndexOf('\n'), Math.Max(before.LastIndexOf(';'), before.LastIndexOf('.')));
        before = before[(cut + 1)..];
        var contrast = Rx(@"\b(?:but|however|although|yet)\b").Matches(before).LastOrDefault();
        if (contrast is not null) before = before[(contrast.Index + contrast.Length)..];
        before = Rx(@"\bnot\s+only\b").Replace(before, "");
        if (Rx(@"\b(?:no|not|never|without|lack|lacks|lacking|unable)\b(?:[\s,:/]+[\p{L}\p{M}\p{N}'’+\-]+){0,8}[\s,:/]*$").IsMatch(before)) return true;
        var after = text[(start + length)..];
        return Rx(@"^\s*(?:(?:is|are|was|were)\s+)?(?:not\s+(?:required|needed|used|known|proficient)|experience\s*:\s*none|:\s*(?:none|no))\b").IsMatch(after);
    }

    internal static bool Tentative(string text, int start)
    {
        var before = text[..start];
        var cut = Math.Max(before.LastIndexOf(';'), Math.Max(before.LastIndexOf('\n'), before.LastIndexOf('.')));
        before = before[(cut + 1)..];
        return Rx(@"\b(?:basic|beginner|introductory|learning|novice|limited|exposure|familiar|studying|coursework|aspiring)\b(?:[\s,:/]+[\p{L}\p{M}\p{N}'’+\-]+){0,6}[\s,:/]*$").IsMatch(before);
    }

    private static readonly HashSet<string> StopWords = new(
        ("a an the and or of for to in on at by with from as is are was were be been being this that these those " +
         "you your we our they their i my have has had will would should can could must required requires require " +
         "preferred preference desirable desired essential minimum qualification qualifications responsibility responsibilities " +
         "experience experienced ability able knowledge skills skill strong excellent good proven demonstrated demonstrate " +
         "working work relevant related professional candidate candidates successful including such least plus year years " +
         "more than role position seeking looking join team using use needed needs need proficient proficiency expertise").Split(' '), StringComparer.Ordinal);

    internal static HashSet<string> Tokens(string text) => Rx(@"[\p{L}\p{M}\p{N}]+(?:[+#]+)?").Matches(text)
        .Select(match => Stem(match.Value.ToLowerInvariant())).Where(token => token.Length > 1 && !StopWords.Contains(token)
            && !double.TryParse(token, NumberStyles.Any, CultureInfo.InvariantCulture, out _)).ToHashSet(StringComparer.Ordinal);

    private static string Stem(string word)
    {
        if (word.Length > 6 && word.EndsWith("ies", StringComparison.Ordinal)) return word[..^3] + "y";
        if (word.Length > 6 && word.EndsWith("ing", StringComparison.Ordinal))
        {
            var stem = word[..^3];
            return stem.Length > 2 && stem[^1] == stem[^2] ? stem[..^1] : stem;
        }
        if (word.Length > 5 && word.EndsWith("ed", StringComparison.Ordinal)) return word[..^2];
        if (word.Length > 4 && word.EndsWith('s') && !word.EndsWith("ss", StringComparison.Ordinal)) return word[..^1];
        return word;
    }

    internal static double Similarity(string left, string right)
    {
        // Restricted Damerau–Levenshtein, bounded to short skill tokens by the caller.
        var distance = new int[left.Length + 1, right.Length + 1];
        for (var i = 0; i <= left.Length; i++) distance[i, 0] = i;
        for (var j = 0; j <= right.Length; j++) distance[0, j] = j;
        for (var i = 1; i <= left.Length; i++)
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                distance[i, j] = Math.Min(Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1), distance[i - 1, j - 1] + cost);
                if (i > 1 && j > 1 && left[i - 1] == right[j - 2] && left[i - 2] == right[j - 1])
                    distance[i, j] = Math.Min(distance[i, j], distance[i - 2, j - 2] + 1);
            }
        return 1d - (double)distance[left.Length, right.Length] / Math.Max(left.Length, right.Length);
    }
}

internal sealed record TextEvidence(string Text, string Section, bool Partial, int Start, int Length);

internal sealed class SkillFinder
{
    private readonly string _text;
    private readonly List<(int Start, int End, string Section)> _sections = [];
    private readonly Dictionary<string, List<Match>> _tokens;
    private readonly HashSet<string> _knownTokens;

    internal SkillFinder(string text, IReadOnlyList<SkillDefinition> definitions, IReadOnlyList<SectionData>? sections = null)
    {
        _text = text;
        _tokens = TextRules.Rx(@"\b[\p{L}\p{M}]{6,30}\b").Matches(text)
            .GroupBy(match => match.Value.ToLowerInvariant()).ToDictionary(group => group.Key, group => group.ToList());
        _knownTokens = definitions.SelectMany(skill => skill.Aliases.Prepend(skill.Name))
            .Where(name => TextRules.Rx(@"^[\p{L}\p{M}]+$").IsMatch(name)).Select(name => name.ToLowerInvariant()).ToHashSet();
        var position = 0;
        foreach (var section in sections ?? [])
        {
            if (section.Text.Length == 0) continue;
            var index = text.IndexOf(section.Text, position, StringComparison.Ordinal);
            if (index < 0) continue;
            _sections.Add((index, index + section.Text.Length, section.Name));
            position = index + section.Text.Length;
        }
    }

    internal List<TextEvidence> Find(SkillDefinition skill, double fuzzyThreshold = 1)
    {
        var results = new List<TextEvidence>();
        foreach (var alias in skill.Aliases.Prepend(skill.Name).Distinct(StringComparer.OrdinalIgnoreCase))
            foreach (Match match in TextRules.Rx(TextRules.PhrasePattern(alias)).Matches(_text))
            {
                if (TextRules.Negated(_text, match.Index, match.Length)) continue;
                if (results.Any(item => match.Index < item.Start + item.Length && item.Start < match.Index + match.Length)) continue;
                results.Add(Evidence(match, TextRules.Tentative(_text, match.Index)));
            }
        if (results.Any(item => !item.Partial) || fuzzyThreshold >= 1) return results;
        // Fuzzy matching is never used to invent job requirements, match acronyms, or cross two known skills.
        foreach (var alias in skill.Aliases.Prepend(skill.Name).Select(name => name.ToLowerInvariant()))
        {
            if (!TextRules.Rx(@"^[\p{L}\p{M}]{6,30}$").IsMatch(alias)) continue;
            foreach (var (token, occurrences) in _tokens)
            {
                if (_knownTokens.Contains(token) || token[0] != alias[0] || Math.Abs(token.Length - alias.Length) > 1) continue;
                if (TextRules.Similarity(token, alias) < Math.Max(.8, fuzzyThreshold)) continue;
                // At most one insertion/deletion/substitution/transposition, even on very long words.
                if ((1 - TextRules.Similarity(token, alias)) * Math.Max(token.Length, alias.Length) > 1.001) continue;
                foreach (var match in occurrences)
                    if (!TextRules.Negated(_text, match.Index, match.Length)) results.Add(Evidence(match, true));
            }
        }
        return results.DistinctBy(item => (item.Start, item.Length)).ToList();
    }

    private TextEvidence Evidence(Match match, bool partial)
    {
        var start = _text.LastIndexOf('\n', Math.Max(0, match.Index - 1)) + 1;
        var end = _text.IndexOf('\n', match.Index);
        if (end < 0) end = _text.Length;
        var section = _sections.FirstOrDefault(item => match.Index >= item.Start && match.Index < item.End).Section ?? "Unsectioned";
        return new TextEvidence(TextRules.CleanLine(_text[start..end]), section, partial, match.Index, match.Length);
    }
}
