using ResumeAnalyzer.Domain;
namespace ResumeAnalyzer.Application;

/// <summary>Coordinates document versions, analysis and persistence independently of WPF.</summary>
public sealed class WorkspaceService(IAnalysisService analyzer, IDocumentParser parser, IExportService exporter, ILocalStorage storage)
{
    public WorkspaceData Data { get; private set; } = new();
    public AppSettings Settings { get; private set; } = new();
    public List<AnalysisSnapshot> History { get; private set; } = [];
    public bool IsStale { get => Data.Stale; private set => Data = Data with { Stale = value }; }
    public async Task LoadAsync()
    {
        Data = await storage.LoadWorkspaceAsync();
        Settings = await storage.LoadSettingsAsync();
        History = await storage.LoadHistoryAsync();
    }
    public void SetText(string text, bool resume)
    {
        if (text.Length > 200_000) throw new ArgumentException("Documents are limited to 200,000 characters. Please shorten the text.");
        var prior = resume ? Data.Resume : Data.Job;
        var doc = new DocumentData(prior?.FileName ?? (resume ? "Resume draft.txt" : "Job description.txt"), text, null, "Edited text", prior?.Warnings);
        Data = resume ? Data with { Resume = doc } : Data with { Job = doc };
        IsStale = true;
    }
    public async Task ImportAsync(string path, bool resume)
    {
        var document = await parser.ParseAsync(path);
        var next = resume ? Data with { Resume = document, Stale = true } : Data with { Job = document, Stale = true };
        await storage.SaveWorkspaceAsync(next);
        Data = next;
    }
    public async Task AnalyzeAsync(IProgress<string>? progress = null)
    {
        var resume = Data.Resume?.Text ?? "";
        var job = Data.Job?.Text ?? "";
        if (string.IsNullOrWhiteSpace(resume) || string.IsNullOrWhiteSpace(job)) throw new ArgumentException("Add nonempty resume and job-description text before analyzing.");
        if (History.Count >= 100) throw new InvalidOperationException("Your history contains 100 snapshots. Delete an older snapshot before analyzing again.");
        var result = await Task.Run(() => analyzer.Analyze(resume, job, Settings.Analysis, progress));
        var warnings = (Data.Resume?.Warnings ?? []).Concat(Data.Job?.Warnings ?? []).ToList();
        result = result with { Warnings = result.Warnings.Concat(warnings).Distinct().ToList() };
        var next = Data with { Previous = Data.Analysis, Analysis = result, Version = Data.Analysis == null ? Data.Version : Data.Version + 1, Stale = false };
        var snapshot = new AnalysisSnapshot(Guid.NewGuid().ToString("N"), result.Job.Title, Data.Resume!.FileName, resume, job, next.Version, result);
        var updated = new List<AnalysisSnapshot> { snapshot };
        updated.AddRange(History);
        // Publish only after successful persistence. History is an immutable record of analyzed text.
        await storage.SaveWorkspaceAsync(next);
        await storage.SaveHistoryAsync(updated);
        Data = next;
        History = updated;
        IsStale = false;
    }
    public async Task SaveAsync() => await storage.SaveWorkspaceAsync(Data);
    public async Task SaveSettingsAsync(AppSettings settings)
    {
        if ((Settings.Analysis ?? new AnalysisOptions()) != (settings.Analysis ?? new AnalysisOptions()) && Data.Analysis != null)
        {
            // Persist invalidation first so a later settings-write failure cannot leave a falsely current score.
            var next = Data with { Stale = true };
            await storage.SaveWorkspaceAsync(next);
            Data = next;
        }
        await storage.SaveSettingsAsync(settings);
        Settings = settings;
    }
    public async Task ResetAsync() { var next = new WorkspaceData(); await storage.SaveWorkspaceAsync(next); Data = next; }
    public async Task ClearAsync() { await storage.ClearAsync(); Data = new(); Settings = new(); History = []; IsStale = false; }
    public async Task ClearHistoryAsync() { await storage.SaveHistoryAsync([]); History = []; }
    public async Task DeleteHistoryAsync(string id) { var next = History.Where(h => h.Id != id).ToList(); await storage.SaveHistoryAsync(next); History = next; }
    public async Task RenameHistoryAsync(string id, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Enter a name for this snapshot.");
        var next = History.Select(h => h.Id == id ? h with { Name = name.Trim()[..Math.Min(name.Trim().Length, 120)] } : h).ToList();
        await storage.SaveHistoryAsync(next); History = next;
    }
    public async Task OpenHistoryAsync(string id, bool compareOnly = false)
    {
        var snapshot = History.Single(h => h.Id == id);
        var next = compareOnly ? Data with { Previous = snapshot.Result } : new(new(snapshot.ResumeName, snapshot.ResumeText), new("Saved job.txt", snapshot.JobText), snapshot.Result, Data.Analysis, snapshot.Version);
        await storage.SaveWorkspaceAsync(next);
        Data = next;
    }
    public async Task ToggleRecommendationAsync(string id, bool completed)
    {
        if (Data.Analysis is not { } analysis) return;
        var next = Data with { Analysis = analysis with { Recommendations = analysis.Recommendations.Select(r => r.Id == id ? r with { Completed = completed } : r).ToList() } };
        await storage.SaveWorkspaceAsync(next);
        Data = next;
    }
    public async Task ExportAsync(string path, bool resume)
    {
        if (resume)
        {
            if (string.IsNullOrWhiteSpace(Data.Resume?.Text)) throw new ArgumentException("Add a resume before exporting.");
            await exporter.ExportResumeAsync(Data.Resume.Text, path);
        }
        else
        {
            if (Data.Analysis == null) throw new ArgumentException("Run an analysis before exporting a report.");
            if (IsStale) throw new InvalidOperationException("The documents or analysis preferences changed. Analyze again before exporting a report.");
            await exporter.ExportAnalysisAsync(Data.Analysis, path);
        }
    }
}
