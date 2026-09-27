using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Glamourer.Api.Enums;
using Glamourer.Api.IpcSubscribers;
using Newtonsoft.Json.Linq;
using OopsAllNudist.Windows;
using Penumbra.Api.Enums;
using System;
using System.Collections.Concurrent;
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

        private static bool HasRunOnce = false;

        public Drawer()
        {
            Service.configWindow.OnConfigChanged += RefreshAllPlayers;
            Service.configWindow.OnConfigChangedSingleChar += RefreshOnePlayer;
            Service.Framework.Update += OnFrameworkUpdate;
            Service.clientState.TerritoryChanged += OnTerritoryChanged;
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

            if (character.ObjectKind != Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Pc && character.Name.TextValue != "")
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

            if (character is not ICharacter iCharacter)
                return false;

            Span<byte> targetCustomize = iCharacter.Customize;
            Span<byte> localCustomize = localPlayer.Customize;

            if (targetCustomize.Length < 26 || localCustomize.Length < 26)
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
                Service.Log.Info("Customization matched, player's clone found.");
            else
                Service.Log.Info("This is not player's clone.");

            return allMatch;
        }

        private void OnGlamourerStateChange(nint actorPtr, StateFinalizationType type)
        {
            try
            {
                var actor = Service.objectTable.FirstOrDefault(o => o.Address == actorPtr);

                if (actor != null)
                {
                    if (actor is IPlayerCharacter && ShouldResetDeathCounterOnStateChange(type) && !IsGlamourerResetSuppressed(actor.ObjectIndex))
                    {
                        if (DeathStates.TryGetValue(actor.Name.TextValue, out var actorState) && actorState.StripApplied)
                        {
                            if (RemoveStrip(actor.ObjectIndex, actor.Name.TextValue))
                                actorState.StripApplied = false;
                        }

                        ResetDeathCounter(actor.Name.TextValue);

                        if (Service.configuration.debugMode)
                            Plugin.OutputChatLine($"Glamourer change ({type}) reset the death counter for {actor.Name}.");
                    }

                    if (Service.configuration.debugMode)
                    {
                        Plugin.OutputChatLine($"Glamourer change ({type}) detected on {actor.Name}. Redrawing.");
                    }
                    Service.penumbraApi.RedrawOne(actor.ObjectIndex, RedrawType.Redraw);
                }
                else if (Service.configuration.debugMode)
                {
                    Plugin.OutputChatLine($"Glamourer change ({type}) detected, but the actor could not be resolved.");
                }
            }
            catch (Exception ex)
            {
                Service.Log.Error($"Error while handling Glamourer state change: {ex.Message}");
            }
        }

        private static bool ShouldResetDeathCounterOnStateChange(StateFinalizationType type)
        {
            return type != StateFinalizationType.ModelChange;
        }

        public static void RefreshAllPlayers(bool force)
        {
            try
            {
                Plugin.OutputChatLine("Refreshing all players");

                Service.Framework.RunOnFrameworkThread(() =>
                {
                    var localPlayer = Service.objectTable.LocalPlayer;

                    foreach (var obj in Service.objectTable)
                    {
                        if (!obj.IsValid()) continue;
                        if (obj is not ICharacter) continue;
                        if (Service.configuration.IsWhitelisted(obj.Name.TextValue)) continue;

                        if (obj.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Companion) continue;

                        bool isPc = obj is IPlayerCharacter;
                        bool isSelf = IsSelfOrPlayerClone(obj, localPlayer);

                        if (Service.configuration.dontMorphSelf && Service.configuration.dontStripSelf && isSelf) continue;
                        if (!force && Service.configuration.dontMorphPC && Service.configuration.dontStripPC && isPc && !isSelf) continue;
                        if (!force && Service.configuration.dontMorphNPC && Service.configuration.dontStripNPC && !isPc) continue;

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

        #region Strip on Death

        private sealed class DeathState
        {
            public int DeathCount;
            public uint JobId;
            public bool WasDead;
            public bool StripAll;

            public string? StateSnapshot;
            public bool SnapshotPending;
            public DateTime SnapshotDueAt;
            public DateTime NextCheckAt;

            public bool ZoneRestoreArmed;
            public DateTime ZoneRestoreArmedUntil;

            public bool StripApplied;

            public DateTime NextRemovalRetryAt;
        }

        private static readonly ConcurrentDictionary<string, DeathState> DeathStates = new();
        private static int LastGearsetIndex = -1;

        private static readonly TimeSpan ZoneRestoreWindow = TimeSpan.FromSeconds(5);

        private static void OnTerritoryChanged(uint territoryType)
        {
            var until = DateTime.UtcNow + ZoneRestoreWindow;

            foreach (var state in DeathStates.Values)
            {
                if (state.DeathCount > 0)
                {
                    state.ZoneRestoreArmed = true;
                    state.ZoneRestoreArmedUntil = until;
                }
            }
        }

        public static void ResetDeathCounters()
        {
            DeathStates.Clear();
        }

        private static void ResetDeathCounter(string name)
        {
            if (string.IsNullOrEmpty(name))
                return;

            if (DeathStates.TryGetValue(name, out var state))
                ClearDeathCount(state);
        }

        private static void ClearDeathCount(DeathState state)
        {
            state.DeathCount = 0;
            state.StateSnapshot = null;
            state.SnapshotPending = false;
            state.ZoneRestoreArmed = false;
        }

        private static bool HasActiveDeathStrip(string name)
        {
            return !string.IsNullOrEmpty(name)
                && DeathStates.TryGetValue(name, out var state)
                && state.StripApplied;
        }

        private static bool RemoveStrip(int objectIndex, string name)
        {
            try
            {
                var revertState = Service.glamourerApi?.RevertStateApi;
                var revertAutomation = Service.glamourerApi?.RevertToAutomationApi;
                if (revertState == null || revertAutomation == null)
                    return false;

                SuppressGlamourerReset(objectIndex);

                var result = revertState.Invoke(objectIndex, 0, (ApplyFlag)0);
                if (result != GlamourerApiEc.Success && result != GlamourerApiEc.NothingDone)
                {
                    Service.Log.Debug($"Could not release strip-on-death for {name}: {result}");
                    return false;
                }

                revertAutomation.Invoke(objectIndex, 0, (ApplyFlag)0);
                Service.penumbraApi?.RedrawOne(objectIndex, RedrawType.Redraw);

                if (Service.configuration.debugMode)
                    Plugin.OutputChatLine($"Released strip-on-death for {name}.");

                return true;
            }
            catch (Exception ex)
            {
                Service.Log.Error($"Error while releasing strip-on-death for {name}: {ex.Message}");
                return false;
            }
        }

        private static readonly Dictionary<int, DateTime> GlamourerResetSuppression = new();
        private static readonly TimeSpan GlamourerResetSuppressionWindow = TimeSpan.FromMilliseconds(500);

        private static void SuppressGlamourerReset(int objectIndex)
        {
            GlamourerResetSuppression[objectIndex] = DateTime.UtcNow + GlamourerResetSuppressionWindow;
        }

        private static bool IsGlamourerResetSuppressed(int objectIndex)
        {
            if (GlamourerResetSuppression.TryGetValue(objectIndex, out var expiry))
            {
                if (DateTime.UtcNow <= expiry)
                    return true;

                GlamourerResetSuppression.Remove(objectIndex);
            }

            return false;
        }

        private static void OnFrameworkUpdate(IFramework framework)
        {
            try
            {
                if (!Service.clientState.IsLoggedIn)
                    return;

                var localPlayer = Service.objectTable.LocalPlayer;
                if (localPlayer == null)
                    return;

                UpdateGearsetReset(localPlayer);

                foreach (var obj in Service.objectTable)
                {
                    if (obj is not IPlayerCharacter pc)
                        continue;
                    if (!pc.IsValid())
                        continue;

                    string name = pc.Name.TextValue;
                    if (string.IsNullOrEmpty(name))
                        continue;

                    uint jobId = pc.ClassJob.RowId;

                    if (!DeathStates.TryGetValue(name, out var state))
                    {
                        DeathStates[name] = new DeathState { JobId = jobId, WasDead = pc.IsDead };
                        continue;
                    }

                    if (state.JobId != jobId)
                    {
                        state.JobId = jobId;

                        if (state.StripApplied && RemoveStrip(pc.ObjectIndex, name))
                            state.StripApplied = false;

                        ClearDeathCount(state);
                        state.WasDead = pc.IsDead;
                        continue;
                    }

                    bool dead = pc.IsDead;
                    if (dead && !state.WasDead)
                    {
                        state.WasDead = true;
                        HandleDeath(pc, name, state);
                    }
                    else if (!dead && state.WasDead)
                    {
                        state.WasDead = false;
                    }

                    CheckReequip(pc, state);

                    if (state.StripApplied && state.DeathCount == 0 && DateTime.UtcNow >= state.NextRemovalRetryAt)
                    {
                        state.NextRemovalRetryAt = DateTime.UtcNow.AddSeconds(1);

                        if (RemoveStrip(pc.ObjectIndex, name))
                            state.StripApplied = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Service.Log.Error($"Error in strip on death tracker: {ex.Message}");
            }
        }

        private static unsafe void UpdateGearsetReset(IPlayerCharacter localPlayer)
        {
            int currentIndex = -1;

            var gearsetModule = RaptureGearsetModule.Instance();
            if (gearsetModule != null)
                currentIndex = gearsetModule->CurrentGearsetIndex;

            if (currentIndex == LastGearsetIndex)
                return;

            LastGearsetIndex = currentIndex;

            if (currentIndex < 0)
                return;

            string name = localPlayer.Name.TextValue;
            if (string.IsNullOrEmpty(name))
                return;

            if (DeathStates.TryGetValue(name, out var state))
            {
                if (state.StripApplied && RemoveStrip(localPlayer.ObjectIndex, name))
                    state.StripApplied = false;

                ClearDeathCount(state);
                state.WasDead = localPlayer.IsDead;

                if (Service.configuration.debugMode)
                    Plugin.OutputChatLine($"Gearset change detected. Death counter reset for {name}.");
            }
        }

        private static void HandleDeath(IPlayerCharacter pc, string name, DeathState state)
        {
            var configuration = Service.configuration;

            bool isSelf = IsSelfOrPlayerClone(pc, Service.objectTable.LocalPlayer);
            bool enabled = isSelf ? configuration.stripOnDeathSelf : configuration.stripOnDeathPC;
            if (!enabled)
                return;

            var getState = Service.glamourerApi?.GetStateApi;
            if (getState != null)
            {
                var (resultCode, _) = getState.Invoke(pc.ObjectIndex);
                if (resultCode == GlamourerApiEc.InvalidKey)
                    return;
            }

            if (Service.clientState.IsPvP && !configuration.stripOnDeathInPvP)
                return;

            if (configuration.IsWhitelisted(name))
                return;

            if (!PassesDeathStripFilters(pc))
                return;

            state.DeathCount++;

            bool stripAll = state.DeathCount >= 2;

            state.StripAll = stripAll;
            state.StripApplied = true;
            state.NextRemovalRetryAt = DateTime.MinValue;

            StripClothes(pc.ObjectIndex, isSelf, stripAll);

            state.StateSnapshot = null;
            state.SnapshotPending = true;
            state.SnapshotDueAt = DateTime.UtcNow.AddMilliseconds(500);
            state.NextCheckAt = state.SnapshotDueAt;

            if (configuration.debugMode)
                Plugin.OutputChatLine($"Strip on death (count {state.DeathCount}, all={stripAll}) applied to {name}.");
        }

        private static void CheckReequip(IPlayerCharacter pc, DeathState state)
        {
            if (state.DeathCount <= 0)
            {
                state.StateSnapshot = null;
                state.SnapshotPending = false;
                return;
            }

            if (DateTime.UtcNow < state.NextCheckAt)
                return;

            state.NextCheckAt = DateTime.UtcNow.AddMilliseconds(500);

            var getState = Service.glamourerApi?.GetStateApi;
            if (getState == null)
                return;

            var (resultCode, stateObject) = getState.Invoke(pc.ObjectIndex);
            if (resultCode != GlamourerApiEc.Success || stateObject == null)
                return;

            string current = stateObject.ToString(Newtonsoft.Json.Formatting.None);

            if (state.SnapshotPending)
            {
                if (DateTime.UtcNow >= state.SnapshotDueAt)
                {
                    state.StateSnapshot = current;
                    state.SnapshotPending = false;

                    if (Service.configuration.debugMode)
                        Plugin.OutputChatLine($"Death snapshot taken for {pc.Name}.");
                }
                return;
            }

            if (state.StateSnapshot == null)
            {
                state.StateSnapshot = current;
                return;
            }

            if (!string.Equals(state.StateSnapshot, current, StringComparison.Ordinal))
            {
                if (state.ZoneRestoreArmed && DateTime.UtcNow < state.ZoneRestoreArmedUntil)
                {
                    state.ZoneRestoreArmed = false;
                    state.StripApplied = true;

                    bool isSelf = IsSelfOrPlayerClone(pc, Service.objectTable.LocalPlayer);
                    StripClothes(pc.ObjectIndex, isSelf, state.StripAll);

                    state.StateSnapshot = null;
                    state.SnapshotPending = true;
                    state.SnapshotDueAt = DateTime.UtcNow.AddMilliseconds(500);
                    state.NextCheckAt = state.SnapshotDueAt;

                    if (Service.configuration.debugMode)
                        Plugin.OutputChatLine($"Re-applied strip-on-death for {pc.Name} after a territory change.");

                    return;
                }

                state.ZoneRestoreArmed = false;
                state.StripApplied = false;
                ClearDeathCount(state);

                if (Service.configuration.debugMode)
                    Plugin.OutputChatLine($"Glamourer state changed on {pc.Name}. Death counter reset.");
            }
        }

        private static bool PassesDeathStripFilters(ICharacter character)
        {
            var configuration = Service.configuration;
            var customize = character.CustomizeData;

            var race = (Race)customize.Race;
            var gender = (Gender)customize.Sex;

            if (configuration.dontStripMale && gender == Gender.MALE)
                return false;
            if (configuration.dontStripFemale && gender == Gender.FEMALE)
                return false;

            if (configuration.SelectedGender != Gender.UNKNOWN && gender != configuration.SelectedGender)
                return false;

            bool isLala = race == Race.LALAFELL;

            if (isLala && configuration.noLala && configuration.dontStripLala)
                return false;

            if (configuration.SelectedRace != Race.UNKNOWN)
            {
                bool lalaConverted = isLala && configuration.noLala;
                if (!lalaConverted && race != configuration.SelectedRace)
                    return false;
            }

            return true;
        }

        #endregion

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
                if (!HasRunOnce)
                {
                    if (!Service.configuration.enabled)
                        return;
                    HasRunOnce = true;
                }

                if (gameObjectAddress == IntPtr.Zero)
                {
                    Service.Log.Error("Invalid gameObjectAddress in OnCreatingCharacterBase");
                    return;
                }

                var localPlayer = Service.objectTable.LocalPlayer;
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

                bool isPc = gameObj->ObjectKind == ObjectKind.Pc;
                bool isSelf = IsSelfOrPlayerClone(characterObject, localPlayer);
                bool isMale = customData.Gender == Gender.MALE;
                bool isFemale = customData.Gender == Gender.FEMALE;

                var charName = gameObj->NameString;
                string[] childNPCNames = { "Alphinaud", "Alisaie" };
                string[] specialNPCs = { "Esteem", "Gaia", "Gosetsu", "Gungnir", "Odin" };

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

                Service.Log.Info($"Processing ObjectIndex={gameObj->ObjectIndex}, Name={charName}, ObjectKind={gameObj->ObjectKind}");

                // Avoid some broken conversions
                if (customData.Race == Race.UNKNOWN)
                    return;

                if (Service.configuration.noChild)
                {
                    if (customData.ModelType == 4)
                    {
                        if (customData.RaceFeatureType == 128)
                            customData.RaceFeatureType = 0;
                        customData.ModelType = 1;

                        if (!isPc)
                        {
                            foreach (string childName in childNPCNames)
                            {
                                if (!string.IsNullOrEmpty(charName) && charName.Contains(childName, StringComparison.OrdinalIgnoreCase))
                                {
                                    switch (childName)
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
                    }
                    Marshal.StructureToPtr(customData, customizePtr, true);
                }

                if (!isPc)
                {
                    foreach (string specialName in specialNPCs)
                    {
                        if (!string.IsNullOrEmpty(charName) && charName.Contains(specialName, StringComparison.OrdinalIgnoreCase))
                        {
                            switch (specialName)
                            {
                                case "Gosetsu":
                                    customData.ModelType = 1;
                                    Marshal.StructureToPtr(customData, customizePtr, true);
                                    break;
                                default:
                                    return;
                            }
                        }
                    }
                }

                if (!isPc && gameObj->ObjectKind != ObjectKind.EventNpc && gameObj->ObjectKind != ObjectKind.BattleNpc && gameObj->ObjectKind != ObjectKind.Retainer)
                    return;

                if (gameObj->ObjectKind == ObjectKind.Companion)
                    return;

                var getState = Service.glamourerApi?.GetStateApi;
                if (getState != null)
                {
                    var (resultCode, _) = getState.Invoke(gameObj->ObjectIndex);
                    bool isLocked = resultCode == Glamourer.Api.Enums.GlamourerApiEc.InvalidKey;
                    //Service.Log.Debug($"[GlamourerState] {charName} (idx={gameObj->ObjectIndex}) IsLocked={isLocked} (ApiEc={resultCode})");
                    if (isLocked) return;
                }

                var revertState = Service.glamourerApi?.RevertStateApi;
                var revertAutomation = Service.glamourerApi?.RevertToAutomationApi;
                if (revertState == null || revertAutomation == null)
                    return;

                var actorKey = new ActorKey(gameObj->ObjectIndex, gameObj->EntityId);

                if (!Service.configuration.enabled)
                {
                    if (HasActiveDeathStrip(charName))
                    {
                        Service.Log.Info($"Skipping revert for {charName}: active strip-on-death.");

                        if (Service.configuration.debugMode)
                            Plugin.OutputChatLine($"Keeping strip-on-death for {charName} across a model rebuild.");

                        return;
                    }

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

                bool dontMorph = Service.configuration.dontMorphSelf && isSelf;
                bool dontStrip = Service.configuration.dontStripSelf && isSelf;

                dontMorph |= Service.configuration.dontMorphPC && isPc && !isSelf;
                dontMorph |= Service.configuration.dontMorphNPC && !isPc;

                dontStrip |= Service.configuration.dontStripPC && isPc && !isSelf;
                dontStrip |= Service.configuration.dontStripNPC && !isPc;
                dontStrip |= Service.configuration.dontStripMale && isMale;
                dontStrip |= Service.configuration.dontStripFemale && isFemale;

                if (Service.configuration.IsWhitelisted(charName))
                    return;

                if (dontMorph && dontStrip)
                    if (!Service.configuration.noLala)
                        return;

                if (!dontMorph)
                    ChangeRace(customData, customizePtr, Service.configuration.SelectedRace, Service.configuration.SelectedGender);

                if (customData.ModelType == 4 && Service.configuration.childClothes)
                    return;

                if (Service.configuration.noLala)
                {
                    if (customData.Race == Race.LALAFELL)
                    {
                        if (Service.configuration.dontStripLala)
                        {
                            dontStrip = true;
                        }
                        else
                        {
                            Random rnd = new Random();
                            int raceRnd = rnd.Next(7) + 1;
                            ChangeRace(customData, customizePtr, (Service.configuration.SelectedRace == Race.UNKNOWN || Service.configuration.SelectedRace == Race.LALAFELL) ? ConfigWindow.MapIndexToRace(raceRnd) : Service.configuration.SelectedRace, Service.configuration.SelectedGender);
                        }
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

            if (customData.Race == Race.LALAFELL && Service.configuration.noLala && !Service.configuration.dontStripLala)
                raceChange = true;

            if (raceChange)
            {
                var clan = (Service.configuration.SelectedClan == Clan.UNKNOWN) ? (byte)(1-(customData.Tribe % 2)) : (byte)Service.configuration.SelectedClan;
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

                if (Service.configuration.stripHats) equipData[0] = isSelf ? 1U : 0;
                if (Service.configuration.stripBodies) equipData[1] = 0;
                if (Service.configuration.stripGloves) equipData[2] = 0;
                if (Service.configuration.stripLegs) equipData[3] = (isEmperor == 0) ? 0 : 279U;
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

        private static void StripClothes(int objectIndex, bool isSelf, bool stripAll = false)
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

                SuppressGlamourerReset(objectIndex);

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

                bool doHats = stripAll || Service.configuration.stripHats;
                bool doBodies = stripAll || Service.configuration.stripBodies;
                bool doGloves = stripAll || Service.configuration.stripGloves;
                bool doBoots = stripAll || Service.configuration.stripBoots;
                bool doLegs = stripAll || Service.configuration.stripLegs;
                bool doAccessories = stripAll || Service.configuration.stripAccessories;

                if (doHats)
                {
                    setItem.Invoke(objectIndex, ApiEquipSlot.Head, 0, noStains, 0, 0);
                }
                if (doBodies)
                {
                    setItem.Invoke(objectIndex, ApiEquipSlot.Body, 0, noStains, 0, 0);
                }
                if (doGloves)
                {
                    setItem.Invoke(objectIndex, ApiEquipSlot.Hands, 0, noStains, 0, 0);
                }
                if (doBoots)
                {
                    setItem.Invoke(objectIndex, ApiEquipSlot.Feet, 0, noStains, 0, 0);
                }
                if (doLegs)
                {
                    setItem.Invoke(objectIndex, ApiEquipSlot.Legs, (isEmperor == 0) ? 0 : 10035U, noStains, 0, 0);
                }
                if (doAccessories)
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
                    isEmperor = (!Service.configuration.empLegsRandomSelf) ? (isSelf ? 1 : rnd.Next(2)) : rnd.Next(2);
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
                    isEmperor = (!Service.configuration.empLegsRandomSelf) ? (isSelf ? 0 : rnd.Next(2)) : rnd.Next(2);
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
            Service.Framework.Update -= OnFrameworkUpdate;
            Service.clientState.TerritoryChanged -= OnTerritoryChanged;
            glamourerSubscription?.Dispose();
            HasRunOnce = false;
        }
    }
}
