using System.IO;
using System.Windows;
using ResumeAnalyzer.Application;
using ResumeAnalyzer.Core;
using ResumeAnalyzer.Documents;
using ResumeAnalyzer.Persistence;

namespace ResumeAnalyzer.App;
public partial class App : System.Windows.Application
{
    private LocalStorage? storage;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show("The operation could not be completed. Your original documents are unchanged. Save your work and restart if the problem continues.", "Northstar", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };
        var qa = e.Args.Contains("--ui-test");
        var index = Array.IndexOf(e.Args, "--data-dir");
        var dataDir = index >= 0 && e.Args.Length > index + 1 ? e.Args[index + 1] : null;
        if (qa && dataDir == null) dataDir = Path.Combine(Path.GetTempPath(), "Northstar-UI-" + Guid.NewGuid().ToString("N"));
        var documents = new DocumentService();
        WorkspaceService controller;
        try
        {
            storage = new LocalStorage(dataDir);
            controller = new WorkspaceService(new OfflineAnalyzer(), documents, documents, storage);
            await controller.LoadAsync();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            // Do not open an empty editor that could overwrite unreadable data (and its backup) on close.
            if (!qa) MessageBox.Show(ex.Message + "\n\nNorthstar will close without saving. Back up the local data folder before restoring a .bak copy or moving the unreadable file aside.", "Northstar — local data could not be loaded", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown(1);
            return;
        }
        var window = new MainWindow(controller);
        MainWindow = window;
        window.Show();
        if (qa)
        {
            var reportIndex = Array.IndexOf(e.Args, "--report");
            var report = reportIndex >= 0 && e.Args.Length > reportIndex + 1 ? e.Args[reportIndex + 1] : Path.Combine(AppContext.BaseDirectory, "ui-results.json");
            var exit = await NativeUiTests.RunAsync(window, controller, documents, report);
            Shutdown(exit);
        }
    }
    protected override void OnExit(ExitEventArgs e) { storage?.Dispose(); base.OnExit(e); }
}
