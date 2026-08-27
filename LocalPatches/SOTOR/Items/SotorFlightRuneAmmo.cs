using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace SOTOR.Items
{

    public static class SotorFlightRuneAmmo
    {
        public const string TraitId = "dw_master_rune_flight";

        private static readonly HashSet<long> ProcessedHitKeys = new HashSet<long>();
        private static EquipmentIndex? _lastThrownSlot;
        private static int? _pendingAgentIndex;
        private static EquipmentIndex? _pendingSlot;
        private static short? _pendingAmount;

        public static void ClearBattleState()
        {
            ProcessedHitKeys.Clear();
            _lastThrownSlot = null;
            ClearPending();
        }

        private static void ClearPending()
        {
            _pendingAgentIndex = null;
            _pendingSlot = null;
            _pendingAmount = null;
        }

        public static void RecordThrowStart(Agent shooter, EquipmentIndex weaponIndex)
        {
            if (shooter == null || !shooter.IsMainAgent) return;
            ClearPending();
            _lastThrownSlot = weaponIndex;
        }

        public static void ApplyPendingSync(Agent attacker)
        {
            if (attacker == null || !attacker.IsActive()) return;
            if (!_pendingAmount.HasValue || !_pendingSlot.HasValue) return;
            if (_pendingAgentIndex != attacker.Index) return;
            attacker.SetWeaponAmountInSlot(_pendingSlot.Value, _pendingAmount.Value, false);
            SotorLog.Debug($"FlightRune: re-applied {_pendingAmount.Value} to slot {_pendingSlot.Value} after the native throw settled");
            ClearPending();
        }

        public static bool ItemHasFlightRune(ItemObject item, Agent agent)
        {
            if (item == null || !item.HasAnyTrait(agent)) return false;
            foreach (var trait in item.GetTraits(agent))
            {
                if (trait.ItemTraitStringId == TraitId) return true;
            }
            return false;
        }

        private static bool IsThrownAmmo(WeaponComponentData usage)
        {
            if (usage == null || usage.IsMeleeWeapon) return false;
            return usage.IsRangedWeapon || usage.IsConsumable
                || usage.WeaponClass == WeaponClass.Javelin
                || usage.WeaponClass == WeaponClass.ThrowingAxe
                || usage.WeaponClass == WeaponClass.ThrowingKnife;
        }

        private static long BuildHitKey(Agent attacker, Agent victim, in AttackCollisionData collisionData)
        {
            int missileIndex = collisionData.AffectorWeaponSlotOrMissileIndex;
            int stamp = Mission.Current != null ? (int)(Mission.Current.CurrentTime * 200f) : 0;
            return ((long)(attacker?.Index ?? 0) << 42)
                 | ((long)(victim?.Index ?? 0) << 21)
                 | (uint)(missileIndex ^ stamp);
        }

        private static bool IsBlockedHit(in AttackCollisionData collisionData, in Blow blow)
        {
            if (collisionData.MissileBlockedWithWeapon || collisionData.AttackBlockedWithShield) return true;

            int damage = blow.InflictedDamage > 0 ? blow.InflictedDamage : collisionData.InflictedDamage;
            return damage <= 0;
        }

        public static void AttemptRecharge(Agent attacker, Agent victim, in Blow blow,
            in MissionWeapon hitWeapon, in AttackCollisionData collisionData, bool killPostfixOnly = false)
        {
            if (attacker == null || victim == null || attacker == victim) return;
            if (!attacker.IsMainAgent) return;

            bool victimAlive = victim.IsActive() && victim.Health > 0f && !victim.IsFadingOut();
            if (killPostfixOnly && victimAlive) return;
            if (IsBlockedHit(collisionData, blow)) return;
            if (hitWeapon.IsEmpty || !IsThrownAmmo(hitWeapon.CurrentUsageItem)) return;

            long key = BuildHitKey(attacker, victim, collisionData);
            if (ProcessedHitKeys.Contains(key)) return;
            if (TryRechargeSlot(attacker, hitWeapon)) ProcessedHitKeys.Add(key);
        }

        private static bool TryRechargeSlot(Agent attacker, in MissionWeapon hitWeapon)
        {
            if (!attacker.IsActive()) return false;

            if (_lastThrownSlot.HasValue && TryIncrementSlot(attacker, _lastThrownSlot.Value)) return true;

            string thrownId = hitWeapon.Item?.StringId;
            for (int i = 0; i < (int)EquipmentIndex.NumAllWeaponSlots; i++)
            {
                var slot = (EquipmentIndex)i;
                if (thrownId != null)
                {
                    var held = attacker.Equipment[slot];
                    if (held.IsEmpty || held.Item?.StringId != thrownId) continue;
                }
                if (TryIncrementSlot(attacker, slot)) return true;
            }
            return false;
        }

        private static bool TryIncrementSlot(Agent attacker, EquipmentIndex slot)
        {
            var weapon = attacker.Equipment[slot];
            if (weapon.IsEmpty || weapon.Item == null) return false;
            if (!ItemHasFlightRune(weapon.Item, attacker)) return false;
            if (!IsThrownAmmo(weapon.CurrentUsageItem)) return false;

            short amount = weapon.Amount;
            short target;
            if (amount <= 0)
            {

                if (!_lastThrownSlot.HasValue || _lastThrownSlot.Value != slot) return false;
                target = 1;
            }
            else
            {
                target = (short)(amount + 1);
            }

            var equipment = attacker.Equipment;
            short max = equipment[slot].ModifiedMaxAmount;
            equipment.SetAmountOfSlot(slot, target, target > max);

            _pendingAgentIndex = attacker.Index;
            _pendingSlot = slot;
            _pendingAmount = target;
            SotorLog.Debug($"HitScript AmmoRechargeOnHit fired (slot {slot}: {amount} -> {target}, max {max}) for {attacker.Name}");
            return true;
        }
    }
}
