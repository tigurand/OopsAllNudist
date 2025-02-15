using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ImGuiNET;
using OopsAllNudist.Utils;
using System;
using System.Numerics;
using static OopsAllNudist.Utils.Constant;

namespace OopsAllNudist.Windows;

internal class WhitelistWindow : Window
{
    private readonly Configuration configuration;

    public WhitelistWindow(Plugin plugin) : base(
        "OopsAllNudist Whitelist",
        ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse)
    {
        Size = new Vector2(220, 300);
        configuration = Service.configuration;
        SizeCondition = ImGuiCond.Always;
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
