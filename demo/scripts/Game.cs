using Godot;

public partial class Game : Control
{
    public override void _Ready()
    {
        var backButton = GetNode<Button>("BackButton");
        backButton.Pressed += OnBackPressed;
    }

    private void OnBackPressed()
    {
        GetTree().ChangeSceneToFile("res://scenes/main_menu.tscn");
    }
}
