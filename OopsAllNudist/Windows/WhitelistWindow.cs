using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using OopsAllNudist.Utils;
using System.Numerics;

namespace OopsAllNudist.Windows;

internal class WhitelistWindow : Window
{
    private readonly Configuration configuration;

    public WhitelistWindow(Plugin plugin) : base(
        "OopsAllNudist Whitelist")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(220, 300),
            MaximumSize = new Vector2(9999, 9999)
        };
        Size = new Vector2(220, 300);
        configuration = Service.configuration;
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        ImGui.Text("Click a name to remove it.");
        ImGui.Separator();

        foreach (var charName in Service.configuration.Whitelist)
        {
            if (ImGui.Selectable(charName))
            {
                configuration.RemoveFromWhitelist(charName);
                configuration.Save();
                Service.configWindow.ReloadCharProxy(charName);
            }
        }
    }
}
