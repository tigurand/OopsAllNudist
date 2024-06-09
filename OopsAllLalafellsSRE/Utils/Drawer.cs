using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Penumbra.Api.Enums;
using System;
using System.Runtime.InteropServices;
using static OopsAllLalafellsSRE.Utils.Constant;

namespace OopsAllLalafellsSRE.Utils
{
    internal class Drawer : IDisposable
    {
        public Drawer()
        {
            Plugin.OutputChatLine("OopsAllLalafellsSRE starting...");

            Service.configWindow.OnConfigChanged += RefreshAllPlayers;
            if (Service.configuration.enabled)
            {
                RefreshAllPlayers();
            }
        }

        private static void RefreshAllPlayers()
        {
            Plugin.OutputChatLine("Refreshing all players");
            Service.penumbraApi.RedrawAll(RedrawType.Redraw);
        }

        public static void OnCreatingCharacterBase(nint gameObjectAddress, Guid _1, nint _2, nint customizePtr, nint _3)
        {
            if (!Service.configuration.enabled) return;

            // return if not a player character
            unsafe
            {
                var gameObj = (GameObject*)gameObjectAddress;
                if (gameObj->ObjectKind != (byte)ObjectKind.Pc) return;
            }

            ChangeRace(customizePtr, Service.configuration.SelectedRace);
        }

        private static void ChangeRace(nint customizePtr, Race selectedRace)
        {
            var customData = Marshal.PtrToStructure<CharaCustomizeData>(customizePtr);

            if (customData.Race == selectedRace || customData.Race == Race.UNKNOWN)
                return;

            customData.Race = selectedRace;
            customData.Tribe = (byte)(((byte)selectedRace * 2) - (customData.Tribe % 2));
            customData.Gender = selectedRace == Race.HROTHGAR ? (byte)0 : customData.Gender;
            customData.FaceType %= 4;
            customData.ModelType %= 2;
            customData.LipColor = selectedRace == Race.HROTHGAR ? (byte)((customData.LipColor % 5) + 1) : customData.LipColor;
            customData.HairStyle = (byte)((customData.HairStyle % RaceMappings.RaceHairs[selectedRace]) + 1);

            Marshal.StructureToPtr(customData, customizePtr, true);
        }

        public void Dispose()
        {
            Service.configWindow.OnConfigChanged -= RefreshAllPlayers;
        }
    }
}
