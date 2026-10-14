using System.Globalization;
using System.Text.RegularExpressions;
using ResumeAnalyzer.Domain;

namespace ResumeAnalyzer.Core;

/// <summary>Deterministic, local-only text analysis. Scores describe visible evidence, not a hiring decision.</summary>
public sealed class OfflineAnalyzer : IAnalysisService
{
    private readonly IReadOnlyList<SkillDefinition> _definitions;

    public OfflineAnalyzer(IKnowledgeBase? kb = null)
    {
        _definitions = (kb ?? new JsonKnowledgeBase()).Skills;
        if (_definitions is null) throw new ArgumentException("The knowledge base must provide a skills list.", nameof(kb));
    }

    public ResumeProfile ParseResume(string text) => DocumentParsing.Resume(TextRules.Input(text, nameof(text))).Profile;
    public JobProfile ParseJob(string text) => DocumentParsing.Job(TextRules.Input(text, nameof(text))).Profile;

    public AnalysisResult Analyze(string resume, string job, AnalysisOptions? options = null, IProgress<string>? progress = null)
    {
        progress?.Report("Validating local documents");
        resume = TextRules.Input(resume, nameof(resume));
        job = TextRules.Input(job, nameof(job));
        options ??= new AnalysisOptions();
        ValidateOptions(options);
        progress?.Report("Parsing resume sections, contacts, education and employment dates");
        var parsedResume = DocumentParsing.Resume(resume);
        progress?.Report("Separating job responsibilities, required and preferred qualifications");
        var parsedJob = DocumentParsing.Job(job);
        var resumeFinder = new SkillFinder(resume, _definitions, parsedResume.Profile.Sections);
        var jobFinder = new SkillFinder(string.Join('\n', parsedJob.Clauses.Select(clause => clause.Text)), _definitions);
        progress?.Report("Matching local skill aliases and checking actual resume evidence");
        var skills = new List<SkillMatch>();
        var clauseSkills = parsedJob.Clauses.ToDictionary(clause => clause, _ => new List<SkillMatch>());
        foreach (var definition in _definitions)
        {
            var jobEvidence = jobFinder.Find(definition);
            if (jobEvidence.Count == 0) continue;
            var clauses = parsedJob.Clauses.Where(clause => jobEvidence.Any(item => item.Text == TextRules.CleanLine(clause.Text))).ToList();
            var evidence = resumeFinder.Find(definition, options.FuzzySensitivity);
            var solid = evidence.Where(item => !item.Partial).ToList();
            var status = solid.Count > 0 ? "Matched" : evidence.Count > 0 ? "Partial" : "Missing";
            var best = (solid.Count > 0 ? solid : evidence).FirstOrDefault();
            var skill = new SkillMatch(definition.Name, status, clauses.Any(clause => clause.Required), best?.Text ?? "",
                evidence.Count, string.Join(", ", evidence.Select(item => item.Section).Distinct(StringComparer.Ordinal)));
            skills.Add(skill);
            foreach (var clause in clauses) clauseSkills[clause].Add(skill);
        }
        skills = skills.OrderByDescending(skill => skill.Required).ThenBy(skill => skill.Skill, StringComparer.OrdinalIgnoreCase).ToList();
        progress?.Report("Comparing job clauses and calculating transparent weighted metrics");
        var comparisons = new List<ComparisonEntry>();
        var clauseOutcomes = new List<(JobClause Clause, Outcome Result)>();
        foreach (var clause in parsedJob.Clauses)
        {
            if (IsNonRequirement(clause.Text)) continue;
            var outcome = Compare(clause, clauseSkills[clause], parsedResume, options);
            comparisons.Add(new ComparisonEntry(clause.Section, clause.Text, outcome.Evidence, outcome.Status, clause.Required));
            clauseOutcomes.Add((clause, outcome));
        }
        var metrics = Metrics(skills, clauseOutcomes, parsedResume, options);
        var score = (int)Math.Round(metrics.Sum(metric => metric.Score * metric.Weight / 100), MidpointRounding.AwayFromZero);
        progress?.Report("Preparing evidence-based recommendations and ATS text checks");
        var recommendations = Recommendations(skills, clauseOutcomes, parsedResume);
        var warnings = new List<string>(parsedResume.Profile.Warnings)
        {
            "Offline heuristic: visible wording is not verification of competence or a hiring prediction. Skill counts never increase a match score.",
            "ATS checks cover extracted text only; original columns, fonts, images and file-format compatibility cannot be verified here."
        };
        if (skills.Count == 0) warnings.Add("No taxonomy skills were found in the job requirements; clause evidence is used instead. Extend the local JSON taxonomy for specialist terms.");
        if (comparisons.Count == 0) warnings.Add("No actionable job clauses were found. The score reflects only available resume-text checks and is not a job-fit assessment.");
        if (skills.Any(skill => skill.Status == "Partial")) warnings.Add("Partial skill matches may be tentative wording or a one-edit typo. Confirm them manually; they are not exact matches.");
        if (parsedJob.Clauses.Any(clause => DocumentParsing.EducationRank(clause.Text, true) > 0
            && TextRules.Rx(@"\bor\s+(?:equivalent|comparable)\s+(?:practical\s+|professional\s+)?experience\b").IsMatch(clause.Text)))
            warnings.Add("An education-or-equivalent-experience alternative needs human review; this analyzer does not automatically equate work tenure with a degree.");
        var strengths = new List<string>();
        var matched = skills.Where(skill => skill.Status == "Matched").ToList();
        if (matched.Count > 0) strengths.Add($"Exact or explicitly defined alias evidence appears for {matched.Count} of {skills.Count} unique job skills: {string.Join(", ", matched.Take(8).Select(skill => skill.Skill))}.");
        if (parsedResume.DateEvidence.Count > 0) strengths.Add($"Employment date ranges show {parsedResume.Profile.ExperienceYears.ToString("0.##", CultureInfo.InvariantCulture)} non-overlapping calendar years; this does not establish years in each specific skill.");
        if (comparisons.Any(comparison => comparison.Status == "Matched")) strengths.Add($"Visible evidence supports {comparisons.Count(comparison => comparison.Status == "Matched")} of {comparisons.Count} distinct job clauses.");
        if (parsedResume.Profile.Email.Length > 0 && parsedResume.Profile.Phone.Length > 0) strengths.Add("Readable email and phone contact details are present.");
        progress?.Report("Offline analysis complete");
        return new AnalysisResult(score, metrics, skills, recommendations, comparisons, parsedResume.Profile, parsedJob.Profile,
            strengths, warnings.Distinct(StringComparer.Ordinal).ToList(), DateTimeOffset.UtcNow);
    }

    private static void ValidateOptions(AnalysisOptions options)
    {
        if (!double.IsFinite(options.Strictness) || options.Strictness is <= 0 or > 3)
            throw new ArgumentException("Strictness must be finite and greater than 0, up to 3.", nameof(options));
        if (!double.IsFinite(options.KeywordSensitivity) || options.KeywordSensitivity is <= 0 or > 3)
            throw new ArgumentException("KeywordSensitivity must be finite and greater than 0, up to 3.", nameof(options));
        if (!double.IsFinite(options.FuzzySensitivity) || options.FuzzySensitivity is < 0 or > 1)
            throw new ArgumentException("FuzzySensitivity must be a similarity threshold between 0 and 1; 1 disables fuzzy matches.", nameof(options));
    }

    private sealed record Outcome(string Status, string Evidence, double Credit);
    private static Outcome Result(double credit, string evidence) => new(credit >= .999 ? "Matched" : credit > 0 ? "Partial" : "Missing", evidence, credit);

    private Outcome Compare(JobClause clause, List<SkillMatch> skills, ResumeDetails resume, AnalysisOptions options)
    {
        var components = new List<Outcome>();
        var education = DocumentParsing.EducationRank(clause.Text, true);
        var years = DocumentParsing.YearsRequired(clause.Text);
        if (education > 0) components.Add(DegreeOutcome(education, resume.Degrees));
        if (years > 0) components.Add(ExperienceOutcome(years, resume));
        if (skills.Count > 0)
        {
            var credit = skills.Average(skill => StatusCredit(skill.Status));
            components.Add(Result(credit, string.Join("\n", skills.Select(skill => skill.Evidence).Where(text => text.Length > 0).Distinct(StringComparer.Ordinal))));
        }
        // A technology name alone does not prove that an applicant carried out a responsibility.
        if (clause.Section == "Responsibilities" || components.Count == 0 || (education == 0 && years == 0 && NeedsPhraseEvidence(clause.Text, skills)))
            components.Add(ClauseEvidence(clause.Text, resume.Profile.Sections, options.KeywordSensitivity));
        if (components.Count == 0) return Result(0, "");
        var evidence = string.Join("\n", components.SelectMany(component => component.Evidence.Split('\n')).Where(text => text.Length > 0).Distinct(StringComparer.Ordinal));
        var overall = components.Average(component => component.Credit);
        // Any unfulfilled component prevents a fully matched clause. Evidence remains verbatim.
        return Result(overall, evidence);
    }

    private bool NeedsPhraseEvidence(string clause, List<SkillMatch> skills)
    {
        if (skills.Count == 0) return true;
        var remainder = clause;
        var names = skills.Select(skill => skill.Skill).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var phrases = _definitions.Where(definition => names.Contains(definition.Name))
            .SelectMany(definition => definition.Aliases.Prepend(definition.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(phrase => phrase.Length);
        foreach (var phrase in phrases) remainder = TextRules.Rx(TextRules.PhrasePattern(phrase)).Replace(remainder, " ");
        var tokens = TextRules.Tokens(remainder);
        // Defined aliases and short skill-only qualifications should be handled by the taxonomy, not lexical overlap.
        return tokens.Count >= 3;
    }

    private static Outcome DegreeOutcome(int requiredRank, List<DegreeEvidence> degrees)
    {
        var completed = degrees.Where(degree => !degree.InProgress).OrderByDescending(degree => degree.Rank).FirstOrDefault();
        if (completed is not null && completed.Rank >= requiredRank) return Result(1, completed.Text);
        var pending = degrees.Where(degree => degree.InProgress && degree.Rank >= requiredRank).OrderByDescending(degree => degree.Rank).FirstOrDefault();
        if (pending is not null) return Result(.5, pending.Text);
        // A lower degree is contextual evidence, not partial satisfaction of a higher minimum degree.
        return Result(0, completed?.Text ?? "");
    }

    private static Outcome ExperienceOutcome(double years, ResumeDetails resume)
    {
        var credit = Math.Min(1, resume.Profile.ExperienceYears / years);
        return Result(credit, string.Join("\n", resume.DateEvidence));
    }

    private static Outcome ClauseEvidence(string requirement, List<SectionData> sections, double sensitivity)
    {
        var target = TextRules.Tokens(requirement);
        if (target.Count == 0) return Result(0, "");
        var best = 0d;
        var evidence = "";
        var fullThreshold = Math.Clamp(.8 + (sensitivity - 1) * .1, .7, .95);
        var partialThreshold = Math.Clamp(.45 + (sensitivity - 1) * .1, .35, .7);
        foreach (var section in sections)
            foreach (var raw in section.Text.Split('\n'))
            {
                var line = TextRules.CleanLine(raw);
                if (line.Length == 0) continue;
                var tokens = TextRules.Tokens(line);
                var shared = target.Intersect(tokens).ToList();
                if (shared.Count == 0) continue;
                // Do not turn a denial into evidence, even if it contains all the right keywords.
                var denial = TextRules.Rx(@"\b(?:no|not|never|without|lack(?:s|ing)?)\b").Matches(line)
                    .Any(match => !TextRules.Rx(@"^not\s+only\b").IsMatch(line[match.Index..]));
                if (denial) continue;
                var coverage = (double)shared.Count / target.Count;
                var minimumShared = target.Count == 1 ? 1 : 2;
                if (shared.Count < minimumShared || coverage < partialThreshold || coverage <= best) continue;
                best = coverage;
                evidence = line;
            }
        if (best == 0) return Result(0, "");
        var tentative = TextRules.Rx(@"\b(?:learning|beginner|basic|familiar|exposure|studying|aspiring)\b").IsMatch(evidence);
        return Result(best >= fullThreshold && !tentative ? 1 : .5, evidence);
    }

    private static bool IsNonRequirement(string text) => TextRules.Rx(@"^\s*(?:no\s+(?:prior\s+)?experience\s+(?:is\s+)?(?:required|needed)|(?:we are|we re)\s+an?\s+equal\s+opportunity|equal opportunity employer|salary\s*:|apply\s+(?:at|by|to))\b").IsMatch(text)
        || TextRules.Rx(@"^(?:no|not)\s+.{0,80}\b(?:required|needed)\.?$").IsMatch(text)
        || TextRules.Rx(@"^.{1,60}\s+(?:is\s+)?not\s+(?:required|needed)\.?$").IsMatch(text);

    private static double StatusCredit(string status) => status switch { "Matched" => 1, "Partial" => .5, _ => 0 };

    private static List<Metric> Metrics(List<SkillMatch> skills, List<(JobClause Clause, Outcome Result)> clauses, ResumeDetails resume, AnalysisOptions options)
    {
        var definitions = new List<(string Name, double Weight, double Credit, string Explanation)>();
        if (skills.Count > 0)
        {
            var total = skills.Sum(skill => skill.Required ? 2d : 1d);
            var credit = skills.Sum(skill => StatusCredit(skill.Status) * (skill.Required ? 2 : 1)) / total;
            definitions.Add(("Skills", 35, credit,
                $"{skills.Count(skill => skill.Status == "Matched")} matched, {skills.Count(skill => skill.Status == "Partial")} partial, {skills.Count(skill => skill.Status == "Missing")} missing unique skills. Required skills weight 2; preferred weight 1. Exact/alias = 1, tentative/fuzzy = 0.5, absent = 0. Frequency has no scoring bonus."));
        }
        else definitions.Add(("Skills", 0, 0, "Not applicable: no recognized job skills. Its weight is redistributed to available metrics."));
        if (clauses.Count > 0)
        {
            var weight = clauses.Sum(item => item.Clause.Required ? 2d : 1d);
            definitions.Add(("Requirement evidence", 30, clauses.Sum(item => item.Result.Credit * (item.Clause.Required ? 2 : 1)) / weight,
                $"{clauses.Count} distinct job clauses compared with actual resume text. Required clauses weight 2; preferred weight 1. Exact/alias and strong token coverage receive full credit; partial evidence receives partial credit. Duplicate clauses are counted once. Keyword sensitivity: {options.KeywordSensitivity:0.##}."));
        }
        else definitions.Add(("Requirement evidence", 0, 0, "Not applicable: no actionable job clauses were identified."));
        var experienceClauses = clauses.Where(item => DocumentParsing.YearsRequired(item.Clause.Text) > 0).ToList();
        if (experienceClauses.Count > 0)
        {
            var weight = experienceClauses.Sum(item => item.Clause.Required ? 2d : 1d);
            var credit = experienceClauses.Sum(item => ExperienceOutcome(DocumentParsing.YearsRequired(item.Clause.Text), resume).Credit * (item.Clause.Required ? 2 : 1)) / weight;
            definitions.Add(("Experience", 15, credit,
                $"{resume.Profile.ExperienceYears:0.##} calendar years from merged month-level employment ranges. Inclusive end months; overlaps merged; education dates excluded. Credit is min(documented years / requested years, 1). This is total tenure, not verified skill-specific tenure."));
        }
        else definitions.Add(("Experience", 0, 0, "Not applicable: no explicit year requirement. Its weight is redistributed."));
        var educationClauses = clauses.Where(item => DocumentParsing.EducationRank(item.Clause.Text, true) > 0).ToList();
        if (educationClauses.Count > 0)
        {
            var weight = educationClauses.Sum(item => item.Clause.Required ? 2d : 1d);
            var credit = educationClauses.Sum(item => DegreeOutcome(DocumentParsing.EducationRank(item.Clause.Text, true), resume.Degrees).Credit * (item.Clause.Required ? 2 : 1)) / weight;
            definitions.Add(("Education", 10, credit,
                "Recognized completed degree must meet the requested level: secondary < associate < bachelor < master < doctorate. Lower levels receive no credit; an explicitly in-progress qualifying degree receives half. Required criteria weight 2; preferred weight 1. Subject equivalence and experience alternatives require human review."));
        }
        else definitions.Add(("Education", 0, 0, "Not applicable: no recognized education-level requirement. Its weight is redistributed."));
        var atsFlags = resume.Profile.Warnings.Count(warning => warning.StartsWith("ATS:", StringComparison.Ordinal));
        definitions.Add(("ATS text readiness", 5, Math.Max(0, 1 - atsFlags * .2),
            $"{atsFlags} text-only checks flagged. Start at 100 and deduct 20 per missing-contact, heading or extraction warning; minimum 0. Original layout is not inspected."));
        if (resume.Profile.Bullets.Count > 0)
        {
            var strong = resume.Profile.Bullets.Count(bullet => !WeakBullet(bullet));
            definitions.Add(("Bullet clarity", 5, (double)strong / resume.Profile.Bullets.Count,
                $"{strong} of {resume.Profile.Bullets.Count} unique bullets have a concrete action opener and sufficient detail. Unsupported numbers are never inferred; duplicate bullets give no bonus."));
        }
        else definitions.Add(("Bullet clarity", 5, 0, "No recognizable bullet lines were found. Add evidence-based action statements where appropriate."));
        var totalWeight = definitions.Sum(definition => definition.Weight);
        var metrics = new List<Metric>();
        var assigned = 0d;
        var lastActive = definitions.FindLastIndex(definition => definition.Weight > 0);
        for (var i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];
            var weight = i == lastActive ? 100 - assigned : Math.Round(definition.Weight / totalWeight * 100, 6);
            assigned += weight;
            var adjusted = Math.Pow(Math.Clamp(definition.Credit, 0, 1), options.Strictness);
            var score = (int)Math.Round(adjusted * 100, MidpointRounding.AwayFromZero);
            metrics.Add(new Metric(definition.Name, weight, score,
                definition.Explanation + (weight > 0 ? $" Base credit {definition.Credit * 100:0.##}%; strictness exponent {options.Strictness:0.##}; contribution = {score} × {weight:0.##}% / 100." : "")));
        }
        return metrics;
    }

    private static bool WeakBullet(string bullet)
    {
        if (TextRules.Rx(@"^(?:responsible for|worked on|helped(?: with)?|assisted(?: with)?|duties included|tasked with|involved in)\b").IsMatch(bullet)) return true;
        if (bullet.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 5) return true;
        return !TextRules.Rx(@"^(?:achieved|administered|analy[sz]ed|arranged|assessed|audited|automated|built|coached|collaborated|completed|conducted|configured|coordinated|created|cut|delivered|deployed|designed|developed|diagnosed|documented|drove|educated|enhanced|established|evaluated|executed|facilitated|forecast|generated|grew|guided|handled|implemented|improved|increased|inspected|installed|integrated|introduced|launched|led|maintained|managed|mentored|monitored|negotiated|operated|optimi[sz]ed|organi[sz]ed|oversaw|performed|planned|prepared|processed|produced|provided|reconciled|recorded|recruited|reduced|repaired|researched|resolved|reviewed|scheduled|secured|served|simplified|sold|streamlined|supervised|supported|taught|tested|trained|translated|updated|validated|wrote)\b").IsMatch(bullet);
    }

    private static List<Recommendation> Recommendations(List<SkillMatch> skills, List<(JobClause Clause, Outcome Result)> clauses, ResumeDetails resume)
    {
        var recommendations = new List<Recommendation>();
        void Add(string priority, string category, string problem, string why, string action, string example = "") =>
            recommendations.Add(new Recommendation($"offline-{recommendations.Count + 1:D3}", priority, category, problem, why, action, example));
        foreach (var skill in skills.Where(skill => skill.Status != "Matched"))
        {
            var partial = skill.Status == "Partial";
            Add(skill.Required ? (partial ? "High" : "Critical") : (partial ? "Low" : "Medium"), "Skills",
                partial ? $"{skill.Skill} has tentative or fuzzy evidence, not an exact confident match." : $"No affirmative evidence for {skill.Skill} was found.",
                skill.Required ? "The job lists this in a required context." : "The job lists this as preferred; it is not a mandatory gate.",
                partial ? "Review the quoted source. Correct spelling or describe your actual proficiency only if accurate; do not upgrade beginner knowledge to expertise."
                    : "If you genuinely have this skill, add a specific, verifiable example under the relevant role or project. Otherwise leave it as a development gap; do not add an unsupported keyword.",
                skill.Evidence);
        }
        foreach (var (clause, result) in clauses.Where(item => item.Result.Status != "Matched"))
        {
            var degree = DocumentParsing.EducationRank(clause.Text, true);
            var years = DocumentParsing.YearsRequired(clause.Text);
            if (degree > 0)
                Add(clause.Required ? "Critical" : "Medium", "Education", $"Education evidence does not fully meet: {clause.Text}",
                    "A lower or unfinished degree does not establish a completed higher qualification.",
                    "Verify the education entry and completion status. List only the degree actually earned; clarify an allowed equivalency with the employer rather than inventing credentials.", result.Evidence);
            else if (years > 0)
                Add(clause.Required ? "High" : "Medium", "Experience", $"Date evidence does not fully support: {clause.Text}",
                    "Only non-overlapping, parseable employment months are counted; summary claims do not substitute for a timeline.",
                    "Check role start/end months and include any genuinely omitted employment. Do not extend dates or add concurrent jobs together.", result.Evidence);
            else if (clause.Section == "Responsibilities" || skills.Count == 0)
                Add(clause.Required ? "High" : "Medium", "Requirement evidence", $"Resume evidence is incomplete for: {clause.Text}",
                    "A matching title or tool name alone does not demonstrate the requested responsibility.",
                    "If you performed this work, identify the real role/project and describe your action, scope and documented outcome. Otherwise retain it as a gap.", result.Evidence);
        }
        foreach (var warning in resume.Profile.Warnings.Where(warning => warning.StartsWith("ATS:", StringComparison.Ordinal)))
            Add("Medium", "ATS", warning, "Clear contact details and standard text structure reduce extraction ambiguity.",
                "Check the original file and exported plain-text reading order. Add accurate missing contact details or standard section headings as applicable; keep essential information out of images.");
        foreach (var bullet in resume.Profile.Bullets.Where(WeakBullet).Take(5))
            Add("Low", "Bullet clarity", "This bullet is vague, very short or lacks a clear action opener.",
                "A reviewer needs to understand what you personally did, not just the assigned duty.",
                "Rewrite using a truthful action, the actual task/scope, and an outcome only if you can verify it. Preserve the original facts; omit unknown metrics.", bullet);
        return recommendations;
    }
}
