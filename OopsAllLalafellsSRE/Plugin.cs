#undef DEBUG

using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Hooking;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace OopsAllLalafellsSRE
{
    public class Plugin : IDalamudPlugin
    {
        private const uint FLAG_INVIS = (1 << 1) | (1 << 11);
        private const uint CHARA_WINDOW_ACTOR_ID = 0xE0000000;
        private const int OFFSET_RENDER_TOGGLE = 0x104;

        private static readonly short[,] RACE_STARTER_GEAR_ID_MAP =
        {
            {84, 85}, // Hyur
            {86, 87}, // Elezen
            {92, 93}, // Lalafell
            {88, 89}, // Miqo
            {90, 91}, // Roe
            {257, 258}, // Au Ra
            {597, -1}, // Hrothgar
            {-1, 581}, // Viera
        };

        private static readonly short[] RACE_STARTER_GEAR_IDS;

        public static string Name => "OopsAllLalafellsSRE";
        private const string CommandName = "/poal";

        [PluginService] private DalamudPluginInterface pluginInterface { get; set; }

        [PluginService] private ICommandManager commandManager { get; set; }
        [PluginService] private IPluginLog pluginLog { get; set; }
        [PluginService] private ISigScanner sigScanner { get; set; }

        [PluginService] private IObjectTable? objectTable { get; set; }
        [PluginService] private IClientState? clientState { get; set; }
        [PluginService] private IGameInteropProvider? gameInterOp { get; set; }

        public Configuration config { get; private set; }

        private bool unsavedConfigChanges = false;

        private readonly PluginUI ui;
        public bool SettingsVisible = false;

        private delegate IntPtr CharacterIsMount(IntPtr actor);

        private delegate IntPtr CharacterInitialize(IntPtr actorPtr, IntPtr customizeDataPtr);

        private delegate IntPtr FlagSlotUpdate(IntPtr actorPtr, uint slot, IntPtr equipData);

        private readonly Hook<CharacterIsMount> charaMountedHook;
        private readonly Hook<CharacterInitialize> charaInitHook;
        private readonly Hook<FlagSlotUpdate> flagSlotUpdateHook;

        private IntPtr lastActor;
        private bool lastWasPlayer;
        private bool lastWasModified;

        private Race lastPlayerRace;
        private byte lastPlayerGender;

        // This sucks, but here we are
        static Plugin()
        {
            try
            {
                var list = new List<short>();
                foreach (short id in RACE_STARTER_GEAR_ID_MAP)
                {
                    if (id != -1)
                    {
                        list.Add(id);
                    }
                }
                RACE_STARTER_GEAR_IDS = [.. list];
            }
            catch { }
        }

        public Plugin(
                [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
                [RequiredVersion("1.0")] IObjectTable objectTable,
                [RequiredVersion("1.0")] ICommandManager commandManager,
                [RequiredVersion("1.0")] ISigScanner sigScanner,
                [RequiredVersion("1.0")] IClientState clientState,
                [RequiredVersion("1.0")] IGameInteropProvider gameInterOp,
                [RequiredVersion("1.0")] IPluginLog pluginLog)
        {
            config = pluginInterface?.GetPluginConfig() as Configuration ?? new Configuration();
            config.Initialize(pluginInterface);

            ui = new PluginUI(this);

            commandManager.AddHandler(CommandName, new CommandInfo(OpenSettingsMenuCommand)
            {
                HelpMessage = "Opens the Oops, All Lalafells! settings menu."
            });

            var charaIsMountAddr =
                sigScanner.ScanText("40 53 48 83 EC 20 48 8B 01 48 8B D9 FF 50 10 83 F8 08 75 08");
            pluginLog.Debug($"Found IsMount address: {charaIsMountAddr.ToInt64():X}");

            charaMountedHook ??=
                gameInterOp.HookFromAddress<CharacterIsMount>(charaIsMountAddr, CharacterIsMountDetour);
            charaMountedHook.Enable();

            var charaInitAddr = sigScanner.ScanText(
                "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 30 48 8B F9 48 8B EA 48 81 C1 ?? ?? ?? ?? E8 ?? ?? ?? ??");
            pluginLog.Debug($"Found Initialize address: {charaInitAddr.ToInt64():X}");
            charaInitHook ??=
                gameInterOp.HookFromAddress<CharacterInitialize>(charaInitAddr, CharacterInitializeDetour);
            charaInitHook.Enable();

            var flagSlotUpdateAddr =
                sigScanner.ScanText(
                    "48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 8B DA 49 8B F0 48 8B F9 83 FA 0A");
            pluginLog.Debug($"Found FlagSlotUpdate address: {flagSlotUpdateAddr.ToInt64():X}");
            flagSlotUpdateHook ??=
                gameInterOp.HookFromAddress<FlagSlotUpdate>(flagSlotUpdateAddr, FlagSlotUpdateDetour);
            flagSlotUpdateHook.Enable();

            // Trigger an initial refresh of all players
            RefreshAllPlayers();
        }

        private IntPtr CharacterIsMountDetour(IntPtr actorPtr)
        {
            try
            {
                // TODO: use native FFXIVClientStructs unsafe methods?
                if (Marshal.ReadByte(actorPtr + 0x8C) == (byte)ObjectKind.Player)
                {
                    lastActor = actorPtr;
                    lastWasPlayer = true;
                }
                else
                {
                    lastWasPlayer = false;
                }

                return charaMountedHook.Original(actorPtr);
            }
            catch (Exception ex)
            {
                pluginLog.Error(ex.ToString());
                return IntPtr.Zero;
            }
        }

        private IntPtr CharacterInitializeDetour(IntPtr drawObjectBase, IntPtr customizeDataPtr)
        {
            try
            {
                if (lastWasPlayer)
                {
                    lastWasModified = false;
                    var actor = objectTable.CreateObjectReference(lastActor);
                    if (actor != null &&
                        (actor.ObjectId != CHARA_WINDOW_ACTOR_ID || config.ImmersiveMode)
                        && clientState.LocalPlayer != null
                        && actor.ObjectId != clientState.LocalPlayer.ObjectId
                        && config.ShouldChangeOthers)
                    {
                        ChangeRace(customizeDataPtr, config.ChangeOthersTargetRace);
                    }
                }

                return charaInitHook.Original(drawObjectBase, customizeDataPtr);
            }
            catch (Exception ex)
            {
                pluginLog.Error(ex.ToString());
                return IntPtr.Zero;
            }
        }

        private void ChangeRace(IntPtr customizeDataPtr, Race targetRace)
        {
            if (customizeDataPtr == IntPtr.Zero)
            {
                pluginLog.Error("customizeDataPtr is null.");
                return;
            }
            try
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
                    lastPlayerRace = customData.Race;
                    lastPlayerGender = customData.Gender;
                    lastWasModified = true;
                }
            }
            catch (Exception ex)
            {
                pluginLog.Error(ex.ToString());
            }
        }

        private IntPtr FlagSlotUpdateDetour(IntPtr actorPtr, uint slot, IntPtr equipDataPtr)
        {
            try
            {
                if (lastWasPlayer && lastWasModified)
                {
                    var equipData = Marshal.PtrToStructure<EquipData>(equipDataPtr);
                    // TODO: Handle gender-locked gear for Viera/Hrothgar
                    equipData = MapRacialEquipModels(lastPlayerRace, lastPlayerGender, equipData);
                    Marshal.StructureToPtr(equipData, equipDataPtr, true);
                }

                return flagSlotUpdateHook.Original(actorPtr, slot, equipDataPtr);
            }
            catch (Exception ex)
            {
                pluginLog.Error(ex.ToString());
                return IntPtr.Zero;
            }
        }

        public bool SaveConfig()
        {
            if (unsavedConfigChanges)
            {
                config.Save();
                unsavedConfigChanges = false;
                RefreshAllPlayers();
                return true;
            }

            return false;
        }

        public void ToggleOtherRace(bool changeRace)
        {
            if (config.ShouldChangeOthers == changeRace)
            {
                return;
            }

            pluginLog.Debug($"Target race for other players toggled to {changeRace}, refreshing players");
            config.ShouldChangeOthers = changeRace;
            unsavedConfigChanges = true;
        }

        public void UpdateOtherRace(Race race)
        {
            if (config == null)
            {
                pluginLog.Error("Config is not initialized.");
                return;
            }

            if (config.ChangeOthersTargetRace == race)
            {
                return;
            }

            pluginLog.Debug($"Target race for other players changed to {race}, refreshing players");
            config.ChangeOthersTargetRace = race;
            unsavedConfigChanges = true;
        }

        public void UpdateImmersiveMode(bool immersiveMode)
        {
            if (config.ImmersiveMode == immersiveMode)
            {
                return;
            }

            pluginLog.Debug($"Immersive mode set to {immersiveMode}, refreshing players");
            config.ImmersiveMode = immersiveMode;
            unsavedConfigChanges = true;
        }

        public async void RefreshAllPlayers()
        {
            try
            {
                // Workaround to prevent literally genociding the actor table if we load at the same time as Dalamud + Dalamud is loading while ingame
                await Task.Delay(100); // LMFAOOOOOOOOOOOOOOOOOOO
                var localPlayer = clientState?.LocalPlayer;
                if (localPlayer == null)
                {
                    return;
                }

                for (var i = 0; i < objectTable?.Length; i++)
                {
                    var actor = objectTable[i];

                    if (actor != null
                        && actor.ObjectKind == ObjectKind.Player)
                    {
                        RerenderActor(actor);
                    }
                }
            }
            catch (Exception ex)
            {
                pluginLog.Error(ex.ToString());
            }
        }

        private async void RerenderActor(GameObject actor)
        {
            try
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
            catch (Exception ex)
            {
                pluginLog.Debug(ex.ToString());
            }
        }

        private static EquipData MapRacialEquipModels(Race race, int gender, EquipData eq)
        {
            if (Array.IndexOf(RACE_STARTER_GEAR_IDS, eq.model) > -1)
            {
#if DEBUG
                pluginLog.Debug($"Modified {eq.model}, {eq.variant}");
                pluginLog.Debug($"Race {race}, index {(byte) (race - 1)}, gender {gender}");
#endif
                eq.model = RACE_STARTER_GEAR_ID_MAP[(byte)race - 1, gender];
                eq.variant = 1;
#if DEBUG
                pluginLog.Debug($"New {eq.model}, {eq.variant}");
#endif
            }

            return eq;
        }

        public void OpenSettingsMenuCommand(string command, string args)
        {
            OpenSettingsMenu();
        }

        private void OpenSettingsMenu()
        {
            SettingsVisible = true;
        }

        #region IDisposable Support

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            try
            {
                pluginInterface.UiBuilder.OpenConfigUi -= OpenSettingsMenu;
                pluginInterface.UiBuilder.Draw -= ui.Draw;
                SaveConfig();

                charaMountedHook.Disable();
                charaInitHook.Disable();
                flagSlotUpdateHook.Disable();

                charaMountedHook.Dispose();
                charaInitHook.Dispose();
                flagSlotUpdateHook.Dispose();

                // Refresh all players again
                RefreshAllPlayers();

                commandManager.RemoveHandler("/poal");
            }
            catch (Exception ex)
            {
                pluginLog.Error(ex.ToString());
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
