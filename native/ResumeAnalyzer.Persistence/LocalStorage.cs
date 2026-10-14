using System.Text.Json;
using ResumeAnalyzer.Domain;

namespace ResumeAnalyzer.Persistence;

/// <summary>Atomic, bounded local storage. The root is chosen by the application, never imported documents.</summary>
public sealed class LocalStorage : ILocalStorage, IDisposable
{
    private readonly string root;
    private readonly SemaphoreSlim gate = new(1, 1);
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private const long MaxBytes = 40 * 1024 * 1024;
    public string Root => root;
    public LocalStorage(string? directory = null)
    {
        root = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Northstar", "ResumeAnalyzer");
        Directory.CreateDirectory(root);
    }
    public Task<WorkspaceData> LoadWorkspaceAsync() => Load("workspace.json", new WorkspaceData());
    public Task<AppSettings> LoadSettingsAsync() => Load("settings.json", new AppSettings());
    public Task<List<AnalysisSnapshot>> LoadHistoryAsync() => Load("history.json", new List<AnalysisSnapshot>());
    public Task SaveWorkspaceAsync(WorkspaceData data) => Save("workspace.json", data);
    public Task SaveSettingsAsync(AppSettings settings) => Save("settings.json", settings);
    public Task SaveHistoryAsync(List<AnalysisSnapshot> history)
    {
        if (history.Count > 100) throw new InvalidOperationException("History is limited to 100 snapshots. Delete older snapshots before saving more.");
        return Save("history.json", history);
    }
    private async Task<T> Load<T>(string name, T fallback)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var path = Path.Combine(root, name);
            if (!File.Exists(path)) return fallback;
            if (new FileInfo(path).Length > MaxBytes) throw new InvalidDataException($"Local {name} exceeds the safety limit. Move it out of the application data folder to recover.");
            try
            {
                await using var stream = File.OpenRead(path);
                return await JsonSerializer.DeserializeAsync<T>(stream, Json).ConfigureAwait(false) ?? fallback;
            }
            catch (JsonException) { throw new InvalidDataException($"Local {name} could not be read. Your original documents are unchanged. A previous .bak copy may be available in the data folder."); }
        }
        finally { gate.Release(); }
    }
    private async Task Save<T>(string name, T data)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        var path = Path.Combine(root, name);
        var temp = path + ".tmp";
        try
        {
            Directory.CreateDirectory(root);
            await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
            {
                await JsonSerializer.SerializeAsync(stream, data, Json).ConfigureAwait(false);
                if (stream.Length > MaxBytes) throw new InvalidOperationException("Local data is too large. Delete older history snapshots and try again.");
                await stream.FlushAsync().ConfigureAwait(false);
            }
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak", true);
            else File.Move(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); gate.Release(); }
    }
    public async Task ClearAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (var name in new[] { "workspace.json", "settings.json", "history.json" })
                foreach (var suffix in new[] { "", ".bak", ".tmp" })
                {
                    var path = Path.Combine(root, name + suffix);
                    if (File.Exists(path)) File.Delete(path);
                }
        }
        finally { gate.Release(); }
    }
    public void Dispose() => gate.Dispose();
}
