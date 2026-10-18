using Microsoft.EntityFrameworkCore;
using TaskTracker.Data;

var builder = WebApplication.CreateBuilder(args);

// Register Razor Pages
builder.Services.AddRazorPages();

// Register application health checks.
// The "database" check runs a real query against the Tasks table, so /health
// reports Unhealthy when the SQLite file or its schema is unavailable.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(
        name: "database",
        customTestQuery: async (db, cancellationToken) =>
        {
            await db.Tasks.AnyAsync(cancellationToken);
            return true;
        });

// Configure SQLite database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

var app = builder.Build();

// Apply database migrations automatically at startup
// Suitable for our current single-instance project deployment
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<AppDbContext>();

    db.Database.Migrate();
}

// Configure HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

// Static files
app.MapStaticAssets();

// Razor Pages
app.MapRazorPages()
   .WithStaticAssets();

// Health check endpoint
app.MapHealthChecks("/health");

// Release identification: the commit SHA baked into the image at build time
// (APP_VERSION), used by the pipeline to confirm which release is running.
app.MapGet("/version", (IConfiguration config, IWebHostEnvironment env) =>
    Results.Json(new
    {
        commit = config["APP_VERSION"] ?? "dev",
        environment = env.EnvironmentName
    }));

// Start application
app.Run();