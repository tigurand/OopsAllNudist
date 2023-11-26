using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Hooking;
using OopsAllLalafellsSRE.Utils;
using System;
using System.Runtime.InteropServices;
using static OopsAllLalafellsSRE.Windows.Constant;

namespace OopsAllLalafellsSRE
{
    public class Hooking
    {
        public Configuration Configuration { get; private set; }
        public Plugin plugin { get; private set; }

        // To be replaced by FFXIVClientStructs and Penumbra.Api
        public delegate IntPtr CharacterIsMount(IntPtr actor);
        public delegate IntPtr CharacterInitialize(IntPtr actorPtr, IntPtr customizeDataPtr);
        public delegate IntPtr FlagSlotUpdate(IntPtr actorPtr, uint slot, IntPtr equipData);

        public Hook<CharacterIsMount> charaMountedHook { get; private set; }

        public Hook<CharacterInitialize> charaInitHook { get; private set; }
        public Hook<FlagSlotUpdate> flagSlotUpdateHook { get; private set; }

        public IntPtr lastActor;
        public bool lastWasPlayer;
        public bool lastWasModified;

        public Race lastPlayerRace;
        public byte lastPlayerGender;

        public Hooking()
        {
            Service.hooking = this;
            Service.pluginLog.Debug("Initializing Hooking");
            InitializeHooks();
        }

        public void InitializeHooks()
        {
            try
            {
                var charaIsMountAddr = Service.sigScanner.ScanText("40 53 48 83 EC 20 48 8B 01 48 8B D9 FF 50 10 83 F8 08 75 08");
                Service.pluginLog.Debug($"Found IsMount address: {charaIsMountAddr.ToInt64():X}");
                charaMountedHook = Service.gameInteropProvider.HookFromAddress<CharacterIsMount>(charaIsMountAddr, CharacterIsMountDetour);
                charaMountedHook.Enable();

                var charaInitAddr = Service.sigScanner.ScanText("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 30 48 8B F9 48 8B EA 48 81 C1 ?? ?? ?? ?? E8 ?? ?? ?? ??");
                Service.pluginLog.Debug($"Found Initialize address: {charaInitAddr.ToInt64():X}");
                charaInitHook = Service.gameInteropProvider.HookFromAddress<CharacterInitialize>(charaInitAddr, CharacterInitializeDetour);
                charaInitHook.Enable();

                var flagSlotUpdateAddr = Service.sigScanner.ScanText("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 8B DA 49 8B F0 48 8B F9 83 FA 0A");
                Service.pluginLog.Debug($"Found FlagSlotUpdate address: {flagSlotUpdateAddr.ToInt64():X}");
                flagSlotUpdateHook = Service.gameInteropProvider.HookFromAddress<FlagSlotUpdate>(flagSlotUpdateAddr, FlagSlotUpdateDetour);
                flagSlotUpdateHook.Enable();
            }
            catch (Exception ex)
            {
                Service.pluginLog.Error($"Exception in InitializeHooks: {ex.Message}");
            }
        }

        private IntPtr CharacterIsMountDetour(IntPtr actorPtr)
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

        private IntPtr CharacterInitializeDetour(IntPtr drawObjectBase, IntPtr customizeDataPtr)
        {
            if (lastWasPlayer)
            {
                lastWasModified = false;
                var actor = Service.objectTable.CreateObjectReference(lastActor);
                if (actor != null &&
                    (actor.ObjectId != CHARA_WINDOW_ACTOR_ID || Configuration.immersiveMode)
                    && Service.clientState.LocalPlayer != null
                    && actor.ObjectId != Service.clientState.LocalPlayer.ObjectId
                    && Configuration.changeOthers)
                {
                    Plugin.ChangeRace(customizeDataPtr, Configuration.SelectedRace);
                }
            }

            return charaInitHook.Original(drawObjectBase, customizeDataPtr);
        }

        private IntPtr FlagSlotUpdateDetour(IntPtr actorPtr, uint slot, IntPtr equipDataPtr)
        {
            if (lastWasPlayer && lastWasModified)
            {
                var equipData = Marshal.PtrToStructure<EquipData>(equipDataPtr);
                // TODO: Handle gender-locked gear for Viera/Hrothgar
                equipData = Plugin.MapRacialEquipModels(lastPlayerRace, lastPlayerGender, equipData);
                Marshal.StructureToPtr(equipData, equipDataPtr, true);
            }

            return flagSlotUpdateHook.Original(actorPtr, slot, equipDataPtr);
        }

        public void Dispose()
        {
            charaMountedHook?.Disable();
            charaInitHook?.Disable();
            flagSlotUpdateHook?.Disable();

            charaMountedHook?.Dispose();
            charaInitHook?.Dispose();
            flagSlotUpdateHook?.Dispose();
        }
    }
}
