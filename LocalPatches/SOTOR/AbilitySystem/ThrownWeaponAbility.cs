using System;
using SOTOR.Extensions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace SOTOR.AbilitySystem
{

    public class ThrownWeaponAbility : Spell
    {

        public const string AmberJavelinItemId = "sotor_amber_javelin";

        public ThrownWeaponAbility(AbilityTemplate template)
            : base(template)
        {
        }

        public override bool IsThrownWeapon => true;

        public override bool TryCast(Agent casterAgent, SotorTarget preferredTarget, out TextObject failureReason)
        {
            failureReason = null;

            if (casterAgent == null || Mission.Current == null)
            {
                failureReason = new TextObject("{=sotor_cast_no_context}No mission context.");
                return false;
            }

            if (IsDisabled(casterAgent, out failureReason))
            {
                SotorLog.Info($"TryCast {StringID} (thrown): blocked — {failureReason?.ToString() ?? "disabled"}.");
                return false;
            }

            try
            {
                var item = MBObjectManager.Instance.GetObject<ItemObject>(AmberJavelinItemId);
                if (item == null)
                {
                    failureReason = new TextObject("{=sotor_cast_no_item}Amber javelin item missing.");
                    SotorLog.Warn($"TryCast {StringID} (thrown): ItemObject '{AmberJavelinItemId}' not found.");
                    return false;
                }

                if ((item.ItemFlags & ItemFlags.QuickFadeOut) == 0)
                {
                    item.SetItemFlagsForCosmetics(item.ItemFlags | ItemFlags.QuickFadeOut);
                }

                var preCastWielded = casterAgent.GetPrimaryWieldedItemIndex();
                var weapon = new MissionWeapon(item, null, null, 1);
                casterAgent.EquipWeaponToExtraSlotAndWield(ref weapon);

                SotorThrownJavelinMissionLogic.Instance?.OnAmberJavelinReadied(casterAgent, this, preCastWielded);

                return true;
            }
            catch (Exception ex)
            {
                failureReason = new TextObject("{=sotor_cast_thrown_failed}Amber javelin ready failed.");
                SotorLog.Error($"TryCast {StringID} (thrown) EXCEPTION: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        public void GrantThrowSpellcraftXp(Hero hero)
        {
            if (hero != null)
            {
                GrantSpellcraftXp(hero);
            }
        }

    }
}
