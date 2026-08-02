using System;
using System.Collections.Generic;
using SOTOR.AbilitySystem.StatusEffects;
using SOTOR.Extensions;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem
{

    public static class SotorDamageHelper
    {

        public static void ApplyDamageOverTime(Agent agent, int damageAmount, Agent applier)
        {
            if (agent == null || !agent.IsHuman || !agent.IsActive() || agent.Health < 1f || agent.IsFadingOut())
            {
                return;
            }
            if (damageAmount <= 0)
            {
                return;
            }

            if (agent.Health > damageAmount)
            {
                agent.Health -= damageAmount;
                return;
            }

            ApplyDamage(agent, damageAmount, agent.Position, applier, false);
        }

        public static void ApplyReflectedDamage(Agent attacker, int damageAmount, Agent reflector)
        {
            if (attacker == null || damageAmount <= 0) return;
            ApplyDamage(attacker, damageAmount, attacker.GetChestGlobalPosition(), reflector, false);
        }

        public static void DamageAgents(IEnumerable<Agent> agents, int minDamage, int maxDamage,
            Agent damager, TriggeredEffectTemplate template, bool hasShockWave, Vec3 impactPosition,
            bool singleTarget = false, string spellName = null)
        {
            if (agents == null || damager == null)
            {
                return;
            }

            var mission = Mission.Current;
            int considered = 0, applied = 0;
            foreach (var agent in agents)
            {
                considered++;
                if (agent == null || !agent.IsHuman || !agent.IsActive() || agent.Health < 1f
                    || agent.IsFadingOut() || (mission != null && mission.FindAgentWithIndex(agent.Index) != agent))
                {
                    continue;
                }

                int num = (maxDamage < minDamage) ? minDamage : MBRandom.RandomInt(minDamage, maxDamage);
                if (num < 0)
                {
                    continue;
                }

                applied++;
                float hpBefore = agent.Health;
                bool enemy = damager != null && agent.IsEnemyOf(damager);
                SotorLog.Info(
                    $"DamageAgents: '{agent.Name}' dmg={num} hpBefore={hpBefore:0} enemyOfCaster={enemy} " +
                    $"(effect='{template?.StringID}', shockwave={hasShockWave})");

                if (impactPosition != Vec3.Zero && hasShockWave && template != null && template.Radius > 0f)
                {
                    float d = agent.Position.Distance(impactPosition);
                    num = (int)((template.Radius - d) / template.Radius * num);
                }

                num = ScaleByDamageType(num, template?.DamageType ?? DamageType.Physical, damager, agent);
                if (num < 0)
                {
                    num = 0;
                }

                num = ScaleBySpellcraft(num, damager, agent);

                ApplyDamage(agent, num, impactPosition, damager, hasShockWave);
                SotorLog.Info($"DamageAgents: '{agent.Name}' hpAfter={agent.Health:0} (delta={hpBefore - agent.Health:0}).");

                int dealt = (int)(hpBefore - agent.Health);
                bool killed = hpBefore > 0f && (!agent.IsActive() || agent.Health < 1f);
                SotorSpellDamageLog.BookHit(damager, agent,
                    template?.DamageType ?? DamageType.Physical, dealt, killed, spellName);

                if (num > 0 && damager != null && agent.IsEnemyOf(damager)
                    && SOTOR.Extensions.AgentExtensions.GetHero(damager) is TaleWorlds.CampaignSystem.Hero xpHero)
                {
                    SotorSpellcraftHelper.GrantAbilityOutcomeXp(xpHero, num / 5, singleTarget);
                }

                if (num > 0 && hpBefore > 0f && (!agent.IsActive() || agent.Health < 1f))
                {
                    TryGrantWindsOnMagicKill(damager, agent);
                }
            }

            SotorLog.Info($"DamageAgents summary: effect='{template?.StringID}' considered={considered} applied={applied}.");
        }

        private static int ScaleByDamageType(int amount, DamageType damageType, Agent attacker, Agent victim)
        {
            if (amount <= 0 || damageType == DamageType.All || damageType == DamageType.Invalid)
            {
                return amount;
            }

            float factor = SotorResistanceHelper.GetDamageFactor(
                attacker, victim, AttackTypeMask.Spell, damageType, out float amp, out float resist, out float ward);
            if (factor == 1f)
            {
                return amount;
            }

            int scaled = (int)(amount * factor);
            SotorLog.Debug(
                $"Spell damage-type scale ({damageType}): amp={amp:0.00} resist={resist:0.00} ward={ward:0.00} | {amount} -> {scaled}.");
            return scaled;
        }

        public static float GetSpellcraftDamageFactorFor(Agent damager)
        {
            var hero = damager?.GetHero();
            if (hero == null)
            {
                return 1f;
            }
            return SotorSpellcraftHelper.GetSpellDamageFactor(hero) * SotorSpellcraftHelper.GetCasterPerkDamageFactor(hero);
        }

        private static int ScaleBySpellcraft(int amount, Agent damager, Agent victim)
        {
            if (amount <= 0)
            {
                return amount;
            }

            var hero = damager.GetHero();
            if (hero == null)
            {
                return amount;
            }

            float skill = SotorSpellcraftHelper.GetSpellDamageFactor(hero);
            float casterPerk = SotorSpellcraftHelper.GetCasterPerkDamageFactor(hero);
            float victimPerk = SotorSpellcraftHelper.GetVictimPerkDamageFactor(hero, damager, victim);
            float factor = skill * casterPerk * victimPerk;
            if (factor == 1f)
            {
                return amount;
            }

            int scaled = (int)(amount * factor);
            SotorLog.Debug($"Spellcraft damage scale: skill={skill:0.000} casterPerk={casterPerk:0.000} victimPerk={victimPerk:0.000} => x{factor:0.000} | {amount} -> {scaled}.");
            return scaled;
        }

        private static void ApplyDamage(Agent agent, int damageAmount, Vec3 impactPosition, Agent damager, bool hasShockWave)
        {
            if (agent == null || !agent.IsHuman || !agent.IsActive() || agent.Health < 1f)
            {
                return;
            }

            try
            {

                if (agent.IsFadingOut() || ((int)agent.State != 1 && (int)agent.State != 2))
                {
                    return;
                }

                Agent source = damager ?? agent;
                var blow = new Blow(source.Index);
                blow.DamageType = (DamageTypes)2;
                blow.BoneIndex = agent.Monster.HeadLookDirectionBoneIndex;
                blow.GlobalPosition = agent.GetChestGlobalPosition();
                blow.BaseMagnitude = damageAmount;
                blow.WeaponRecord.FillAsMeleeBlow(null, null, -1, -1);
                blow.WeaponRecord.Weight = 5f;
                blow.InflictedDamage = damageAmount;

                Vec3 dir = (blow.GlobalPosition == impactPosition)
                    ? -agent.LookDirection
                    : (blow.GlobalPosition - impactPosition);
                dir.Normalize();
                blow.Direction = dir;
                blow.SwingDirection = dir;
                blow.DamageCalculated = true;
                blow.AttackType = (AgentAttackType)1;
                blow.BlowFlag = (BlowFlags)64;
                blow.VictimBodyPart = (BoneBodyPartType)2;
                blow.StrikeType = (StrikeType)1;

                if (hasShockWave)
                {
                    blow.BlowFlag = (BlowFlags)((uint)blow.BlowFlag | (agent.HasMount ? 0x800u : 0x20u));
                    blow.BaseMagnitude = 1000f;
                }

                blow.WeaponRecord.Velocity = dir * blow.BaseMagnitude;

                sbyte mainHandBone = source.Monster.MainHandItemBoneIndex;

                var collisionData = AttackCollisionData.GetAttackCollisionDataForDebugPurpose(
                    false, false, false, true, false, false, false, false, false, false, false, false,
                    (CombatCollisionResult)1, -999, 1, 2, blow.BoneIndex, blow.VictimBodyPart,
                    mainHandBone, (Agent.UsageDirection)0, -1, (CombatHitResultFlags)0,
                    0.5f, 1f, 0f, 0f, 0f, 0f, 0f, 0f, Vec3.Up, blow.Direction, blow.GlobalPosition,
                    Vec3.Zero, Vec3.Zero, agent.Velocity, Vec3.Up);

                InSpellBlow = true;
                try { agent.RegisterBlow(blow, in collisionData); }
                finally { InSpellBlow = false; }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorDamageHelper.ApplyDamage failed: {ex.Message}");
            }
        }

        public static bool InSpellBlow;

        private static void TryGrantWindsOnMagicKill(Agent caster, Agent victim)
        {
            try
            {
                if (!SotorSettings.EnableWindsOnMagicKill) return;
                float amount = SotorSettings.WindsOnMagicKillAmount;
                if (amount == 0f || caster == null || victim == null || caster == victim) return;
                if (!caster.IsPlayerControlled) return;
                if (!victim.IsEnemyOf(caster)) return;

                var hero = caster.GetHero();
                if (hero == null) return;
                hero.AddWindsOfMagic(amount, allowOverMax: true);
                SotorLog.Info($"Winds on magic kill: {amount:0.0} to '{hero.Name}' (killed '{victim.Name}').");
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"TryGrantWindsOnMagicKill failed: {ex.Message}");
            }
        }
    }
}
