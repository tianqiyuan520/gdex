using Godot;
using System;
using System.Linq;

public partial class GdExample : Node
{
    [Export] public Button button;
    public override void _Ready() 
    {
        Call("a");

        button.Pressed += _press;
    } 

    public void _press()
    {
        var classList = ClassDB.GetClassList();
        var godotBuiltinTypeNames = classList
            .Where(x => ClassDB.ClassGetApiType(x) is ClassDB.ApiType.Core or ClassDB.ApiType.Editor).ToHashSet();
        var gdeClassTypes = classList
            .Where(x => ClassDB.ClassGetApiType(x) is ClassDB.ApiType.Extension or ClassDB.ApiType.EditorExtension).ToArray();

        //GD.Print("原版: ");
        //foreach (var item in godotBuiltinTypeNames)
        //{
        //    GD.Print(item);
        //}
        string mycpp = ""; 
        GD.Print("插件: ");
        foreach (var item in gdeClassTypes)
        {
            mycpp = item.ToString();
            GD.Print(item);
        }

        GD.Print(GDExtensionManager.GetLoadedExtensions());
        GD.Print(ClassDB.ClassGetMethodList(mycpp));
        GD.Print("\nProperty");
        GD.Print(ClassDB.ClassGetPropertyList(mycpp));
    }

}



