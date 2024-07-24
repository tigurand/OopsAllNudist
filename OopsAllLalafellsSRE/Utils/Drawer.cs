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

        public static unsafe void OnCreatingCharacterBase(nint gameObjectAddress, Guid _, nint _1, nint customizePtr, nint _2)
        {
            var gameObj = (GameObject*)gameObjectAddress;
            if (gameObj->ObjectKind != ObjectKind.Pc) return;

            // remove HQ symbol if plugin is disabled
            if (!Service.configuration.enabled)
            {
                RemoveHQSymbol(gameObj);
                return;
            }

            // return if not player character
            var customData = Marshal.PtrToStructure<CharaCustomizeData>(customizePtr);
            if (customData.Race == Race.UNKNOWN) return;

            if (customData.Race == Service.configuration.SelectedRace && Service.configuration.nameHQ)
            {
                // add HQ symbol if player is the selected race
                AddHQSymbol(gameObj);
                return;
            }
            else
            {
                // remove HQ symbol and change the race
                RemoveHQSymbol(gameObj);
                ChangeRace(customData, customizePtr, Service.configuration.SelectedRace);
            }
        }

        private static unsafe void ChangeRace(CharaCustomizeData customData, nint customizePtr, Race selectedRace)
        {
            customData.Race = selectedRace;
            customData.Tribe = (byte)(((byte)selectedRace * 2) - (customData.Tribe % 2));
            customData.FaceType %= 4;
            customData.ModelType %= 2;
            customData.HairStyle = (byte)((customData.HairStyle % RaceMappings.RaceHairs[selectedRace]) + 1);
            Marshal.StructureToPtr(customData, customizePtr, true);
        }

        private static unsafe void AddHQSymbol(GameObject* gameObj)
        {
            string nameStr = gameObj->NameString;
            if (!nameStr.EndsWith(" \uE03C"))
            {
                gameObj->NameString = nameStr + " \uE03C";
            }
        }

        private static unsafe void RemoveHQSymbol(GameObject* gameObj)
        {
            string nameStr = gameObj->NameString;
            if (nameStr.EndsWith(" \uE03C"))
            {
                gameObj->NameString = nameStr.Replace(" \uE03C", string.Empty);
            }
        }
    }
}
