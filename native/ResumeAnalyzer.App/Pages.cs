using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media;
using ResumeAnalyzer.Domain;
using static ResumeAnalyzer.App.Visuals;

namespace ResumeAnalyzer.App;
public sealed partial class MainWindow
{
    private UIElement DashboardPage()
    {
        var data = Service.Data;
        var panel = Heading("Your next chapter, with clarity.", "Understand the match. Improve the evidence. Keep your story yours.");
        if (!Service.Settings.Onboarded)
        {
            var intro = Stack(Text("Four steps to a stronger application", 18, "Ink", FontWeights.SemiBold), Text("1  Add your resume     2  Add a job description     3  Analyze the match     4  Edit and compare", 14, "Muted"), Row(Button("Start with my resume", "OnboardStart", () => Navigate("Resume"), true), Button("Try synthetic examples", "LoadSamples", () => _ = LoadSamplesAsync()), Button("Skip introduction", "SkipOnboarding", () => _ = SkipOnboardingAsync())));
            panel.Children.Add(Card(intro, "AccentSoft"));
        }
        var scoreContent = new DockPanel(); var ring = Score(data.Analysis?.Score); DockPanel.SetDock(ring, Dock.Left); scoreContent.Children.Add(ring);
        var scoreCopy = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        scoreCopy.Children.Add(new TextBlock { Text = "Estimated compatibility", FontSize = 22, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White });
         scoreCopy.Children.Add(new TextBlock { Text = data.Analysis == null ? "Add both documents and run analysis.\nEvery score is calculated from your text." : Service.IsStale ? "This result is from the previous text.\nAnalyze again after your edits." : "Transparent, weighted signals from your documents.\nNot an employer ATS score or a hiring prediction.", Foreground = Hex("#B4B8C0"), Margin = new(0, 12, 0, 10), TextWrapping = TextWrapping.Wrap, LineHeight = 22 });
        scoreCopy.Children.Add(Button(data.Analysis == null ? "Analyze the match" : "Analyze again", "Analyze", () => _ = AnalyzeAsync(), true)); scoreContent.Children.Add(scoreCopy);
         var scoreCard = Card(scoreContent); scoreCard.Background = Hex("#1B1D22"); panel.Children.Add(scoreCard);
        var resume = Stack(Text("Your resume", 16, "Ink", FontWeights.SemiBold), Text(data.Resume?.FileName ?? "No resume loaded", 14), Text(data.Resume == null ? "PDF, DOCX, TXT, or RTF. Up to 15 MB." : $"{data.Resume.Text.Length:N0} characters  •  Pages: {data.Resume.Pages?.ToString() ?? "not known"}  •  Sections: {data.Analysis?.Resume.Sections.Count.ToString() ?? "analyze to detect"}", 12, "Muted"), Row(Button("Import resume", "DashboardImportResume", () => ImportDialog(true)), Button("Edit resume", "DashboardEdit", () => Navigate("Resume Editor"))));
        var job = Stack(Text("Target opportunity", 16, "Ink", FontWeights.SemiBold), Text(data.Job == null ? "No job description loaded" : data.Analysis?.Job.Title ?? data.Job.FileName), Text(data.Analysis == null ? "Paste the full listing, or import a document." : $"{data.Analysis.Skills.Count} skills  •  {data.Analysis.Job.Qualifications.Count} qualification signals", 12, "Muted"), Row(Button("Add job description", "DashboardJob", () => Navigate("Job Description"))));
        panel.Children.Add(Columns(Card(resume), Card(job)));
        if (data.Analysis is { } a)
        {
            panel.Children.Add(Heading("Match breakdown", "Explainable weighted signals. Missing evidence is not proof that you lack a skill."));
            panel.Children.Add(Metrics(a));
            panel.Children.Add(Row(Button("View recommendations", "DashboardRecommendations", () => Navigate("Recommendations"), true), Button("Why this score?", "WhyScore", () => Navigate("Analysis")), Button("Export report", "DashboardExport", () => ExportDialog(false))));
        }
        return panel;
    }
    private async Task SkipOnboardingAsync() { if (await Operate(async () => await Service.SaveSettingsAsync(Service.Settings with { Onboarded = true }))) Navigate("Dashboard"); }
    private UIElement DocumentPage(bool resume)
    {
        var document = resume ? Service.Data.Resume : Service.Data.Job;
        var title = resume ? "Your resume" : "The opportunity in your words";
        var panel = Heading(title, "Import or paste text, then verify what was extracted. No content leaves this computer.");
        var drop = Card(Stack(Text(resume ? "Drop a resume here" : "Drop a job description here", 18, "Ink", FontWeights.SemiBold), Text("PDF, DOCX, TXT, RTF  •  Maximum 15 MB / 200,000 characters", 13, "Muted"), Row(Button("Browse files…", resume ? "ImportResume" : "ImportJob", () => ImportDialog(resume), true), Button("Load synthetic examples", "DocumentSamples", () => _ = LoadSamplesAsync()))), "Soft");
        AttachDrop(drop, resume); AutomationProperties.SetAutomationId(drop, resume ? "ResumeDrop" : "JobDrop"); panel.Children.Add(drop);
        panel.Children.Add(Text(document == null ? "Paste text below" : $"{document.FileName}  /  {document.Format}  /  Pages: {document.Pages?.ToString() ?? "not known"}", 14, "Ink", FontWeights.SemiBold));
        var input = Input(document?.Text ?? "", resume ? "ResumeText" : "JobText", 310);
        input.TextChanged += (_, _) => { DraftChanged(input.Text, resume); if (resume) editingSections = null; }; AttachDrop(input, resume); panel.Children.Add(input);
        if (document?.Warnings?.Count > 0) foreach (var warning in document.Warnings) panel.Children.Add(Card(Text(warning, 13, "Warn"), "WarnSoft", new(13)));
        panel.Children.Add(Row(Button("Save draft", "SaveDocument", () => _ = Operate(SaveNowAsync)), Button(resume ? "Continue to job description" : "Analyze the match", "DocumentContinue", () => { if (resume) Navigate("Job Description"); else _ = AnalyzeAsync(); }, true)));
        panel.Children.Add(Text("Imported formatting is converted to editable text. Keep the original document if exact layout preservation matters.", 12, "Muted")); return panel;
    }
    private UIElement NeedAnalysis()
        => Card(Stack(Text("A real result starts with your documents.", 21, "Ink", FontWeights.SemiBold), Text("Add a resume and a job description, then calculate an estimated match. No example score is shown as your result.", 14, "Muted"), Row(Button("Add resume", "EmptyAddResume", () => Navigate("Resume")), Button("Add job description", "EmptyAddJob", () => Navigate("Job Description")), Button("Analyze", "EmptyAnalyze", () => _ = AnalyzeAsync(), true))));
    private UIElement Metrics(AnalysisResult a)
    {
        var metrics = new StackPanel();
        foreach (var metric in a.Metrics)
        {
            var row = new Grid { Margin = new(0, 10, 0, 12) }; row.ColumnDefinitions.Add(new() { Width = new(220) }); row.ColumnDefinitions.Add(new()); row.ColumnDefinitions.Add(new() { Width = new(70) });
            var label = Text($"{metric.Name}  ·  {metric.Weight:0}% weight", 14, "Ink", FontWeights.SemiBold); row.Children.Add(label);
            var body = Stack(new ProgressBar { Value = metric.Score, Maximum = 100, Height = 5, Margin = new(0, 7, 0, 8) }, Text(metric.Explanation, 12, "Muted")); Grid.SetColumn(body, 1); row.Children.Add(body);
            var number = Text($"{metric.Score}/100", 14, "Ink", FontWeights.SemiBold); number.HorizontalAlignment = HorizontalAlignment.Right; Grid.SetColumn(number, 2); row.Children.Add(number); metrics.Children.Add(row);
        }
        return Card(metrics);
    }
    private UIElement AnalysisPage()
    {
        var panel = Heading("The evidence behind your score.", "Estimated compatibility, not an employer ATS score. Review the explanations and verify every claim.");
        if (Service.Data.Analysis is not { } a) { panel.Children.Add(NeedAnalysis()); return panel; }
        if (Service.IsStale) panel.Children.Add(Card(Text("Draft changed. These results describe the last analyzed version. Analyze again before relying on them.", 14, "Warn"), "WarnSoft"));
        var delta = Service.Data.Previous is { } p ? $"{p.Score} → {a.Score} ({a.Score - p.Score:+0;-0;0})" : $"{a.Score}/100";
        panel.Children.Add(Card(Stack(Text(delta, 36, "AccentText", FontWeights.SemiBold), Text(a.Job.Title, 19, "Ink", FontWeights.SemiBold), Text($"Analyzed {a.Created.ToLocalTime():g}   •   Resume version {Service.Data.Version}", 12, "Muted"), Row(Button("Analyze again", "Reanalyze", () => _ = AnalyzeAsync(), true), Button("Improve my resume", "AnalysisEdit", () => Navigate("Resume Editor")), Button("Export analysis", "ExportAnalysis", () => ExportDialog(false))))));
        panel.Children.Add(Text("Why this score?", 20, "Ink", FontWeights.SemiBold)); panel.Children.Add(Metrics(a));
        panel.Children.Add(Text("Skills and keywords", 20, "Ink", FontWeights.SemiBold));
        foreach (var group in new[] { "Matched", "Partial", "Missing" })
        {
            var skills = a.Skills.Where(s => s.Status == group).ToList();
            var content = Stack(Text($"{group}  /  {skills.Count}", 16, "Ink", FontWeights.SemiBold));
            var chips = new WrapPanel(); foreach (var skill in skills) { var chip = Chip(skill.Skill + (skill.Required ? " • required" : " • preferred"), group != "Matched"); chip.ToolTip = $"{skill.Evidence}\nFrequency: {skill.Frequency}; placement: {skill.Placement}"; chips.Children.Add(chip); }
            content.Children.Add(chips);
            foreach (var skill in skills) content.Children.Add(Text($"{skill.Skill}: {skill.Evidence}  |  Mentions: {skill.Frequency}  |  {skill.Placement}", 12, "Muted"));
            if (group != "Matched") content.Children.Add(Text("Add this only if you genuinely have this skill or experience. A missing mention is not a verified lack of ability.", 12, "Warn"));
            panel.Children.Add(Card(content));
        }
        var strengths = Stack(Text("Strengths", 17, "Ink", FontWeights.SemiBold)); foreach (var s in a.Strengths) strengths.Children.Add(Text("✓ " + s, 13));
        var warnings = Stack(Text("Readability and uncertainty", 17, "Ink", FontWeights.SemiBold)); foreach (var w in a.Warnings) warnings.Children.Add(Text("• " + w, 13));
        warnings.Children.Add(Text("Layout checks are heuristic. Plain extracted text cannot reliably reveal every image, column, or embedded element.", 12, "Muted")); panel.Children.Add(Columns(Card(strengths), Card(warnings))); return panel;
    }
    private UIElement RecommendationsPage()
    {
        var panel = Heading("Your next best edits.", "Recommendations distinguish missing requirements from improvements to the way you present real experience.");
        if (Service.Data.Analysis is not { } a) { panel.Children.Add(NeedAnalysis()); return panel; }
        panel.Children.Add(Text($"{a.Recommendations.Count(r => r.Completed)} of {a.Recommendations.Count} completed. Completion does not change the score; edits and re-analysis do.", 14, "Muted"));
        var filters = new ComboBox { ItemsSource = new[] { "All", "High priority", "Incomplete", "Completed" }, SelectedItem = filter is "High priority" or "Incomplete" or "Completed" ? filter : "All", Margin = new(0, 0, 0, 15) }; AutomationProperties.SetName(filters, "Recommendation filter"); filters.SelectionChanged += (_, _) => { filter = (string)filters.SelectedItem; Navigate("Recommendations"); }; panel.Children.Add(filters);
        var recs = a.Recommendations.Where(r => filter switch { "High priority" => r.Priority is "Critical" or "High", "Incomplete" => !r.Completed, "Completed" => r.Completed, _ => true });
        foreach (var r in recs)
        {
            var check = new CheckBox { Content = $"{r.Priority}  ·  {r.Category}", IsChecked = r.Completed, FontSize = 16, FontWeight = FontWeights.SemiBold }; AutomationProperties.SetAutomationId(check, "Rec" + r.Id);
            check.Click += async (_, _) => await Operate(async () => { await Service.ToggleRecommendationAsync(r.Id, check.IsChecked == true); Notify("Checklist saved locally. Re-analyze after editing to update the score."); });
            panel.Children.Add(Card(Stack(check, Text(r.Problem, 15, "Ink", FontWeights.SemiBold), Text("Why it matters: " + r.Why, 13, "Muted"), Text("Suggested action: " + r.Action, 14), Text("Example / guidance: " + r.Example, 13, "AccentText"))));
        }
        panel.Children.Add(Row(Button("Open resume editor", "RecommendationsEdit", () => Navigate("Resume Editor"), true), Button("Analyze again", "RecommendationsAnalyze", () => _ = AnalyzeAsync()))); return panel;
    }
    private UIElement ComparisonPage()
    {
        var panel = Heading("Read the match in context.", "Actual requirements alongside detected evidence. Partial similarity is not proof of meeting a requirement.");
        if (Service.Data.Analysis is not { } a) { panel.Children.Add(NeedAnalysis()); return panel; }
        if (Service.Data.Previous is { } previous)
        {
            var old = previous.Skills.Where(s => s.Status == "Matched").Select(s => s.Skill).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var added = a.Skills.Where(s => s.Status == "Matched" && !old.Contains(s.Skill)).Select(s => s.Skill).ToList();
            panel.Children.Add(Card(Stack(Text($"{previous.Score} → {a.Score}   ({a.Score - previous.Score:+0;-0;0})", 26, "AccentText", FontWeights.SemiBold), Text("New matches: " + (added.Count == 0 ? "none detected" : string.Join(", ", added)), 14), Text("Remaining gaps: " + string.Join(", ", a.Skills.Where(s => s.Status != "Matched").Select(s => s.Skill)), 13, "Muted"))));
        }
        var tabs = new WrapPanel(); foreach (var f in new[] { "All", "Matched", "Partial", "Missing", "High priority" }) tabs.Children.Add(Button(f, "Filter" + f.Replace(" ", ""), () => { filter = f; Navigate("Comparison"); }, filter == f)); panel.Children.Add(tabs);
        var query = Input(search, "ComparisonSearch", 42); query.AcceptsReturn = false;
        panel.Children.Add(Row(query, Button("Search", "SearchComparison", () => { search = query.Text; Navigate("Comparison"); }), Button("Clear", "ClearComparison", () => { search = ""; Navigate("Comparison"); })));
        panel.Children.Add(Columns(Text("Job description", 18, "Ink", FontWeights.SemiBold), Text("Resume evidence", 18, "Ink", FontWeights.SemiBold)));
        var entries = a.Comparisons.Where(c => (filter == "All" || filter is "Incomplete" or "Completed" || c.Status == filter || filter == "High priority" && c.HighPriority) && (c.Requirement + c.Evidence).Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var c in entries)
        {
            var left = Stack(Chip(c.Status + " · " + c.Section, c.Status != "Matched"), Text(c.Requirement, 14));
            var right = Stack(Text(string.IsNullOrWhiteSpace(c.Evidence) ? "No supporting statement detected." : c.Evidence, 14), Text(c.Status == "Missing" ? "Add evidence only if you genuinely have it." : "Verify context and relevance before using this match.", 12, "Muted"));
            panel.Children.Add(Columns(Card(left), Card(right)));
        }
        if (entries.Count == 0) panel.Children.Add(Card(Text("No comparison rows match this filter."))); return panel;
    }
}
