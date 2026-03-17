using Godot;

public partial class MainMenu : Control
{
    public override void _Ready()
    {
        var startButton = GetNode<Button>("VBoxContainer/StartButton");
        var quitButton = GetNode<Button>("VBoxContainer/QuitButton");

        startButton.Pressed += OnStartPressed;
        quitButton.Pressed += OnQuitPressed;
    }

    private void OnStartPressed()
    {
        GetTree().ChangeSceneToFile("res://scenes/game.tscn");
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }
}
