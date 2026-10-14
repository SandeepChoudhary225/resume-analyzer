using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Microsoft.Win32;
using ResumeAnalyzer.Application;
using ResumeAnalyzer.Domain;
using static ResumeAnalyzer.App.Visuals;

namespace ResumeAnalyzer.App;
public sealed partial class MainWindow
{
    private UIElement EditorPage()
    {
        var panel = Heading("Make your real experience clearer.", "Edit sections and bullets, reorder with buttons, save locally, then analyze again. Export creates a new document.");
        if (Service.Data.Resume == null) { panel.Children.Add(Card(Stack(Text("Add a resume to start editing."), Button("Import resume", "EditorImport", () => ImportDialog(true), true)))); return panel; }
        editingSections ??= SectionEditor.Split(Service.Data.Resume.Text);
        panel.Children.Add(Row(Button("Save changes", "SaveEditor", () => _ = Operate(SaveNowAsync)), Button("Analyze again", "EditorAnalyze", () => _ = AnalyzeAsync(), true), Button("Export resume…", "ExportResume", () => ExportDialog(true)), Button("Add section", "AddSection", AddSection)));
        for (int index = 0; index < editingSections.Count; index++)
        {
            var i = index; var section = editingSections[i];
            var up = Button("Move up", "SectionUp" + i, () => MoveSection(i, -1)); up.IsEnabled = i > 0;
            var down = Button("Move down", "SectionDown" + i, () => MoveSection(i, 1)); down.IsEnabled = i < editingSections.Count - 1;
            var text = Input(section.Text, "SectionText" + i, Math.Clamp(section.Text.Split('\n').Length * 24 + 40, 130, 290));
            text.TextChanged += (_, _) => { if (editingSections != null) { editingSections[i] = editingSections[i] with { Text = text.Text }; DraftChanged(SectionEditor.Join(editingSections), true); } };
            var row = Row(up, down, Button("Rename", "SectionRename" + i, () => RenameSection(i)), Button("Remove", "SectionRemove" + i, () => RemoveSection(i)), Button("Add bullet", "SectionBullet" + i, () => { text.AppendText((text.Text.Length > 0 ? "\n" : "") + "• "); text.CaretIndex = text.Text.Length; text.Focus(); }));
            panel.Children.Add(Card(Stack(Text(section.Name, 18, "Ink", FontWeights.SemiBold), text, row)));
        }
        panel.Children.Add(Text("Use Ctrl+S to save, Ctrl+Enter to analyze, and Ctrl+O to import. Metrics must come from your records; never use a made-up number.", 12, "Muted")); return panel;
    }
    private void MoveSection(int index, int direction) { if (editingSections == null) return; SectionEditor.Move(editingSections, index, direction); DraftChanged(SectionEditor.Join(editingSections), true); Navigate("Resume Editor"); }
    private void AddSection()
    {
        if (editingSections == null) return;
        var name = Prompt("Add section", "Section name", "Projects"); if (string.IsNullOrWhiteSpace(name)) return;
        if (editingSections.Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) { Notify("That section already exists. Edit it or choose a different heading.", true); return; }
        editingSections.Add(new(name.Trim(), "")); DraftChanged(SectionEditor.Join(editingSections), true); Navigate("Resume Editor");
    }
    private void RenameSection(int index)
    {
        if (editingSections == null) return;
        var name = Prompt("Rename section", "Section name", editingSections[index].Name); if (string.IsNullOrWhiteSpace(name)) return;
        if (editingSections.Where((_, i) => i != index).Any(s => s.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase))) { Notify("A section with that name already exists.", true); return; }
        editingSections[index] = editingSections[index] with { Name = name.Trim() }; DraftChanged(SectionEditor.Join(editingSections), true); Navigate("Resume Editor");
    }
    private void RemoveSection(int index)
    {
        if (editingSections == null || !Confirm("Remove this section from your working copy? Your imported file will not be changed.")) return;
        editingSections.RemoveAt(index); DraftChanged(SectionEditor.Join(editingSections), true); Navigate("Resume Editor");
    }
    private string? Prompt(string title, string label, string initial)
    {
        if (PromptOverride != null) return PromptOverride(initial);
        var dialog = new Window { Title = title, Width = 460, Height = 250, ResizeMode = ResizeMode.NoResize, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var input = Input(initial, label, 45); input.AcceptsReturn = false; input.MaxLength = 120;
        var save = Button("Save", "PromptSave", () => dialog.DialogResult = true, true); save.IsDefault = true;
        var cancel = Button("Cancel", "PromptCancel", () => dialog.DialogResult = false); cancel.IsCancel = true;
        dialog.Content = new Border { Padding = new(24), Child = Stack(Text(label, 16, "Ink", FontWeights.SemiBold), input, Row(cancel, save)) };
        dialog.Loaded += (_, _) => { input.Focus(); input.SelectAll(); }; return dialog.ShowDialog() == true ? input.Text.Trim() : null;
    }
    private UIElement HistoryPage()
    {
        var panel = Heading("Every version, kept here.", "Snapshots include the exact resume, job text, score, and recommendations from each analysis. Up to 100 versions are kept locally.");
        var query = Input(search, "HistorySearch", 42); query.AcceptsReturn = false;
        panel.Children.Add(Row(query, Button("Search", "SearchHistory", () => { search = query.Text; Navigate("History"); }), Button("Clear search", "ClearHistorySearch", () => { search = ""; Navigate("History"); }), Button("Clear history", "ClearHistory", () => _ = ClearHistoryUiAsync())));
        foreach (var snapshot in Service.History.Where(s => (s.Name + s.ResumeName).Contains(search, StringComparison.OrdinalIgnoreCase)))
        {
            var actions = Row(Button("Open", "HistoryOpen" + snapshot.Id, () => _ = OpenSnapshotAsync(snapshot.Id, false)), Button("Compare to current", "HistoryCompare" + snapshot.Id, () => _ = OpenSnapshotAsync(snapshot.Id, true)), Button("Rename", "HistoryRename" + snapshot.Id, () => _ = RenameSnapshotAsync(snapshot.Id, snapshot.Name)), Button("Delete", "HistoryDelete" + snapshot.Id, () => _ = DeleteSnapshotAsync(snapshot.Id)));
            panel.Children.Add(Card(Stack(Text(snapshot.Name, 17, "Ink", FontWeights.SemiBold), Text($"{snapshot.Result.Created.ToLocalTime():g}  •  {snapshot.ResumeName}  •  v{snapshot.Version}", 13, "Muted"), Text($"{snapshot.Result.Score}/100 estimated match    |    {snapshot.Result.Recommendations.Count} recommendations", 14, "AccentText"), actions)));
        }
        if (Service.History.Count == 0) panel.Children.Add(Card(Text("No saved analyses yet. Run an analysis to create your first snapshot."))); return panel;
    }
    private async Task OpenSnapshotAsync(string id, bool compare)
    {
        if (await Operate(async () => { await SaveNowAsync(); await Service.OpenHistoryAsync(id, compare); editingSections = null; })) Navigate(compare ? "Comparison" : "Analysis");
    }
    private async Task RenameSnapshotAsync(string id, string name)
    {
        var rename = Prompt("Rename analysis", "Snapshot name", name); if (rename == null) return;
        if (await Operate(() => Service.RenameHistoryAsync(id, rename))) Navigate("History");
    }
    private async Task DeleteSnapshotAsync(string id)
    {
        if (!Confirm("Delete this saved snapshot? Your working draft is not deleted.")) return;
        if (await Operate(() => Service.DeleteHistoryAsync(id))) Navigate("History");
    }
    private async Task ClearHistoryUiAsync()
    {
        if (!Confirm("Delete all saved analysis snapshots? Your working draft is not deleted.")) return;
        if (await Operate(Service.ClearHistoryAsync)) Navigate("History");
    }
    private UIElement SettingsPage()
    {
        var panel = Heading("A workspace that fits you.", "Settings and drafts are saved on this device. No telemetry is implemented or collected.");
        var theme = new ComboBox { ItemsSource = new[] { "Light", "Dark", "System" }, SelectedItem = Service.Settings.Theme, HorizontalAlignment = HorizontalAlignment.Left }; AutomationProperties.SetAutomationId(theme, "ThemeChoice"); AutomationProperties.SetName(theme, "Appearance theme");
        theme.SelectionChanged += async (_, _) => { var selected = theme.SelectedItem?.ToString() ?? "System"; if (await Operate(() => Service.SaveSettingsAsync(Service.Settings with { Theme = selected }))) { Theme(selected); Navigate("Settings"); } };
        panel.Children.Add(Card(Stack(Text("Appearance", 18, "Ink", FontWeights.SemiBold), Text("System follows Windows. High-contrast and reduced-motion preferences are respected.", 13, "Muted"), theme)));
        var options = Service.Settings.Analysis ?? new AnalysisOptions();
        var strict = Slider("Strictness", 0.7, 1.3, options.Strictness);
        var keyword = Slider("Keyword sensitivity", 0.5, 1.5, options.KeywordSensitivity);
        var fuzzy = Slider("Fuzzy similarity threshold", 0.75, 0.98, options.FuzzySensitivity);
        var apply = Button("Save analysis preferences", "SaveAnalysisSettings", () => _ = Operate(async () => { await Service.SaveSettingsAsync(Service.Settings with { Analysis = new(strict.Value, keyword.Value, fuzzy.Value) }); Notify("Analysis preferences saved. Run analysis again to apply them."); }), true);
        panel.Children.Add(Card(Stack(Text("Analysis preferences", 18, "Ink", FontWeights.SemiBold), Text("Sensitivity influences heuristic matching, not the underlying truth of a qualification. Scores can change when settings change.", 13, "Muted"), Text("Strictness — higher is more conservative"), strict, Text("Keyword sensitivity — higher requires stronger coverage"), keyword, Text("Fuzzy threshold — higher accepts fewer spelling variations"), fuzzy, Row(apply))));
        var format = new ComboBox { ItemsSource = new[] { "DOCX", "PDF", "TXT" }, SelectedItem = Service.Settings.DefaultExportFormat, HorizontalAlignment = HorizontalAlignment.Left }; AutomationProperties.SetName(format, "Default resume export format");
        var location = Input(Service.Settings.SaveLocation, "Default save folder", 44); location.AcceptsReturn = false;
        panel.Children.Add(Card(Stack(Text("Export defaults", 18, "Ink", FontWeights.SemiBold), Text("Resume format"), format, Text("Save folder (optional)", 14), location, Row(Button("Choose folder", "ChooseExportFolder", () => { var dialog = new OpenFolderDialog { Title = "Choose the default export folder" }; if (dialog.ShowDialog(this) == true) location.Text = dialog.FolderName; }), Button("Save export preferences", "SaveExportSettings", () => _ = Operate(async () => { if (location.Text.Length > 0 && !Directory.Exists(location.Text)) throw new ArgumentException("Choose an existing export folder."); await Service.SaveSettingsAsync(Service.Settings with { DefaultExportFormat = (string)format.SelectedItem, SaveLocation = location.Text }); Notify("Export defaults saved."); }))))));
        panel.Children.Add(Card(Stack(Text("Privacy and local storage", 18, "Ink", FontWeights.SemiBold), Text("Local-only processing is always on. Telemetry is always off. Data is stored as unencrypted JSON under your Windows account; use Windows device encryption for protection at rest.", 14, "Muted"), Text(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Northstar", "ResumeAnalyzer"), 12), Row(Button("Clear history", "SettingsClearHistory", () => _ = ClearHistoryUiAsync()), Button("Clear application data", "ClearData", () => _ = ClearDataUiAsync()))))); return panel;
    }
    private static Slider Slider(string label, double min, double max, double value)
    {
        var slider = new Slider { Minimum = min, Maximum = max, Value = value, TickFrequency = 0.05, IsSnapToTickEnabled = false, AutoToolTipPlacement = System.Windows.Controls.Primitives.AutoToolTipPlacement.BottomRight, Margin = new(0, 6, 0, 18), Width = 300, HorizontalAlignment = HorizontalAlignment.Left };
        AutomationProperties.SetName(slider, label); return slider;
    }
    private async Task ClearDataUiAsync()
    {
        if (!Confirm("Delete all local drafts, analyses, settings, and backup copies? Exported files and original imported documents will not be deleted.")) return;
        if (await Operate(Service.ClearAsync)) { editingSections = null; dirty = false; Theme(Service.Settings.Theme); Navigate("Dashboard"); }
    }
    private UIElement AboutPage()
    {
        var panel = Heading("Private by design. Honest by default.", "Northstar Resume Analyzer 1.0.0 — native Windows desktop application.");
        panel.Children.Add(Card(Stack(Text("Your documents stay on this computer.", 22, "Ink", FontWeights.SemiBold), Text("No cloud APIs, external servers, browser runtime, Node.js, Python, Java, or separate .NET installation are required by the self-contained Windows package. There is no telemetry code.", 14, "Muted"), Text("Scoring is a deterministic estimate using local vocabulary, aliases, text evidence, and explicit weights. It is not a proprietary employer ATS score, hiring prediction, or verification of qualifications.", 14), Text("Never invent a skill, employer, qualification, achievement, or metric. Missing requirements should be addressed through truthful evidence or real development, not fabricated wording.", 14, "AccentText"))));
        panel.Children.Add(Card(Stack(Text("License and credits", 18, "Ink", FontWeights.SemiBold), Text("An application-source license has not yet been selected. .NET/WPF and document dependencies retain their own licenses; see Documentation/DEPENDENCIES.md and Documentation/ThirdParty in the release. Review redistribution terms before distributing the package.", 14), Text("Limitations", 18, "Ink", FontWeights.SemiBold), Text("English-oriented deterministic analysis, not a local language model. Scanned PDFs require an existing text layer; OCR is not bundled. Complex layout extraction is best-effort. Resume exports use a clean single-column template, not the original layout. Review all parsed text, evidence, and exports.", 14, "Muted"))));
        return panel;
    }
}
