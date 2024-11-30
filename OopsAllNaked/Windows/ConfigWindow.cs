using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ImGuiNET;
using OopsAllNaked.Utils;
using System;
using System.Numerics;
using static OopsAllNaked.Utils.Constant;

namespace OopsAllNaked.Windows;

internal class ConfigWindow : Window
{
    private readonly Configuration configuration;
    private readonly string[] race = ["Lalafell", "Hyur", "Elezen", "Miqo'te", "Roegadyn", "Au Ra", "Hrothgar", "Viera", "Keep Original Race"];
    private readonly string[] gender = ["Female", "Male", "Keep Original Sex"];
    private readonly string[] clan = ["0", "1", "Automatic Clan"];
    private int selectedRaceIndex = 0;
    private int selectedGenderIndex = 0;
    private int selectedClanIndex = 0;
    public event Action? OnConfigChanged;
    public event Action<string>? OnConfigChangedSingleChar;

    // proxy function so whitelist editor can reload a character too
    internal void ReloadCharProxy(string charName)
    {
        OnConfigChangedSingleChar?.Invoke(charName);
    }

    public ConfigWindow(Plugin plugin) : base(
        "OopsAllNaked Configuration Window",
        ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoScrollWithMouse)
    {
        Size = new Vector2(285, 500);
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

    public override void Draw()
    {
        // select race
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Target Race");
        ImGui.SameLine();
        if (ImGui.Combo("###Race", ref selectedRaceIndex, race, race.Length))
        {
            configuration.SelectedRace = MapIndexToRace(selectedRaceIndex);
            configuration.Save();
            if (configuration.enabled)
                OnConfigChanged?.Invoke();
        }
        ImGui.TextUnformatted("Target Sex");
        ImGui.SameLine();
        if (ImGui.Combo("###Sex", ref selectedGenderIndex, gender, gender.Length))
        {
            configuration.SelectedGender = MapIndexToGender(selectedGenderIndex);
            configuration.Save();
            if (configuration.enabled)
                OnConfigChanged?.Invoke();
        }
        ImGui.BeginDisabled(selectedRaceIndex == 8);
        ImGui.TextUnformatted("Target Clan");
        ImGui.SameLine();
        if (ImGui.Combo("###Clan", ref selectedClanIndex, clan, clan.Length))
        {
            configuration.SelectedClan = MapIndexToClan(selectedClanIndex);
            configuration.Save();
            if (configuration.enabled)
                OnConfigChanged?.Invoke();
        }
        ImGui.EndDisabled();

        // Enabled
        bool _Enabled = configuration.enabled;
        if (ImGui.Checkbox("Enable", ref _Enabled))
        {
            configuration.enabled = _Enabled;
            configuration.Save();
            OnConfigChanged?.Invoke();
        }

        bool _StayOn = configuration.stayOn;
        if (ImGui.Checkbox("Stay on", ref _StayOn))
        {
            configuration.stayOn = _StayOn;
            configuration.Save();
        }

        ImGui.Separator();
        bool _dontStripSelf = configuration.dontStripSelf;
        if (ImGui.Checkbox("Don't strip self", ref _dontStripSelf))
        {
            configuration.dontStripSelf = _dontStripSelf;
            configuration.Save();
            if (configuration.enabled && Service.clientState.LocalPlayer != null)
                OnConfigChangedSingleChar?.Invoke(Service.clientState.LocalPlayer.Name.TextValue);
        }

        bool _dontLalaSelf = configuration.dontLalaSelf;
        if (ImGui.Checkbox("Don't race/sex change self", ref _dontLalaSelf))
        {
            configuration.dontLalaSelf = _dontLalaSelf;
            configuration.Save();
            if (configuration.enabled && Service.clientState.LocalPlayer != null)
                OnConfigChangedSingleChar?.Invoke(Service.clientState.LocalPlayer.Name.TextValue);
        }

        ImGui.Separator();
        bool _stripHats = configuration.stripHats;
        if (ImGui.Checkbox("Strip Hats", ref _stripHats))
        {
            configuration.stripHats = _stripHats;
            configuration.Save();
            if (configuration.enabled)
                OnConfigChanged?.Invoke();
        }

        bool _stripBodies = configuration.stripBodies;
        if (ImGui.Checkbox("Strip Bodies", ref _stripBodies))
        {
            configuration.stripBodies = _stripBodies;
            configuration.Save();
            if (configuration.enabled)
                OnConfigChanged?.Invoke();
        }

        bool _stripLegs = configuration.stripLegs;
        if (ImGui.Checkbox("Strip Legs", ref _stripLegs))
        {
            configuration.stripLegs = _stripLegs;
            configuration.Save();
            if (configuration.enabled)
                OnConfigChanged?.Invoke();
        }

        bool _stripGloves = configuration.stripGloves;
        if (ImGui.Checkbox("Strip Gloves", ref _stripGloves))
        {
            configuration.stripGloves = _stripGloves;
            configuration.Save();
            if (configuration.enabled)
                OnConfigChanged?.Invoke();
        }

        bool _stripBoots = configuration.stripBoots;
        if (ImGui.Checkbox("Strip Boots", ref _stripBoots))
        {
            configuration.stripBoots = _stripBoots;
            configuration.Save();
            if (configuration.enabled)
                OnConfigChanged?.Invoke();
        }

        bool _stripAccessories = configuration.stripAccessories;
        if (ImGui.Checkbox("Strip Accessories", ref _stripAccessories))
        {
            configuration.stripAccessories = _stripAccessories;
            configuration.Save();
            if (configuration.enabled)
                OnConfigChanged?.Invoke();
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

    public void InvokeConfigChanged()
    {
        OnConfigChanged?.Invoke();
    }
}
