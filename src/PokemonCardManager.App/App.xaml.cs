using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PokemonCardManager.App.Dialogs;
using PokemonCardManager.App.Navigation;
using PokemonCardManager.App.Theming;
using PokemonCardManager.App.ViewModels;
using PokemonCardManager.App.Views;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Infrastructure;
using PokemonCardManager.Infrastructure.Options;
using Serilog;

namespace PokemonCardManager.App;

/// <summary>
/// The composition root: the one place that builds the app.
/// The .NET Generic Host gives us configuration (appsettings.json), logging and
/// dependency injection; then we prepare the database and show the main window.
/// </summary>
public partial class App : Application
{
    private IHost? _host;
    private Mutex? _singleInstance;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // One copy at a time: two apps writing the same database and photo folder could step on each other.
        _singleInstance = new Mutex(initiallyOwned: true, @"Local\PokemonCardManager", out var isFirst);
        if (!isFirst)
        {
            // A restart after a restore can briefly overlap with the closing copy, so wait a moment before giving up.
            if (!WaitForOtherCopy(_singleInstance))
            {
                MessageBox.Show("Pokémon Card Manager is already running.", "Pokémon Card Manager", MessageBoxButton.OK, MessageBoxImage.Information);
                _singleInstance = null;
                Shutdown(0);
                return;
            }
        }

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = e.Args,
            ContentRootPath = AppContext.BaseDirectory, // so appsettings.json is found next to the .exe
        });

        // Logs go to rolling daily files next to the database, kept for 14 days.
        var storage = builder.Configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new();
        var logFile = Path.Combine(storage.ResolveDataDirectory(), "logs", "app-.log");
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(new LoggerConfiguration()
            .MinimumLevel.Verbose() // filtering is done by the "Logging" section in appsettings.json
            .WriteTo.File(logFile, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
            .CreateLogger(), dispose: true);

        builder.Services.AddSingleton<PokemonCardManager.Core.Identification.IOcrEngine, Ocr.WindowsOcrEngine>(); // before AddInfrastructure, which only adds a fallback
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddTransient<UnlockViewModel>();
        builder.Services.AddTransient<UnlockWindow>();
        builder.Services.AddSingleton<IDialogService, DialogService>();
        builder.Services.AddSingleton<NavigationService>();
        builder.Services.AddSingleton<ThemeService>();
        builder.Services.AddSingleton<ShellViewModel>();
        builder.Services.AddSingleton<ShellWindow>();
        builder.Services.AddSingleton<CollectionViewModel>(); // singleton: keeps filters between visits
        builder.Services.AddTransient<CardEditorViewModel>();  // transient: a fresh form each time
        builder.Services.AddTransient<StorageLocationsViewModel>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<InspectionViewModel>();
        builder.Services.AddTransient<MarketViewModel>();
        builder.Services.AddTransient<SubmissionsViewModel>();
        builder.Services.AddTransient<ReportsViewModel>();
        builder.Services.AddTransient<BackupViewModel>();
        builder.Services.AddTransient<IdentifyViewModel>(); // leaving with unadded matches asks first

        _host = builder.Build();
        var logger = _host.Services.GetRequiredService<ILogger<App>>();
        DispatcherUnhandledException += (_, args) => OnUnhandledException(logger, args);
        // Errors outside the UI thread: logged, so nothing fails silently.
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            logger.LogError(args.Exception, "Unobserved background task exception");
            args.SetObserved();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            logger.LogCritical(args.ExceptionObject as Exception, "Fatal unhandled exception");

        try
        {
            await _host.StartAsync();
            await _host.Services.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
            await _host.Services.GetRequiredService<ThemeService>().LoadAsync(); // before the window shows, so it never flashes the wrong theme
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Startup failed");
            MessageBox.Show($"The app could not start.\n\n{ex.Message}\n\nDetails are in {Path.GetDirectoryName(logFile)}. " +
                            $"Backups are in {Path.Combine(storage.ResolveDataDirectory(), "backups")} unless Backup:Directory says otherwise.",
                "Pokémon Card Manager", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        // Lock screen before anything else is shown. Closing it without unlocking quits the app.
        if (!Unlock())
        {
            Shutdown(0);
            return;
        }

        // Weekly safety net. A failure here must not stop the app, so it's logged and skipped.
        try
        {
            await _host.Services.GetRequiredService<IBackupService>().CreateAutomaticIfDueAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Automatic backup failed");
        }

        var window = _host.Services.GetRequiredService<ShellWindow>();
        MainWindow = window;
        ShutdownMode = ShutdownMode.OnMainWindowClose; // back to normal now the real main window exists
        window.Show();
        await _host.Services.GetRequiredService<NavigationService>().NavigateToAsync<DashboardViewModel>();
        logger.LogInformation("Started, database at {DatabasePath}",
            _host.Services.GetRequiredService<IOptions<StorageOptions>>().Value.ResolveDatabasePath());
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose(); // flushes log files
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); // closes the database file cleanly
        _singleInstance?.ReleaseMutex();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    /// <summary>Shows the create/unlock screen. Returns true once the vault holds the data key.</summary>
    private bool Unlock()
    {
        // WPF treats the first window it opens as the main window. With OnMainWindowClose,
        // closing this lock screen would end the app before the real window opens, so
        // shutdown is made explicit until the shell takes over.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var unlockWindow = _host!.Services.GetRequiredService<UnlockWindow>();
        return unlockWindow.ShowDialog() == true;
    }

    private static bool WaitForOtherCopy(Mutex mutex)
    {
        try
        {
            return mutex.WaitOne(TimeSpan.FromSeconds(10));
        }
        catch (AbandonedMutexException)
        {
            return true; // the other copy crashed without releasing it; we own it now
        }
    }

    private static void OnUnhandledException(Microsoft.Extensions.Logging.ILogger logger, DispatcherUnhandledExceptionEventArgs args)
    {
        logger.LogError(args.Exception, "Unhandled UI exception");
        MessageBox.Show($"Something went wrong: {args.Exception.Message}", "Pokémon Card Manager",
            MessageBoxButton.OK, MessageBoxImage.Error);
        args.Handled = true; // keep the app running; the error is logged
    }
}
