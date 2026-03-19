using GodotPlay;
using GodotPlay.Protocol;

namespace GodotPlay.Tests.Integration;

[TestFixture]
[Category("Integration")]
public class Phase1InputTests
{
    private GodotPlaySession? _session;

    [OneTimeSetUp]
    public async Task Setup()
    {
        _session = await GodotPlaySession.ConnectAsync("http://localhost:50051", TimeSpan.FromSeconds(5));
    }

    [OneTimeTearDown]
    public async Task Teardown()
    {
        if (_session != null) await _session.DisposeAsync();
    }

    // --- Low-Level Mouse ---

    [Test, Order(1)]
    public async Task MouseMove_Succeeds()
    {
        var r = await _session!.MouseMoveAsync(500, 300);
        Assert.That(r.Success, Is.True, r.Error);
    }

    [Test, Order(2)]
    public async Task MouseClick_AtCoordinates_Succeeds()
    {
        var r = await _session!.MouseClickAsync(500, 300);
        Assert.That(r.Success, Is.True, r.Error);
    }

    [Test, Order(3)]
    public async Task MouseClick_RightButton_Succeeds()
    {
        var r = await _session!.MouseClickAsync(500, 300, button: 2);
        Assert.That(r.Success, Is.True, r.Error);
    }

    [Test, Order(4)]
    public async Task MouseClick_DoubleClick_Succeeds()
    {
        var r = await _session!.MouseClickAsync(500, 300, clickCount: 2);
        Assert.That(r.Success, Is.True, r.Error);
    }

    [Test, Order(5)]
    public async Task MouseDown_And_MouseUp_Succeeds()
    {
        var down = await _session!.MouseDownAsync(400, 300);
        Assert.That(down.Success, Is.True, down.Error);
        var up = await _session!.MouseUpAsync(400, 300);
        Assert.That(up.Success, Is.True, up.Error);
    }

    [Test, Order(6)]
    public async Task MouseWheel_Succeeds()
    {
        var r = await _session!.MouseWheelAsync(400, 300, deltaY: -3);
        Assert.That(r.Success, Is.True, r.Error);
    }

    // --- Low-Level Keyboard ---

    [Test, Order(10)]
    public async Task KeyPress_Escape_Succeeds()
    {
        var r = await _session!.KeyPressAsync("Escape");
        Assert.That(r.Success, Is.True, r.Error);
    }

    [Test, Order(11)]
    public async Task KeyPress_WithModifiers_Succeeds()
    {
        var r = await _session!.KeyPressAsync("S", ctrl: true);
        Assert.That(r.Success, Is.True, r.Error);
    }

    [Test, Order(12)]
    public async Task KeyDown_And_KeyUp_Succeeds()
    {
        var down = await _session!.KeyDownAsync("Space");
        Assert.That(down.Success, Is.True, down.Error);
        var up = await _session!.KeyUpAsync("Space");
        Assert.That(up.Success, Is.True, up.Error);
    }

    // --- Low-Level Touch ---

    [Test, Order(20)]
    public async Task Touch_Down_And_Up_Succeeds()
    {
        var down = await _session!.TouchAsync(0, 400, 300, pressed: true);
        Assert.That(down.Success, Is.True, down.Error);
        var up = await _session!.TouchAsync(0, 400, 300, pressed: false);
        Assert.That(up.Success, Is.True, up.Error);
    }

    [Test, Order(21)]
    public async Task TouchDrag_Succeeds()
    {
        var r = await _session!.TouchDragAsync(100, 100, 500, 500);
        Assert.That(r.Success, Is.True, r.Error);
    }

    // --- Gestures ---

    [Test, Order(30)]
    public async Task Gesture_Pinch_Succeeds()
    {
        var r = await _session!.GestureAsync("pinch", 400, 300, factor: 1.5f);
        Assert.That(r.Success, Is.True, r.Error);
    }

    [Test, Order(31)]
    public async Task Gesture_Pan_Succeeds()
    {
        var r = await _session!.GestureAsync("pan", 400, 300, deltaX: 50);
        Assert.That(r.Success, Is.True, r.Error);
    }

    // --- Gamepad ---

    [Test, Order(40)]
    public async Task GamepadButton_A_Succeeds()
    {
        var r = await _session!.GamepadButtonAsync("a");
        Assert.That(r.Success, Is.True, r.Error);
    }

    [Test, Order(41)]
    public async Task GamepadAxis_LeftX_Succeeds()
    {
        var r = await _session!.GamepadAxisAsync("left_x", 0.75f);
        Assert.That(r.Success, Is.True, r.Error);
    }

    // --- Input Actions ---

    [Test, Order(50)]
    public async Task ActionPress_UiAccept_Succeeds()
    {
        var r = await _session!.ActionPressAsync("ui_accept");
        Assert.That(r.Success, Is.True, r.Error);
    }

    [Test, Order(51)]
    public async Task ActionEvent_UiCancel_PressAndRelease_Succeeds()
    {
        var press = await _session!.ActionEventAsync("ui_cancel", pressed: true);
        Assert.That(press.Success, Is.True, press.Error);
        var release = await _session!.ActionEventAsync("ui_cancel", pressed: false);
        Assert.That(release.Success, Is.True, release.Error);
    }

    // --- High-Level (Node-based) ---

    [Test, Order(60)]
    public async Task Hover_NodePath_Succeeds()
    {
        var r = await _session!.HoverAsync("/root/StartScreen/Menu");
        Assert.That(r.Success, Is.True, r.Error);
    }

    [Test, Order(61)]
    public async Task ClickNode_Succeeds()
    {
        var r = await _session!.ClickNodeAsync("/root/StartScreen/Menu");
        Assert.That(r.Success, Is.True, r.Error);
    }

    [Test, Order(62)]
    public async Task ClickNode_RightClick_Succeeds()
    {
        var r = await _session!.ClickNodeAsync("/root/StartScreen/Menu", button: 2);
        Assert.That(r.Success, Is.True, r.Error);
    }

    [Test, Order(63)]
    public async Task ClickNode_DoubleClick_Succeeds()
    {
        var r = await _session!.ClickNodeAsync("/root/StartScreen/Menu", clickCount: 2);
        Assert.That(r.Success, Is.True, r.Error);
    }

    [Test, Order(64)]
    public async Task ScrollNode_Succeeds()
    {
        var r = await _session!.ScrollNodeAsync("/root/StartScreen/Menu", deltaY: -2);
        Assert.That(r.Success, Is.True, r.Error);
    }

    // --- Screenshot after all input to verify game didn't crash ---

    [Test, Order(99)]
    public async Task Screenshot_AfterAllInput_StillWorks()
    {
        var screenshot = await _session!.ScreenshotAsync();
        Assert.That(screenshot.PngData, Is.Not.Empty);
        Assert.That(screenshot.Width, Is.GreaterThan(0));
    }
}
