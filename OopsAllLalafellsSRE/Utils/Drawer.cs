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
#if DEBUG
            Plugin.OutputChatLine("OopsAllLalafellsSRE starting...");
#endif

            Service.configWindow.OnConfigChanged += RefreshAllPlayers;
            if (Service.configuration.enabled)
            {
                RefreshAllPlayers();
            }
        }

        private static void RefreshAllPlayers()
        {
#if DEBUG
            Plugin.OutputChatLine("Refreshing all players");
#endif
            Service.penumbraApi.RedrawAll(RedrawType.Redraw);
        }

        public static void OnCreatingCharacterBase(nint _, Guid _1, nint _2, nint customizePtr, nint _3)
        {
            if (!Service.configuration.enabled) return;
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
