using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Glamourer.Api.Enums;
using Glamourer.Api.IpcSubscribers;
using OopsAllNudist.Windows;
using Penumbra.Api.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using static OopsAllNudist.Utils.Constant;

namespace OopsAllNudist.Utils
{
    internal class Drawer : IDisposable
    {
        public static HashSet<uint> ModifiedActorIds = new HashSet<uint>();
        private readonly IDisposable glamourerSubscription;
        public Drawer()
        {
            Service.configWindow.OnConfigChanged += RefreshAllPlayers;
            Service.configWindow.OnConfigChangedSingleChar += RefreshOnePlayer;
            glamourerSubscription = StateFinalized.Subscriber(Service.pluginInterface, OnGlamourerStateChange);

            if (Service.configuration.enabled)
            {
                Plugin.OutputChatLine("OopsAllNudist starting...");
                RefreshAllPlayers(false);
            }
        }

        private static bool IsSelfOrPlayerClone(IGameObject? character, IPlayerCharacter? localPlayer)
        {
            if (!Service.clientState.IsLoggedIn)
                return true;

            if (character == null || localPlayer == null)
                return false;

            if (character.Address == localPlayer.Address)
                return true;

            if (character.ObjectKind != Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Player)
                return false;

            uint objectIndex = character.ObjectIndex;
            bool isPotentialClone = (objectIndex >= 200 && objectIndex < 240) || (objectIndex >= 440 && objectIndex < 460);
            if (!isPotentialClone)
                return false;

            var targetCustomize = (character as ICharacter)?.Customize;
            var localCustomize = localPlayer.Customize;

            if (targetCustomize == null || localCustomize == null || targetCustomize.Length < 26 || localCustomize.Length < 26)
                return false;

            return targetCustomize[0] == localCustomize[0] &&
                   targetCustomize[1] == localCustomize[1] &&
                   targetCustomize[4] == localCustomize[4] &&
                   targetCustomize[5] == localCustomize[5] &&
                   targetCustomize[6] == localCustomize[6] &&
                   targetCustomize[8] == localCustomize[8] &&
                   targetCustomize[9] == localCustomize[9] &&
                   targetCustomize[10] == localCustomize[10] &&
                   targetCustomize[11] == localCustomize[11] &&
                   targetCustomize[15] == localCustomize[15] &&
                   targetCustomize[20] == localCustomize[20];
        }

        private void OnGlamourerStateChange(nint actorPtr, StateFinalizationType type)
        {
            var actor = Service.objectTable.FirstOrDefault(o => o.Address == actorPtr);

            if (actor != null)
            {
                if (Service.configuration.debugMode)
                {
                    Plugin.OutputChatLine($"Glamourer change ({type}) detected on {actor.Name}. Redrawing.");
                }
                Service.penumbraApi.RedrawOne(actor.ObjectIndex, RedrawType.Redraw);
            }
        }

        public static void RefreshAllPlayers(bool force)
        {
            Plugin.OutputChatLine("Refreshing all players");

            Service.Framework.RunOnFrameworkThread(() =>
            {
                var localPlayer = Service.clientState.LocalPlayer;

                foreach (var obj in Service.objectTable)
                {
                    if (!obj.IsValid()) continue;
                    if (obj is not ICharacter) continue;
                    if (Service.configuration.IsWhitelisted(obj.Name.TextValue)) continue;

                    if (obj.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Companion)
                        continue;

                    bool isPc = obj is IPlayerCharacter;
                    bool isSelf = IsSelfOrPlayerClone(obj, localPlayer);

                    if (Service.configuration.dontLalaSelf && Service.configuration.dontStripSelf && isSelf) continue;
                    if (!force && Service.configuration.dontLalaPC && Service.configuration.dontStripPC && isPc && !isSelf) continue;
                    if (!force && Service.configuration.dontLalaNPC && Service.configuration.dontStripNPC && !isPc) continue;

                    Service.glamourerApi.RevertStateApi?.Invoke(obj.ObjectIndex, 0, (ApplyFlag)0);
                    Service.glamourerApi.RevertToAutomationApi?.Invoke(obj.ObjectIndex, 0, (ApplyFlag)0);
                    Service.penumbraApi.RedrawOne(obj.ObjectIndex, RedrawType.Redraw);
                }
            });
        }

        private static void RefreshOnePlayer(string charName)
        {
            if (!Service.configuration.enabled)
                return;

            int objectIndex = -1;
            Dalamud.Game.ClientState.Objects.Enums.ObjectKind objectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind.None;

            Service.Framework.RunOnFrameworkThread(() =>
            {
                foreach (var obj in Service.objectTable)
                {
                    if (!obj.IsValid()) continue;
                    if (obj is not ICharacter) continue;
                    if (obj.Name.TextValue != charName) continue;
                    objectIndex = obj.ObjectIndex;
                    objectKind = obj.ObjectKind;
                    break;
                }
            });

            if (objectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Companion)
                return;

            if (objectIndex == -1)
                return;

            Service.glamourerApi.RevertStateApi?.Invoke(objectIndex, 0, (ApplyFlag)0);
            Service.glamourerApi.RevertToAutomationApi?.Invoke(objectIndex, 0, (ApplyFlag)0);
            Service.penumbraApi.RedrawOne(objectIndex, RedrawType.Redraw);
        }

        public static unsafe void OnCreatingCharacterBase(nint gameObjectAddress, Guid _1, nint _2, nint customizePtr, nint equipPtr)
        {
            var localPlayer = Service.clientState.LocalPlayer;
            var characterObject = Service.objectTable.FirstOrDefault(o => o.Address == gameObjectAddress);

            var gameObj = (GameObject*)gameObjectAddress;
            var customData = Marshal.PtrToStructure<CharaCustomizeData>(customizePtr);
            var equipData = (ulong*)equipPtr;
            var charName = gameObj->NameString;
            string[] childNPCNames = { "Alphinaud", "Alisaie" };

            if (gameObj->ObjectKind == ObjectKind.Companion)
                return;

            var revertState = Service.glamourerApi?.RevertStateApi;
            if (revertState == null)
                return;

            if (!Service.configuration.enabled)
            {
                if (ModifiedActorIds.Contains(gameObj->ObjectIndex))
                {
                    revertState.Invoke(gameObj->ObjectIndex, 0, ApplyFlag.Equipment);
                    ModifiedActorIds.Remove(gameObj->ObjectIndex);
                }
                return;
            }

            bool isPc = gameObj->ObjectKind == ObjectKind.Pc;
            bool isSelf = IsSelfOrPlayerClone(characterObject, localPlayer);

            if (Service.configuration.debugMode)
            {
                Plugin.OutputChatLine("Name: " + charName);
                Plugin.OutputChatLine("ObjectIndex: " + gameObj->ObjectIndex);
                Plugin.OutputChatLine("ObjectKind: " + gameObj->ObjectKind);
                Plugin.OutputChatLine("ModelType: " + customData.ModelType);
                Plugin.OutputChatLine("RaceFeatureType: " + customData.RaceFeatureType);
            }

            // Avoid some broken conversions
            if (customData.Race == Race.UNKNOWN)
                return;

            if (!isPc && gameObj->ObjectKind != ObjectKind.EventNpc && gameObj->ObjectKind != ObjectKind.BattleNpc && gameObj->ObjectKind != ObjectKind.Retainer)
                return;

            if (Service.configuration.noChild)
            {
                if (customData.ModelType == 4)
                {
                    if (customData.RaceFeatureType == 128)
                        customData.RaceFeatureType = 0;
                    customData.ModelType = 0;

                    foreach (string name in childNPCNames)
                    {
                        if (!string.IsNullOrEmpty(charName) && charName.Contains(name, StringComparison.OrdinalIgnoreCase))
                        {
                            switch (name)
                            {
                                case "Alphinaud":
                                    customData.FaceType = 1;
                                    customData.HairStyle = 169;
                                    break;
                                case "Alisaie":
                                    customData.FaceType = 4;
                                    customData.HairStyle = 174;
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                }
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
                if (!Service.configuration.noLala)
                    return;

            if (!dontLala)
                ChangeRace(customData, customizePtr, Service.configuration.SelectedRace, Service.configuration.SelectedGender);

            if (customData.ModelType == 4 && Service.configuration.childClothes)
                return;

            if (Service.configuration.noLala)
            {
                if (customData.Race == Race.LALAFELL)
                {
                    Random rnd = new Random();
                    int raceRnd = rnd.Next(7) + 1;
                    ChangeRace(customData, customizePtr, (Service.configuration.SelectedRace == Race.UNKNOWN || Service.configuration.SelectedRace == Race.LALAFELL) ? ConfigWindow.MapIndexToRace(raceRnd) : Service.configuration.SelectedRace, Service.configuration.SelectedGender);
                }
            }

            if (!dontStrip)
            {
                ModifiedActorIds.Add(gameObj->ObjectIndex);

                StripClothes(equipData, isSelf);
                if (isPc)
                    StripClothes(gameObj->ObjectIndex, isSelf);
            }
        }

        private static unsafe void ChangeRace(CharaCustomizeData customData, nint customizePtr, Race selectedRace, Gender selectedGender)
        {
            bool raceChange = (Service.configuration.SelectedRace != Race.UNKNOWN && customData.Race != Service.configuration.SelectedRace);
            bool sexChange = (Service.configuration.SelectedGender != Gender.UNKNOWN && customData.Gender != Service.configuration.SelectedGender);

            if (Service.configuration.SelectedRace != Race.UNKNOWN && Service.configuration.SelectedClan != Clan.UNKNOWN)
                raceChange = true;

            if (customData.Race == Race.LALAFELL && Service.configuration.noLala)
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
                customData.FaceType %= 4;
                customData.Gender = selectedGender;
            }

            if (raceChange || sexChange)
            {
                // Fur pattern should be 1-5 for hrothgar
                if (customData.Race == Race.HROTHGAR)
                    customData.LipColor = (byte)(1 + (customData.LipColor % 5));

                // Ears should be 1-4 for viera
                if (customData.Race == Race.VIERA)
                    customData.RaceFeatureType = (byte)(1 + (customData.RaceFeatureType % 4));

                customData.HairStyle = (byte)RaceMappings.SelectHairFor(customData.Race, customData.Gender, (Clan)customData.ModelType, customData.HairStyle);
            }

            Marshal.StructureToPtr(customData, customizePtr, true);
        }

        private static unsafe void StripClothes(ulong* equipData, bool isSelf)
        {
            Random rnd = new Random();
            int empRnd = (!Service.configuration.empLegsRandomSelf) ? ((isSelf) ? 0 : rnd.Next(2)) : rnd.Next(2);
            if (Service.configuration.stripHats) equipData[0] = (isSelf) ? 1U : 0;
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

        private static void StripClothes(int objectIndex, bool isSelf)
        {
            if (Service.glamourerApi?.SetItemApi == null) return;

            var setItem = Service.glamourerApi.SetItemApi;

            var noStains = new List<byte>();

            var accessorySlots = new[]
            {
                ApiEquipSlot.Ears,
                ApiEquipSlot.Neck,
                ApiEquipSlot.Wrists,
                ApiEquipSlot.RFinger,
                ApiEquipSlot.LFinger,
            };

            if (Service.configuration.stripHats)
            {
                setItem.Invoke(objectIndex, ApiEquipSlot.Head, 0, noStains, 0, 0);
            }
            if (Service.configuration.stripBodies)
            {
                setItem.Invoke(objectIndex, ApiEquipSlot.Body, 0, noStains, 0, 0);
            }
            if (Service.configuration.stripGloves)
            {
                setItem.Invoke(objectIndex, ApiEquipSlot.Hands, 0, noStains, 0, 0);
            }
            if (Service.configuration.stripBoots)
            {
                setItem.Invoke(objectIndex, ApiEquipSlot.Feet, 0, noStains, 0, 0);
            }
            if (Service.configuration.stripLegs)
            {
                uint legId = 0;

                if (Service.configuration.empLegs)
                {
                    if (Service.configuration.empLegsRandom)
                    {
                        Random rnd = new Random();

                        if (isSelf)
                        {
                            if (Service.configuration.empLegsRandomSelf)
                            {
                                legId = (rnd.Next(2) == 1) ? 10035U : 0;
                            }
                            else
                            {
                                legId = 0;
                            }
                        }
                        else
                        {
                            legId = (rnd.Next(2) == 1) ? 10035U : 0;
                        }
                    }
                    else
                    {
                        legId = 10035U;
                    }
                }

                setItem.Invoke(objectIndex, ApiEquipSlot.Legs, legId, noStains, 0, 0);
            }
            if (Service.configuration.stripAccessories)
            {
                foreach (var slot in accessorySlots)
                {
                    setItem.Invoke(objectIndex, slot, 0, noStains, 0, 0);
                }
            }
        }

        public void Dispose()
        {
            Service.configWindow.OnConfigChanged -= RefreshAllPlayers;
            Service.configWindow.OnConfigChangedSingleChar -= RefreshOnePlayer;
            glamourerSubscription?.Dispose();
        }
    }
}
