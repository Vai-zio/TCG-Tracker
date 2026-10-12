using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TCGTracker.App.ViewModels;
using TCGTracker.App.Views;
using TCGTracker.Data;
using TCGTracker.Data.Services;

namespace TCGTracker.App;

/// <summary>
/// The "composition root": the one place that wires the layers together.
/// Data -> service -> ViewModel -> View. Nothing else in the app calls "new" on these.
/// </summary>
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // The database lives in %LocalAppData%\TCGTracker, outside the install folder.
        var dataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TCGTracker");
        Directory.CreateDirectory(dataFolder);
        var dbPath = Path.Combine(dataFolder, "collection.db");

        var options = new DbContextOptionsBuilder<TcgTrackerDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        var contextFactory = new PooledDbContextFactory<TcgTrackerDbContext>(options);

        // Create the database, or upgrade it to the latest migration.
        try
        {
            await using var db = await contextFactory.CreateDbContextAsync();
            await db.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open the database at {dbPath}.\n\n{ex.Message}", "TCG Tracker");
            Shutdown(1);
            return;
        }

        var viewModel = new MainViewModel(new SqliteCollectionService(contextFactory));
        var window = new MainWindow { DataContext = viewModel };
        window.Show();

        await viewModel.LoadAsync();
    }
}
