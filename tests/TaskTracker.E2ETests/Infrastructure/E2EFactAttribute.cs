using System.Runtime.CompilerServices;

namespace TaskTracker.E2ETests.Infrastructure;

/// <summary>
/// A [Fact] that only runs when E2E_BASE_URL is set; otherwise it is reported as skipped
/// with an explanation (xUnit v3 conditional skip).
/// </summary>
public sealed class E2EFactAttribute : FactAttribute
{
    public E2EFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        Skip = E2ESettings.NotConfiguredReason;
        SkipType = typeof(E2ESettings);
        SkipUnless = nameof(E2ESettings.IsConfigured);
    }
}
