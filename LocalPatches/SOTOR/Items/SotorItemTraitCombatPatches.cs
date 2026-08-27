using System;
using HarmonyLib;
using SandBox.GameComponents;
using SOTOR.AbilitySystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace SOTOR.Items
{

    [HarmonyPatch(typeof(SandboxAgentApplyDamageModel), nameof(SandboxAgentApplyDamageModel.ApplyGeneralDamageModifiers))]
    public static class SotorItemTraitDamagePatch
    {
        public static void Postfix(ref float __result, in AttackInformation attackInformation, in AttackCollisionData collisionData)
        {
            try
            {
                if (__result <= 0f) return;
                Agent attacker = attackInformation.AttackerAgent;
                Agent victim = attackInformation.VictimAgent;
                if (attacker == null || victim == null || attacker == victim) return;
                if (!attacker.IsHuman || !victim.IsHuman) return;

                int typeCount = Enum.GetValues(typeof(DamageType)).Length;
                var amp = new float[typeCount];
                var add = new float[typeCount];
                var resist = new float[typeCount];
                SotorItemExtensions.SumAttackTuples(attacker, collisionData.IsMissile, amp, add);
                SotorItemExtensions.SumResistTuples(victim, resist);

                float statusWard = AbilitySystem.StatusEffects.SotorWardSave.StatusWard(
                    victim, AbilitySystem.StatusEffects.SotorResistanceHelper.ChannelForWeapon(collisionData.IsMissile));
                float wardFactor = AbilitySystem.StatusEffects.SotorWardSave.FactorFrom(
                    resist[(int)DamageType.All], statusWard);

                bool anything = wardFactor != 1f;
                for (int i = 0; i < typeCount && !anything; i++)
                {
                    if (amp[i] != 0f || add[i] != 0f || resist[i] != 0f) { anything = true; }
                }
                if (!anything) return;

                float baseDamage = __result;

                int phys = (int)DamageType.Physical;
                float physFactor = Math.Max(0f, 1f + amp[phys] - resist[phys]);

                float bonus = 0f;
                for (int i = 0; i < typeCount; i++)
                {
                    if (i == phys || i == (int)DamageType.Invalid || i == (int)DamageType.All) continue;
                    if (add[i] == 0f) continue;
                    bonus += baseDamage * add[i] * Math.Max(0f, 1f + amp[i] - resist[i]);
                }

                if (add[phys] != 0f)
                {
                    bonus += baseDamage * add[phys] * physFactor;
                }

                float total = baseDamage * physFactor + bonus;

                total *= wardFactor;

                if (Math.Abs(total - __result) > 0.01f)
                {
                    SotorLog.Debug($"Item trait damage: '{attacker.Name}' -> '{victim.Name}' {__result:0.0} -> {total:0.0} "
                                   + $"(physFactor={physFactor:0.00} bonus={bonus:0.0} "
                                   + $"ward={resist[(int)DamageType.All]:0.00}+{statusWard:0.00} -> x{wardFactor:0.00})");
                    __result = total;
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorItemTraitDamagePatch failed: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(SandboxAgentApplyDamageModel), nameof(SandboxAgentApplyDamageModel.CalculateRemainingMomentum))]
    public static class SotorItemTraitCleavePatch
    {
        public static void Postfix(ref float __result, float originalMomentum, Agent attacker, in AttackCollisionData collisionData, bool isCrushThrough)
        {
            try
            {
                if (isCrushThrough || attacker == null || __result >= originalMomentum * 0.3f) return;
                if (collisionData.IsMissile || collisionData.AttackBlockedWithShield) return;
                if (SotorItemExtensions.SumWieldedStat(attacker, SotorItemTraitStatType.Cleave, meleeOnly: true) > 0f)
                {
                    __result = originalMomentum * 0.3f;
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorItemTraitCleavePatch failed: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(SandboxStrikeMagnitudeModel), nameof(SandboxStrikeMagnitudeModel.CalculateAdjustedArmorForBlow))]
    public static class SotorItemTraitArmorPenPatch
    {
        public static void Postfix(ref float __result, in AttackInformation attackInformation)
        {
            try
            {
                if (__result <= 0f) return;
                Agent attacker = attackInformation.AttackerAgent;
                if (attacker == null || !attacker.IsHuman) return;

                float pen = SotorItemExtensions.SumWieldedStat(attacker, SotorItemTraitStatType.ArmorPenetration);
                var wielded = attacker.WieldedWeapon;
                bool fromAmmo = false;
                if (!wielded.IsEmpty && !wielded.AmmoWeapon.IsEmpty && wielded.AmmoWeapon.Item != null)
                {
                    foreach (var t in wielded.AmmoWeapon.Item.GetTraits(attacker))
                    {
                        if (t.StatsTuple != null && t.StatsTuple.StatType == SotorItemTraitStatType.ArmorPenetration)
                        {
                            pen += t.StatsTuple.Value;
                            fromAmmo = true;
                        }
                    }
                }
                if (pen > 0f)
                {
                    float before = __result;
                    __result *= Math.Max(0f, 1f - pen / 100f);

                    if (attacker.IsMainAgent)
                    {
                        string source = (!wielded.IsEmpty && wielded.Item != null)
                            ? wielded.Item.Name.ToString() : "(nothing wielded)";
                        SotorLog.Debug($"ArmorPen: {pen:0}% from '{source}'{(fromAmmo ? " (+ammo)" : "")} "
                                    + $"- armour {before:0.#} -> {__result:0.#}");
                    }
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorItemTraitArmorPenPatch failed: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(SandboxAgentStatCalculateModel), nameof(SandboxAgentStatCalculateModel.UpdateAgentStats))]
    public static class SotorItemTraitStatPatch
    {
        public static void Postfix(Agent agent, AgentDrivenProperties agentDrivenProperties)
        {
            try
            {
                if (agent == null || agentDrivenProperties == null || !agent.IsHuman || !agent.IsActive()) return;

                float swing = SotorItemExtensions.SumWieldedStat(agent, SotorItemTraitStatType.SwingSpeed, meleeOnly: true);
                if (swing != 0f)
                {
                    agentDrivenProperties.SwingSpeedMultiplier *= 1f + swing / 100f;
                }
                float reload = SotorItemExtensions.SumWieldedStat(agent, SotorItemTraitStatType.ReloadSpeed, rangedOnly: true);
                if (reload != 0f)
                {
                    agentDrivenProperties.ReloadSpeed *= 1f + reload / 100f;
                }
                float missile = SotorItemExtensions.SumWieldedStat(agent, SotorItemTraitStatType.MissileSpeed, rangedOnly: true);

                var wielded = agent.WieldedWeapon;
                if (!wielded.IsEmpty && !wielded.AmmoWeapon.IsEmpty && wielded.AmmoWeapon.Item != null)
                {
                    foreach (var t in wielded.AmmoWeapon.Item.GetTraits(agent))
                    {
                        if (t.StatsTuple != null && t.StatsTuple.StatType == SotorItemTraitStatType.MissileSpeed)
                            missile += t.StatsTuple.Value;
                    }
                }
                if (missile != 0f)
                {
                    agentDrivenProperties.MissileSpeedMultiplier *= 1f + missile / 100f;
                }
                float move = SotorItemExtensions.SumArmorStat(agent, SotorItemTraitStatType.MovementSpeed);
                if (move != 0f)
                {
                    float f = Math.Max(0f, 1f + move / 100f);
                    agentDrivenProperties.MaxSpeedMultiplier *= f;
                    agentDrivenProperties.CombatMaxSpeedMultiplier *= f;
                }

                var offhand = agent.WieldedOffhandWeapon;
                if (!offhand.IsEmpty && offhand.Item != null && offhand.CurrentUsageItem != null
                    && offhand.CurrentUsageItem.IsShield && offhand.Item.HasAnyTrait(agent))
                {
                    short bonus = 0;
                    foreach (var t in offhand.Item.GetTraits(agent))
                    {
                        if (t.StatsTuple != null && t.StatsTuple.StatType == SotorItemTraitStatType.ShieldHealth)
                            bonus += (short)t.StatsTuple.Value;
                    }
                    if (bonus > 0)
                    {
                        short baseMax = offhand.ModifiedMaxHitPoints;

                        for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumAllWeaponSlots; slot++)
                        {
                            var w = agent.Equipment[slot];
                            if (!w.IsEmpty && w.Item == offhand.Item && w.HitPoints == baseMax)
                            {
                                agent.Equipment.SetHitPointsOfSlot(slot, (short)(baseMax + bonus), true);
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorItemTraitStatPatch failed: {ex.Message}");
            }
        }
    }
}
