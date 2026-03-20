using GodotPlay;
using GodotPlay.Protocol;

namespace GodotPlay.Tests.Integration;

[TestFixture]
[Category("Integration")]
public class Phase1InputTests
{
    private GodotPlaySession? _session;

    [SetUp]
    public async Task Setup()
    {
        _session = await GodotPlayLauncher.LaunchAsync(new LaunchOptions
        {
            ProjectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "demo")),
            Headless = true,
            Scene = "res://scenes/main_menu.tscn",
            Port = 50051,
            GodotPath = Environment.GetEnvironmentVariable("GODOT_PATH") ?? "godot",
            StartupTimeout = TimeSpan.FromSeconds(30)
        });
    }

    [TearDown]
    public async Task Teardown()
    {
        if (_session != null) await _session.DisposeAsync();
    }

    // --- Low-Level Mouse ---

    [Test]
    public async Task MouseMove_Succeeds()
    {
        var r = await _session!.MouseMoveAsync(500, 300);
        Assert.That(r.Success, Is.True, r.Error);
    }

    [Test]
    public async Task MouseClick_AtCoordinates_Succeeds()
    {
        var r = await _session!.MouseClickAsync(500, 300);
        Assert.That(r.Success, Is.True, r.Error);
    }

    [Test]
    public async Task MouseClick_RightButton_Succeeds()
    {
        var r = await _session!.MouseClickAsync(500, 300, button: 2);
        Assert.That(r.Success, Is.True, r.Error);
    }

    [Test]
    public async Task MouseClick_DoubleClick_Succeeds()
    {
        var r = await _session!.MouseClickAsync(500, 300, clickCount: 2);
        Assert.That(r.Success, Is.True, r.Error);
    }

    [Test]
    public async Task MouseDown_And_MouseUp_Succeeds()
    {
        var down = await _session!.MouseDownAsync(400, 300);
        Assert.That(down.Success, Is.True, down.Error);
        var up = await _session!.MouseUpAsync(400, 300);
        Assert.That(up.Success, Is.True, up.Error);
    }

    [Test]
    public async Task MouseWheel_Succeeds()
    {
        var r = await _session!.MouseWheelAsync(400, 300, deltaY: -3);
        Assert.That(r.Success, Is.True, r.Error);
    }

    // --- Low-Level Keyboard ---

    [Test]
    public async Task KeyPress_Escape_Succeeds()
    {
        var r = await _session!.KeyPressAsync("Escape");
        Assert.That(r.Success, Is.True, r.Error);
    }

    [Test]
    public async Task KeyPress_WithModifiers_Succeeds()
    {
        var r = await _session!.KeyPressAsync("S", ctrl: true);
        Assert.That(r.Success, Is.True, r.Error);
    }

    [Test]
    public async Task KeyDown_And_KeyUp_Succeeds()
    {
        var down = await _session!.KeyDownAsync("Space");
        Assert.That(down.Success, Is.True, down.Error);
        var up = await _session!.KeyUpAsync("Space");
        Assert.That(up.Success, Is.True, up.Error);
    }

    // --- Low-Level Touch ---

    [Test]
    public async Task Touch_Down_And_Up_Succeeds()
    {
        var down = await _session!.TouchAsync(0, 400, 300, pressed: true);
        Assert.That(down.Success, Is.True, down.Error);
        var up = await _session!.TouchAsync(0, 400, 300, pressed: false);
        Assert.That(up.Success, Is.True, up.Error);
    }

    [Test]
    public async Task TouchDrag_Succeeds()
    {
        var r = await _session!.TouchDragAsync(100, 100, 500, 500);
        Assert.That(r.Success, Is.True, r.Error);
    }

    // --- Gestures ---

    [Test]
    public async Task Gesture_Pinch_Succeeds()
    {
        var r = await _session!.GestureAsync("pinch", 400, 300, factor: 1.5f);
        Assert.That(r.Success, Is.True, r.Error);
    }

    [Test]
    public async Task Gesture_Pan_Succeeds()
    {
        var r = await _session!.GestureAsync("pan", 400, 300, deltaX: 50);
        Assert.That(r.Success, Is.True, r.Error);
    }

    // --- Gamepad ---

    [Test]
    public async Task GamepadButton_A_Succeeds()
    {
        var r = await _session!.GamepadButtonAsync("a");
        Assert.That(r.Success, Is.True, r.Error);
    }

    [Test]
    public async Task GamepadAxis_LeftX_Succeeds()
    {
        var r = await _session!.GamepadAxisAsync("left_x", 0.75f);
        Assert.That(r.Success, Is.True, r.Error);
    }

    // --- Input Actions ---

    [Test]
    public async Task ActionPress_UiAccept_Succeeds()
    {
        var r = await _session!.ActionPressAsync("ui_accept");
        Assert.That(r.Success, Is.True, r.Error);
    }

    [Test]
    public async Task ActionEvent_UiCancel_PressAndRelease_Succeeds()
    {
        var press = await _session!.ActionEventAsync("ui_cancel", pressed: true);
        Assert.That(press.Success, Is.True, press.Error);
        var release = await _session!.ActionEventAsync("ui_cancel", pressed: false);
        Assert.That(release.Success, Is.True, release.Error);
    }

    // --- High-Level (Node-based) ---

    [Test]
    public async Task Hover_NodePath_Succeeds()
    {
        var r = await _session!.HoverAsync("/root/MainMenu/VBoxContainer");
        Assert.That(r.Success, Is.True, r.Error);
    }

    [Test]
    public async Task ClickNode_Succeeds()
    {
        var r = await _session!.ClickNodeAsync("/root/MainMenu/VBoxContainer");
        Assert.That(r.Success, Is.True, r.Error);
    }

    [Test]
    public async Task ClickNode_RightClick_Succeeds()
    {
        var r = await _session!.ClickNodeAsync("/root/MainMenu/VBoxContainer", button: 2);
        Assert.That(r.Success, Is.True, r.Error);
    }

    [Test]
    public async Task ClickNode_DoubleClick_Succeeds()
    {
        var r = await _session!.ClickNodeAsync("/root/MainMenu/VBoxContainer", clickCount: 2);
        Assert.That(r.Success, Is.True, r.Error);
    }

    [Test]
    public async Task ScrollNode_Succeeds()
    {
        var r = await _session!.ScrollNodeAsync("/root/MainMenu/VBoxContainer", deltaY: -2);
        Assert.That(r.Success, Is.True, r.Error);
    }
}
