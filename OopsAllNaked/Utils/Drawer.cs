using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using ImGuiNET;
using Penumbra.Api.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static OopsAllNaked.Utils.Constant;

namespace OopsAllNaked.Utils
{
    internal class Drawer : IDisposable
    {
        public Drawer()
        {
            Service.configWindow.OnConfigChanged += RefreshAllPlayers;
            Service.configWindow.OnConfigChangedSingleChar += RefreshOnePlayer;
            if (Service.configuration.enabled)
            {
                Plugin.OutputChatLine("OopsAllNaked starting...");
                RefreshAllPlayers(false);
            }
        }

        private static void RefreshAllPlayers(bool force)
        {
            Plugin.OutputChatLine("Refreshing all players");

            foreach (var obj in Service.objectTable)
            {
                if (!obj.IsValid()) continue;
                if (obj is not ICharacter) continue;
                if (Service.configuration.IsWhitelisted(obj.Name.TextValue)) continue;

                bool isPc = obj is IPlayerCharacter;
                bool isSelf = obj.ObjectIndex == 0 || obj.ObjectIndex == 201;
                if (Service.configuration.dontLalaSelf && Service.configuration.dontStripSelf && isSelf) continue;
                if (!force && Service.configuration.dontLalaPC && Service.configuration.dontStripPC && isPc && !isSelf) continue;
                if (!force && Service.configuration.dontLalaNPC && Service.configuration.dontStripNPC && !isPc) continue;
                
                Service.penumbraApi.RedrawOne(obj.ObjectIndex, RedrawType.Redraw);
            }
        }

        private static void RefreshOnePlayer(string charName)
        {
            if (!Service.configuration.enabled)
                return;
            
            int objectIndex = -1;
            
            foreach (var obj in Service.objectTable)
            {
                if (!obj.IsValid()) continue;
                if (obj is not ICharacter) continue;
                if (obj.Name.TextValue != charName) continue;
                objectIndex = obj.ObjectIndex;
                break;
            }

            if (objectIndex == -1)
                return;
            
            Service.penumbraApi.RedrawOne(objectIndex, RedrawType.Redraw);
        }

        public static unsafe void OnCreatingCharacterBase(nint gameObjectAddress, Guid _1, nint _2, nint customizePtr, nint equipPtr)
        {
            if (!Service.configuration.enabled) return;

            var gameObj = (GameObject*)gameObjectAddress;
            var customData = Marshal.PtrToStructure<CharaCustomizeData>(customizePtr);
            var equipData = (ulong*)equipPtr;
            var charName = gameObj->NameString;

            bool isPc = gameObj->ObjectKind == ObjectKind.Pc;
            bool isSelf = gameObj->ObjectIndex == 0 || gameObj->ObjectIndex == 201;            

            // Avoid some broken conversions
            if (customData.Race == Race.UNKNOWN)
                return;         

            if (!isPc && gameObj->ObjectKind != ObjectKind.EventNpc && gameObj->ObjectKind != ObjectKind.BattleNpc && gameObj->ObjectKind != ObjectKind.Retainer)
                return;

            if (Service.configuration.noChild)
            {
                if (customData.ModelType == 4)
                    customData.ModelType = 1;
                Marshal.StructureToPtr(customData, customizePtr, true);
            }            

            bool dontLala = Service.configuration.dontLalaSelf && isSelf;
            bool dontStrip = Service.configuration.dontStripSelf && isSelf;

            dontLala |= Service.configuration.dontLalaPC && isPc && !isSelf;
            dontLala |= Service.configuration.dontLalaNPC && !isPc;

            dontStrip |= Service.configuration.dontStripPC && isPc && !isSelf;
            dontStrip |= Service.configuration.dontStripNPC && !isPc;

            if (Service.configuration.IsWhitelisted(charName))
                return;

            if (dontLala && dontStrip)
                return;

            if (!dontLala)
                ChangeRace(customData, customizePtr, Service.configuration.SelectedRace, Service.configuration.SelectedGender);

            if (customData.ModelType == 4 && Service.configuration.childClothes)
                return;

            if (!dontStrip)
                StripClothes(equipData, equipPtr, gameObj->ObjectIndex);            
        }

        private static unsafe void ChangeRace(CharaCustomizeData customData, nint customizePtr, Race selectedRace, Gender selectedGender)
        {            
            bool raceChange = (Service.configuration.SelectedRace != Race.UNKNOWN && customData.Race != Service.configuration.SelectedRace);
            bool sexChange = (Service.configuration.SelectedGender != Gender.UNKNOWN && customData.Gender != Service.configuration.SelectedGender);

            if (Service.configuration.SelectedRace != Race.UNKNOWN && Service.configuration.SelectedClan != Clan.UNKNOWN)
                raceChange = true;

            if (raceChange)
            {
                var clan = (Service.configuration.SelectedClan == Clan.UNKNOWN) ? (byte)(customData.Tribe % 2) : (byte)Service.configuration.SelectedClan;
                customData.Tribe = (byte)(((byte)selectedRace * 2) - 1 + clan);
                customData.Race = selectedRace;
                customData.FaceType %= 4;
                customData.ModelType = clan;
            }

            if (sexChange)
            {
                customData.Gender = selectedGender;
            }

            if (raceChange || sexChange)
            {
                // Fur pattern should be 1-5 for hrothgar
                if (customData.Race == Race.HROTHGAR || customData.Race == Race.VIERA)
                    customData.LipColor = (byte)(1 + (customData.LipColor % 5));

                // Ears should be 1-4 for viera
                if (customData.Race == Race.HROTHGAR || customData.Race == Race.VIERA)
                    customData.RaceFeatureType = (byte)(1 + (customData.RaceFeatureType % 4));

                customData.HairStyle = (byte)RaceMappings.SelectHairFor(customData.Race, customData.Gender, (Clan)customData.ModelType, customData.HairStyle);
            }

            Marshal.StructureToPtr(customData, customizePtr, true);
        }

        private static unsafe void StripClothes(ulong* equipData, nint equipPtr, ushort objectIndex)
        {
            Random rnd = new Random();
            int empRnd = (objectIndex == 0 || objectIndex == 201 || objectIndex == 440) ? 0 : rnd.Next(2);
            if (Service.configuration.stripHats) equipData[0] = 0;
            if (Service.configuration.stripBodies) equipData[1] = 0;
            if (Service.configuration.stripGloves) equipData[2] = 0;
            if (Service.configuration.stripLegs) equipData[3] = Service.configuration.empLegs ? (Service.configuration.empLegsRandom ? (empRnd == 0 ? 0 : 279U) : 279U) : 0;
            if (Service.configuration.stripBoots) equipData[4] = 0;
            if (Service.configuration.stripAccessories)
            {
                for (int i = 5; i <= 9; ++i)
                    equipData[i] = 0;
            }                    
        }

        public void Dispose()
        {
            Service.configWindow.OnConfigChanged -= RefreshAllPlayers;
            Service.configWindow.OnConfigChangedSingleChar -= RefreshOnePlayer;
        }
    }
}
