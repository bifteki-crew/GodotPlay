using GodotPlay.Protocol;

namespace GodotPlay;

public interface IGodotPlaySession : IAsyncDisposable
{
    NodeLocator Locator(string? path = null, string? className = null, string? namePattern = null);
    Task<SceneTreeResponse> GetSceneTreeAsync(string? nodePath = null, int maxDepth = 0, CancellationToken ct = default);
    Task<ScreenshotResponse> ScreenshotAsync(CancellationToken ct = default);
    Task ShutdownAsync(CancellationToken ct = default);
    string CurrentScenePath { get; }

    // Low-Level Input
    Task<ActionResult> MouseMoveAsync(float x, float y, CancellationToken ct = default);
    Task<ActionResult> MouseClickAsync(float x, float y, int button = 1, int clickCount = 1, bool shift = false, bool ctrl = false, bool alt = false, bool meta = false, CancellationToken ct = default);
    Task<ActionResult> MouseDownAsync(float x, float y, int button = 1, bool shift = false, bool ctrl = false, bool alt = false, bool meta = false, CancellationToken ct = default);
    Task<ActionResult> MouseUpAsync(float x, float y, int button = 1, bool shift = false, bool ctrl = false, bool alt = false, bool meta = false, CancellationToken ct = default);
    Task<ActionResult> MouseWheelAsync(float x, float y, float deltaX = 0, float deltaY = 0, CancellationToken ct = default);
    Task<ActionResult> KeyPressAsync(string keyLabel, bool shift = false, bool ctrl = false, bool alt = false, bool meta = false, CancellationToken ct = default);
    Task<ActionResult> KeyDownAsync(string keyLabel, bool shift = false, bool ctrl = false, bool alt = false, bool meta = false, CancellationToken ct = default);
    Task<ActionResult> KeyUpAsync(string keyLabel, bool shift = false, bool ctrl = false, bool alt = false, bool meta = false, CancellationToken ct = default);
    Task<ActionResult> TouchAsync(int index, float x, float y, bool pressed, CancellationToken ct = default);
    Task<ActionResult> TouchDragAsync(float fromX, float fromY, float toX, float toY, int steps = 10, CancellationToken ct = default);
    Task<ActionResult> GestureAsync(string type, float x, float y, float factor = 1, float deltaX = 0, float deltaY = 0, CancellationToken ct = default);
    Task<ActionResult> GamepadButtonAsync(string buttonName, bool pressed = true, int device = 0, CancellationToken ct = default);
    Task<ActionResult> GamepadAxisAsync(string axisName, float value, int device = 0, CancellationToken ct = default);
    Task<ActionResult> ActionPressAsync(string action, float strength = 1, CancellationToken ct = default);
    Task<ActionResult> ActionEventAsync(string action, bool pressed, float strength = 1, CancellationToken ct = default);

    // High-Level Input
    Task<ActionResult> HoverAsync(string nodePath, CancellationToken ct = default);
    Task<ActionResult> DragToAsync(string fromNodePath, string toNodePath, int steps = 10, CancellationToken ct = default);
    Task<ActionResult> ClickNodeAsync(string nodePath, int button = 1, int clickCount = 1, CancellationToken ct = default);
    Task<ActionResult> ScrollNodeAsync(string nodePath, float deltaX = 0, float deltaY = 0, CancellationToken ct = default);
}
