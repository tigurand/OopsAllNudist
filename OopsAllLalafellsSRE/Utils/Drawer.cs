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

        public void Dispose()
        {
            Service.configWindow.OnConfigChanged -= RefreshAllPlayers;
        }

        private static void RefreshAllPlayers()
        {
            Plugin.OutputChatLine("Refreshing all players");
            Service.penumbraApi.RedrawAll(RedrawType.Redraw);
        }

        public static void OnCreatingCharacterBase(nint gameObjectAddress, Guid _, nint _1, nint customizePtr, nint _2)
        {
            if (!Service.configuration.enabled) return;

            unsafe
            {
                var gameObj = (GameObject*)gameObjectAddress;
                if (gameObj->ObjectKind == ObjectKind.Pc)
                {
                    ChangeRace(gameObj, customizePtr, Service.configuration.SelectedRace);
                }
            }
        }

        private static unsafe void ChangeRace(GameObject* gameObj, nint customizePtr, Race selectedRace)
        {
            var customData = Marshal.PtrToStructure<CharaCustomizeData>(customizePtr);
            if (customData.Race == Race.UNKNOWN) return;

            if (Service.configuration.nameHQ && customData.Race == selectedRace)
            {
                gameObj->NameString += "\uE03C";
            }

            customData.Race = selectedRace;
            customData.Tribe = (byte)(((byte)selectedRace * 2) - (customData.Tribe % 2));
            customData.FaceType %= 4;
            customData.ModelType %= 2;
            customData.HairStyle = (byte)((customData.HairStyle % RaceMappings.RaceHairs[selectedRace]) + 1);
            Marshal.StructureToPtr(customData, customizePtr, true);
        }
    }
}
