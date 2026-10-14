using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using ResumeAnalyzer.Application;
using ResumeAnalyzer.Domain;
using static ResumeAnalyzer.App.Visuals;

namespace ResumeAnalyzer.App;
public sealed partial class MainWindow : Window
{
    internal readonly WorkspaceService Service;
    private readonly ContentControl page = new();
    private readonly TextBlock status = new();
    private readonly ProgressBar progress = new() { IsIndeterminate = true, Visibility = Visibility.Collapsed, Width = 140, Height = 4, Margin = new(16, 0, 10, 0) };
    private readonly Dictionary<string, Button> navigation = [];
    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(700) };
    private string route = "Dashboard";
    private bool busy, closing, dirty;
    private string filter = "All";
    private string search = "";
    private List<SectionData>? editingSections;
    internal bool Busy => busy;
    internal Func<string, bool>? ConfirmOverride { get; set; }
    internal Func<string, string?>? PromptOverride { get; set; }
    internal string CurrentRoute => route;
    public MainWindow(WorkspaceService service)
    {
        Service = service;
        // Implicit Window styles do not automatically apply to this derived MainWindow type.
        Style = (Style)System.Windows.Application.Current.FindResource(typeof(Window));
        Title = "Northstar — Resume Analyzer";
        Width = 1320; Height = 870; MinWidth = 920; MinHeight = 650;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        if (SystemParameters.WorkArea.Width < Width) Width = SystemParameters.WorkArea.Width;
        if (SystemParameters.WorkArea.Height < Height) Height = SystemParameters.WorkArea.Height;
        Theme(service.Settings.Theme);
        Content = BuildShell();
        saveTimer.Tick += async (_, _) => { saveTimer.Stop(); await Operate(async () => { await Service.SaveAsync(); dirty = false; Notify("Draft saved on this computer."); }, false, false); };
        Closing += OnClosing;
        PreviewKeyDown += (_, e) =>
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S) { e.Handled = true; _ = Operate(SaveNowAsync); }
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.O) { e.Handled = true; ImportDialog(true); }
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Enter) { e.Handled = true; _ = AnalyzeAsync(); }
        };
        SystemEvents.UserPreferenceChanged += SystemPreferenceChanged;
        Closed += (_, _) => SystemEvents.UserPreferenceChanged -= SystemPreferenceChanged;
        Navigate("Dashboard");
    }
    private void SystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (Service.Settings.Theme == "System") Dispatcher.BeginInvoke(() => { Theme("System"); Navigate(route); });
    }
    private UIElement BuildShell()
    {
        var root = new Grid(); root.ColumnDefinitions.Add(new() { Width = new(225) }); root.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
         var sidebar = new DockPanel { Background = Hex("#111216"), LastChildFill = true };
         var brand = Stack(new TextBlock { Text = "northstar", FontSize = 27, FontWeight = FontWeights.Bold, Foreground = Brushes.White }, new TextBlock { Text = "Resume intelligence", Foreground = Hex("#B4B8C0"), Margin = new(0, 3, 0, 28) }); brand.Margin = new(24, 25, 18, 12); DockPanel.SetDock(brand, Dock.Top); sidebar.Children.Add(brand);
         var privacy = Stack(new TextBlock { Text = "Local by design", Foreground = Hex("#C1C5CC"), FontWeight = FontWeights.SemiBold }, new TextBlock { Text = "No uploads. No account.\nYour documents stay here.", Foreground = Hex("#AEB3BB"), FontSize = 12, Margin = new(0, 8, 0, 0) }); privacy.Margin = new(24, 20, 20, 24); DockPanel.SetDock(privacy, Dock.Bottom); sidebar.Children.Add(privacy);
        var nav = new StackPanel { Margin = new(12, 0, 12, 0) }; navigation.Clear();
        var routes = new[] { "Dashboard", "Resume", "Job Description", "Analysis", "Recommendations", "Resume Editor", "Comparison", "History", "Settings", "About" };
        string[] glyphs = ["\uE80F", "\uE8A5", "\uE821", "\uE9D9", "\uE73A", "\uE70F", "\uE8A4", "\uE81C", "\uE713", "\uE946"];
        for (int i = 0; i < routes.Length; i++)
        {
            var name = routes[i];
            var b = Button(name, "Nav" + name.Replace(" ", ""), () => Navigate(name));
            var row = new StackPanel { Orientation = Orientation.Horizontal };
             row.Children.Add(new TextBlock { Text = glyphs[i], FontFamily = new("Segoe MDL2 Assets"), FontSize = 16, Foreground = Hex("#AEB4BD"), Width = 28, VerticalAlignment = VerticalAlignment.Center });
             row.Children.Add(new TextBlock { Text = name, Foreground = Hex("#E5E6E9"), FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
            b.Content = row; b.Background = Brushes.Transparent; b.BorderThickness = new(0); b.Margin = new(0, 2, 0, 2); b.HorizontalContentAlignment = HorizontalAlignment.Left; b.Padding = new(13, 10, 8, 10);
            navigation[name] = b; nav.Children.Add(b);
        }
        sidebar.Children.Add(nav); root.Children.Add(sidebar);
        var work = new DockPanel(); Grid.SetColumn(work, 1);
        var top = new DockPanel { Margin = new(0, 0, 0, 0), LastChildFill = true };
        top.SetResourceReference(Panel.BackgroundProperty, "Surface"); AutomationProperties.SetAutomationId(top, "WorkspaceHeader");
        var actions = Row(Button("New analysis", "NewAnalysis", () => _ = NewWorkspaceAsync(), true)); actions.Margin = new(0, 8, 22, 8); DockPanel.SetDock(actions, Dock.Right); top.Children.Add(actions);
        var workspaceLabel = new TextBlock { Text = "Personal workspace   /   Offline", Margin = new(30, 22, 8, 16), FontSize = 13 };
        workspaceLabel.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        top.Children.Add(workspaceLabel); DockPanel.SetDock(top, Dock.Top); work.Children.Add(top);
        var footer = new DockPanel { MinHeight = 40 }; footer.SetResourceReference(Panel.BackgroundProperty, "Surface"); DockPanel.SetDock(progress, Dock.Right); footer.Children.Add(progress);
        status.Margin = new(26, 9, 10, 8); status.FontSize = 12; status.TextWrapping = TextWrapping.Wrap; status.Foreground = Brush("Muted"); AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite); AutomationProperties.SetAutomationId(status, "StatusMessage"); footer.Children.Add(status); DockPanel.SetDock(footer, Dock.Bottom); work.Children.Add(footer);
        work.Children.Add(new ScrollViewer { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new(30, 25, 30, 20) }); root.Children.Add(work); return root;
    }
    internal void Navigate(string destination)
    {
        if (busy) return;
        route = destination;
         foreach (var (name, b) in navigation) b.Background = name == route ? Hex("#2A2D34") : Brushes.Transparent;
        page.Content = route switch
        {
            "Resume" => DocumentPage(true), "Job Description" => DocumentPage(false), "Analysis" => AnalysisPage(), "Recommendations" => RecommendationsPage(), "Resume Editor" => EditorPage(), "Comparison" => ComparisonPage(), "History" => HistoryPage(), "Settings" => SettingsPage(), "About" => AboutPage(), _ => DashboardPage()
        };
        page.UpdateLayout();
        if (SystemParameters.ClientAreaAnimation) page.BeginAnimation(OpacityProperty, new DoubleAnimation(0.4, 1, TimeSpan.FromMilliseconds(140)));
        Notify(Service.IsStale ? "Document changed. Analyze again to refresh the results." : "Your resume and job descriptions are processed locally. No documents are uploaded.");
    }
    private StackPanel Heading(string title, string subtitle)
    {
        var h = Text(title, 29, "Ink", FontWeights.SemiBold); AutomationProperties.SetHeadingLevel(h, AutomationHeadingLevel.Level1);
        return Stack(h, new Border { Child = Text(subtitle, 14, "Muted"), Margin = new(0, 0, 0, 18) });
    }
    public void Notify(string message, bool error = false) { status.Text = message; status.Foreground = Brush(error ? "Danger" : "Muted"); }
    private async Task<bool> Operate(Func<Task> operation, bool showProgress = true, bool redraw = false)
    {
        if (busy) return false;
        var success = false;
        busy = true; saveTimer.Stop(); page.IsEnabled = false; foreach (var n in navigation.Values) n.IsEnabled = false;
        if (showProgress) { progress.Visibility = Visibility.Visible; Notify("Working locally…"); }
        try
        {
            // An unrelated action must not cancel the pending debounced save and silently lose the draft.
            if (dirty) await SaveNowAsync();
            await operation(); success = true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException or DocumentException)
        { Notify(ex.Message, true); }
        catch (Exception) { Notify("The operation could not be completed. Check the document and destination folder, then try again. Your original file was not changed.", true); }
        finally { busy = false; page.IsEnabled = true; foreach (var n in navigation.Values) n.IsEnabled = true; progress.Visibility = Visibility.Collapsed; }
        if (success && redraw) Navigate(route);
        return success;
    }
    internal async Task SaveNowAsync() { saveTimer.Stop(); await Service.SaveAsync(); dirty = false; Notify("Saved locally."); }
    private void DraftChanged(string text, bool resume)
    {
        try { Service.SetText(text, resume); dirty = true; saveTimer.Stop(); saveTimer.Start(); Notify("Unsaved draft — saving locally…"); }
        catch (ArgumentException ex) { Notify(ex.Message, true); }
    }
    internal async Task AnalyzeAsync()
    {
        var success = false;
        await Operate(async () => { await SaveNowAsync(); await Service.AnalyzeAsync(new Progress<string>(s => Notify(s))); success = true; dirty = false; });
        if (success) { editingSections = null; Navigate("Analysis"); Notify($"Analysis saved. Estimated compatibility: {Service.Data.Analysis!.Score}/100."); }
    }
    private void ImportDialog(bool resume)
    {
        if (busy) return;
        var open = new OpenFileDialog { Title = resume ? "Import a resume" : "Import a job description", Filter = "Supported documents|*.pdf;*.docx;*.txt;*.rtf|All files|*.*", CheckFileExists = true, Multiselect = false };
        if (open.ShowDialog(this) == true) _ = ImportPathAsync(open.FileName, resume);
    }
    internal async Task ImportPathAsync(string path, bool resume)
    {
        bool success = false;
        await Operate(async () => { await SaveNowAsync(); await Service.ImportAsync(path, resume); success = true; editingSections = null; });
        if (success) { Navigate(resume ? "Resume" : "Job Description"); Notify("Document imported locally. Verify the extracted text before analyzing."); }
    }
    private void AttachDrop(UIElement element, bool resume)
    {
        element.AllowDrop = true;
        element.PreviewDragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
        element.PreviewDrop += async (_, e) =>
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length == 1) { e.Handled = true; await ImportPathAsync(files[0], resume); }
            else Notify("Drop one supported document at a time.", true);
        };
    }
    private async Task LoadSamplesAsync()
    {
        if (busy) return;
        if ((Service.Data.Resume != null || Service.Data.Job != null) && !Confirm("Replace the current draft with synthetic sample documents? Saved analyses will be kept.")) return;
        if (await Operate(async () =>
        {
            await Service.ImportAsync(Path.Combine(AppContext.BaseDirectory, "Data", "Samples", "resume-strong.txt"), true);
            await Service.ImportAsync(Path.Combine(AppContext.BaseDirectory, "Data", "Samples", "job.txt"), false);
            editingSections = null;
        }))
        {
            Navigate("Dashboard"); Notify("Synthetic sample documents loaded. Run analysis to calculate a real score.");
        }
    }
    private void ExportDialog(bool resume)
    {
        if (busy) return;
        var save = new SaveFileDialog { Title = resume ? "Export your resume" : "Export analysis report", FileName = resume ? "Improved resume" : "Northstar analysis", Filter = resume ? "Word document|*.docx|PDF document|*.pdf|Text document|*.txt" : "PDF report|*.pdf|HTML report|*.html|Text report|*.txt", AddExtension = true, OverwritePrompt = true };
        if (resume) save.FilterIndex = Service.Settings.DefaultExportFormat switch { "PDF" => 2, "TXT" => 3, _ => 1 };
        if (Directory.Exists(Service.Settings.SaveLocation)) save.InitialDirectory = Service.Settings.SaveLocation;
        if (save.ShowDialog(this) == true) _ = Operate(async () => { await SaveNowAsync(); await Service.ExportAsync(save.FileName, resume); Notify("Export saved to " + save.FileName); });
    }
    private bool Confirm(string message) => ConfirmOverride?.Invoke(message) ?? MessageBox.Show(this, message, "Northstar", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
    private async Task NewWorkspaceAsync()
    {
        if (!Confirm("Start a new workspace? Saved analyses remain in History.")) return;
        if (await Operate(async () => { await SaveNowAsync(); await Service.ResetAsync(); editingSections = null; })) Navigate("Resume");
    }
    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (closing) return;
        e.Cancel = true;
        if (busy) { Notify("Wait for the current operation before closing."); return; }
        try { await SaveNowAsync(); closing = true; Close(); }
        catch (Exception) { if (!dirty || Confirm("Your latest changes could not be saved. Exit without saving?")) { closing = true; Close(); } }
    }
}
