using System;
using System.Collections.Generic;
using SOTOR.AbilitySystem;
using SOTOR.AbilitySystem.StatusEffects;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace SOTOR.Items
{

    public abstract class SotorWeaponHitScript
    {
        protected string[] _arguments = new string[0];

        public void SetArguments(List<string> args)
        {
            _arguments = args != null ? args.ToArray() : new string[0];
        }

        public abstract void OnHit(Agent attackingAgent, Agent attackedAgent, in Blow blow,
            in MissionWeapon missionWeapon, in AttackCollisionData collisionData);

        protected static void TriggerEffect(string effectId, Vec3 position, Agent triggerer, Agent singleTarget = null)
        {
            var template = TriggeredEffectManager.GetTemplate(effectId);
            if (template == null)
            {
                SotorLog.Warn($"Weapon hit script: unknown triggered effect '{effectId}'");
                return;
            }
            var effect = new TriggeredEffect(template);
            if (singleTarget != null)
            {
                var list = new MBList<Agent> { singleTarget };
                effect.Trigger(position, Vec3.Up, triggerer, list);
            }
            else
            {
                effect.Trigger(position, Vec3.Up, triggerer);
            }
        }

        protected static void ApplyStackBuff(Agent receiver, string effectId, int maxStacks, float duration)
        {
            var comp = receiver?.GetComponent<StatusEffectComponent>();
            if (comp == null) return;
            if (comp.GetActiveEffectCount(effectId) < maxStacks)
            {
                comp.RunStatusEffect(effectId, receiver, duration, append: false, stack: true);
            }
        }

        protected static void DealDamage(Agent victim, int amount, Agent attributedTo, bool allowNonHuman = false)
        {
            if (victim == null || amount <= 0) return;
            if (allowNonHuman)
            {
                SotorDamageHelper.ApplyBonusDamage(victim, amount, attributedTo ?? victim);
            }
            else
            {
                SotorDamageHelper.ApplyReflectedDamage(victim, amount, attributedTo ?? victim);
            }
        }

        protected static bool IsUndeadAgent(Agent agent)
        {
            var culture = agent?.Character?.Culture;
            return culture != null && culture.StringId == "sotor_skeleton";
        }

        protected static int DamageOf(in Blow blow, in AttackCollisionData collisionData)
        {
            return blow.InflictedDamage > 0 ? blow.InflictedDamage : collisionData.InflictedDamage;
        }
    }

    public class WeaponTriggerEffectScriptPort : SotorWeaponHitScript
    {
        protected Agent _triggererAgent;

        public override void OnHit(Agent attackingAgent, Agent attackedAgent, in Blow blow,
            in MissionWeapon missionWeapon, in AttackCollisionData collisionData)
        {
            if (attackingAgent == null || attackedAgent == null || _arguments.Length < 1) return;
            if (_triggererAgent == null) _triggererAgent = attackingAgent;

            string effectId = _arguments[0];
            bool atAttacker = _arguments.Length >= 2 && bool.TryParse(_arguments[1], out var b1) && b1;
            bool singleTarget = _arguments.Length >= 3 && bool.TryParse(_arguments[2], out var b2) && b2;

            if (singleTarget)
            {
                var target = atAttacker ? attackingAgent : attackedAgent;
                TriggerEffect(effectId, attackedAgent.Position, _triggererAgent, target);
            }
            else
            {
                var position = atAttacker ? attackingAgent.Position : attackedAgent.Position;
                TriggerEffect(effectId, position, _triggererAgent);
            }
        }
    }

    public class DefenseTriggerEffectScriptPort : WeaponTriggerEffectScriptPort
    {
        public override void OnHit(Agent attackingAgent, Agent attackedAgent, in Blow blow,
            in MissionWeapon missionWeapon, in AttackCollisionData collisionData)
        {
            if (attackedAgent == null) return;
            _triggererAgent = attackedAgent;
            base.OnHit(attackingAgent, attackedAgent, blow, missionWeapon, collisionData);
        }
    }

    public class AttackTypeDefenseTriggerScriptPort : DefenseTriggerEffectScriptPort
    {
        public override void OnHit(Agent attackingAgent, Agent attackedAgent, in Blow blow,
            in MissionWeapon missionWeapon, in AttackCollisionData collisionData)
        {
            if (attackingAgent == null || missionWeapon.IsEmpty || _arguments.Length < 4) return;
            var usage = missionWeapon.GetWeaponComponentDataForUsage(missionWeapon.CurrentUsageIndex);
            if (usage == null) return;
            switch (_arguments[3])
            {
                case "ranged":
                    if (usage.IsRangedWeapon) base.OnHit(attackingAgent, attackedAgent, blow, missionWeapon, collisionData);
                    break;
                case "melee":
                    if (usage.IsMeleeWeapon) base.OnHit(attackingAgent, attackedAgent, blow, missionWeapon, collisionData);
                    break;

            }
        }
    }

    public class UndeadConditionDefenseTriggerEffectScriptPort : DefenseTriggerEffectScriptPort
    {
        public override void OnHit(Agent attackingAgent, Agent attackedAgent, in Blow blow,
            in MissionWeapon missionWeapon, in AttackCollisionData collisionData)
        {
            if (attackingAgent != null && attackedAgent != null && IsUndeadAgent(attackingAgent))
            {
                base.OnHit(attackingAgent, attackedAgent, blow, missionWeapon, collisionData);
            }
        }
    }

    public class TriggerOnKillScriptPort : WeaponTriggerEffectScriptPort
    {
        public override void OnHit(Agent attackingAgent, Agent attackedAgent, in Blow blow,
            in MissionWeapon missionWeapon, in AttackCollisionData collisionData)
        {
            if (attackedAgent != null && attackedAgent.Health <= 0f)
            {
                base.OnHit(attackingAgent, attackedAgent, blow, missionWeapon, collisionData);
            }
        }
    }

    public class WeaponBuffStackScriptPort : SotorWeaponHitScript
    {
        public override void OnHit(Agent attackingAgent, Agent attackedAgent, in Blow blow,
            in MissionWeapon missionWeapon, in AttackCollisionData collisionData)
        {
            ApplyToReceiver(attackingAgent);
        }

        protected void ApplyToReceiver(Agent receiver)
        {
            if (receiver == null || _arguments.Length < 3) return;
            if (int.TryParse(_arguments[1], out var maxStacks) && int.TryParse(_arguments[2], out var duration))
            {
                ApplyStackBuff(receiver, _arguments[0], maxStacks, duration);
            }
        }
    }

    public class DefenseStackBuffScriptPort : WeaponBuffStackScriptPort
    {
        public override void OnHit(Agent attackingAgent, Agent attackedAgent, in Blow blow,
            in MissionWeapon missionWeapon, in AttackCollisionData collisionData)
        {
            ApplyToReceiver(attackedAgent);
        }
    }

    public class BuffStackOnKillPort : WeaponBuffStackScriptPort
    {
        public override void OnHit(Agent attackingAgent, Agent attackedAgent, in Blow blow,
            in MissionWeapon missionWeapon, in AttackCollisionData collisionData)
        {
            if (attackedAgent != null && attackedAgent.Health <= 0f)
            {
                ApplyToReceiver(attackingAgent);
            }
        }
    }

    public class BloodLeechingScriptPort : SotorWeaponHitScript
    {
        public override void OnHit(Agent attackingAgent, Agent attackedAgent, in Blow blow,
            in MissionWeapon missionWeapon, in AttackCollisionData collisionData)
        {
            if (attackingAgent == null || _arguments.Length < 2) return;
            if (int.TryParse(_arguments[0], out var heal) && int.TryParse(_arguments[1], out var threshold)
                && attackingAgent.Health < threshold)
            {
                attackingAgent.Health = Math.Min(attackingAgent.HealthLimit, attackingAgent.Health + heal);
            }
        }
    }

    public class BloodLettingTriggerScriptPort : WeaponTriggerEffectScriptPort
    {
        public override void OnHit(Agent attackingAgent, Agent attackedAgent, in Blow blow,
            in MissionWeapon missionWeapon, in AttackCollisionData collisionData)
        {
            base.OnHit(attackingAgent, attackedAgent, blow, missionWeapon, collisionData);
            if (_arguments.Length >= 6 && int.TryParse(_arguments[5], out var selfCost))
            {
                DealDamage(attackingAgent, selfCost, attackingAgent);
            }
        }
    }

    public class BonusDOTDamageEffectScriptPort : SotorWeaponHitScript
    {
        public override void OnHit(Agent attackingAgent, Agent attackedAgent, in Blow blow,
            in MissionWeapon missionWeapon, in AttackCollisionData collisionData)
        {
            if (attackingAgent == null || attackedAgent == null || blow.InflictedDamage <= 0 || _arguments.Length < 1) return;
            var comp = attackedAgent.GetComponent<StatusEffectComponent>();
            if (comp != null && int.TryParse(_arguments[0], out var percent) && comp.GetDamageOverTimeAggregate() > 0f)
            {
                DealDamage(attackedAgent, (int)(blow.InflictedDamage * (percent / 100f)), attackingAgent);
            }
        }
    }

    public class ExtraHeadshotDamageScriptPort : SotorWeaponHitScript
    {
        public override void OnHit(Agent attackingAgent, Agent attackedAgent, in Blow blow,
            in MissionWeapon missionWeapon, in AttackCollisionData collisionData)
        {
            if (attackedAgent == null || blow.InflictedDamage <= 0) return;
            if (collisionData.VictimHitBodyPart != BoneBodyPartType.Head) return;
            float fraction = (_arguments.Length >= 1 && int.TryParse(_arguments[0], out var pct)) ? pct / 100f : 0.25f;
            DealDamage(attackedAgent, (int)(blow.InflictedDamage * fraction), attackingAgent);
        }
    }

    public class StealthAttackScriptPort : SotorWeaponHitScript
    {
        public override void OnHit(Agent attackingAgent, Agent attackedAgent, in Blow blow,
            in MissionWeapon missionWeapon, in AttackCollisionData collisionData)
        {
            if (attackedAgent == null) return;
            float fraction = (_arguments.Length >= 1 && int.TryParse(_arguments[0], out var pct)) ? pct / 100f : 0.25f;

            var look = attackedAgent.LookDirection;
            var blowDir = collisionData.WeaponBlowDir.NormalizedCopy();
            bool fromBehind = false;
            if (look.Length != 0f && blowDir.Length != 0f)
            {
                fromBehind = MBMath.ToDegrees(Vec3.AngleBetweenTwoVectors(look, blowDir)) < 90f;
            }
            bool unaware = !attackedAgent.AIStateFlags.HasFlag(Agent.AIStateFlag.Alarmed);
            if (fromBehind || unaware)
            {
                MBInformationManager.AddQuickInformation(SotorText.GetObject("sotor_stealth_attack"));
                DealDamage(attackedAgent, (int)(collisionData.InflictedDamage * fraction), attackingAgent);
            }
        }
    }

    public class ReviveScriptPort : SotorWeaponHitScript
    {
        private static Mission _trackedMission;
        private static readonly HashSet<int> _revived = new HashSet<int>();

        public override void OnHit(Agent attackingAgent, Agent attackedAgent, in Blow blow,
            in MissionWeapon missionWeapon, in AttackCollisionData collisionData)
        {
            if (attackedAgent == null) return;
            if (_trackedMission != Mission.Current)
            {
                _trackedMission = Mission.Current;
                _revived.Clear();
            }
            if (attackedAgent.Health > 0f || _revived.Contains(attackedAgent.Index)) return;
            _revived.Add(attackedAgent.Index);
            attackedAgent.Health = attackedAgent.HealthLimit * 0.5f;
            if (attackedAgent.IsMainAgent)
            {
                MBInformationManager.AddQuickInformation(SotorText.GetObject("sotor_ward_revive"));
            }
            SotorLog.Info($"Ward of the Lady revived '{attackedAgent.Name}' at {attackedAgent.Health:0}/{attackedAgent.HealthLimit:0}");
        }
    }

    public class UndeadTriggeredEffectScriptPort : WeaponTriggerEffectScriptPort
    {
        public override void OnHit(Agent attackingAgent, Agent attackedAgent, in Blow blow,
            in MissionWeapon missionWeapon, in AttackCollisionData collisionData)
        {
            if (attackingAgent == null || attackedAgent == null || !IsUndeadAgent(attackedAgent)) return;
            SotorLog.Debug($"HitScript UndeadTriggeredEffect fired ({attackingAgent.Name} vs {attackedAgent.Name})");
            base.OnHit(attackingAgent, attackedAgent, blow, missionWeapon, collisionData);
        }
    }

    public class UndeadConditionDefenseStackBuffScriptPort : DefenseStackBuffScriptPort
    {
        public override void OnHit(Agent attackingAgent, Agent attackedAgent, in Blow blow,
            in MissionWeapon missionWeapon, in AttackCollisionData collisionData)
        {
            if (attackingAgent == null || attackedAgent == null || !IsUndeadAgent(attackingAgent)) return;
            SotorLog.Debug($"HitScript UndeadConditionDefenseStackBuff fired (defender {attackedAgent.Name})");
            base.OnHit(attackingAgent, attackedAgent, blow, missionWeapon, collisionData);
        }
    }

    public class BonusDamageOnUndeadEffectScriptPort : SotorWeaponHitScript
    {
        public override void OnHit(Agent attackingAgent, Agent attackedAgent, in Blow blow,
            in MissionWeapon missionWeapon, in AttackCollisionData collisionData)
        {
            int damage = DamageOf(blow, collisionData);
            if (attackingAgent == null || attackedAgent == null || damage <= 0) return;
            if (!IsUndeadAgent(attackedAgent)) return;
            if (_arguments.Length < 1 || !int.TryParse(_arguments[0], out var percent)) return;
            int bonus = (int)(damage * (percent / 100f));
            SotorLog.Debug($"HitScript BonusDamageOnUndead fired (+{bonus} on {attackedAgent.Name})");
            DealDamage(attackedAgent, bonus, attackingAgent);
        }
    }

    public class BeastSlayingScriptPort : SotorWeaponHitScript
    {
        public override void OnHit(Agent attackingAgent, Agent attackedAgent, in Blow blow,
            in MissionWeapon missionWeapon, in AttackCollisionData collisionData)
        {
            int damage = DamageOf(blow, collisionData);
            if (attackingAgent == null || attackedAgent == null || damage <= 0) return;
            if (!attackedAgent.IsMount) return;
            if (IsUndeadAgent(attackedAgent) || IsUndeadAgent(attackedAgent.RiderAgent)) return;
            float fraction = (_arguments.Length >= 1 && int.TryParse(_arguments[0], out var pct)) ? pct / 100f : 0.25f;
            int bonus = (int)(damage * fraction);
            if (bonus <= 0) return;
            float hpBefore = attackedAgent.Health;
            DealDamage(attackedAgent, bonus, attackingAgent, allowNonHuman: true);
            SotorLog.Debug($"HitScript BeastSlaying fired (+{bonus} on {attackedAgent.Name}: "
                        + $"hp {hpBefore:0} -> {attackedAgent.Health:0})");
        }
    }

    public class KnockOutCheckTriggerScriptPort : WeaponTriggerEffectScriptPort
    {
        public override void OnHit(Agent attackingAgent, Agent attackedAgent, in Blow blow,
            in MissionWeapon missionWeapon, in AttackCollisionData collisionData)
        {
            if (attackingAgent == null || attackedAgent == null || attackedAgent.Health > 0f) return;
            var model = TaleWorlds.MountAndBlade.MissionGameModels.Current?.AgentDecideKilledOrUnconsciousModel;
            if (model == null) return;
            var flags = (WeaponFlags)1;
            if (!missionWeapon.IsEmpty && missionWeapon.CurrentUsageItem != null)
            {
                flags = missionWeapon.CurrentUsageItem.WeaponFlags;
            }
            float knockOutChance = model.GetAgentStateProbability(attackingAgent, attackedAgent,
                blow.DamageType, flags, out _);
            if (MBRandom.RandomFloat < knockOutChance)
            {
                SotorLog.Debug($"HitScript KnockOutCheckTrigger fired (spared {attackedAgent.Name})");
                base.OnHit(attackingAgent, attackedAgent, blow, missionWeapon, collisionData);
            }
        }
    }

    public class TimeCoolDownReductionShieldScriptPort : SotorWeaponHitScript
    {
        private const float ReductionSeconds = 1f;

        public override void OnHit(Agent attackingAgent, Agent attackedAgent, in Blow blow,
            in MissionWeapon missionWeapon, in AttackCollisionData collisionData)
        {
            if (attackingAgent == null || attackedAgent == null) return;
            var component = attackedAgent.GetComponent<SOTOR.AbilitySystem.AbilityComponent>();
            if (component == null) return;

            var onCooldown = new List<SOTOR.AbilitySystem.Ability>();
            foreach (var ability in component.KnownAbilitySystem)
            {
                if (ability != null && ability.IsOnCooldown()) onCooldown.Add(ability);
            }
            if (onCooldown.Count == 0) return;

            var pick = onCooldown[MBRandom.RandomInt(onCooldown.Count)];
            int before = pick.GetCoolDownLeft();
            pick.ReduceCooldown(ReductionSeconds);
            SotorLog.Debug($"HitScript TimeCoolDownReductionShield fired (-{ReductionSeconds:0}s on '{pick.StringID}' "
                        + $"for {attackedAgent.Name}: {before}s -> {pick.GetCoolDownLeft()}s, "
                        + $"chosen from {onCooldown.Count} on cooldown)");
        }
    }

    public class AmmoRechargeOnHitPort : SotorWeaponHitScript
    {
        public override void OnHit(Agent attackingAgent, Agent attackedAgent, in Blow blow,
            in MissionWeapon missionWeapon, in AttackCollisionData collisionData)
        {
            SotorFlightRuneAmmo.AttemptRecharge(attackingAgent, attackedAgent, blow, missionWeapon, collisionData);
        }
    }

    public static class SotorWeaponHitScriptRegistry
    {
        private static readonly Dictionary<string, Func<SotorWeaponHitScript>> Factories =
            new Dictionary<string, Func<SotorWeaponHitScript>>(StringComparer.Ordinal)
            {
                ["WeaponTriggerEffectScript"] = () => new WeaponTriggerEffectScriptPort(),
                ["DefenseTriggerEffectScript"] = () => new DefenseTriggerEffectScriptPort(),
                ["AttackTypeDefenseTriggerScript"] = () => new AttackTypeDefenseTriggerScriptPort(),
                ["UndeadConditionDefenseTriggerEffectScript"] = () => new UndeadConditionDefenseTriggerEffectScriptPort(),
                ["TriggerOnKillScript"] = () => new TriggerOnKillScriptPort(),
                ["WeaponBuffStackScript"] = () => new WeaponBuffStackScriptPort(),
                ["DefenseStackBuffScript"] = () => new DefenseStackBuffScriptPort(),
                ["BuffStackOnKill"] = () => new BuffStackOnKillPort(),
                ["BloodLeechingScript"] = () => new BloodLeechingScriptPort(),
                ["BloodLettingTriggerScript"] = () => new BloodLettingTriggerScriptPort(),
                ["BonusDOTDamageEffectScript"] = () => new BonusDOTDamageEffectScriptPort(),
                ["ExtraHeadshotDamageScript"] = () => new ExtraHeadshotDamageScriptPort(),
                ["StealthAttackScript"] = () => new StealthAttackScriptPort(),
                ["ReviveScript"] = () => new ReviveScriptPort(),

                ["UndeadTriggeredEffectScript"] = () => new UndeadTriggeredEffectScriptPort(),
                ["UndeadConditionDefenseStackBuffScript"] = () => new UndeadConditionDefenseStackBuffScriptPort(),
                ["BonusDamageOnUndeadEffectScript"] = () => new BonusDamageOnUndeadEffectScriptPort(),
                ["BeastSlayingScript"] = () => new BeastSlayingScriptPort(),
                ["KnockOutCheckTriggerScript"] = () => new KnockOutCheckTriggerScriptPort(),
                ["TimeCoolDownReductionShieldScript"] = () => new TimeCoolDownReductionShieldScriptPort(),
                ["AmmoRechargeOnHit"] = () => new AmmoRechargeOnHitPort(),
            };

        public static SotorWeaponHitScript Create(SotorItemTrait trait)
        {
            var tuple = trait?.OnWeaponHitScript;
            if (tuple == null) return null;
            var shortName = tuple.ShortName;
            if (string.IsNullOrEmpty(shortName) || shortName == "invalid") return null;
            if (!Factories.TryGetValue(shortName, out var factory))
            {
                SotorLog.Warn($"Unknown weapon hit script '{shortName}' on trait {trait.ItemTraitStringId}");
                return null;
            }
            var script = factory();
            script.SetArguments(tuple.WeaponScriptArguments);
            return script;
        }

        public static bool IsKillTrigger(SotorItemTrait trait) => trait?.OnWeaponHitScript?.ShortName == "TriggerOnKillScript";
        public static bool IsReviveScript(SotorItemTrait trait) => trait?.OnWeaponHitScript?.ShortName == "ReviveScript";
        public static bool IsKillOnlyScript(SotorItemTrait trait)
        {
            var s = trait?.OnWeaponHitScript?.ShortName;

            return s == "TriggerOnKillScript" || s == "BuffStackOnKill" || s == "KnockOutCheckTriggerScript";
        }
    }
}
