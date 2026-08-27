using System;
using System.Collections.Generic;
using System.Linq;
using SOTOR.Extensions;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem.StatusEffects
{

    public class StatusEffectComponent : AgentComponent, IDisposable
    {
        private class Aggregate
        {
            public float HealthOverTime;
            public float DamageOverTime;
            public float SpeedProperties;
            public float AttackSpeedProperties;
            public float ReloadSpeedProperties;

            public float WindsOverTime;

            public float Thorns;

            public readonly Dictionary<AttackTypeMask, float[]> DamageAmplifications = NewMaskTable();
            public readonly Dictionary<AttackTypeMask, float[]> Resistances = NewMaskTable();

            private static Dictionary<AttackTypeMask, float[]> NewMaskTable()
            {
                var d = new Dictionary<AttackTypeMask, float[]>();
                foreach (AttackTypeMask m in Enum.GetValues(typeof(AttackTypeMask)))
                {
                    d[m] = new float[8];
                }
                return d;
            }

            public void AddEffect(StatusEffect effect)
            {
                var t = effect.Template;

                float v = effect.EffectValueOverride ?? t.BaseEffectValue;
                switch (t.Type)
                {
                    case StatusEffectTemplate.EffectType.WindsOverTime: WindsOverTime += v; break;
                    case StatusEffectTemplate.EffectType.DamageOverTime: DamageOverTime += v; break;
                    case StatusEffectTemplate.EffectType.HealthOverTime: HealthOverTime += v; break;
                    case StatusEffectTemplate.EffectType.MovementManipulation: SpeedProperties += v; break;
                    case StatusEffectTemplate.EffectType.AttackSpeedManipulation: AttackSpeedProperties += v; break;
                    case StatusEffectTemplate.EffectType.ReloadSpeedManipulation: ReloadSpeedProperties += v; break;
                    case StatusEffectTemplate.EffectType.DamageAmplification:
                        AddToMaskTable(DamageAmplifications, t.DamageType, t.AttackTypeMask, v); break;
                    case StatusEffectTemplate.EffectType.Resistance:
                        AddToMaskTable(Resistances, t.DamageType, t.AttackTypeMask, v); break;
                    case StatusEffectTemplate.EffectType.TemporaryAttributeOnly:

                        if (t.TemporaryAttributes != null &&
                            t.TemporaryAttributes.Exists(a => a.Equals("Thorns", StringComparison.OrdinalIgnoreCase)))
                        {
                            Thorns += v;
                        }
                        break;
                }
            }

            private static void AddToMaskTable(Dictionary<AttackTypeMask, float[]> table, DamageType dt, AttackTypeMask mask, float value)
            {
                if ((mask & AttackTypeMask.Ranged) != 0) table[AttackTypeMask.Ranged][(int)dt] += value;
                if ((mask & AttackTypeMask.Spell) != 0) table[AttackTypeMask.Spell][(int)dt] += value;
                if ((mask & AttackTypeMask.Melee) != 0) table[AttackTypeMask.Melee][(int)dt] += value;
            }
        }

        private class EffectData
        {
            public StatusEffect Effect;
            public GameEntity ParticleEntity;
            public EffectData(StatusEffect effect, GameEntity particleEntity)
            {
                Effect = effect;
                ParticleEntity = particleEntity;
            }
        }

        private readonly Dictionary<StatusEffect, EffectData> _currentEffects = new Dictionary<StatusEffect, EffectData>();
        private Aggregate _aggregate = new Aggregate();
        private const float UpdateFrequency = 1f;
        private float _deltaSinceLastTick;
        private bool _disabled;

        private readonly List<GameEntity> _groundEntities = new List<GameEntity>();

        private readonly List<GameEntity> _extraCarriers = new List<GameEntity>();

        private EquipmentIndex _weaponFxWieldedIndex = EquipmentIndex.None;
        private bool _hasWeaponAttachedEffect;

        public bool HasActiveEffects => _currentEffects.Count > 0;
        public bool NeedsStatusEffectTick => _currentEffects.Count > 0;

        public StatusEffectComponent(Agent agent) : base(agent)
        {
            _deltaSinceLastTick = MBRandom.RandomFloatRanged(0f, 0.1f);
        }

        private bool HasUsableVisuals()
        {
            return Agent != null && !Agent.IsFadingOut() && Agent.AgentVisuals != null && Agent.AgentVisuals.IsValid();
        }

        public bool RunStatusEffect(string effectId, Agent applierAgent, float duration, bool append, string originSpellName = null, bool stack = false)
        {
            if (Agent == null || _disabled) return false;

            var existing = stack ? null : _currentEffects.Keys.FirstOrDefault(e => e.Template.StringID == effectId);
            if (existing != null)
            {
                existing.CurrentDuration = append ? existing.CurrentDuration + duration : duration;

                if (string.IsNullOrEmpty(existing.OriginSpellName) && !string.IsNullOrEmpty(originSpellName))
                {
                    existing.OriginSpellName = originSpellName;
                }
                return false;
            }

            var effect = StatusEffectManager.CreateNewStatusEffect(effectId, applierAgent);
            if (effect == null)
            {
                SotorLog.Warn($"StatusEffectComponent: unknown status effect '{effectId}'.");
                return false;
            }
            effect.CurrentDuration = duration;
            effect.OriginSpellName = originSpellName;

            ApplyArcaneConduitScaling(effect, effectId, applierAgent);

            AddEffect(effect);

            SotorLog.Debug($"StatusEffect '{effectId}' applied to '{Agent?.Name}' for {duration}s (type={effect.Template.Type}).");
            return true;
        }

        private static void ApplyArcaneConduitScaling(StatusEffect effect, string effectId, Agent applierAgent)
        {
            if (effect == null || string.IsNullOrEmpty(effectId)) return;
            if (effectId != SotorArcaneConduitHelper.WindsRegenStatusId
                && effectId != SotorArcaneConduitHelper.SlowStatusId
                && effectId != SotorArcaneConduitHelper.VulnerabilityStatusId
                && effectId != SotorArcaneConduitHelper.DamageBuffStatusId)
            {
                return;
            }

            var hero = SOTOR.Extensions.AgentExtensions.GetHero(applierAgent);
            if (hero == null) return;

            if (effectId == SotorArcaneConduitHelper.WindsRegenStatusId)
            {
                effect.EffectValueOverride = SotorArcaneConduitHelper.GetWindsRegenPerSec(hero);
            }
            else if (effectId == SotorArcaneConduitHelper.SlowStatusId)
            {
                effect.EffectValueOverride = SotorArcaneConduitHelper.GetSelfSlow(hero);
            }
            else if (effectId == SotorArcaneConduitHelper.VulnerabilityStatusId)
            {
                effect.EffectValueOverride = SotorArcaneConduitHelper.GetVulnerability(hero);
            }
            else
            {
                effect.EffectValueOverride = SotorArcaneConduitHelper.GetSpellDamageBonus(hero);
            }
        }

        public new void OnTick(float dt)
        {
            if (_currentEffects.Count == 0) return;

            UpdateGroundEntities();

            RefreshWeaponAttachedParticlesOnSwap();

            _deltaSinceLastTick += dt;
            if (_deltaSinceLastTick > UpdateFrequency)
            {
                _deltaSinceLastTick = MBRandom.RandomFloatRanged(0f, 0.1f);
                OnElapsed();
            }
        }

        private void UpdateGroundEntities()
        {
            if (_groundEntities.Count == 0) return;
            if (Agent == null || !Agent.IsActive() || Agent.IsFadingOut()) return;

            var frame = new MatrixFrame(Mat3.Identity, Agent.GetChestGlobalPosition());
            foreach (var e in _groundEntities)
            {
                if (e != null)
                {
                    e.SetGlobalFrame(frame, true);
                }
            }
        }

        private void RefreshWeaponAttachedParticlesOnSwap()
        {
            if (!_hasWeaponAttachedEffect) return;
            if (Agent == null || !Agent.IsActive() || Agent.IsFadingOut()) return;

            var current = Agent.GetPrimaryWieldedItemIndex();
            if (current == _weaponFxWieldedIndex) return;
            _weaponFxWieldedIndex = current;

            foreach (var kv in _currentEffects.ToList())
            {
                var data = kv.Value;
                if (data.Effect?.Template == null || !data.Effect.Template.ApplyToWeapon) continue;

                RemoveVisuals(data);
                foreach (var e in _extraCarriers)
                {
                    try
                    {
                        e?.RemoveAllParticleSystems();
                        if (HasUsableVisuals()) { Agent.AgentVisuals.RemoveChildEntity(e, 0); }
                        else { e?.Remove(0); }
                    }
                    catch {  }
                }
                _extraCarriers.Clear();

                data.ParticleEntity = TryAttachParticle(data.Effect.Template.ParticleId, true, false);
            }
        }

        private void OnElapsed()
        {
            foreach (var kv in _currentEffects.ToList())
            {
                kv.Key.CurrentDuration -= 1f;
                if (kv.Key.CurrentDuration <= 0f)
                {
                    RemoveEffect(kv.Key);
                }
            }

            CalculateAggregate();

            if (Agent == null || !Agent.IsActive() || Agent.IsFadingOut()) return;

            if (_aggregate.WindsOverTime > 0f && Agent.IsHero)
            {
                var windsHero = SOTOR.Extensions.AgentExtensions.GetHero(Agent);
                if (windsHero != null && windsHero.GetExtendedInfo() != null)
                {
                    windsHero.AddWindsOfMagic(_aggregate.WindsOverTime);
                }
            }

            if (_aggregate.DamageOverTime > 0f)
            {

                var dotEffects = _currentEffects.Keys
                    .Where(x => x.Template.Type == StatusEffectTemplate.EffectType.DamageOverTime).ToList();

                foreach (var eff in dotEffects)
                {
                    if (Agent == null || !Agent.IsActive() || Agent.Health < 1f || Agent.IsFadingOut())
                    {
                        break;
                    }

                    int tickDmg = (int)eff.Template.BaseEffectValue;
                    if (tickDmg <= 0) continue;

                    float hpBefore = Agent.Health;
                    SotorDamageHelper.ApplyDamageOverTime(Agent, tickDmg, eff.ApplierAgent);
                    int dealt = (int)(hpBefore - Agent.Health);
                    bool killed = hpBefore > 0f && (!Agent.IsActive() || Agent.Health < 1f);
                    SotorLog.Info($"StatusEffect DoT tick: '{Agent?.Name}' takes {dealt} from '{eff.OriginSpellName ?? eff.Template.StringID}' (health now {Agent?.Health:0}).");

                    if (dealt > 0 && eff.ApplierAgent != null)
                    {
                        SotorSpellDamageLog.BookHit(eff.ApplierAgent, Agent,
                            eff.Template.DamageType, dealt, killed, eff.OriginSpellName);
                    }
                }
            }
            else if (_aggregate.HealthOverTime > 0f)
            {
                int heal = (int)_aggregate.HealthOverTime;
                float before = Agent.Health;
                Agent.Health = Math.Min(Agent.Health + heal, Agent.HealthLimit);
                float healed = Agent.Health - before;

                if (healed > 0f)
                {
                    var healEffect = _currentEffects.Keys
                        .FirstOrDefault(x => x.Template.Type == StatusEffectTemplate.EffectType.HealthOverTime);
                    var healApplier = healEffect?.ApplierAgent;
                    if (SOTOR.Extensions.AgentExtensions.GetHero(healApplier) is TaleWorlds.CampaignSystem.Hero healHero)
                    {
                        SotorSpellcraftHelper.GrantAbilityOutcomeXp(healHero, (int)healed / 5, false);
                    }

                    SotorSpellDamageLog.BookHeal(healApplier, Agent, (int)healed, healEffect?.OriginSpellName);

                    AbilitySystem.Rivals.SotorPracticeTracker.NoteHealed(
                        healApplier, healEffect?.OriginSpellName, (int)healed);
                }
            }
        }

        private bool _hadSpeedModifier;

        private void CalculateAggregate()
        {
            _aggregate = new Aggregate();
            foreach (var e in _currentEffects.Keys)
            {
                _aggregate.AddEffect(e);
            }

            bool hasSpeedModifier = _aggregate.SpeedProperties != 0f || _aggregate.AttackSpeedProperties != 0f;
            if ((hasSpeedModifier || _hadSpeedModifier) && Agent != null && Agent.IsActive())
            {
                Agent.UpdateAgentProperties();

                var mount = Agent.MountAgent;
                if (mount != null && mount.IsActive())
                {
                    mount.UpdateAgentProperties();
                }
            }
            _hadSpeedModifier = hasSpeedModifier;
        }

        public float[] GetResistances(AttackTypeMask mask) => (_aggregate ?? (_aggregate = new Aggregate())).Resistances[mask];
        public float[] GetAmplifiers(AttackTypeMask mask) => (_aggregate ?? (_aggregate = new Aggregate())).DamageAmplifications[mask];
        public float GetMovementSpeedModifier() => (_aggregate ?? (_aggregate = new Aggregate())).SpeedProperties;
        public float GetAttackSpeedModifier() => (_aggregate ?? (_aggregate = new Aggregate())).AttackSpeedProperties;

        public float GetThorns() => (_aggregate ?? (_aggregate = new Aggregate())).Thorns;

        public int GetActiveEffectCount(string effectId)
        {
            int n = 0;
            foreach (var e in _currentEffects.Keys)
            {
                if (e.Template.StringID == effectId) n++;
            }
            return n;
        }

        public float GetDamageOverTimeAggregate() => (_aggregate ?? (_aggregate = new Aggregate())).DamageOverTime;

        private static bool IsMeleeWeaponClass(WeaponClass wc)
        {
            switch (wc)
            {
                case WeaponClass.Dagger:
                case WeaponClass.OneHandedSword:
                case WeaponClass.TwoHandedSword:
                case WeaponClass.OneHandedAxe:
                case WeaponClass.TwoHandedAxe:
                case WeaponClass.Mace:
                case WeaponClass.TwoHandedMace:
                case WeaponClass.OneHandedPolearm:
                case WeaponClass.TwoHandedPolearm:
                case WeaponClass.LowGripPolearm:
                    return true;
                default:
                    return false;
            }
        }

        private void AddEffect(StatusEffect effect)
        {
            var particleEntity = TryAttachParticle(effect.Template.ParticleId, effect.Template.ApplyToWeapon, effect.Template.DoNotAttachToSkeleton);
            _currentEffects.Add(effect, new EffectData(effect, particleEntity));

            if (effect.Template.ApplyToWeapon)
            {
                _hasWeaponAttachedEffect = true;
                if (Agent != null && Agent.IsActive())
                {
                    _weaponFxWieldedIndex = Agent.GetPrimaryWieldedItemIndex();
                }
            }

            CalculateAggregate();
        }

        private GameEntity TryAttachParticle(string particleId, bool weaponAttach, bool groundEffect = false)
        {
            var id = particleId?.Trim();
            if (string.IsNullOrWhiteSpace(id) || id.Equals("none", StringComparison.OrdinalIgnoreCase)) return null;
            if (!HasUsableVisuals()) return null;

            try
            {
                var scene = Mission.Current?.Scene;
                if (scene == null) return null;

                if (weaponAttach)
                {

                    var skel = Agent.AgentVisuals?.GetSkeleton();
                    if (skel == null || !skel.IsValid) return null;

                    sbyte handBone = Agent.Monster != null ? Agent.Monster.MainHandItemBoneIndex : (sbyte)-1;
                    if (handBone < 0 || handBone >= skel.GetBoneCount())
                    {
                        return null;
                    }

                    var wieldedIndex = Agent.GetPrimaryWieldedItemIndex();
                    if (wieldedIndex == EquipmentIndex.None)
                    {
                        return null;
                    }
                    MissionWeapon weapon = Agent.Equipment[wieldedIndex];
                    if (weapon.IsEmpty || weapon.CurrentUsageItem == null)
                    {
                        return null;
                    }
                    var wclass = weapon.CurrentUsageItem.WeaponClass;
                    if (!IsMeleeWeaponClass(wclass))
                    {
                        return null;
                    }

                    float bladeLength = weapon.CurrentUsageItem.GetRealWeaponLength();
                    if (bladeLength <= 0.1f) bladeLength = 1.0f;
                    float start = bladeLength * 0.3f;
                    const float spacing = 0.1f;
                    int copies = (int)((bladeLength - start) / spacing);
                    if (copies < 1) copies = 1;

                    GameEntity firstCarrier = null;
                    for (int i = 0; i < copies; i++)
                    {
                        var wchild = GameEntity.CreateEmpty(scene, true, true, true);
                        var wframe = MatrixFrame.Identity;
                        wframe.Elevate(start + i * spacing);
                        var wps = ParticleSystem.CreateParticleSystemAttachedToEntity(id, wchild, ref wframe);
                        if (wps == null)
                        {
                            wchild.Remove(0);
                            continue;
                        }
                        Agent.AgentVisuals.AddChildEntity(wchild);
                        skel.AddComponentToBone(handBone, wps);

                        if (firstCarrier == null) firstCarrier = wchild;
                        else _extraCarriers.Add(wchild);
                    }
                    return firstCarrier;
                }

                if (groundEffect)
                {

                    var groundEntity = GameEntity.CreateEmpty(scene, true, true, true);
                    var gframe = new MatrixFrame(Mat3.Identity, Agent.GetChestGlobalPosition());
                    groundEntity.SetGlobalFrame(gframe, true);
                    var localFrame = MatrixFrame.Identity;
                    var gps = ParticleSystem.CreateParticleSystemAttachedToEntity(id, groundEntity, ref localFrame);
                    if (gps == null)
                    {
                        groundEntity.Remove(0);
                        return null;
                    }
                    _groundEntities.Add(groundEntity);
                    return groundEntity;
                }

                var skeleton = Agent.AgentVisuals?.GetSkeleton();
                if (skeleton == null || !skeleton.IsValid) return null;

                var child = GameEntity.CreateEmpty(scene, true, true, true);
                var frame = MatrixFrame.Identity;
                var ps = ParticleSystem.CreateParticleSystemAttachedToEntity(id, child, ref frame);
                if (ps == null)
                {
                    child.Remove(0);
                    return null;
                }

                Agent.AgentVisuals.AddChildEntity(child);

                sbyte bone = 1;
                if (bone < skeleton.GetBoneCount())
                {
                    skeleton.AddComponentToBone(bone, ps);
                }
                return child;
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"StatusEffectComponent.TryAttachParticle('{id}') failed: {ex.Message}");
                return null;
            }
        }

        private void RemoveEffect(StatusEffect effect)
        {
            if (!_currentEffects.TryGetValue(effect, out var data)) return;
            RemoveVisuals(data);
            _currentEffects.Remove(effect);

            SotorLog.Debug($"StatusEffect '{effect.Template.StringID}' expired on '{Agent?.Name}'.");
        }

        private void RemoveVisuals(EffectData data)
        {
            if (data?.ParticleEntity == null) return;
            try
            {
                data.ParticleEntity.RemoveAllParticleSystems();

                if (_groundEntities.Remove(data.ParticleEntity))
                {
                    data.ParticleEntity.Remove(0);
                }
                else if (HasUsableVisuals())
                {
                    Agent.AgentVisuals.RemoveChildEntity(data.ParticleEntity, 0);
                }
                else
                {
                    data.ParticleEntity.Remove(0);
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"StatusEffectComponent.RemoveVisuals failed: {ex.Message}");
            }
            data.ParticleEntity = null;
        }

        public override void OnAgentRemoved() => CleanUp();
        public override void OnComponentRemoved() => CleanUp();

        private void CleanUp()
        {
            foreach (var data in _currentEffects.Values.ToList())
            {
                RemoveVisuals(data);
            }

            foreach (var e in _extraCarriers)
            {
                try
                {
                    e?.RemoveAllParticleSystems();
                    if (HasUsableVisuals()) { Agent.AgentVisuals.RemoveChildEntity(e, 0); }
                    else { e?.Remove(0); }
                }
                catch {  }
            }
            _extraCarriers.Clear();
            _currentEffects.Clear();
            _aggregate = null;
            _disabled = true;
        }

        public void Dispose() => CleanUp();
    }
}
