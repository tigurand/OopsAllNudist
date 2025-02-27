using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ImGuiNET;
using OopsAllNudist.Utils;
using System;
using System.Numerics;
using static OopsAllNudist.Utils.Constant;

namespace OopsAllNudist.Windows;

internal class ConfigWindow : Window
{
    private readonly Configuration configuration;
    private readonly string[] race = ["Lalafell", "Hyur", "Elezen", "Miqo'te", "Roegadyn", "Au Ra", "Hrothgar", "Viera", "Keep Original Race"];
    private readonly string[] gender = ["Female", "Male", "Keep Original Sex"];
    private readonly string[] clan = ["0", "1", "Automatic Clan"];
    private int selectedRaceIndex = 0;
    private int selectedGenderIndex = 0;
    private int selectedClanIndex = 0;
    public event Action<bool>? OnConfigChanged;
    public event Action<string>? OnConfigChangedSingleChar;

    // proxy function so whitelist editor can reload a character too
    internal void ReloadCharProxy(string charName)
    {
        OnConfigChangedSingleChar?.Invoke(charName);
    }

    public ConfigWindow(Plugin plugin) : base(
        "OopsAllNudist Configuration Window",
        ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoScrollWithMouse)
    {
        Size = new Vector2(480, 560);
        SizeCondition = ImGuiCond.Always;

        configuration = Service.configuration;

        selectedRaceIndex = configuration.SelectedRace switch
        {
            Race.LALAFELL => 0,
            Race.HYUR => 1,
            Race.ELEZEN => 2,
            Race.MIQOTE => 3,
            Race.ROEGADYN => 4,
            Race.AU_RA => 5,
            Race.HROTHGAR => 6,
            Race.VIERA => 7,
            _ => 8
        };

        selectedGenderIndex = configuration.SelectedGender switch
        {
            Gender.FEMALE => 0,
            Gender.MALE => 1,
            _ => 2
        };

        selectedClanIndex = configuration.SelectedClan switch
        {
            Clan.CLAN0 => 0,
            Clan.CLAN1 => 1,
            _ => 2
        };
    }

    private static void Tooltip(string text)
    {
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted(text);
            ImGui.EndTooltip();
        }
    }

    public override void Draw()
    {
        // select race
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Target Race");
        ImGui.SameLine();
        ImGui.SetCursorPosX(80.0f);
        if (ImGui.Combo("###Race", ref selectedRaceIndex, race, race.Length))
        {
            configuration.SelectedRace = MapIndexToRace(selectedRaceIndex);
            if (configuration.SelectedRace == Race.UNKNOWN)
            {
                configuration.SelectedClan = Clan.UNKNOWN;
                selectedClanIndex = 2;
            }
            configuration.Save();
            if (configuration.enabled)
                InvokeConfigChanged();
        }
        ImGui.TextUnformatted("Target Sex");
        ImGui.SameLine();
        ImGui.SetCursorPosX(80.0f);
        if (ImGui.Combo("###Sex", ref selectedGenderIndex, gender, gender.Length))
        {
            configuration.SelectedGender = MapIndexToGender(selectedGenderIndex);
            configuration.Save();
            if (configuration.enabled)
                InvokeConfigChanged();
        }
        ImGui.BeginDisabled(selectedRaceIndex == 8);
        ImGui.TextUnformatted("Target Clan");
        Tooltip("e.g. 0 = Midlander, 1 = Highlander");
        ImGui.SameLine();
        ImGui.SetCursorPosX(80.0f);
        if (ImGui.Combo("###Clan", ref selectedClanIndex, clan, clan.Length))
        {
            configuration.SelectedClan = MapIndexToClan(selectedClanIndex);
            configuration.Save();
            if (configuration.enabled)
                InvokeConfigChanged();
        }
        ImGui.EndDisabled();

        // Enabled
        bool _Enabled = configuration.enabled;
        if (ImGui.Checkbox("Enable", ref _Enabled))
        {
            configuration.enabled = _Enabled;
            configuration.Save();
            InvokeConfigChanged();
        }

        ImGui.BeginDisabled(!configuration.enabled);
        ImGui.SameLine();
        ImGui.SetCursorPosX(150.0f);
        bool _StayOn = configuration.stayOn;
        if (ImGui.Checkbox("Stay on", ref _StayOn))
        {
            configuration.stayOn = _StayOn;
            configuration.Save();
        }
        Tooltip("Enable when plugin loads.");
        ImGui.EndDisabled();

        ImGui.Separator();
        ImGui.Text("Apply clothing changes to:");
        ImGui.SetCursorPosX(20.0f);
        bool _StripSelf = !configuration.dontStripSelf;
        if (ImGui.Checkbox("Self##dontStripSelf", ref _StripSelf))
        {
            configuration.dontStripSelf = !_StripSelf;
            configuration.Save();
            if (configuration.enabled && Service.clientState.LocalPlayer != null)
                OnConfigChangedSingleChar?.Invoke(Service.clientState.LocalPlayer.Name.TextValue);
        }

        ImGui.SameLine();
        ImGui.SetCursorPosX(100.0f);
        bool _StripPC = !configuration.dontStripPC;
        if (ImGui.Checkbox("PCs##dontStripPC", ref _StripPC))
        {
            configuration.dontStripPC = !_StripPC;
            configuration.Save();
            if (configuration.enabled)
                InvokeConfigChanged(true);
        }

        ImGui.SameLine();
        ImGui.SetCursorPosX(180.0f);
        bool _StripNPC = !configuration.dontStripNPC;
        if (ImGui.Checkbox("NPCs##dontStripNPC", ref _StripNPC))
        {
            configuration.dontStripNPC = !_StripNPC;
            configuration.Save();
            if (configuration.enabled)
                InvokeConfigChanged(true);
        }

        ImGui.Text("Apply race/sex changes to:");
        ImGui.SetCursorPosX(20.0f);
        bool _LalaSelf = !configuration.dontLalaSelf;
        if (ImGui.Checkbox("Self##dontLalaSelf", ref _LalaSelf))
        {
            configuration.dontLalaSelf = !_LalaSelf;
            configuration.Save();
            if (configuration.enabled && Service.clientState.LocalPlayer != null)
                OnConfigChangedSingleChar?.Invoke(Service.clientState.LocalPlayer.Name.TextValue);
        }

        ImGui.SameLine();
        ImGui.SetCursorPosX(100.0f);
        bool _LalaPC = !configuration.dontLalaPC;
        if (ImGui.Checkbox("PCs##dontLalaPC", ref _LalaPC))
        {
            configuration.dontLalaPC = !_LalaPC;
            configuration.Save();
            if (configuration.enabled)
                InvokeConfigChanged(true);
        }

        ImGui.SameLine();
        ImGui.SetCursorPosX(180.0f);
        bool _LalaNPC = !configuration.dontLalaNPC;
        if (ImGui.Checkbox("NPCs##dontLalaNPC", ref _LalaNPC))
        {
            configuration.dontLalaNPC = !_LalaNPC;
            configuration.Save();
            if (configuration.enabled)
                InvokeConfigChanged(true);
        }
        Tooltip("Warning: Altering NPCs will cause animation issues.");

        ImGui.Text("Turn children into adults?");
        ImGui.SetCursorPosX(20.0f);
        bool _noChild = configuration.noChild;
        if (ImGui.Checkbox("No Children", ref _noChild))
        {
            configuration.noChild = _noChild;
            configuration.Save();
            if (configuration.enabled)
                InvokeConfigChanged(true);
        }
		Tooltip("Warning: Enabling this will cause animation issues.");

        ImGui.SameLine();
        ImGui.SetCursorPosX(180.0f);
        bool _childClothes = configuration.childClothes;
        if (ImGui.Checkbox("Children's Clothing", ref _childClothes))
        {
            configuration.childClothes = _childClothes;
            configuration.Save();
            if (configuration.enabled)
                InvokeConfigChanged(true);
        }

        ImGui.Text("Turn Lalafells into other races?");
        ImGui.SetCursorPosX(20.0f);
        bool _noLala = configuration.noLala;
        if (ImGui.Checkbox("No Lalafells", ref _noLala))
        {
            configuration.noLala = _noLala;
            configuration.Save();
            if (configuration.enabled)
                InvokeConfigChanged(true);
        }
        Tooltip("Warning: Enabling this will turn all Lalafells into other races, including NPCs.");

        ImGui.Separator();
        bool _stripHats = configuration.stripHats;
        if (ImGui.Checkbox("Strip Hats", ref _stripHats))
        {
            configuration.stripHats = _stripHats;
            configuration.Save();
            if (configuration.enabled)
                InvokeConfigChanged();
        }

        bool _stripBodies = configuration.stripBodies;
        if (ImGui.Checkbox("Strip Bodies", ref _stripBodies))
        {
            configuration.stripBodies = _stripBodies;
            configuration.Save();
            if (configuration.enabled)
                InvokeConfigChanged();
        }

        bool _stripLegs = configuration.stripLegs;
        if (ImGui.Checkbox("Strip Legs", ref _stripLegs))
        {
            configuration.stripLegs = _stripLegs;
            if (configuration.stripLegs == false)
            {
                configuration.empLegs = false;
                configuration.empLegsRandom = false;
                configuration.empLegsRandomSelf = false;
            }
            configuration.Save();
            if (configuration.enabled)
                InvokeConfigChanged();
        }

        ImGui.BeginDisabled(!configuration.stripLegs);
        ImGui.SameLine();
        ImGui.SetCursorPosX(120.0f);
        bool _empLegs = configuration.empLegs;
        if (ImGui.Checkbox("Emperor's", ref _empLegs))
        {
            configuration.empLegs = _empLegs;
            if (configuration.empLegs == false)
            {
                configuration.empLegsRandom = false;
                configuration.empLegsRandomSelf = false;
            }
            configuration.Save();
            if (configuration.enabled)
                InvokeConfigChanged();
        }
        Tooltip("Use Emperor's New Legs.");
        ImGui.EndDisabled();

        ImGui.BeginDisabled(!configuration.empLegs);
        ImGui.SameLine();
        ImGui.SetCursorPosX(240.0f);
        bool _empLegsRandom = configuration.empLegsRandom;
        if (ImGui.Checkbox("Random", ref _empLegsRandom))
        {
            configuration.empLegsRandom = _empLegsRandom;
            if (configuration.empLegsRandom == false)
                configuration.empLegsRandomSelf = false;
            configuration.Save();
            if (configuration.enabled)
                InvokeConfigChanged();
        }
        Tooltip("Randomly Use Emperor's New Legs.");
        ImGui.EndDisabled();

        ImGui.BeginDisabled(!configuration.empLegsRandom);
        ImGui.SameLine();
        ImGui.SetCursorPosX(360.0f);
        bool _empLegsRandomSelf = configuration.empLegsRandomSelf;
        if (ImGui.Checkbox("Random Self", ref _empLegsRandomSelf))
        {
            configuration.empLegsRandomSelf = _empLegsRandomSelf;
            configuration.Save();
            if (configuration.enabled)
                InvokeConfigChanged();
        }
        Tooltip("Randomize self.");
        ImGui.EndDisabled();

        bool _stripGloves = configuration.stripGloves;
        if (ImGui.Checkbox("Strip Gloves", ref _stripGloves))
        {
            configuration.stripGloves = _stripGloves;
            configuration.Save();
            if (configuration.enabled)
                InvokeConfigChanged();
        }

        bool _stripBoots = configuration.stripBoots;
        if (ImGui.Checkbox("Strip Boots", ref _stripBoots))
        {
            configuration.stripBoots = _stripBoots;
            configuration.Save();
            if (configuration.enabled)
                InvokeConfigChanged();
        }

        bool _stripAccessories = configuration.stripAccessories;
        if (ImGui.Checkbox("Strip Accessories", ref _stripAccessories))
        {
            configuration.stripAccessories = _stripAccessories;
            configuration.Save();
            if (configuration.enabled)
                InvokeConfigChanged();
        }

        ImGui.Separator();

        string? targetName = Service.targetManager.Target?.Name.TextValue;
        bool targetIsWhitelisted = !targetName.IsNullOrEmpty() && configuration.IsWhitelisted(targetName);

        ImGui.BeginDisabled(targetName.IsNullOrEmpty());

        try
        {
            if (!targetIsWhitelisted)
            {
                if (ImGui.Button($"Add {targetName ?? "Target"} to Whitelist"))
                {
                    configuration.AddToWhitelist(targetName!);
                    configuration.Save();
                    OnConfigChangedSingleChar?.Invoke(targetName!);
                }
            }
            else
            {
                if (ImGui.Button($"Remove {targetName ?? "Target"}"))
                {
                    configuration.RemoveFromWhitelist(targetName!);
                    configuration.Save();
                    OnConfigChangedSingleChar?.Invoke(targetName!);
                }
            }
        }
        finally
        {
            ImGui.EndDisabled();
        }

        var n = configuration.Whitelist.Count;

        if (ImGui.Button($"Edit Whitelist ({n})"))
        {
            Service.whitelistWindow.IsOpen = true;
        }
    }

    public static Race MapIndexToRace(int index)
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
            _ => Race.UNKNOWN
        };
    }

    private static Gender MapIndexToGender(int index)
    {
        return index switch
        {
            0 => Gender.FEMALE,
            1 => Gender.MALE,
            _ => Gender.UNKNOWN,
        };
    }

    private static Clan MapIndexToClan(int index)
    {
        return index switch
        {
            0 => Clan.CLAN0,
            1 => Clan.CLAN1,
            _ => Clan.UNKNOWN,
        };
    }

    public void InvokeConfigChanged(bool force = false)
    {
        OnConfigChanged?.Invoke(force);
    }
}
