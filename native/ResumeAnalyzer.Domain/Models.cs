namespace ResumeAnalyzer.Domain;

public sealed record DocumentData(string FileName, string Text, int? Pages = null, string Format = "TXT", List<string>? Warnings = null);
public sealed record SectionData(string Name, string Text);
public sealed record SkillDefinition(string Name, string Category, string[] Aliases);
public sealed record SkillMatch(string Skill, string Status, bool Required, string Evidence, int Frequency, string Placement);
public sealed record Metric(string Name, double Weight, int Score, string Explanation);
public sealed record Recommendation(string Id, string Priority, string Category, string Problem, string Why, string Action, string Example, bool Completed = false);
public sealed record ComparisonEntry(string Section, string Requirement, string Evidence, string Status, bool HighPriority);
public sealed record ResumeProfile(string Name, string Email, string Phone, List<SectionData> Sections, double ExperienceYears, List<string> Bullets, List<string> Warnings);
public sealed record JobProfile(string Title, double YearsRequired, List<string> Responsibilities, List<string> Qualifications);
public sealed record AnalysisOptions(double Strictness = 1.0, double KeywordSensitivity = 1.0, double FuzzySensitivity = 0.85);
public sealed record AnalysisResult(int Score, List<Metric> Metrics, List<SkillMatch> Skills, List<Recommendation> Recommendations, List<ComparisonEntry> Comparisons, ResumeProfile Resume, JobProfile Job, List<string> Strengths, List<string> Warnings, DateTimeOffset Created);
public sealed record AnalysisSnapshot(string Id, string Name, string ResumeName, string ResumeText, string JobText, int Version, AnalysisResult Result);
public sealed record AppSettings(string Theme = "System", AnalysisOptions? Analysis = null, string DefaultExportFormat = "DOCX", string SaveLocation = "", bool Onboarded = false);
public sealed record WorkspaceData(DocumentData? Resume = null, DocumentData? Job = null, AnalysisResult? Analysis = null, AnalysisResult? Previous = null, int Version = 1, bool Stale = false);

public interface IAnalysisService
{
    AnalysisResult Analyze(string resume, string job, AnalysisOptions? options = null, IProgress<string>? progress = null);
    ResumeProfile ParseResume(string text);
    JobProfile ParseJob(string text);
}
public interface IKnowledgeBase { IReadOnlyList<SkillDefinition> Skills { get; } }
public interface IDocumentParser { Task<DocumentData> ParseAsync(string path, CancellationToken cancellationToken = default); }
public interface IExportService
{
    Task ExportResumeAsync(string text, string path, CancellationToken cancellationToken = default);
    Task ExportAnalysisAsync(AnalysisResult result, string path, CancellationToken cancellationToken = default);
}
public interface ILocalStorage
{
    Task SaveWorkspaceAsync(WorkspaceData data);
    Task<WorkspaceData> LoadWorkspaceAsync();
    Task SaveSettingsAsync(AppSettings settings);
    Task<AppSettings> LoadSettingsAsync();
    Task<List<AnalysisSnapshot>> LoadHistoryAsync();
    Task SaveHistoryAsync(List<AnalysisSnapshot> history);
    Task ClearAsync();
}
public sealed class DocumentException(string message) : Exception(message);
