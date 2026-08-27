using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;

namespace SOTOR.Items
{

    [HarmonyPatch]
    public static class SotorInventoryPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(SPInventoryVM), MethodType.Constructor,
            typeof(InventoryLogic), typeof(bool), typeof(Func<WeaponComponentData, ItemObject.ItemUsageSetFlags>))]
        public static void ReplaceItemMenu(SPInventoryVM __instance,
            InventoryLogic ____inventoryLogic,
            Func<WeaponComponentData, ItemObject.ItemUsageSetFlags> ____getItemUsageSetFlags)
        {
            try
            {
                var resetComparedItems = (Action<ItemVM, int>)Delegate.CreateDelegate(
                    typeof(Action<ItemVM, int>), __instance, "ResetComparedItems");
                var getItemFromIndex = (Func<EquipmentIndex, SPItemVM>)Delegate.CreateDelegate(
                    typeof(Func<EquipmentIndex, SPItemVM>), __instance, "GetItemFromIndex");
                if (____inventoryLogic == null || ____getItemUsageSetFlags == null) return;

                __instance.ItemMenu?.OnFinalize();
                __instance.ItemMenu = new SotorItemMenuVM(resetComparedItems, ____inventoryLogic,
                    ____getItemUsageSetFlags, getItemFromIndex);
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorInventoryPatches.ReplaceItemMenu failed: {ex.Message} (native tooltip stays)");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ItemMenuVM), nameof(ItemMenuVM.SetItem))]
        public static void AfterSetItem(ItemMenuVM __instance, SPItemVM item)
        {
            if (__instance is SotorItemMenuVM vm)
            {
                try { vm.SetItemExtra(item); }
                catch (Exception ex) { SotorLog.Warn($"SetItemExtra failed: {ex.Message}"); }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Helpers.InventoryScreenHelper), nameof(Helpers.InventoryScreenHelper.CloseScreen))]
        public static void AfterCloseInventory(bool fromCancel)
        {
            try
            {
                if (TaleWorlds.Core.GameStateManager.Current?.ActiveState
                    is TaleWorlds.CampaignSystem.GameState.InventoryState) return;

                if (fromCancel) CampaignBehaviors.SotorBlueprintBookBehavior.DiscardStagedReads();
                else CampaignBehaviors.SotorBlueprintBookBehavior.CommitStagedReads();
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"AfterCloseInventory failed: {ex.Message}");
            }
        }
    }
}
