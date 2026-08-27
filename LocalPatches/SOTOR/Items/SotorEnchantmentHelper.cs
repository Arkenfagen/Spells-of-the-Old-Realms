using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace SOTOR.Items
{

    public static class SotorEnchantmentHelper
    {
        public static ItemObject CreateEnchantedItem(ItemObject original, List<string> traitIds,
            string newName = null, bool playerCrafted = false)
        {
            if (original == null || traitIds == null || traitIds.Count == 0) return null;

            int salt = MBRandom.RandomInt();
            string newId = original.StringId + salt;
            string name = newName;
            if (string.IsNullOrEmpty(name))
            {

                var t = SotorText.GetObject("sotor_enchanted_default_name");
                t.SetTextVariable("ITEM", original.Name);
                t.SetTextVariable("MODIFIER",
                    SotorText.Rendered("sotor_loot_rarity_" + Math.Min(traitIds.Count, 4)));
                name = t.ToString();
            }

            var item = CreateItemCopy(original, newId, name, playerCrafted);
            SotorExtendedItemManager.RegisterItemTraits(item.StringId, traitIds);
            CampaignBehaviors.SotorEnchantingBehavior.Instance?.RecordEnchantedItem(item, original, traitIds, playerCrafted);
            SotorLog.Info($"Enchanted item created: {item.StringId} ({name}) traits=[{string.Join(",", traitIds)}]");
            return item;
        }

        private static ItemObject CreateItemCopy(ItemObject copyFrom, string newId, string newName, bool playerCrafted)
        {
            var item = new ItemObject();
            CopyItemProperties(item, copyFrom);
            item.StringId = newId;
            SetItemName(item, newName);
            item.Initialize();
            if (playerCrafted)
            {
                ItemObject.InitAsPlayerCraftedItem(ref item);
            }
            item.DetermineItemCategoryForItem();
            MBObjectManager.Instance.RegisterObject(item);
            item.AfterInitialized();
            return item;
        }

        public static void SetItemName(ItemObject item, string name)
        {
            AccessTools.Property(typeof(ItemObject), "Name")?.SetValue(item, new TextObject(name));
        }

        public static void CopyItemProperties(ItemObject item, ItemObject other)
        {
            CopyProp(item, other, "Culture");
            CopyProp(item, other, "ItemComponent");
            CopyProp(item, other, "MultiMeshName");
            CopyProp(item, other, "HolsterMeshName");
            CopyProp(item, other, "HolsterWithWeaponMeshName");
            CopyProp(item, other, "ItemHolsters");
            CopyProp(item, other, "HolsterPositionShift");
            CopyProp(item, other, "FlyingMeshName");
            CopyProp(item, other, "BodyName");
            CopyProp(item, other, "SkeletonName");
            CopyProp(item, other, "StaticAnimationName");
            CopyProp(item, other, "HolsterBodyName");
            CopyProp(item, other, "CollisionBodyName");
            CopyProp(item, other, "RecalculateBody");
            CopyProp(item, other, "PrefabName");
            CopyProp(item, other, "Name");
            CopyProp(item, other, "ItemFlags");
            CopyProp(item, other, "Value");
            CopyProp(item, other, "Weight");
            CopyProp(item, other, "Difficulty");
            CopyProp(item, other, "ArmBandMeshName");
            CopyProp(item, other, "IsFood");
            CopyProp(item, other, "ScaleFactor");
            CopyProp(item, other, "WeaponDesign");
            item.Type = other.Type;
        }

        private static readonly HashSet<string> _warnedProps = new HashSet<string>();

        private static void CopyProp(ItemObject item, ItemObject other, string propName)
        {
            var prop = AccessTools.Property(typeof(ItemObject), propName);
            if (prop == null)
            {
                if (_warnedProps.Add(propName))
                    SotorLog.Warn($"ItemObject property '{propName}' not found on this game version; skipped in item copy");
                return;
            }
            prop.SetValue(item, prop.GetValue(other));
        }
    }
}
