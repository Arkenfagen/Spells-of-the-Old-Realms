using System;
using SOTOR.Extensions;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem.AI
{

    public static class SotorConduitAI
    {

        private const float MeleeContactRadius = 5f;

        private const float MaxUnderFireRatio = 0.25f;

        private const float LastChargeMinDeficitRatio = 0.35f;

        public static bool ShouldChannel(Agent caster, out string reason)
        {
            reason = null;
            try
            {
                var hero = caster?.GetHero();
                if (hero == null) { reason = "no hero"; return false; }

                int pieces = SotorArcaneConduitHelper.GetPieces(hero);
                bool archmage = pieces >= 4;

                float winds = hero.GetWindsOfMagic();
                float best = 0f;
                string blockedBy = null;
                var component = caster.GetComponent<AbilityComponent>();
                if (component != null)
                {
                    for (int i = 0; i < component.KnownAbilitySystem.Count; i++)
                    {
                        var ability = component.KnownAbilitySystem[i];
                        var template = ability?.Template;
                        if (template == null) continue;
                        if (template.StringID == SotorArcaneConduitHelper.AbilityId) continue;

                        int cost = hero.GetEffectiveWindsCostForSpell(template);
                        bool unaffordable = winds < cost;
                        bool onCooldown = ability.IsOnCooldown();

                        bool blocked = unaffordable || (archmage && onCooldown);
                        if (!blocked) continue;

                        if (unaffordable && onCooldown && !archmage) continue;

                        if (cost > best)
                        {
                            best = cost;
                            blockedBy = template.StringID;
                        }
                    }
                }
                if (blockedBy == null) { reason = "nothing worth channelling for is blocked"; return false; }

                float regen = SotorArcaneConduitHelper.GetWindsRegenPerSec(hero);
                float channel = SotorArcaneConduitHelper.GetChannelDuration(hero);
                float deficit = best - winds;
                float secondsNeeded = regen > 0f ? deficit / regen : float.MaxValue;
                if (deficit > 0f && secondsNeeded > channel)
                {
                    reason = $"channel is too short: needs {secondsNeeded:0.0}s of regen, has {channel:0.0}s";
                    return false;
                }

                int used = SotorArcaneConduitMissionLogic.GetUses(caster);
                int max = SotorArcaneConduitHelper.GetUsesPerBattle(hero);
                bool lastCharge = (max - used) <= 1;
                if (lastCharge && !archmage)
                {
                    float maxWinds = Math.Max(1f, hero.GetMaxWindsOfMagic());
                    if (deficit / maxWinds < LastChargeMinDeficitRatio)
                    {
                        reason = $"saving the last charge (deficit {deficit:0} is only {deficit / maxWinds:P0} of the pool)";
                        return false;
                    }
                }

                if (!archmage)
                {
                    var near = CommonAIFunctions.AssessBlast(caster, caster.Position, MeleeContactRadius);
                    if (near.EnemyCount > 0)
                    {
                        reason = $"in contact: {near.EnemyCount} enemy within {MeleeContactRadius:0}m";
                        return false;
                    }

                    var formation = caster.Formation;
                    float underFire = formation?.QuerySystem?.UnderRangedAttackRatio ?? 0f;
                    if (underFire > MaxUnderFireRatio)
                    {
                        reason = $"under fire ({underFire:P0})";
                        return false;
                    }
                }

                reason = $"channel for {blockedBy}: need {deficit:0} winds, {secondsNeeded:0.0}s at {regen:0.0}/s"
                         + $" (pieces={pieces}, charge {used + 1}/{max})";
                return true;
            }
            catch (Exception ex)
            {
                reason = $"check failed: {ex.GetType().Name}";
                return false;
            }
        }
    }
}
