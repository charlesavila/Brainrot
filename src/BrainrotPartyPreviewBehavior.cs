using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using TaleWorlds.ScreenSystem;

namespace Brainrot
{
    public sealed class BrainrotPartyPreviewBehavior : CampaignBehaviorBase
    {
        private readonly Dictionary<object, object> watchedViews =
            new Dictionary<object, object>();
        private float refreshTimer;

        public override void RegisterEvents()
        {
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnTick(float dt)
        {
            refreshTimer += dt;
            if (refreshTimer < 0.1f || ScreenManager.TopScreen == null)
            {
                return;
            }

            refreshTimer = 0.0f;

            foreach (object partyVm in FindPartyViews(ScreenManager.TopScreen))
            {
                if (!watchedViews.ContainsKey(partyVm))
                {
                    watchedViews.Add(partyVm, partyVm);
                    EventInfo changedEvent =
                        partyVm.GetType().BaseType.GetEvent("PropertyChanged");
                    if (changedEvent != null)
                    {
                        changedEvent.AddEventHandler(
                            partyVm,
                            new PropertyChangedEventHandler(
                                OnPartyPropertyChanged));
                    }
                }

                ApplySelectedPreview(partyVm);
            }
        }

        private void OnPartyPropertyChanged(
            object sender,
            PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "CurrentCharacter" ||
                e.PropertyName == "SelectedCharacter")
            {
                ApplySelectedPreview(sender);
            }
        }

        private void ApplySelectedPreview(object partyVm)
        {
            PropertyInfo currentCharacterProperty =
                partyVm.GetType().GetProperty("CurrentCharacter");
            object partyCharacter = currentCharacterProperty == null
                ? null
                : currentCharacterProperty.GetValue(partyVm);
            if (partyCharacter == null)
            {
                return;
            }

            PropertyInfo characterProperty =
                partyCharacter.GetType().GetProperty("Character");
            CharacterObject character = characterProperty == null
                ? null
                : characterProperty.GetValue(partyCharacter) as CharacterObject;
            if (character == null)
            {
                return;
            }

            string troopId = character.StringId;
            if (troopId != null && troopId.Contains("."))
            {
                troopId = troopId.Substring(
                    troopId.LastIndexOf('.') + 1);
            }

            if (!BrainrotSubModule.ReplacementConfig.TryGetItemId(
                troopId,
                out string itemId))
            {
                return;
            }

            ItemObject memeItem =
                MBObjectManager.Instance.GetObject<ItemObject>(itemId);
            if (memeItem == null)
            {
                Debug.Print(
                    "Brainrot: Party preview item not registered: " +
                    itemId);
                return;
            }

            Equipment preview =
                character.FirstBattleEquipment.Clone(false);
            preview.AddEquipmentToSlotWithoutAgent(
                EquipmentIndex.Body,
                new EquipmentElement(memeItem));
            preview.AddEquipmentToSlotWithoutAgent(
                EquipmentIndex.Head,
                EquipmentElement.Invalid);
            preview.AddEquipmentToSlotWithoutAgent(
                EquipmentIndex.Cape,
                EquipmentElement.Invalid);
            preview.AddEquipmentToSlotWithoutAgent(
                EquipmentIndex.Gloves,
                EquipmentElement.Invalid);
            preview.AddEquipmentToSlotWithoutAgent(
                EquipmentIndex.Leg,
                EquipmentElement.Invalid);

            string equipmentCode =
                preview.CalculateEquipmentCode();
            Equipment decoded =
                Equipment.CreateFromEquipmentCode(equipmentCode);
            EquipmentElement decodedBody =
                decoded.GetEquipmentFromSlot(EquipmentIndex.Body);

            if (decodedBody.Item == null ||
                decodedBody.Item.StringId != memeItem.StringId)
            {
                Debug.Print(
                    "Brainrot: Party preview equipment-code round-trip failed. " +
                    "Expected=" + memeItem.StringId + ", Decoded=" +
                    (decodedBody.Item == null
                        ? "null"
                        : decodedBody.Item.StringId));
                return;
            }

            PropertyInfo selectedCharacterProperty =
                partyVm.GetType().GetProperty("SelectedCharacter");
            object selectedCharacter =
                selectedCharacterProperty == null
                    ? null
                    : selectedCharacterProperty.GetValue(partyVm);
            if (selectedCharacter == null)
            {
                return;
            }

            MethodInfo setEquipment =
                selectedCharacter.GetType().GetMethod(
                    "SetEquipment",
                    BindingFlags.Instance | BindingFlags.Public);
            setEquipment?.Invoke(
                selectedCharacter,
                new object[] { decoded });

            PropertyInfo equipmentCodeProperty =
                selectedCharacter.GetType().GetProperty("EquipmentCode");
            equipmentCodeProperty?.SetValue(
                selectedCharacter,
                equipmentCode);

            PropertyInfo tableauEnabledProperty =
                selectedCharacter.GetType().GetProperty(
                    "IsTableauEnabled");
            tableauEnabledProperty?.SetValue(
                selectedCharacter,
                true);

            Debug.Print(
                "Brainrot: Party preview body round-trip OK for " +
                troopId + ": " + memeItem.StringId);
        }

        private static IEnumerable<object> FindPartyViews(
            object root)
        {
            var found = new List<object>();
            var visited = new HashSet<object>();
            var queue = new Queue<object>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                object current = queue.Dequeue();
                if (current == null || visited.Contains(current))
                {
                    continue;
                }

                visited.Add(current);
                if (current.GetType().FullName ==
                    "TaleWorlds.CampaignSystem.ViewModelCollection.Party.PartyVM")
                {
                    found.Add(current);
                }

                FieldInfo[] fields =
                    current.GetType().GetFields(
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);

                foreach (FieldInfo field in fields)
                {
                    if (field.FieldType.IsPrimitive ||
                        field.FieldType == typeof(string) ||
                        field.FieldType.IsEnum)
                    {
                        continue;
                    }

                    object value;
                    try
                    {
                        value = field.GetValue(current);
                    }
                    catch
                    {
                        continue;
                    }

                    if (value != null)
                    {
                        queue.Enqueue(value);
                    }
                }
            }

            return found;
        }
    }
}
