#if TOOLS
using Godot;

namespace GodotPlay.Plugin;

[Tool]
public partial class GodotPlayPlugin : EditorPlugin
{
    public override void _EnterTree()
    {
        AddAutoloadSingleton("GodotPlayServer", "res://addons/godotplay/GodotPlayServer.cs");
        GD.Print("[GodotPlay] Plugin enabled — GodotPlayServer autoload registered.");
    }

    public override void _ExitTree()
    {
        RemoveAutoloadSingleton("GodotPlayServer");
        GD.Print("[GodotPlay] Plugin disabled — GodotPlayServer autoload removed.");
    }
}
#endif
