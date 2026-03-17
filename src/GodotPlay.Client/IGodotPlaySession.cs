using GodotPlay.Protocol;

namespace GodotPlay;

public interface IGodotPlaySession : IAsyncDisposable
{
    NodeLocator Locator(string? path = null, string? className = null, string? namePattern = null);
    Task<SceneTreeResponse> GetSceneTreeAsync(CancellationToken ct = default);
    Task<ScreenshotResponse> ScreenshotAsync(CancellationToken ct = default);
    Task ShutdownAsync(CancellationToken ct = default);
    string CurrentScenePath { get; }
}
