using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ResumeAnalyzer.Application;
using ResumeAnalyzer.Documents;
using ResumeAnalyzer.Persistence;

namespace ResumeAnalyzer.App;
/// <summary>Opt-in native-control smoke harness. Runs an actual WPF window, never the normal user workspace.</summary>
internal static class NativeUiTests
{
    internal static async Task<int> RunAsync(MainWindow window, WorkspaceService service, DocumentService documents, string report)
    {
        var results = new List<object>(); int failed = 0, passed = 0;
        var folder = Path.GetDirectoryName(Path.GetFullPath(report))!; Directory.CreateDirectory(folder);
        window.ConfirmOverride = _ => true;
        async Task Case(string name, Func<Task> action)
        {
            try { await action(); passed++; results.Add(new { name, status = "PASS" }); }
            catch (Exception ex) { failed++; results.Add(new { name, status = "FAIL", error = ex.Message }); }
        }
        async Task Idle() { await Task.Delay(90); var timer = Stopwatch.StartNew(); while (window.Busy) { if (timer.Elapsed.TotalSeconds > 45) throw new TimeoutException("UI remained busy"); await Task.Delay(40); } await Task.Delay(30); }
        async Task Click(string id) { var button = Find<Button>(window, id); Assert(button.IsEnabled, "disabled control: " + id); button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Idle(); }
        await Case("Window launches with real empty state", () => { Assert(window.IsVisible && service.Data.Analysis == null, "not a clean launch"); return Task.CompletedTask; });
        await Case("All ten navigation buttons execute", async () => { foreach (var name in new[] { "Dashboard", "Resume", "Job Description", "Analysis", "Recommendations", "Resume Editor", "Comparison", "History", "Settings", "About" }) { await Click("Nav" + name.Replace(" ", "")); Assert(window.CurrentRoute == name, "navigation failed: " + name); } });
        await Case("Empty analysis surfaces a recoverable error without a fake score", async () =>
        {
            window.Navigate("Dashboard"); await Click("Analyze");
            Assert(service.Data.Analysis == null && Find<TextBlock>(window, "StatusMessage").Text.Contains("nonempty"), "empty analysis error missing");
        });
        await Case("Canceling samples preserves a job-only draft and does not claim success", async () =>
        {
            try
            {
                service.SetText("My unsaved target opportunity", false); window.Navigate("Dashboard"); window.ConfirmOverride = _ => false;
                await Click("LoadSamples");
                Assert(service.Data.Resume == null && service.Data.Job!.Text == "My unsaved target opportunity", "cancel replaced the draft");
                Assert(!Find<TextBlock>(window, "StatusMessage").Text.Contains("documents loaded"), "cancel claimed success");
            }
            finally { window.ConfirmOverride = _ => true; await service.ResetAsync(); }
        });
        await Case("Synthetic samples require explicit load; analyze calculates result", async () => { window.Navigate("Dashboard"); await Click("LoadSamples"); Assert(service.Data.Resume != null && service.Data.Job != null, "samples failed"); Assert(service.Data.Analysis == null, "fake sample analysis"); await Click("Analyze"); Assert(service.Data.Analysis != null && service.History.Count == 1, "analysis failed"); });
        await Case("Resume import uses bundled DOCX parser", async () => { var path = Path.Combine(folder, "ui-resume.docx"); await documents.ExportResumeAsync(service.Data.Resume!.Text, path); await window.ImportPathAsync(path, true); Assert(service.Data.Resume?.Format.Contains("DOCX", StringComparison.OrdinalIgnoreCase) == true, "DOCX import not applied"); });
        await Case("Native file drop routes to job import", async () =>
        {
            var path = Path.Combine(folder, "ui-job.txt"); await File.WriteAllTextAsync(path, service.Data.Job!.Text);
            window.Navigate("Job Description"); var target = Find<FrameworkElement>(window, "JobDrop");
            var data = new DataObject(DataFormats.FileDrop, new[] { path });
            // WPF creates DragEventArgs internally. Construct the actual routed event for this opt-in harness.
            var constructor = typeof(DragEventArgs).GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Single();
            var args = (DragEventArgs)constructor.Invoke([data, DragDropKeyStates.None, DragDropEffects.Copy, target, new Point(10, 10)]);
            args.RoutedEvent = UIElement.PreviewDropEvent;
            target.RaiseEvent(args); await Idle(); Assert(service.Data.Job?.FileName == "ui-job.txt", "drop ignored");
        });
        await Case("Editor changes skills; save and reanalysis show new match", async () =>
        {
            window.Navigate("Resume Editor"); var sections = SectionEditor.Split(service.Data.Resume!.Text); int index = sections.FindIndex(s => s.Name.Equals("Skills", StringComparison.OrdinalIgnoreCase)); Assert(index >= 0, "skills editor missing");
            Find<TextBox>(window, "SectionText" + index).AppendText("\nKubernetes, Terraform"); await Click("SaveEditor"); Assert(service.Data.Resume!.Text.Contains("Kubernetes"), "edit not saved");
            await Click("EditorAnalyze"); Assert(service.Data.Previous != null, "comparison absent"); Assert(service.Data.Analysis!.Skills.Any(s => s.Skill.Contains("Kubernetes", StringComparison.OrdinalIgnoreCase) && s.Status == "Matched"), "edited match absent");
        });
        await Case("Section add, reorder, rename, remove execute", async () =>
        {
            window.Navigate("Resume Editor"); window.PromptOverride = _ => "Volunteer Experience"; await Click("AddSection");
            var sections = SectionEditor.Split(service.Data.Resume!.Text); var index = sections.Count - 1; Assert(sections[index].Name == "Volunteer Experience", "add failed");
            await Click("SectionUp" + index); index--; window.PromptOverride = _ => "Community Work"; await Click("SectionRename" + index); Assert(service.Data.Resume.Text.Contains("Community Work"), "rename failed");
            await Click("SectionRemove" + index); Assert(!service.Data.Resume.Text.Contains("Community Work"), "remove failed"); window.PromptOverride = null;
        });
        await Case("Another action flushes a pending draft save", async () =>
        {
            window.Navigate("Resume"); Find<TextBox>(window, "ResumeText").AppendText("\nDraft persistence marker");
            window.Navigate("Settings"); Find<ComboBox>(window, "ThemeChoice").SelectedItem = "Dark"; await Idle();
            await service.LoadAsync(); Assert(service.Data.Resume!.Text.Contains("Draft persistence marker"), "interrupted autosave lost the draft");
        });
        await Case("Comparison filter and search execute", async () => { window.Navigate("Comparison"); await Click("FilterMissing"); Find<TextBox>(window, "ComparisonSearch").Text = "Python"; await Click("SearchComparison"); await Click("ClearComparison"); await Click("FilterAll"); });
        await Case("Checklist completion preserves score", async () =>
        {
            window.Navigate("Recommendations"); var before = service.Data.Analysis!.Score; var rec = service.Data.Analysis.Recommendations.First(); var box = Find<CheckBox>(window, "Rec" + rec.Id); box.IsChecked = true; box.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent)); await Idle(); Assert(service.Data.Analysis.Recommendations.First(r => r.Id == rec.Id).Completed, "checkbox unsaved"); Assert(before == service.Data.Analysis.Score, "fake score improvement");
        });
        await Case("Light/dark/system settings persist; screenshots rendered", async () =>
        {
            foreach (var theme in new[] { "Dark", "Light", "System" })
            {
                window.Navigate("Settings"); Find<ComboBox>(window, "ThemeChoice").SelectedItem = theme; await Idle(); Assert(service.Settings.Theme == theme, "theme not persisted");
                Assert(((SolidColorBrush)Find<DockPanel>(window, "WorkspaceHeader").Background).Color == ((SolidColorBrush)Visuals.Brush("Surface")).Color, "shell retained the old theme");
                Assert(window.Background is SolidColorBrush canvas && canvas.Color == ((SolidColorBrush)Visuals.Brush("Canvas")).Color, "window canvas did not follow the theme");
                if (theme != "System") { window.Navigate("Dashboard"); await Task.Delay(220); Screenshot(window, Path.Combine(folder, "dashboard-" + theme.ToLowerInvariant() + ".png")); }
            }
        });
        await Case("All native export formats create readable files", async () =>
        {
            await window.AnalyzeAsync(); Assert(!service.IsStale, "analysis did not refresh edited sections");
            foreach (var ext in new[] { "txt", "docx", "pdf" }) { var file = Path.Combine(folder, "ui-export." + ext); await service.ExportAsync(file, true); Assert((await documents.ParseAsync(file)).Text.Contains("Kubernetes"), "export lost edit: " + ext); }
            foreach (var ext in new[] { "txt", "pdf", "html" }) { var file = Path.Combine(folder, "ui-report." + ext); await service.ExportAsync(file, false); Assert(new FileInfo(file).Length > 100, "report empty"); }
        });
        await Case("History open, compare, rename, delete use snapshots", async () =>
        {
            window.Navigate("History"); var id = service.History.First().Id; window.PromptOverride = _ => "Renamed snapshot"; await Click("HistoryRename" + id); Assert(service.History.First().Name == "Renamed snapshot", "rename failed");
            window.PromptOverride = null; await Click("HistoryCompare" + id); Assert(window.CurrentRoute == "Comparison", "history compare failed"); window.Navigate("History"); await Click("HistoryOpen" + id); Assert(window.CurrentRoute == "Analysis", "history open failed"); window.Navigate("History"); await Click("HistoryDelete" + id); Assert(service.History.All(h => h.Id != id), "delete failed");
        });
        await Case("Invalid document surfaces recoverable error, no crash", async () =>
        {
            var before = service.Data.Resume; var bad = Path.Combine(folder, "corrupt.pdf"); await File.WriteAllTextAsync(bad, "%PDF invalid"); await window.ImportPathAsync(bad, true); Assert(service.Data.Resume == before, "invalid replaced draft"); Assert(window.IsVisible, "window crashed");
        });
        await Case("Native controls have names and tab navigation", () =>
        {
            window.Navigate("Resume"); var input = Find<TextBox>(window, "ResumeText"); input.Focus(); Assert(input.IsKeyboardFocused, "text cannot receive focus"); Assert(input.MoveFocus(new(System.Windows.Input.FocusNavigationDirection.Next)), "tab navigation failed");
            foreach (var button in Descendants(window).OfType<Button>().Where(b => b.IsVisible)) Assert(!string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)) || button.Content is string, "unnamed control"); return Task.CompletedTask;
        });
        await Case("History clear produces empty list; draft survives", async () => { window.Navigate("History"); await Click("ClearHistory"); Assert(service.History.Count == 0 && service.Data.Resume != null, "clear failed"); });
        await window.SaveNowAsync();
        await File.WriteAllTextAsync(report, JsonSerializer.Serialize(new { passed, failed, timestamp = DateTimeOffset.Now, method = "Actual WPF window and routed controls; confirmation prompts injected for destructive tests. Not an external UIA/screen-reader certification.", results }, new JsonSerializerOptions { WriteIndented = true }));
        return failed == 0 ? 0 : 1;
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject element)
    {
        yield return element;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++) foreach (var child in Descendants(VisualTreeHelper.GetChild(element, i))) yield return child;
    }
    private static T Find<T>(DependencyObject root, string id) where T : DependencyObject => Descendants(root).OfType<T>().FirstOrDefault(e => AutomationProperties.GetAutomationId(e) == id) ?? throw new InvalidOperationException("Control not found: " + id);
    private static void Assert(bool success, string message) { if (!success) throw new InvalidOperationException(message); }
    private static void Screenshot(Window window, string path)
    {
        window.UpdateLayout(); var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(path); png.Save(file);
    }
}
