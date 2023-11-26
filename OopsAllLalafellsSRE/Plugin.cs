using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using OopsAllLalafellsSRE.Utils;
using OopsAllLalafellsSRE.Windows;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static OopsAllLalafellsSRE.Windows.Constant;

namespace OopsAllLalafellsSRE
{
    public sealed class Plugin : IDalamudPlugin
    {
        public static string Name => "OopsAllLalafellsSRE";
        private const string CommandName = "/polala";

        public WindowSystem WindowSystem { get; private set; } = new("OopsAllLalafellsSRE");

        public Plugin([RequiredVersion("1.0")] DalamudPluginInterface pluginInterface)
        {
            _ = pluginInterface.Create<Service>();
            _ = pluginInterface.Create<Hooking>();
            Service.plugin = this;

            Service.configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Service.configuration.Initialize(pluginInterface);

            Service.configWindow = new ConfigWindow(this);
            WindowSystem.AddWindow(Service.configWindow);

            pluginInterface.UiBuilder.Draw += DrawUI;
            pluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

            Service.commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens OopsAllLalafellsSRE config menu."
            });

            // main logic
            Service.pluginLog.Debug("Plugin starting...");
            RefreshAllPlayers();
            Service.configWindow.OnConfigChanged += RefreshAllPlayers;
        }

        public static async void RefreshAllPlayers()
        {
            Service.pluginLog.Debug("Refreshing all players");
            // Workaround to prevent literally genociding the actor table if we load at the same time as Dalamud + Dalamud is loading while ingame
            await Task.Delay(100); // LMFAOOOOOOOOOOOOOOOOOOO
            var localPlayer = Service.clientState?.LocalPlayer;
            if (localPlayer == null)
            {
                return;
            }

            for (var i = 0; i < Service.objectTable?.Length; i++)
            {
                var actor = Service.objectTable[i];

                if (actor != null
                    && actor.ObjectKind == ObjectKind.Player)
                {
                    RerenderActor(actor);
                }
            }
        }

        private static async void RerenderActor(GameObject actor)
        {
            var addrRenderToggle = actor.Address + OFFSET_RENDER_TOGGLE;
            var val = Marshal.ReadInt32(addrRenderToggle);

            // Trigger a rerender
            val |= (int)FLAG_INVIS;
            Marshal.WriteInt32(addrRenderToggle, val);
            await Task.Delay(100);
            val &= ~(int)FLAG_INVIS;
            Marshal.WriteInt32(addrRenderToggle, val);
        }

        public static void ChangeRace(IntPtr customizeDataPtr, Race targetRace)
        {
            var customData = Marshal.PtrToStructure<CharaCustomizeData>(customizeDataPtr);

            if (customData.Race != targetRace)
            {
                // Modify the race/tribe accordingly
                customData.Race = targetRace;
                customData.Tribe = (byte)(((byte)customData.Race * 2) - (customData.Tribe % 2));

                // Special-case Hrothgar gender to prevent fuckery
                customData.Gender = targetRace switch
                {
                    Race.HROTHGAR => 0, // Force male for Hrothgar
                    _ => customData.Gender
                };

                // TODO: Re-evaluate these for valid race-specific values? (These are Lalafell values)
                // Constrain face type to 0-3 so we don't decapitate the character
                customData.FaceType %= 4;

                // Constrain body type to 0-1 so we don't crash the game
                customData.ModelType %= 2;

                // Hrothgar have a limited number of lip colors?
                customData.LipColor = targetRace switch
                {
                    Race.HROTHGAR => (byte)((customData.LipColor % 5) + 1),
                    _ => customData.LipColor
                };

                customData.HairStyle = (byte)((customData.HairStyle % RaceMappings.RaceHairs[targetRace]) + 1);

                Marshal.StructureToPtr(customData, customizeDataPtr, true);

                // Record the new race/gender for equip model mapping, and mark the equip as dirty
                Service.hooking.lastPlayerRace = customData.Race;
                Service.hooking.lastPlayerGender = customData.Gender;
                Service.hooking.lastWasModified = true;
            }
        }

        public static EquipData MapRacialEquipModels(Race race, int gender, EquipData eq)
        {
            if (Array.IndexOf(RACE_STARTER_GEAR_IDS, eq.model) > -1)
            {
                Service.pluginLog.Debug($"Modified {eq.model}, {eq.variant}");
                Service.pluginLog.Debug($"Race {race}, index {(byte)(race - 1)}, gender {gender}");
                eq.model = RACE_STARTER_GEAR_ID_MAP[(byte)race - 1, gender];
                eq.variant = 1;
                Service.pluginLog.Debug($"New {eq.model}, {eq.variant}");
            }

            return eq;
        }

        internal static void OutputChatLine(SeString message)
        {
            SeStringBuilder sb = new();
            sb.AddUiForeground("[OAL] ", 58).Append(message);

            Service.chatGui.Print(new XivChatEntry { Message = sb.BuiltString });
        }

        public void Dispose()
        {
            WindowSystem.RemoveAllWindows();

            Service.configWindow.Dispose();
            Service.hooking?.Dispose();

            // Refresh all players again
            RefreshAllPlayers();

            Service.commandManager.RemoveHandler(CommandName);
        }

        private void OnCommand(string command, string args)
        {
            Service.configWindow.IsOpen = true;
        }

        private void DrawUI() => WindowSystem.Draw();
        public static void DrawConfigUI() => Service.configWindow.IsOpen = true;
    }
}
