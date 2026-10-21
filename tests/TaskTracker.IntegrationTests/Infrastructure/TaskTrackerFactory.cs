using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using TaskTracker.Data;
using TaskTracker.Models;

namespace TaskTracker.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real TaskTracker app in memory (TestServer) for component integration tests.
/// Each instance owns its own temporary SQLite database file, so test classes never share data
/// and never touch a developer's local tasktracker.db.
/// </summary>
public class TaskTrackerFactory : WebApplicationFactory<Program>
{
    // One unique database file per factory instance (= per test class, via IClassFixture).
    public string DatabasePath { get; } =
        Path.Combine(Path.GetTempPath(), $"tasktracker-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the app's SQLite registration (which points at tasktracker.db) ...
            var appDbConfig = services
                .Where(d => d.ServiceType == typeof(IDbContextOptionsConfiguration<AppDbContext>))
                .ToList();

            foreach (var descriptor in appDbConfig)
            {
                services.Remove(descriptor);
            }

            // ... and point the same DbContext at this factory's temporary file instead.
            // A real SQLite file (not the EF in-memory provider), as agreed in the brief.
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite($"Data Source={DatabasePath}"));
        });
    }

    /// <summary>Deletes every task so each test starts from an empty, known state.</summary>
    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Tasks.ExecuteDeleteAsync();
    }

    /// <summary>Inserts known tasks directly into the test database and returns them with their IDs.</summary>
    public async Task<List<WorkTask>> SeedAsync(params WorkTask[] tasks)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Tasks.AddRange(tasks);
        await db.SaveChangesAsync();
        return tasks.ToList();
    }

    /// <summary>Runs a read-only query against the test database to assert what was actually stored.</summary>
    public async Task<T> QueryAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await query(db);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        // Release pooled connections first, otherwise Windows keeps the file locked.
        SqliteConnection.ClearAllPools();

        foreach (var file in new[] { DatabasePath, DatabasePath + "-wal", DatabasePath + "-shm" })
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch (IOException)
            {
                // A leftover temp file is harmless; never fail a test run over cleanup.
            }
        }

        GC.SuppressFinalize(this);
    }
}
