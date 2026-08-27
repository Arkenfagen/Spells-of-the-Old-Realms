using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace SOTOR.Items
{

    public class SotorItemTraitAgentComponent : AgentComponent
    {

        private readonly List<Tuple<ItemObject, SotorItemTrait, float>> _dynamicTraits =
            new List<Tuple<ItemObject, SotorItemTrait, float>>();

        private readonly List<GameEntity> _glowCarriers = new List<GameEntity>();
        private readonly List<ParticleSystem> _glowParticles = new List<ParticleSystem>();
        private ItemObject _glowBuiltForItem;

        private float _tickAccum;

        public SotorItemTraitAgentComponent(Agent agent) : base(agent) { }

        public Agent OwnerAgent => Agent;

        public void OnWieldedItemChanged()
        {
            UpdateGlow();
        }

        internal void TickDynamicTraits(float dt)
        {
            if (_dynamicTraits.Count == 0) return;
            _tickAccum += dt;
            if (_tickAccum < 1f) return;
            float elapsed = _tickAccum;
            _tickAccum = 0f;
            bool changed = false;
            for (int i = _dynamicTraits.Count - 1; i >= 0; i--)
            {
                float remaining = _dynamicTraits[i].Item3 - elapsed;
                if (remaining <= 0f)
                {
                    _dynamicTraits.RemoveAt(i);
                    changed = true;
                }
                else
                {
                    _dynamicTraits[i] = Tuple.Create(_dynamicTraits[i].Item1, _dynamicTraits[i].Item2, remaining);
                }
            }
            if (changed) UpdateGlow();
        }

        public void AddTraitToWeapon(ItemObject weaponItem, SotorItemTrait trait, float duration)
        {
            if (weaponItem == null || trait == null || duration <= 0f) return;
            var existing = _dynamicTraits.FirstOrDefault(x => x.Item1 == weaponItem && x.Item2 == trait);
            if (existing != null)
            {
                _dynamicTraits.Remove(existing);
                _dynamicTraits.Add(Tuple.Create(weaponItem, trait, existing.Item3 + duration));
            }
            else
            {
                _dynamicTraits.Add(Tuple.Create(weaponItem, trait, duration));
                UpdateGlow();
            }
        }

        public void RemoveTraitFromWieldedWeapon(string traitId)
        {
            var wielded = Agent.WieldedWeapon;
            if (wielded.IsEmpty || wielded.Item == null) return;
            var entry = _dynamicTraits.FirstOrDefault(x => x.Item1 == wielded.Item && x.Item2.ItemTraitStringId == traitId);
            if (entry != null)
            {
                _dynamicTraits.Remove(entry);
                UpdateGlow();
            }
        }

        public List<SotorItemTrait> GetDynamicTraits(ItemObject item)
        {
            var list = new List<SotorItemTrait>();
            foreach (var t in _dynamicTraits)
            {
                if (t.Item1 == item) list.Add(t.Item2);
            }
            return list;
        }

        public bool HasDynamicTraits(ItemObject item)
        {
            for (int i = 0; i < _dynamicTraits.Count; i++)
            {
                if (_dynamicTraits[i].Item1 == item) return true;
            }
            return false;
        }

        public override void OnAgentRemoved()
        {
            ClearGlow();
        }

        private void UpdateGlow()
        {
            try
            {
                var wielded = Agent.WieldedWeapon;
                var item = (!wielded.IsEmpty && wielded.CurrentUsageItem != null) ? wielded.Item : null;
                var presets = new List<WeaponParticlePreset>();
                if (item != null && item.HasAnyTrait(Agent) && !wielded.CurrentUsageItem.IsRangedWeapon)
                {
                    foreach (var t in item.GetTraits(Agent))
                    {
                        var p = t.WeaponParticlePreset;
                        if (p != null && !string.IsNullOrEmpty(p.ParticlePrefab)
                            && p.ParticlePrefab != "invalid" && p.ParticlePrefab != "none")
                        {
                            presets.Add(p);
                        }
                    }
                }

                if (presets.Count == 0)
                {
                    ClearGlow();
                    return;
                }
                if (_glowBuiltForItem == item && _glowCarriers.Count > 0) return;

                ClearGlow();
                _glowBuiltForItem = item;

                var scene = Mission.Current?.Scene;
                var skel = Agent.AgentVisuals?.GetSkeleton();
                if (scene == null || skel == null || !skel.IsValid) return;
                sbyte handBone = Agent.Monster != null ? Agent.Monster.MainHandItemBoneIndex : (sbyte)-1;
                if (handBone < 0 || handBone >= skel.GetBoneCount()) return;

                float length = wielded.CurrentUsageItem.GetRealWeaponLength();
                if (length <= 0.1f) length = 1.0f;
                float start = length * StartOffsetFraction(wielded.CurrentUsageItem.WeaponClass);
                const float spacing = 0.1f;

                foreach (var preset in presets)
                {
                    int copies = preset.IsUniqueSingleCopy ? 1 : Math.Max(1, (int)((length - start) / spacing));
                    for (int i = 0; i < copies; i++)
                    {
                        var carrier = GameEntity.CreateEmpty(scene, true, true, true);
                        var frame = MatrixFrame.Identity;
                        frame.Elevate(preset.IsUniqueSingleCopy ? 0f : start + i * spacing);
                        var ps = ParticleSystem.CreateParticleSystemAttachedToEntity(preset.ParticlePrefab, carrier, ref frame);
                        if (ps == null)
                        {
                            carrier.Remove(0);
                            continue;
                        }
                        Agent.AgentVisuals.AddChildEntity(carrier);
                        skel.AddComponentToBone(handBone, ps);
                        _glowCarriers.Add(carrier);
                        _glowParticles.Add(ps);
                    }
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"Item trait glow update failed: {ex.Message}");
            }
        }

        private static float StartOffsetFraction(WeaponClass weaponClass)
        {
            switch (weaponClass)
            {
                case WeaponClass.OneHandedSword:
                case WeaponClass.TwoHandedSword:
                    return 0.3f;
                case WeaponClass.OneHandedPolearm:
                case WeaponClass.TwoHandedPolearm:
                case WeaponClass.LowGripPolearm:
                    return 0.7f;
                default:
                    return 0.85f;
            }
        }

        private void ClearGlow()
        {
            foreach (var ps in _glowParticles)
            {
                try { ps?.SetEnable(false); } catch { }
            }
            foreach (var e in _glowCarriers)
            {
                try
                {
                    if (e == null) continue;
                    e.RemoveAllParticleSystems();
                    if (Agent?.AgentVisuals != null && Agent.AgentVisuals.IsValid())
                    {
                        Agent.AgentVisuals.RemoveChildEntity(e, 0);
                    }
                    else
                    {
                        e.Remove(0);
                    }
                }
                catch {  }
            }
            _glowParticles.Clear();
            _glowCarriers.Clear();
            _glowBuiltForItem = null;
        }
    }
}
