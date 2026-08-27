using System;
using System.Collections.Generic;
using System.Linq;
using SOTOR.AbilitySystem.StatusEffects;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace SOTOR.Items
{

    public class SotorWeaponHitMissionLogic : MissionLogic
    {
        private const float TriggerCooldown = 2f;
        private const float ImbuedEffectDuration = 5f;

        private readonly Dictionary<int, Dictionary<string, float>> _cooldowns =
            new Dictionary<int, Dictionary<string, float>>();

        private float _cleanupAccum;

        private readonly List<SotorItemTraitAgentComponent> _components =
            new List<SotorItemTraitAgentComponent>();

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            if (agent == null || !agent.IsHuman) return;
            var component = new SotorItemTraitAgentComponent(agent);
            agent.AddComponent(component);
            agent.OnAgentWieldedItemChange += component.OnWieldedItemChanged;
            component.OnWieldedItemChanged();
            _components.Add(component);
            if (agent.IsMainAgent) LogLoadout(agent);
        }

        private static void LogLoadout(Agent agent)
        {
            try
            {
                var seen = new List<string>();
                var equipment = agent.Equipment;
                if (equipment != null)
                {
                    for (int i = 0; i < (int)EquipmentIndex.NumAllWeaponSlots; i++)
                    {
                        var weapon = equipment[(EquipmentIndex)i];
                        if (!weapon.IsEmpty && weapon.Item != null) Describe(weapon.Item, agent, "weapon" + i, seen);
                    }
                }
                foreach (var item in SotorItemExtensions.GetArmorItems(agent)) Describe(item, agent, "armor", seen);
                if (seen.Count == 0) SotorLog.Debug("Loadout: the player carries NO enchanted equipment.");
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"LogLoadout failed: {ex.Message}");
            }
        }

        private static void Describe(ItemObject item, Agent agent, string slot, List<string> seen)
        {
            if (item == null || !item.HasAnyTrait(agent)) return;
            foreach (var trait in item.GetTraits(agent))
            {
                string script = trait.OnWeaponHitScript?.ShortName;
                bool registered = !string.IsNullOrEmpty(script) && script != "invalid"
                                  && SotorWeaponHitScriptRegistry.Create(trait) != null;
                SotorLog.Debug($"Loadout [{slot}] '{item.Name}' -> {trait.ItemTraitStringId} "
                            + $"({trait.ValidItemType}, chance={trait.ImbuedEffectChance:0.00}) "
                            + $"script={(string.IsNullOrEmpty(script) || script == "invalid" ? "none" : script)}"
                            + (string.IsNullOrEmpty(script) || script == "invalid" ? "" : registered ? " [registered]" : " [NOT REGISTERED]"));
                seen.Add(trait.ItemTraitStringId);
            }
        }

        public override void OnMissionTick(float dt)
        {
            for (int i = _components.Count - 1; i >= 0; i--)
            {
                var c = _components[i];
                if (c?.OwnerAgent == null || !c.OwnerAgent.IsActive())
                {
                    _components.RemoveAt(i);
                    continue;
                }
                c.TickDynamicTraits(dt);
            }

            _cleanupAccum += dt;
            if (_cleanupAccum < TriggerCooldown) return;
            _cleanupAccum = 0f;
            if (_cooldowns.Count == 0) return;
            float now = Mission.Current.CurrentTime;
            foreach (var agentIndex in _cooldowns.Keys.ToList())
            {
                var map = _cooldowns[agentIndex];
                foreach (var key in map.Keys.ToList())
                {
                    if (map[key] <= now) map.Remove(key);
                }
                if (map.Count == 0) _cooldowns.Remove(agentIndex);
            }
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            if (affectedAgent != null) _cooldowns.Remove(affectedAgent.Index);
        }

        protected override void OnEndMission()
        {
            _cooldowns.Clear();
            SotorFlightRuneAmmo.ClearBattleState();

            SOTOR.AbilitySystem.SotorDamageHelper.ClearPendingReflects();
            SOTOR.AbilitySystem.SotorDamageHelper.ClearPendingBlows();
            SotorLog.Flush();
        }

        public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon,
            in Blow blow, in AttackCollisionData attackCollisionData)
        {
            try
            {
                HandleHit(affectedAgent, affectorAgent, affectorWeapon, blow, attackCollisionData);
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorWeaponHitMissionLogic.OnAgentHit failed: {ex.Message}");
            }
        }

        private void HandleHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon,
            in Blow blow, in AttackCollisionData attackCollisionData)
        {
            if (affectedAgent == affectorAgent) return;

            SotorFlightRuneAmmo.AttemptRecharge(affectorAgent, affectedAgent, blow, affectorWeapon,
                attackCollisionData, killPostfixOnly: true);

            var weaponItem = affectorWeapon.IsEmpty ? null : affectorWeapon.Item;
            if (weaponItem != null && weaponItem.HasAnyTrait(affectorAgent) && IsValidTarget(affectedAgent, affectorAgent))
            {
                var traits = weaponItem.GetTraits(affectorAgent);
                foreach (var trait in traits)
                {
                    if (HasImbue(trait) && CanReceiveStatusEffect(affectedAgent)
                        && MBRandom.RandomFloat <= trait.ImbuedEffectChance)
                    {
                        ApplyImbue(affectedAgent, trait.ImbuedStatusEffectId, affectorAgent);
                    }
                }
                foreach (var trait in traits)
                {
                    if (!HasScript(trait)) continue;

                    if (SotorWeaponHitScriptRegistry.IsKillTrigger(trait))
                    {
                        if (affectedAgent.Health > 0f || blow.IsMissile) continue;
                    }
                    else if (SotorWeaponHitScriptRegistry.IsKillOnlyScript(trait))
                    {
                        if (affectedAgent.Health > 0f) continue;
                    }
                    else if (affectedAgent.Health <= 0f)
                    {
                        continue;
                    }
                    RunScript(trait, affectorAgent, affectedAgent, cooldownOnTarget: true, blow, affectorWeapon, attackCollisionData);
                }
            }

            if (!affectedAgent.IsHero) return;

            var defenseItems = new List<ItemObject>();
            bool blockedWithShield = attackCollisionData.CollidedWithShieldOnBack
                || attackCollisionData.AttackBlockedWithShield || attackCollisionData.IsShieldBroken;
            if (!blockedWithShield)
            {
                defenseItems.AddRange(SotorItemExtensions.GetArmorItems(affectedAgent));
            }
            else
            {
                var offhand = affectedAgent.WieldedOffhandWeapon;
                if (!offhand.IsEmpty && offhand.Item != null) defenseItems.Add(offhand.Item);
            }

            var defenseTraits = new List<SotorItemTrait>();
            foreach (var item in defenseItems)
            {
                if (item.HasAnyTrait(affectedAgent)) defenseTraits.AddRange(item.GetTraits(affectedAgent));
            }

            if (affectedAgent.Health <= 0f && blockedWithShield)
            {
                foreach (var item in SotorItemExtensions.GetArmorItems(affectedAgent))
                {
                    defenseTraits.AddRange(item.GetTraits(affectedAgent)
                        .Where(SotorWeaponHitScriptRegistry.IsReviveScript));
                }
            }

            foreach (var trait in defenseTraits)
            {
                if (HasImbue(trait) && MBRandom.RandomFloat <= trait.ImbuedEffectChance)
                {
                    ApplyImbue(affectedAgent, trait.ImbuedStatusEffectId, affectedAgent);
                }
            }
            foreach (var trait in defenseTraits)
            {
                if (!HasScript(trait)) continue;
                if (SotorWeaponHitScriptRegistry.IsReviveScript(trait))
                {
                    if (affectedAgent.Health > 0f) continue;
                }
                else if (affectedAgent.Health <= 0f)
                {
                    continue;
                }
                RunScript(trait, affectorAgent, affectedAgent, cooldownOnTarget: true, blow, affectorWeapon, attackCollisionData);
            }
        }

        public override void OnMissileHit(Agent attacker, Agent victim, bool isCanceled, AttackCollisionData collisionData)
        {
            try
            {
                if (attacker == null || attacker == victim) return;
                if (!TryGetWieldedTraits(attacker, out var traits)) return;
                if (victim != null && victim.IsActive() && victim.Health > 0f && !victim.IsFadingOut())
                {
                    bool blocked = collisionData.MissileBlockedWithWeapon || collisionData.AttackBlockedWithShield;
                    foreach (var trait in traits)
                    {
                        if (!blocked && HasImbue(trait) && MBRandom.RandomFloat <= trait.ImbuedEffectChance)
                        {
                            ApplyImbue(victim, trait.ImbuedStatusEffectId, attacker);
                        }
                    }
                    foreach (var trait in traits)
                    {
                        if (HasScript(trait))
                        {
                            RunScript(trait, attacker, victim, cooldownOnTarget: false, default, attacker.WieldedWeapon, collisionData);
                        }
                    }
                }

                int missileIndex = collisionData.AffectorWeaponSlotOrMissileIndex;
                var missile = Mission.Current.MissilesList.FirstOrDefault(x => x.Index == missileIndex);
                missile?.Entity?.RemoveAllParticleSystems();
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorWeaponHitMissionLogic.OnMissileHit failed: {ex.Message}");
            }
            finally
            {

                if (!isCanceled) SotorFlightRuneAmmo.ApplyPendingSync(attacker);
            }
        }

        public override void OnAgentShootMissile(Agent shooterAgent, EquipmentIndex weaponIndex, Vec3 position,
            Vec3 velocity, Mat3 orientation, bool hasRigidBody, int forcedMissileIndex)
        {
            try
            {

                SotorFlightRuneAmmo.RecordThrowStart(shooterAgent, weaponIndex);

                if (shooterAgent == null || !TryGetWieldedTraits(shooterAgent, out var traits)) return;
                var missile = Mission.Current.MissilesList.FirstOrDefault(x => x.ShooterAgent == shooterAgent);
                if (missile?.Entity == null) return;
                foreach (var trait in traits)
                {
                    var preset = trait.WeaponParticlePreset;
                    if (preset != null && !string.IsNullOrEmpty(preset.ParticlePrefab)
                        && preset.ParticlePrefab != "invalid" && preset.ParticlePrefab != "none")
                    {
                        missile.Entity.AddParticleSystemComponent(preset.ParticlePrefab);
                    }
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorWeaponHitMissionLogic.OnAgentShootMissile failed: {ex.Message}");
            }
        }

        private static bool TryGetWieldedTraits(Agent agent, out List<SotorItemTrait> traits)
        {
            traits = null;
            if (!agent.IsHuman) return false;
            var wielded = agent.WieldedWeapon;
            if (wielded.IsEmpty || wielded.Item == null) return false;
            var list = wielded.Item.GetTraits(agent);
            if (list == null || list.Count == 0) return false;
            traits = list;
            return true;
        }

        private static bool HasImbue(SotorItemTrait trait)
        {
            return !string.IsNullOrWhiteSpace(trait.ImbuedStatusEffectId)
                && !trait.ImbuedStatusEffectId.Equals("none", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasScript(SotorItemTrait trait)
        {
            var name = trait.OnWeaponHitScript?.WeaponScriptName;
            return !string.IsNullOrWhiteSpace(name) && name != "invalid";
        }

        private static bool IsValidTarget(Agent affectedAgent, Agent affectorAgent)
        {
            return affectedAgent != null && affectorAgent != null && affectedAgent != affectorAgent
                && affectedAgent.IsActive() && !affectedAgent.IsFadingOut()
                && (Mission.Current == null || Mission.Current.FindAgentWithIndex(affectedAgent.Index) == affectedAgent);
        }

        private static bool CanReceiveStatusEffect(Agent agent)
        {
            return agent != null && agent.IsHuman && agent.IsActive() && agent.Health > 0f && !agent.IsFadingOut();
        }

        private static void ApplyImbue(Agent target, string effectId, Agent applier)
        {
            if (target == null || string.IsNullOrWhiteSpace(effectId)) return;
            target.GetComponent<StatusEffectComponent>()
                ?.RunStatusEffect(effectId, applier, ImbuedEffectDuration, append: false);
        }

        private void RunScript(SotorItemTrait trait, Agent affectorAgent, Agent affectedAgent, bool cooldownOnTarget,
            in Blow blow, in MissionWeapon affectorWeapon, in AttackCollisionData collisionData)
        {
            var cooldownAgent = cooldownOnTarget ? affectedAgent : affectorAgent;
            if (cooldownAgent == null) return;
            if (MBRandom.RandomFloatRanged(0f, 1f) > trait.ImbuedEffectChance) return;

            bool exemptFromCooldown = trait.ItemTraitStringId == SotorFlightRuneAmmo.TraitId;
            if (!exemptFromCooldown && IsOnCooldown(trait, cooldownAgent)) return;

            var script = SotorWeaponHitScriptRegistry.Create(trait);
            if (script == null) return;
            try
            {
                script.OnHit(affectorAgent, affectedAgent, blow, affectorWeapon, collisionData);
                RegisterCooldown(trait, cooldownAgent);
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"Weapon hit script failed | trait={trait.ItemTraitStringId} | {ex.GetType().Name}: {ex.Message}");
            }
        }

        private bool IsOnCooldown(SotorItemTrait trait, Agent agent)
        {
            return _cooldowns.TryGetValue(agent.Index, out var map)
                && map.TryGetValue(trait.ItemTraitStringId, out var until)
                && until > Mission.Current.CurrentTime;
        }

        private void RegisterCooldown(SotorItemTrait trait, Agent agent)
        {
            if (!_cooldowns.TryGetValue(agent.Index, out var map))
            {
                map = new Dictionary<string, float>();
                _cooldowns.Add(agent.Index, map);
            }
            map[trait.ItemTraitStringId] = Mission.Current.CurrentTime + TriggerCooldown;
        }
    }
}
