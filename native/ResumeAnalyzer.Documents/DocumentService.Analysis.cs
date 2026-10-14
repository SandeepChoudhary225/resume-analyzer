using System.Globalization;
using System.Net;
using System.Text;
using ResumeAnalyzer.Domain;

namespace ResumeAnalyzer.Documents;

public sealed partial class DocumentService
{
    private static string AnalysisText(AnalysisResult result)
    {
        var text = new StringBuilder();
        text.AppendLine("RESUME ANALYSIS");
        text.AppendLine($"Generated: {result.Created:O}");
        text.AppendLine($"Match score: {result.Score}/100");
        text.AppendLine("Heuristic guidance, not a hiring decision or a guarantee of ATS acceptance.");
        text.AppendLine();
        text.AppendLine("CANDIDATE & ROLE");
        text.AppendLine($"Candidate: {result.Resume.Name}");
        text.AppendLine($"Email: {result.Resume.Email}");
        text.AppendLine($"Phone: {result.Resume.Phone}");
        text.AppendLine($"Estimated experience: {result.Resume.ExperienceYears.ToString("0.##", CultureInfo.InvariantCulture)} years");
        text.AppendLine($"Target role: {result.Job.Title}");
        text.AppendLine($"Required experience: {result.Job.YearsRequired.ToString("0.##", CultureInfo.InvariantCulture)} years");
        text.AppendLine();
        text.AppendLine("SCORE BREAKDOWN");
        foreach (var metric in result.Metrics)
        {
            text.AppendLine($"{metric.Name}: {metric.Score}/100 (weight {metric.Weight.ToString("0.##", CultureInfo.InvariantCulture)})");
            text.AppendLine(metric.Explanation);
        }
        text.AppendLine();
        text.AppendLine("SKILL EVIDENCE");
        foreach (var skill in result.Skills)
        {
            text.AppendLine($"{skill.Skill} — {skill.Status}; {(skill.Required ? "required" : "optional")}; mentions: {skill.Frequency}; placement: {skill.Placement}");
            text.AppendLine($"Evidence: {skill.Evidence}");
        }
        text.AppendLine();
        text.AppendLine("STRENGTHS");
        foreach (var strength in result.Strengths) text.AppendLine($"- {strength}");
        text.AppendLine();
        text.AppendLine("PRIORITIZED RECOMMENDATIONS");
        foreach (var recommendation in result.Recommendations)
        {
            text.AppendLine($"[{recommendation.Priority}] {recommendation.Category}{(recommendation.Completed ? " (completed)" : "")}");
            text.AppendLine($"Problem: {recommendation.Problem}");
            text.AppendLine($"Why: {recommendation.Why}");
            text.AppendLine($"Action: {recommendation.Action}");
            text.AppendLine($"Example (adapt truthfully): {recommendation.Example}");
            text.AppendLine();
        }
        text.AppendLine("REQUIREMENTS COMPARISON");
        foreach (var comparison in result.Comparisons)
        {
            text.AppendLine($"{comparison.Section}: {comparison.Requirement} — {comparison.Status}{(comparison.HighPriority ? " (high priority)" : "")}");
            text.AppendLine($"Evidence: {comparison.Evidence}");
        }
        text.AppendLine();
        text.AppendLine("WARNINGS & REVIEW NOTES");
        foreach (var warning in result.Warnings.Concat(result.Resume.Warnings).Distinct()) text.AppendLine($"- {warning}");
        text.AppendLine();
        text.AppendLine("JOB RESPONSIBILITIES");
        foreach (var responsibility in result.Job.Responsibilities) text.AppendLine($"- {responsibility}");
        text.AppendLine();
        text.AppendLine("JOB QUALIFICATIONS");
        foreach (var qualification in result.Job.Qualifications) text.AppendLine($"- {qualification}");
        text.AppendLine();
        text.AppendLine("RESUME SECTIONS");
        foreach (var section in result.Resume.Sections)
        {
            text.AppendLine(section.Name);
            text.AppendLine(section.Text);
            text.AppendLine();
        }
        return text.ToString();
    }

    private static string AnalysisHtml(AnalysisResult result, string text) => $$"""
        <!doctype html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'">
        <title>Resume analysis</title>
        <style>
        :root { color-scheme: light; font-family: system-ui, sans-serif; color: #182334; background: #f5f7fa; }
        body { max-width: 900px; margin: 36px auto; padding: 32px; background: white; border: 1px solid #d7dde5; }
        h1 { margin: 0 0 12px; font-size: 30px; } .score { font-size: 20px; color: #1b1e24; }
        pre { white-space: pre-wrap; overflow-wrap: anywhere; font: 14px/1.65 system-ui, sans-serif; }
        @media print { body { margin: 0; padding: 0; border: 0; } pre { font-size: 11px; } }
        </style>
        </head>
        <body>
        <h1>Resume analysis</h1>
        <p class="score">Match score: {{result.Score}}/100</p>
        <pre>{{WebUtility.HtmlEncode(text)}}</pre>
        </body>
        </html>
        """;
}
