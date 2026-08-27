using System;
using HarmonyLib;
using SandBox.GameComponents;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem.StatusEffects
{

    [HarmonyPatch(typeof(SandboxAgentApplyDamageModel), nameof(SandboxAgentApplyDamageModel.ApplyGeneralDamageModifiers))]
    public static class StatusEffectDamagePatch
    {

        public static void Postfix(ref float __result, in AttackInformation attackInformation, in AttackCollisionData collisionData)
        {
            try
            {
                float scaled = __result;
                if (scaled <= 0f)
                {
                    return;
                }

                Agent attacker = attackInformation.AttackerAgent;
                Agent victim = attackInformation.VictimAgent;
                if (attacker == null || victim == null || attacker == victim)
                {
                    return;
                }

                var atkWeapon = attackInformation.AttackerWeapon;

                if (!atkWeapon.IsEmpty && atkWeapon.Item?.StringId == SOTOR.AbilitySystem.ThrownWeaponAbility.AmberJavelinItemId)
                {
                    float spellFactor = SOTOR.AbilitySystem.SotorDamageHelper.GetSpellcraftDamageFactorFor(attacker);
                    if (spellFactor != 1f)
                    {
                        __result = __result * spellFactor;
                    }

                    if (__result > 0f && victim.IsEnemyOf(attacker)
                        && SOTOR.Extensions.AgentExtensions.GetHero(attacker) is TaleWorlds.CampaignSystem.Hero javelinHero)
                    {
                        SOTOR.AbilitySystem.SotorSpellcraftHelper.GrantAbilityOutcomeXp(javelinHero, (int)__result / 5, singleTarget: true);
                    }
                    return;
                }

                var channel = SotorResistanceHelper.ChannelForWeapon(collisionData.IsMissile);
                float factor = SotorResistanceHelper.GetDamageFactor(
                    attacker, victim, channel, DamageType.Physical, out float amp, out float resist, out float ward);

                if (factor == 1f)
                {
                    return;
                }

                __result = scaled * factor;

                SotorLog.Debug(
                    $"StatusEffect damage mod: '{attacker.Name}' -> '{victim.Name}' ({channel}) " +
                    $"amp={amp:0.00} resist={resist:0.00} ward={ward:0.00} | {scaled:0.0} -> {__result:0.0}");

                if (!collisionData.IsMissile)
                {
                    var victimComp = victim.GetComponent<StatusEffectComponent>();
                    float thorns = victimComp?.GetThorns() ?? 0f;
                    if (thorns > 0f && __result > 0f)
                    {
                        int reflected = (int)(__result * thorns);
                        if (reflected > 0)
                        {

                            SotorDamageHelper.QueueReflectedDamage(attacker, reflected, victim);
                            SotorLog.Debug(
                                $"Fire-Cloak thorns: '{victim.Name}' reflects {thorns:0.00}x -> {reflected} Fire to '{attacker.Name}'.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"StatusEffectDamagePatch.Postfix failed: {ex.Message}");
            }
        }
    }
}
