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
        public static HashSet<ActorKey> RevertedActorIds = new HashSet<ActorKey>();
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

            if (character.ObjectKind != Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Player && character.Name.TextValue != "")
                return false;

            if (character.Address == IntPtr.Zero || !character.IsValid())
            {
                Service.Log.Warning("Invalid character in IsSelfOrPlayerClone");
                return false;
            }

            uint objectIndex = character.ObjectIndex;

            Service.Log.Info($"Checking clone for ObjectIndex={objectIndex}, Address={character.Address:X}");

            bool isPotentialClone = (objectIndex >= 200 && objectIndex < 240) || (objectIndex >= 440 && objectIndex < 460);
            if (!isPotentialClone)
                return false;

            var targetCustomize = (character as ICharacter)?.Customize;
            var localCustomize = localPlayer.Customize;

            if (targetCustomize == null || localCustomize == null || targetCustomize.Length < 26 || localCustomize.Length < 26)
                return false;

            int[] indicesToCheck = { 0, 1, 4, 5, 6, 8, 9, 10, 11, 15, 20 };
            bool allMatch = true;

            foreach (int i in indicesToCheck)
            {
                if (targetCustomize[i] != localCustomize[i])
                {
                    if (Service.configuration.debugMode)
                        Service.Log.Info($"Mismatch at Index {i}: Target={targetCustomize[i]}, Local={localCustomize[i]}");
                    allMatch = false;
                }
            }
            if (allMatch)
            {
                Service.Log.Info("Customization matched, player's clone found.");
            }
            else
            {
                Service.Log.Info("This is not player's clone.");
            }

            return allMatch;
        }

        private void OnGlamourerStateChange(nint actorPtr, StateFinalizationType type)
        {
            try
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
            catch (Exception ex)
            {
                Service.Log.Error($"Error while handling Glamourer state change: {ex.Message}");
            }
        }

        public static void RefreshAllPlayers(bool force)
        {
            try
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

                        if (obj.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Companion) continue;

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
            catch (Exception ex)
            {
                Service.Log.Error($"Error while refreshing all players: {ex.Message}");
            }
        }

        private static void RefreshOnePlayer(string charName)
        {
            try
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

                if (objectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Companion) return;
                if (objectIndex == -1) return;

                Service.glamourerApi.RevertStateApi?.Invoke(objectIndex, 0, (ApplyFlag)0);
                Service.glamourerApi.RevertToAutomationApi?.Invoke(objectIndex, 0, (ApplyFlag)0);
                Service.penumbraApi.RedrawOne(objectIndex, RedrawType.Redraw);
            }
            catch (Exception ex)
            {
                Service.Log.Error($"Error while refreshing player {charName}: {ex.Message}");
            }
        }

        public struct ActorKey
        {
            public uint ObjectIndex { get; set; }
            public uint EntityId { get; set; }

            public ActorKey(uint objectIndex, uint entityId)
            {
                ObjectIndex = objectIndex;
                EntityId = entityId;
            }

            public override bool Equals(object? obj)
            {
                return obj is ActorKey other && ObjectIndex == other.ObjectIndex && EntityId == other.EntityId;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(ObjectIndex, EntityId);
            }

            public override string ToString()
            {
                return $"ObjectIndex={ObjectIndex}, EntityId={EntityId}";
            }
        }

        public static unsafe void OnCreatingCharacterBase(nint gameObjectAddress, Guid _1, nint _2, nint customizePtr, nint equipPtr)
        {
            try
            {
                if (gameObjectAddress == IntPtr.Zero)
                {
                    Service.Log.Error("Invalid gameObjectAddress in OnCreatingCharacterBase");
                    return;
                }

                var localPlayer = Service.clientState.LocalPlayer;
                var characterObject = Service.objectTable.FirstOrDefault(o => o.Address == gameObjectAddress);
                var gameObj = (GameObject*)gameObjectAddress;

                if (gameObj == null)
                {
                    Service.Log.Error("Null GameObject pointer in OnCreatingCharacterBase");
                    return;
                }
                if (gameObj->ObjectKind == ObjectKind.None)
                {
                    Service.Log.Error("Invalid ObjectKind in OnCreatingCharacterBase");
                    return;
                }

                var customData = Marshal.PtrToStructure<CharaCustomizeData>(customizePtr);
                var equipData = (ulong*)equipPtr;
                var charName = gameObj->NameString;
                string[] childNPCNames = { "Alphinaud", "Alisaie" };

                Service.Log.Info($"Processing ObjectIndex={gameObj->ObjectIndex}, Name={charName}, ObjectKind={gameObj->ObjectKind}");

                var actorKey = new ActorKey(gameObj->ObjectIndex, gameObj->EntityId);

                if (gameObj->ObjectKind == ObjectKind.Companion)
                    return;

                var revertState = Service.glamourerApi?.RevertStateApi;
                var revertAutomation = Service.glamourerApi?.RevertToAutomationApi;
                if (revertState == null || revertAutomation == null)
                    return;

                if (!Service.configuration.enabled)
                {
                    Service.Log.Info($"Accessing actor for {actorKey}");
                    if (!RevertedActorIds.Contains(actorKey))
                    {
                        Service.Log.Info($"Reverting state for {actorKey}");
                        revertState.Invoke(gameObj->ObjectIndex, 0, (ApplyFlag)0);
                        revertAutomation.Invoke(gameObj->ObjectIndex, 0, (ApplyFlag)0);
                        RevertedActorIds.Add(actorKey);
                    }
                    return;
                }

                if (RevertedActorIds.Count > 0)
                {
                    Service.Log.Info("Clearing RevertedActorIds");
                    RevertedActorIds.Clear();
                }

                bool isPc = gameObj->ObjectKind == ObjectKind.Pc;
                bool isSelf = IsSelfOrPlayerClone(characterObject, localPlayer);

                if (Service.configuration.debugMode)
                {
                    Plugin.OutputChatLine("Name: " + charName);
                    Plugin.OutputChatLine("ObjectIndex: " + gameObj->ObjectIndex);
                    Plugin.OutputChatLine("EntityId: " + gameObj->EntityId);
                    Plugin.OutputChatLine("ObjectKind: " + gameObj->ObjectKind);
                    Plugin.OutputChatLine("Race: " + customData.Race);
                    Plugin.OutputChatLine("Clan: " + customData.Tribe);
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
                    StripClothes(equipData, isSelf);
                    if (isPc)
                        StripClothes(gameObj->ObjectIndex, isSelf);
                }
            }
            catch (Exception ex)
            {
                Service.Log.Error($"Error while creating character base: {ex.Message}");
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
            try
            {
                int isEmperor = CheckRandom(isSelf);

                if (Service.configuration.stripHats) equipData[0] = (isSelf) ? 1U : 0;
                if (Service.configuration.stripBodies) equipData[1] = 0;
                if (Service.configuration.stripGloves) equipData[2] = 0;
                if (Service.configuration.stripLegs) equipData[3] = (isEmperor == 0 ? 0 : 279U);
                if (Service.configuration.stripBoots) equipData[4] = 0;
                if (Service.configuration.stripAccessories)
                {
                    for (int i = 5; i <= 9; ++i)
                        equipData[i] = 0;
                }
            }
            catch (Exception ex)
            {
                Service.Log.Error($"Error while stripping clothes for equipData: {ex.Message}");
            }
        }

        private static void StripClothes(int objectIndex, bool isSelf)
        {
            try
            {
                if (Service.glamourerApi?.GetStateApi == null || Service.glamourerApi?.SetItemApi == null)
                {
                    return;
                }

                var (returnCode, _) = Service.glamourerApi.GetStateApi.Invoke(objectIndex);
                if (returnCode == GlamourerApiEc.ActorNotFound)
                {
                    return;
                }

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

                int isEmperor = CheckRandom(isSelf);

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
                    setItem.Invoke(objectIndex, ApiEquipSlot.Legs, (isEmperor == 0) ? 0 : 10035U, noStains, 0, 0);
                }
                if (Service.configuration.stripAccessories)
                {
                    foreach (var slot in accessorySlots)
                    {
                        setItem.Invoke(objectIndex, slot, 0, noStains, 0, 0);
                    }
                }
            }
            catch (Exception ex)
            {
                Service.Log.Error($"Error while stripping clothes for object index {objectIndex}: {ex.Message}");
            }
        }

        private static int CheckRandom(bool isSelf)
        {
            Random rnd = new Random();
            int isEmperor = 0;
            if (Service.configuration.empLegs)
            {
                if (Service.configuration.empLegsRandom)
                {
                    isEmperor = (!Service.configuration.empLegsRandomSelf) ? ((isSelf) ? 1 : rnd.Next(2)) : rnd.Next(2);
                }
                else
                {
                    isEmperor = 1;
                }
            }
            else
            {
                if (Service.configuration.empLegsRandom)
                {
                    isEmperor = (!Service.configuration.empLegsRandomSelf) ? ((isSelf) ? 0 : rnd.Next(2)) : rnd.Next(2);
                }
                else
                {
                    isEmperor = 0;
                }
            }
            return isEmperor;
        }

        public void Dispose()
        {
            Service.configWindow.OnConfigChanged -= RefreshAllPlayers;
            Service.configWindow.OnConfigChangedSingleChar -= RefreshOnePlayer;
            glamourerSubscription?.Dispose();
        }
    }
}
