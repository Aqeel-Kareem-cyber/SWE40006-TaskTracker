using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace TaskTracker.IntegrationTests.Infrastructure;

/// <summary>
/// Minimal IWebHostEnvironment so a PageModel can be tested as if it were running in
/// Development or Production (the dashboard's role preview is Development-only).
/// </summary>
public sealed class TestHostEnvironment(string environmentName) : IWebHostEnvironment
{
    public string EnvironmentName { get; set; } = environmentName;
    public string ApplicationName { get; set; } = "TaskTracker";
    public string WebRootPath { get; set; } = string.Empty;
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; } = string.Empty;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
