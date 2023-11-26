using Dalamud.Interface.Windowing;
using ImGuiNET;
using OopsAllLalafellsSRE.Utils;
using System;
using System.Numerics;
using System.Reflection;
using static OopsAllLalafellsSRE.Windows.Constant;

namespace OopsAllLalafellsSRE.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private readonly string[] race = ["Lalafell", "Hyur", "Elezen", "Miqo'te", "Roegadyn", "Au Ra", "Hrothgar", "Viera"];
    private int selectedRaceIndex = 0;
    public event Action? OnConfigChanged;

    public ConfigWindow(Plugin plugin) : base(
        "OopsAllLalafellsSRE Configuration Window",
        ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoScrollWithMouse)
    {
        Size = new Vector2(350, 275);
        SizeCondition = ImGuiCond.Always;

        configuration = Service.configuration;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public override void Draw()
    {
        // select race
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Target Race");
        ImGui.SameLine();
        if (ImGui.Combo("Race", ref selectedRaceIndex, race, race.Length))
        {
            configuration.SelectedRace = MapIndexToRace(selectedRaceIndex);
            configuration.Save();
            OnConfigChanged?.Invoke();
        }

        // Change Self
        bool _ChangeSelf = configuration.changeSelf;
        if (ImGui.Checkbox("Change Self", ref _ChangeSelf))
        {
            configuration.changeSelf = _ChangeSelf;
            configuration.Save();
            OnConfigChanged?.Invoke();
        }

        // Change Others
        bool _ChangeOthers = configuration.changeOthers;
        if (ImGui.Checkbox("Change Others", ref _ChangeOthers))
        {
            configuration.changeOthers = _ChangeOthers;
            configuration.Save();
            OnConfigChanged?.Invoke();
        }

        // Immersive Mode
        bool _ImmersiveMode = configuration.immersiveMode;
        if (ImGui.Checkbox("Immersive Mode", ref _ImmersiveMode))
        {
            if (_ImmersiveMode && !configuration.immersiveMode)
            {
                // Open the popup only if the option is being enabled
                ImGui.OpenPopup("Confirm Immersive Mode");
            }
            else if (!_ImmersiveMode)
            {
                // Directly update the configuration if the option is being disabled
                configuration.immersiveMode = false;
                configuration.Save();
                OnConfigChanged?.Invoke();
            }
        }
        ImGui.Text("Note: If Immersive Mode is enabled, \"Examine\" windows\n will also be modified.");

        // Confirm window for immersive mode
        if (ImGui.BeginPopupModal("Confirm Immersive Mode", ref _ImmersiveMode, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("Enabling Immersive Mode may cause crashes on launch. Are you sure?");
            if (ImGui.Button("Yes"))
            {
                // Update the configuration when confirmed
                configuration.immersiveMode = true;
                configuration.Save();
                OnConfigChanged?.Invoke();
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("No")) { ImGui.CloseCurrentPopup(); }
            ImGui.EndPopup();
        }
    }
    private static Race MapIndexToRace(int index)
    {
        return index switch
        {
            0 => Race.LALAFELL,
            1 => Race.HYUR,
            2 => Race.ELEZEN,
            3 => Race.MIQOTE,
            4 => Race.ROEGADYN,
            5 => Race.AU_RA,
            6 => Race.HROTHGAR,
            7 => Race.VIERA,
            _ => Race.LALAFELL,
        };
    }
}
