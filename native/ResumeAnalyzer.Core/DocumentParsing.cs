using System.Globalization;
using System.Text.RegularExpressions;
using ResumeAnalyzer.Domain;

namespace ResumeAnalyzer.Core;

internal sealed record JobClause(string Text, string Section, bool Required);
internal sealed record DegreeEvidence(int Rank, string Text, bool InProgress);
internal sealed record ResumeDetails(ResumeProfile Profile, List<DegreeEvidence> Degrees, List<string> DateEvidence);
internal sealed record JobDetails(JobProfile Profile, List<JobClause> Clauses);

internal static class DocumentParsing
{
    private static readonly Dictionary<string, string> ResumeHeadings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["experience"] = "Experience", ["work experience"] = "Experience", ["professional experience"] = "Experience",
        ["employment"] = "Experience", ["employment history"] = "Experience", ["work history"] = "Experience",
        ["relevant experience"] = "Experience", ["career history"] = "Experience", ["internships"] = "Experience",
        ["education"] = "Education", ["academic background"] = "Education", ["qualifications"] = "Education",
        ["educational qualifications"] = "Education", ["academic qualifications"] = "Education",
        ["skills"] = "Skills", ["technical skills"] = "Skills", ["core competencies"] = "Skills",
        ["key skills"] = "Skills", ["professional skills"] = "Skills", ["expertise"] = "Skills",
        ["summary"] = "Summary", ["professional summary"] = "Summary", ["profile"] = "Summary",
        ["professional profile"] = "Summary", ["objective"] = "Summary", ["career objective"] = "Summary",
        ["projects"] = "Projects", ["selected projects"] = "Projects", ["personal projects"] = "Projects",
        ["certifications"] = "Certifications", ["certificates"] = "Certifications", ["licenses"] = "Certifications",
        ["licenses and certifications"] = "Certifications", ["languages"] = "Languages",
        ["awards"] = "Awards", ["publications"] = "Publications", ["volunteering"] = "Volunteering",
        ["volunteer experience"] = "Volunteering", ["interests"] = "Interests", ["references"] = "References"
    };

    internal static ResumeDetails Resume(string text)
    {
        var sections = Sections(text);
        var email = TextRules.Rx(@"[\p{L}\p{N}._%+\-]+@[\p{L}\p{N}.\-]+\.[\p{L}]{2,}").Match(text).Value;
        var phone = TextRules.Rx(@"(?<![\p{L}\p{N}])\+?\d[\d ()\-.]{7,}\d(?![\p{L}\p{N}])").Matches(text)
            .Select(match => match.Value).FirstOrDefault(value => value.Count(char.IsDigit) is >= 10 and <= 15
                && !TextRules.Rx(@"\b(?:19|20)\d{2}\s*[-–]\s*(?:19|20)\d{2}\b").IsMatch(value)) ?? "";
        var first = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim()).FirstOrDefault() ?? "";
        var name = TextRules.Rx(@"^[\p{L}\p{M}][\p{L}\p{M}'’.\-]*(?:\s+[\p{L}\p{M}][\p{L}\p{M}'’.\-]*){1,5}$").IsMatch(first)
            && !ResumeHeadings.ContainsKey(first) ? first : "";
        var bullets = text.Split('\n').Where(line => TextRules.Rx(@"^\s*(?:[•●▪◦*\-–—]\s+|\d+[.)]\s+)").IsMatch(line))
            .Select(TextRules.CleanLine).Where(line => line.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var warnings = new List<string>();
        if (email.Length == 0) warnings.Add("ATS: no readable email address was found.");
        if (phone.Length == 0) warnings.Add("ATS: no readable phone number was found.");
        if (!sections.Any(section => section.Name == "Experience")) warnings.Add("ATS: no standard Experience heading was found; verify work-history parsing.");
        if (!sections.Any(section => section.Name is "Skills" or "Education")) warnings.Add("ATS: use standard Skills and Education headings where applicable.");
        if (text.Contains('\t') || text.Split('\n').Count(line => line.Count(character => character == '|') >= 2) >= 2)
            warnings.Add("ATS: tabs or repeated table separators may indicate columns; check the exported document's reading order.");
        if (text.Contains('\uFFFD')) warnings.Add("ATS: replacement characters indicate an extraction/encoding problem. Check the source document.");
        var degrees = new List<DegreeEvidence>();
        foreach (var section in sections)
            foreach (var raw in section.Text.Split('\n'))
            {
                var line = TextRules.CleanLine(raw);
                var degree = EducationRank(line);
                if (degree == 0 || TextRules.Rx(@"\b(?:no|without|lack(?:ing)?)\s+(?:a\s+)?(?:college\s+)?(?:degree|bachelor|master|associate|doctorate|phd)").IsMatch(line)) continue;
                var inProgress = TextRules.Rx(@"\b(?:pursuing|studying|expected|candidate|in progress|incomplete|planned|enrolled)\b").IsMatch(line);
                degrees.Add(new DegreeEvidence(degree, line, inProgress));
            }
        var (years, dates) = Experience(sections, warnings);
        return new ResumeDetails(new ResumeProfile(name, email, phone, sections, years, bullets, warnings), degrees, dates);
    }

    private static List<SectionData> Sections(string text)
    {
        var sections = new List<SectionData>();
        var name = "Header";
        var lines = new List<string>();
        void Flush()
        {
            var body = string.Join('\n', lines).Trim();
            if (body.Length > 0) sections.Add(new SectionData(name, body));
            lines.Clear();
        }
        foreach (var line in text.Split('\n'))
        {
            var heading = line.Trim().TrimEnd(':').Trim();
            if (ResumeHeadings.TryGetValue(heading, out var normalized))
            {
                Flush(); name = normalized;
            }
            else
            {
                var colon = line.IndexOf(':');
                if (colon > 0 && ResumeHeadings.TryGetValue(line[..colon].Trim(), out normalized))
                {
                    Flush(); name = normalized;
                    lines.Add(line[(colon + 1)..].Trim());
                }
                else lines.Add(line);
            }
        }
        Flush();
        return sections;
    }

    internal static JobDetails Job(string text)
    {
        var clauses = new List<JobClause>();
        var section = "Requirements";
        var required = true;
        var title = "";
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var lines = text.Split('\n').Select(TextRules.CleanLine).Where(line => line.Length > 0).ToList();
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var colon = line.IndexOf(':');
            var heading = (colon >= 0 ? line[..colon] : line).Trim();
            if (JobHeading(heading, out var nextSection, out var nextRequired))
            {
                section = nextSection;
                required = nextRequired;
                if (colon < 0 || colon == line.Length - 1) continue;
                line = line[(colon + 1)..].Trim();
            }
            else if (index == 0 && IsTitle(line))
            {
                title = TextRules.Rx(@"^(?:job title|position|role)\s*:\s*").Replace(line, "");
                continue;
            }
            if (section == "About") continue;
            foreach (var rawClause in SplitClauses(line))
            {
                var clause = rawClause.Trim();
                if (clause.Length == 0 || !seen.Add(TextRules.Key(clause))) continue;
                var preferred = TextRules.Rx(@"\b(?:preferred|desirable|desired|nice[ -]to[ -]have|a plus|bonus|optional|ideally)\b").IsMatch(clause);
                var explicitRequired = TextRules.Rx(@"\b(?:required|must|mandatory|essential|minimum|need to)\b").IsMatch(clause);
                clauses.Add(new JobClause(clause, section, explicitRequired || (!preferred && required)));
            }
        }
        var years = clauses.Where(clause => clause.Required).Select(clause => YearsRequired(clause.Text)).DefaultIfEmpty().Max();
        return new JobDetails(new JobProfile(title, years,
            clauses.Where(clause => clause.Section == "Responsibilities").Select(clause => clause.Text).ToList(),
            clauses.Where(clause => clause.Section != "Responsibilities").Select(clause => clause.Text).ToList()), clauses);
    }

    private static IEnumerable<string> SplitClauses(string line) =>
        // Keep clauses verbatim, including punctuation. Do not split .NET, decimals, or common degree abbreviations.
        TextRules.Rx(@"(?<=[;!?])\s+|(?<=\.)\s+(?=[A-Z][a-z])|\s+(?:and|but)\s+(?=(?:a\s+)?(?:master|bachelor|doctorate|ph\.?d).{0,60}\bpreferred\b)").Split(line);

    private static bool IsTitle(string line) => line.Length <= 120 &&
        (TextRules.Rx(@"^(?:job title|position|role)\s*:").IsMatch(line)
         || (line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 12
             && TextRules.Rx(@"\b(?:engineer|developer|analyst|manager|specialist|designer|nurse|teacher|accountant|coordinator|director|assistant|consultant|technician|scientist|administrator|representative|supervisor|officer|chef|driver|counselor|counsellor|writer)\b").IsMatch(line)
             && !TextRules.Rx(@"\b(?:required|preferred|must|need|years|experience|will|responsible)\b").IsMatch(line)));

    private static bool JobHeading(string text, out string section, out bool required)
    {
        text = TextRules.Key(text);
        section = "Requirements"; required = true;
        if (text is "responsibilities" or "key responsibilities" or "duties" or "job duties" or "what you will do" or "what you ll do" or "the role" or "role responsibilities")
        { section = "Responsibilities"; return true; }
        if (text is "preferred" or "preferred qualifications" or "preferred skills" or "nice to have" or "desirable" or "desirable qualifications" or "bonus skills" or "optional")
        { section = "Preferred qualifications"; required = false; return true; }
        if (text is "requirements" or "required" or "required skills" or "qualifications" or "required qualifications" or "minimum qualifications" or "essential skills" or "what you bring" or "what we re looking for" or "must have" or "skills and experience" or "education")
        { section = "Qualifications"; return true; }
        if (text is "about us" or "about the company" or "benefits" or "what we offer" or "compensation" or "equal opportunity" or "how to apply")
        { section = "About"; required = false; return true; }
        return false;
    }

    internal static double YearsRequired(string text)
    {
        if (TextRules.Rx(@"\b(?:no|without)\s+(?:prior\s+)?experience\s+(?:is\s+)?(?:required|needed)\b").IsMatch(text)) return 0;
        var values = new List<double>();
        foreach (Match match in TextRules.Rx(@"(?<![\p{L}\p{N}])(?<years>\d{1,2}(?:\.\d+)?|one|two|three|four|five|six|seven|eight|nine|ten)\s*(?:[-–]\s*\d{1,2}\s*)?\+?\s*(?:years?|yrs?)\b").Matches(text))
        {
            if (TextRules.Negated(text, match.Index, match.Length)) continue;
            var value = match.Groups["years"].Value.ToLowerInvariant();
            var words = new[] { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten" };
            var number = double.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : Array.IndexOf(words, value);
            if (number > 0 && number <= 60) values.Add(number);
        }
        return values.DefaultIfEmpty().Max();
    }

    internal static int EducationRank(string text, bool requirement = false)
    {
        var levels = new List<int>();
        if (TextRules.Rx(@"\b(?:ph\.?d\.?|doctorate|doctoral|doctor of)\b").IsMatch(text)) levels.Add(5);
        if (TextRules.Rx(@"\b(?:master['’]?s(?:\s+degree)?|master of|m\.?sc\.?|m\.?s\.?|m\.?a\.?|mba|m\.?eng\.?)\b").IsMatch(text)) levels.Add(4);
        if (TextRules.Rx(@"\b(?:bachelor['’]?s(?:\s+degree)?|bachelor of|b\.?sc\.?|b\.?s\.?|b\.?a\.?|b\.?eng\.?)\b").IsMatch(text)) levels.Add(3);
        if (TextRules.Rx(@"\b(?:associate['’]?s(?:\s+degree)?|associate (?:of|degree)|a\.?a\.?s\.?)\b").IsMatch(text)) levels.Add(2);
        if (TextRules.Rx(@"\b(?:high school|secondary school|ged|a[ -]levels?)\b").IsMatch(text)) levels.Add(1);
        if (levels.Count == 0) return 0;
        return requirement && TextRules.Rx(@"\bor\b").IsMatch(text) ? levels.Min() : levels.Max();
    }

    internal static string DegreeName(int rank) => rank switch
    {
        5 => "doctorate", 4 => "master's degree", 3 => "bachelor's degree", 2 => "associate degree", 1 => "secondary education", _ => "no recognized degree"
    };

    private const string Month = @"(?:Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|Jun(?:e)?|Jul(?:y)?|Aug(?:ust)?|Sep(?:t(?:ember)?)?|Oct(?:ober)?|Nov(?:ember)?|Dec(?:ember)?)";
    private const string Year = @"(?:19|20)\d{2}";
    private const string DateToken = @"(?:" + Month + @"\.?\s+" + Year + @"|(?:0?[1-9]|1[0-2])[/\-]" + Year + @"|" + Year + @"[/\-](?:0?[1-9]|1[0-2])(?!\d)|" + Year + @")";
    private static readonly Regex DateRange = TextRules.Rx(@"(?<!\d)(?<start>" + DateToken + @")\s*(?:[-–—]|to|until|through)\s*(?<end>" + DateToken + @"|present|current|now|ongoing)(?![\p{L}\p{N}])");

    private static (double Years, List<string> Evidence) Experience(List<SectionData> sections, List<string> warnings)
    {
        var spans = new List<(DateTime Start, DateTime End)>();
        var evidence = new List<string>();
        var explicitExperience = sections.Any(section => section.Name == "Experience");
        var excluded = new HashSet<string> { "Education", "Certifications", "Projects", "Skills", "Languages", "Awards", "Publications", "Interests", "References", "Volunteering" };
        var today = DateTime.Today;
        var currentEnd = new DateTime(today.Year, today.Month, 1).AddMonths(1);
        foreach (var section in sections)
        {
            if (excluded.Contains(section.Name) || (explicitExperience && section.Name != "Experience")) continue;
            foreach (var line in section.Text.Split('\n'))
            {
                if (EducationRank(line) > 0 || TextRules.Rx(@"\b(?:graduated|graduation|university|college|high school|studied|education)\b").IsMatch(line)) continue;
                foreach (Match match in DateRange.Matches(line))
                {
                    if (TextRules.Negated(line, match.Index, match.Length)) continue;
                    var start = ParseDate(match.Groups["start"].Value, false);
                    var end = ParseDate(match.Groups["end"].Value, true);
                    if (start is null || end is null || start >= end || start >= currentEnd || (end.Value.Year - start.Value.Year) > 70)
                    {
                        warnings.Add($"Work-history date range could not be used: {match.Value}. Check its dates.");
                        continue;
                    }
                    if (end > currentEnd)
                    {
                        // Year-only current dates may end in December; do not claim future experience.
                        end = currentEnd;
                    }
                    spans.Add((start.Value, end.Value));
                    evidence.Add(TextRules.CleanLine(line));
                }
            }
        }
        if (spans.Count == 0)
        {
            warnings.Add("No usable employment date ranges were found; experience years are unverified, not assumed from a summary claim.");
            return (0, evidence);
        }
        var months = 0;
        var ordered = spans.OrderBy(span => span.Start).ThenBy(span => span.End).ToList();
        var merged = ordered[0];
        foreach (var span in ordered.Skip(1))
        {
            if (span.Start <= merged.End) merged.End = span.End > merged.End ? span.End : merged.End;
            else
            {
                months += MonthCount(merged.Start, merged.End);
                merged = span;
            }
        }
        months += MonthCount(merged.Start, merged.End);
        if (months < spans.Sum(span => MonthCount(span.Start, span.End)))
            warnings.Add("Overlapping employment ranges were merged; concurrent roles are not counted twice.");
        if (!explicitExperience) warnings.Add("Experience dates were inferred outside a standard Experience section. Confirm they describe employment.");
        return (Math.Round(months / 12d, 2), evidence.Distinct(StringComparer.Ordinal).ToList());
    }

    private static int MonthCount(DateTime start, DateTime end) => (end.Year - start.Year) * 12 + end.Month - start.Month;

    private static DateTime? ParseDate(string token, bool end)
    {
        if (TextRules.Rx(@"^(?:present|current|now|ongoing)$").IsMatch(token))
        {
            var today = DateTime.Today;
            return new DateTime(today.Year, today.Month, 1).AddMonths(1);
        }
        token = token.Replace(".", "", StringComparison.Ordinal);
        if (int.TryParse(token, out var year) && year is >= 1900 and <= 2099)
            return end ? new DateTime(year + 1, 1, 1) : new DateTime(year, 1, 1);
        token = TextRules.Rx(@"\bSept\b").Replace(token, "Sep");
        var formats = new[] { "MMM yyyy", "MMMM yyyy", "M/yyyy", "MM/yyyy", "M-yyyy", "MM-yyyy", "yyyy-M", "yyyy-MM", "yyyy/M", "yyyy/MM" };
        if (!DateTime.TryParseExact(token, formats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed)) return null;
        return end ? parsed.AddMonths(1) : parsed;
    }
}
