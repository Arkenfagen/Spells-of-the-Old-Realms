using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Security;
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

            if (IsSparedCivilian(agent, applier))
            {
                return;
            }

            if (agent.Health > damageAmount)
            {
                agent.Health -= damageAmount;
                return;
            }

            ApplyDamage(agent, damageAmount, agent.Position, applier, false, context: "status-dot");
        }

        public static void ApplyReflectedDamage(Agent attacker, int damageAmount, Agent reflector)
        {
            if (attacker == null || damageAmount <= 0) return;
            ApplyDamage(attacker, damageAmount, attacker.GetChestGlobalPosition(), reflector, false,
                context: "reflect");
        }

        private static readonly List<(Agent victim, int amount, Agent source)> PendingReflects =
            new List<(Agent, int, Agent)>();

        public static void QueueReflectedDamage(Agent attacker, int damageAmount, Agent reflector)
        {
            if (attacker == null || damageAmount <= 0) return;
            lock (PendingReflects)
            {
                PendingReflects.Add((attacker, damageAmount, reflector));
            }
        }

        public static void DeliverPendingReflects()
        {
            if (PendingReflects.Count == 0) return;
            List<(Agent victim, int amount, Agent source)> batch;
            lock (PendingReflects)
            {
                batch = new List<(Agent, int, Agent)>(PendingReflects);
                PendingReflects.Clear();
            }
            foreach (var entry in batch)
            {
                if (entry.victim == null || !entry.victim.IsActive()) continue;
                ApplyReflectedDamage(entry.victim, entry.amount, entry.source);
            }
        }

        public static void ClearPendingReflects()
        {
            lock (PendingReflects)
            {
                PendingReflects.Clear();
            }
        }

        public static bool IsSparedCivilian(Agent victim, Agent caster)
        {
            if (!SotorSettings.SpellsSpareCivilians) return false;
            if (victim == null || !victim.IsHuman) return false;
            if (caster != null && victim.IsEnemyOf(caster)) return false;

            var character = victim.Character as TaleWorlds.CampaignSystem.CharacterObject;
            if (character == null) return false;
            switch (character.Occupation)
            {
                case TaleWorlds.CampaignSystem.Occupation.Tavernkeeper:
                case TaleWorlds.CampaignSystem.Occupation.GoodsTrader:
                case TaleWorlds.CampaignSystem.Occupation.ArenaMaster:
                case TaleWorlds.CampaignSystem.Occupation.Townsfolk:
                case TaleWorlds.CampaignSystem.Occupation.RansomBroker:
                case TaleWorlds.CampaignSystem.Occupation.Weaponsmith:
                case TaleWorlds.CampaignSystem.Occupation.Armorer:
                case TaleWorlds.CampaignSystem.Occupation.HorseTrader:
                case TaleWorlds.CampaignSystem.Occupation.TavernWench:
                case TaleWorlds.CampaignSystem.Occupation.TavernGameHost:
                case TaleWorlds.CampaignSystem.Occupation.Artisan:
                case TaleWorlds.CampaignSystem.Occupation.Merchant:
                case TaleWorlds.CampaignSystem.Occupation.Preacher:
                case TaleWorlds.CampaignSystem.Occupation.Headman:
                case TaleWorlds.CampaignSystem.Occupation.RuralNotable:
                case TaleWorlds.CampaignSystem.Occupation.ShopWorker:
                case TaleWorlds.CampaignSystem.Occupation.Musician:
                case TaleWorlds.CampaignSystem.Occupation.Blacksmith:
#if !BL13

                case TaleWorlds.CampaignSystem.Occupation.ShipWright:
#endif
                    return true;
                default:
                    return false;
            }
        }

        public const int BlowBudgetPerTick = 12;

        private static int BlowBudget
        {
            get
            {
                int n = SotorSettings.SpellDeathsAtOnce;
                return n < 2 ? 2 : (n > 24 ? 24 : n);
            }
        }

        private struct QueuedSpellBlow
        {
            public Agent Agent;
            public int Damage;
            public Vec3 ImpactPosition;
            public Agent Damager;
            public bool HasShockWave;
            public DamageType DamageType;
            public string SpellName;
            public string EffectId;
            public bool SingleTarget;

            public SotorSpellGoreMissionLogic.Outcome Gore;
        }

        private static readonly Queue<QueuedSpellBlow> PendingBlows = new Queue<QueuedSpellBlow>();
        private static int _blowsThisTick;

        public static void TickBlowBudget()
        {
            _blowsThisTick = 0;
            if (PendingBlows.Count == 0) return;
            var mission = Mission.Current;
            if (mission == null || mission.MissionEnded)
            {
                PendingBlows.Clear();
                return;
            }
            int delivered = 0, dropped = 0;
            while (PendingBlows.Count > 0 && _blowsThisTick < BlowBudget)
            {
                var blow = PendingBlows.Dequeue();

                if (!CanStillApply(blow, mission)) { dropped++; continue; }
                ApplyOneBlow(blow);
                delivered++;
            }
            if (delivered > 0 || dropped > 0)
            {
                SotorLog.Debug($"Blow budget: delivered {delivered}, dropped {dropped} (target gone), "
                               + $"{PendingBlows.Count} still queued.");
            }

            if (PendingBlows.Count == 0) SotorSpellGoreMissionLogic.ReportGore();
        }

        public static void ClearPendingBlows()
        {
            PendingBlows.Clear();
            _blowsThisTick = 0;
        }

        private static bool CanStillApply(QueuedSpellBlow blow, Mission mission)
        {
            var agent = blow.Agent;
            if (agent == null || !agent.IsHuman || !agent.IsActive() || agent.Health < 1f
                || agent.IsFadingOut() || mission.FindAgentWithIndex(agent.Index) != agent) return false;
            if (blow.Damager == null || mission.FindAgentWithIndex(blow.Damager.Index) != blow.Damager) return false;
            return true;
        }

        public static void DamageAgents(IEnumerable<Agent> agents, int minDamage, int maxDamage,
            Agent damager, TriggeredEffectTemplate template, bool hasShockWave, Vec3 impactPosition,
            bool singleTarget = false, string spellName = null, float effectiveRadius = 0f,
            AbilityEffectType effectType = AbilityEffectType.Missile, int spellTier = 0)
        {
            if (agents == null || damager == null)
            {
                return;
            }

            var mission = Mission.Current;
            int considered = 0, queued = 0;
            foreach (var agent in agents)
            {
                considered++;
                if (agent == null || !agent.IsHuman || !agent.IsActive() || agent.Health < 1f
                    || agent.IsFadingOut() || (mission != null && mission.FindAgentWithIndex(agent.Index) != agent))
                {
                    continue;
                }

                if (IsSparedCivilian(agent, damager))
                {
                    continue;
                }

                int num = (maxDamage < minDamage) ? minDamage : MBRandom.RandomInt(minDamage, maxDamage);
                if (num < 0)
                {
                    continue;
                }

                float falloffRadius = effectiveRadius > 0f ? effectiveRadius : (template?.Radius ?? 0f);
                if (impactPosition != Vec3.Zero && hasShockWave && falloffRadius > 0f)
                {
                    float d = agent.Position.Distance(impactPosition);
                    num = (int)((falloffRadius - d) / falloffRadius * num);
                }

                num = ScaleByDamageType(num, template?.DamageType ?? DamageType.Physical, damager, agent);
                if (num < 0)
                {
                    num = 0;
                }

                num = ScaleBySpellcraft(num, damager, agent);

                var blow = new QueuedSpellBlow
                {
                    Agent = agent,
                    Damage = num,
                    ImpactPosition = impactPosition,
                    Damager = damager,
                    HasShockWave = hasShockWave,
                    DamageType = template?.DamageType ?? DamageType.Physical,
                    SpellName = spellName,
                    EffectId = template?.StringID,
                    SingleTarget = singleTarget,

                    Gore = SotorSpellGoreMissionLogic.Roll(agent, damager, num, effectType, spellTier),
                };

                queued++;
                PendingBlows.Enqueue(blow);
            }

            SotorLog.Info($"DamageAgents summary: effect='{template?.StringID}' considered={considered} "
                          + $"deferred={queued} (queue now {PendingBlows.Count}).");
        }

        private static void ApplyOneBlow(QueuedSpellBlow blow)
        {
            _blowsThisTick++;
            var agent = blow.Agent;
            var damager = blow.Damager;
            int num = blow.Damage;
            var impactPosition = blow.ImpactPosition;
            string spellName = blow.SpellName;
            bool singleTarget = blow.SingleTarget;

            float hpBefore = agent.Health;
            bool enemy = damager != null && agent.IsEnemyOf(damager);

            SotorLog.Debug(
                $"DamageAgents: '{agent.Name}'#{agent.Index} dmg={num} hpBefore={hpBefore:0} enemyOfCaster={enemy} " +
                $"(effect='{blow.EffectId}', shockwave={blow.HasShockWave})");

            ApplyDamage(agent, num, impactPosition, damager, blow.HasShockWave,
                context: spellName ?? blow.EffectId);
            SotorLog.Debug($"DamageAgents: '{agent.Name}'#{agent.Index} hpAfter={agent.Health:0} (delta={hpBefore - agent.Health:0}).");

                int dealt = (int)(hpBefore - agent.Health);
                bool killed = hpBefore > 0f && (!agent.IsActive() || agent.Health < 1f);
                SotorSpellDamageLog.BookHit(damager, agent,
                    blow.DamageType, dealt, killed, spellName);

                if (killed && blow.Gore != SotorSpellGoreMissionLogic.Outcome.None)
                {
                    var goreDir = impactPosition != Vec3.Zero
                        ? (agent.GetChestGlobalPosition() - impactPosition).NormalizedCopy()
                        : -agent.LookDirection;
                    SotorSpellGoreMissionLogic.Execute(blow.Gore, agent, impactPosition, goreDir);
                }

                if (SotorLog.MinLevel <= SotorLog.Level.Debug
                    && killed && impactPosition != Vec3.Zero && damager == Mission.Current?.MainAgent)
                {
                    SotorLog.Debug($"BLAST KILL '{spellName ?? blow.EffectId}': '{agent.Name}' "
                                  + $"flat={agent.Position.AsVec2.Distance(impactPosition.AsVec2):0.#}m "
                                  + $"true3d={agent.Position.Distance(impactPosition):0.#}m "
                                  + $"dmg={dealt} team={(agent.Team == null ? "NONE" : agent.IsEnemyOf(damager) ? "enemy" : "ally")}");
                }

                if (killed && agent.IsEnemyOf(damager))
                {
                    Rivals.SotorPracticeTracker.NoteKill(damager, spellName);
                }

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

        private static int ScaleByDamageType(int amount, DamageType damageType, Agent attacker, Agent victim)
        {
            if (amount <= 0 || damageType == DamageType.All || damageType == DamageType.Invalid)
            {
                return amount;
            }

            float factor = SotorResistanceHelper.GetDamageFactor(
                attacker, victim, AttackTypeMask.Spell, damageType, out float amp, out float resist, out float ward);

            float gearAmp = SOTOR.Items.SotorItemExtensions.SumArmorDamageBonus(attacker, damageType);
            float gearResist = SOTOR.Items.SotorItemExtensions.SumItemResist(victim, damageType);
            if (gearAmp != 0f || gearResist != 0f)
            {
                factor *= Math.Max(0f, 1f + gearAmp - gearResist);
            }

            float itemWard = StatusEffects.SotorWardSave.ItemWard(victim);
            float wardFactor = StatusEffects.SotorWardSave.FactorFrom(itemWard, ward);
            factor *= wardFactor;

            if (factor == 1f)
            {
                return amount;
            }

            int scaled = (int)(amount * factor);
            SotorLog.Debug(
                $"Spell damage-type scale ({damageType}): statusAmp={amp:0.00} statusResist={resist:0.00} "
                + $"gearAmp={gearAmp:0.00} gearResist={gearResist:0.00} "
                + $"ward={itemWard:0.00}+{ward:0.00} -> x{wardFactor:0.00} | {amount} -> {scaled}.");
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

        public static void ApplyBonusDamage(Agent victim, int damageAmount, Agent source)
        {
            if (victim == null || damageAmount <= 0) return;
            ApplyDamage(victim, damageAmount, victim.GetChestGlobalPosition(), source ?? victim, false,
                allowNonHuman: true, context: "trait-bonus");
        }

        [SecurityCritical]
        [HandleProcessCorruptedStateExceptions]
        private static void ApplyDamage(Agent agent, int damageAmount, Vec3 impactPosition, Agent damager,
            bool hasShockWave, bool allowNonHuman = false, string context = null)
        {
            if (agent == null || (!agent.IsHuman && !allowNonHuman) || !agent.IsActive() || agent.Health < 1f)
            {
                return;
            }

            try
            {

                if (agent.IsFadingOut() || ((int)agent.State != 1 && (int)agent.State != 2))
                {
                    return;
                }

                if (damager != null && (!damager.IsActive() || damager.Monster == null))
                {
                    damager = null;
                }

                Agent source = damager ?? agent;
                if (source.Monster == null) return;
                var blow = new Blow(source.Index);
                blow.DamageType = (DamageTypes)2;
                blow.BoneIndex = agent.Monster.HeadLookDirectionBoneIndex;
                blow.GlobalPosition = agent.GetChestGlobalPosition();
                blow.BaseMagnitude = damageAmount;
                blow.WeaponRecord.FillAsMeleeBlow(null, null, -1, -1);

                blow.WeaponRecord.WeaponFlags |= WeaponFlags.CanKillEvenIfBlunt;
                blow.WeaponRecord.Weight = 5f;
                blow.InflictedDamage = damageAmount;

                Vec3 dir = (blow.GlobalPosition == impactPosition)
                    ? -agent.LookDirection
                    : (blow.GlobalPosition - impactPosition);

                if (!(dir.LengthSquared > 1E-06f)) dir = -agent.LookDirection;
                if (!(dir.LengthSquared > 1E-06f)) dir = Vec3.Up;
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
            catch (AccessViolationException ex)
            {

                InSpellBlow = false;
                SotorLog.Error($"ACCESS VIOLATION survived in ApplyDamage - effect='{context ?? "unknown"}' "
                             + $"victim='{agent?.Name}'#{agent?.Index} damager='{damager?.Name}'#{damager?.Index} "
                             + $"damage={damageAmount} shockwave={hasShockWave} pos={impactPosition} :: {ex.Message}");
            }
            catch (Exception ex)
            {
                InSpellBlow = false;
                SotorLog.Warn($"SotorDamageHelper.ApplyDamage failed (effect='{context ?? "unknown"}', "
                            + $"victim='{agent?.Name}', damager='{damager?.Name}'): {ex.Message}");
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
