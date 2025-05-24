#if TOOLS
using Godot;
using System;
using System.Linq;

[Tool]
public partial class GdexToCS : EditorPlugin
{
    public override void _EnterTree()
    {
        GD.Print("GdexToCS : 加载完成");

        var classList = ClassDB.GetClassList();
        var godotBuiltinTypeNames = classList
            .Where(x => ClassDB.ClassGetApiType(x) is ClassDB.ApiType.Core or ClassDB.ApiType.Editor).ToHashSet();
        var gdeClassTypes = classList
            .Where(x => ClassDB.ClassGetApiType(x) is ClassDB.ApiType.Extension or ClassDB.ApiType.EditorExtension).ToArray();
        GD.Print("已经加载的插件的自定义gdex类: ");
        foreach (var item in gdeClassTypes)
        {
            GD.Print(item);
        }
    }

	public override void _ExitTree()
	{
		GD.Print("GdexToCS : 卸载完成");
	}

    public override void _Ready()
    {
    }
}

#endif
