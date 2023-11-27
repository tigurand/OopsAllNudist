using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Penumbra.Api.Enums;
using System.Runtime.InteropServices;
using static OopsAllLalafellsSRE.Windows.Constant;

namespace OopsAllLalafellsSRE.Utils
{
    public class Drawer
    {
        public Drawer()
        {
            Plugin.OutputChatLine("OopsAllLalafellsSRE starting...");
            if (Service.penumbraApi != null)
            {
                Service.configWindow.OnConfigChanged += RefreshAllPlayers;
                Initialize();
            }
            else
            {
                Plugin.OutputChatLine("Error: penumbraApi is not initialized.");
            }
        }

        public static void Initialize()
        {
            if (Service.configuration.changeOthers == true)
            {
                RefreshAllPlayers();
            }
        }

        public static void RefreshAllPlayers()
        {
            Plugin.OutputChatLine("Refreshing all players");

            foreach (var actor in Service.objectTable)
            {
                if (actor != null && actor.ObjectKind == ObjectKind.Player)
                {
                    ChangeRace(actor.Address, Service.configuration.SelectedRace);
                    Service.penumbraApi.RedrawObject(actor, RedrawType.Redraw);
                }
            }
        }


        public static void OnCreatingCharacterBase(nint gameObject, string collectionName, nint modelId, nint customize, nint equipData)
        {
            if (Service.configuration.changeOthers == false) { return; }
            //ChangeRace(customize, Service.configuration.SelectedRace);

            // Redraw the character to apply changes
            GameObject? character = Service.objectTable.CreateObjectReference(gameObject);
            if (character != null && character is PlayerCharacter)
            {
                Service.penumbraApi.RedrawObject(character, RedrawType.Redraw);
            }
        }

        private static void ChangeRace(nint customizePtr, Race SelectedRace)
        {
            // something wrong with this mess
            var customData = Marshal.PtrToStructure<CharaCustomizeData>(customizePtr);


            if (customData.Race != SelectedRace)
            {
                // Modify the race/tribe accordingly
                customData.Race = SelectedRace;
                customData.Tribe = (byte)(((byte)customData.Race * 2) - (customData.Tribe % 2));

                // Force male for Hrothgar
                customData.Gender = SelectedRace switch
                {
                    Race.HROTHGAR => 0,
                    _ => customData.Gender
                };

                // TODO: Re-evaluate these for valid race-specific values? (These are Lalafell values)
                // Constrain face type to 0-3 so we don't decapitate the character
                customData.FaceType %= 4;

                // Constrain body type to 0-1 so we don't crash the game
                customData.ModelType %= 2;

                // Hrothgar have a limited number of lip colors?
                customData.LipColor = SelectedRace switch
                {
                    Race.HROTHGAR => (byte)((customData.LipColor % 5) + 1),
                    _ => customData.LipColor
                };

                customData.HairStyle = (byte)((customData.HairStyle % RaceMappings.RaceHairs[SelectedRace]) + 1);

                Marshal.StructureToPtr(customData, customizePtr, true);
            }
        }

        public void Dispose()
        {
            Service.configWindow.OnConfigChanged -= RefreshAllPlayers;
        }
    }
}
