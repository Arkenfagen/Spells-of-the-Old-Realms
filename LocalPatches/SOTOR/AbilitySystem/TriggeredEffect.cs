using System;
using System.Collections.Generic;
using SOTOR.AbilitySystem.StatusEffects;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem
{

    public class TriggeredEffect
    {
        private readonly TriggeredEffectTemplate _template;

        public bool OwnerIsSingleTarget { get; set; }

        public string OwnerSpellName { get; set; }

        public string OwnerShipTag { get; set; } = "untagged";
        public int OwnerSpellTier { get; set; }

        public TriggeredEffect(TriggeredEffectTemplate template)
        {
            _template = template;
        }

        public void Trigger(Vec3 position, Vec3 normal, Agent triggererAgent, MBList<Agent> targets = null, float damageMultiplier = 1f)
        {
            if (_template == null || triggererAgent == null || !triggererAgent.IsActive())
            {
                return;
            }

            float radius = _template.Radius;

            if (targets == null)
            {
                targets = new MBList<Agent>();
                switch (_template.TargetType)
                {
                    case TargetType.Self:
                        targets.Add(triggererAgent);
                        break;
                    case TargetType.Enemy:
                        targets = Mission.Current.GetNearbyEnemyAgents(position.AsVec2, radius, triggererAgent.Team, targets);
                        break;
                    case TargetType.Friendly:
                        targets = Mission.Current.GetNearbyAllyAgents(position.AsVec2, radius, triggererAgent.Team, targets);
                        break;
                    default:
                        targets = Mission.Current.GetNearbyAgents(position.AsVec2, radius, targets);
                        break;
                }
            }

            targets = NormalizeTriggeredTargets(targets);

            PlaySound(position);

            if (_template.DamageAmount > 0)
            {
                int min = (int)(_template.DamageAmount * (1f - _template.DamageVariance) * damageMultiplier);
                int max = (int)(_template.DamageAmount * (1f + _template.DamageVariance) * damageMultiplier);
                SotorDamageHelper.DamageAgents(targets, min, max, triggererAgent, _template, _template.HasShockWave, position, OwnerIsSingleTarget, OwnerSpellName);
            }

            TryDamageShip(position, triggererAgent, targets, damageMultiplier);

            float scaledDuration = _template.ImbuedStatusEffectDuration;
            if (_template.ImbuedStatusEffectDuration > 1.5f
                && SOTOR.Extensions.AgentExtensions.GetHero(triggererAgent) is TaleWorlds.CampaignSystem.Hero casterHero)
            {
                scaledDuration *= SotorSpellcraftHelper.GetSpellDurationFactor(casterHero);
            }

            ApplyStatusEffects(targets, triggererAgent, scaledDuration);

            RunTriggeredScript(position, triggererAgent, targets, scaledDuration);

            SpawnVisuals(position, normal);
        }

        private void TryDamageShip(Vec3 position, Agent triggererAgent, MBList<Agent> targets, float damageMultiplier)
        {
            try
            {

                var mission = Mission.Current;
                if (!SotorNavalBridge.IsNavalMission(mission)) return;
                bool tagged = !string.IsNullOrEmpty(OwnerShipTag)
                    && !OwnerShipTag.Equals("untagged", StringComparison.OrdinalIgnoreCase);
                if (!tagged) return;
                if (!SotorSettings.EnableSpellShipDamage) return;
                if (_template.DamageAmount <= 0) return;
                float mcm = SotorSettings.SpellShipDamageMultiplier;
                if (mcm <= 0f) return;

                float tierWeight = SotorNavalBridge.GetTierWeight(OwnerSpellTier);
                float effectiveness = SotorDamageHelper.GetSpellcraftDamageFactorFor(triggererAgent);
                float scaledBase = _template.DamageAmount * tierWeight * effectiveness * mcm;
                if (scaledBase <= 0f) return;

                int hintIndex = -1;
                if (targets != null)
                {
                    for (int i = 0; i < targets.Count; i++)
                    {
                        if (targets[i] != null && targets[i].IsActive())
                        {
                            hintIndex = targets[i].Index;
                            break;
                        }
                    }
                }

                SotorNavalBridge.ApplyShipDamage(mission, triggererAgent, position, OwnerShipTag,
                    scaledBase, hintIndex, _template.DamageType, OwnerSpellName);
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"TriggeredEffect.TryDamageShip failed: {ex.Message}");
            }
        }

        private void RunTriggeredScript(Vec3 position, Agent triggererAgent, MBList<Agent> targets, float scaledDuration)
        {
            var script = SOTOR.AbilitySystem.TriggeredScripts.TriggeredScriptRegistry.Resolve(_template.ScriptNameToTrigger);
            if (script == null)
            {
                return;
            }

            try
            {
                script.OnTrigger(position, triggererAgent, targets, scaledDuration, _template, OwnerSpellName);
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"TriggeredEffect script '{_template.ScriptNameToTrigger}' failed: {ex.Message}");
            }
        }

        private void ApplyStatusEffects(MBList<Agent> targets, Agent triggererAgent, float scaledDuration)
        {
            if (_template.ImbuedStatusEffects == null || _template.ImbuedStatusEffects.Count == 0)
            {
                return;
            }

            bool friendlyBuff = _template.TargetType == TargetType.Friendly
                                || _template.TargetType == TargetType.FriendlyHero;
            bool hasOtherAlly = false;
            if (targets != null)
            {
                foreach (var t in targets)
                {
                    if (t != null && t != triggererAgent) { hasOtherAlly = true; break; }
                }
            }
            if (friendlyBuff && hasOtherAlly && targets != null && !targets.Contains(triggererAgent)
                && AbilitySystem.SotorPerks.ArcaneLink != null
                && SOTOR.Extensions.AgentExtensions.GetHero(triggererAgent) is TaleWorlds.CampaignSystem.Hero linkHero
                && linkHero.GetPerkValue(AbilitySystem.SotorPerks.ArcaneLink))
            {
                targets.Add(triggererAgent);
                SotorLog.Debug($"ArcaneLink: caster '{triggererAgent.Name}' added to ally-buff targets (buffed {targets.Count - 1} ally/allies).");
            }

            var casterForXp = SOTOR.Extensions.AgentExtensions.GetHero(triggererAgent);
            System.Collections.Generic.HashSet<int> xpAwardedTargets =
                casterForXp != null ? new System.Collections.Generic.HashSet<int>() : null;

            bool isArcaneConduit = OwnerSpellName == AbilitySystem.SotorArcaneConduitHelper.AbilityDisplayName;
            if (isArcaneConduit)
            {
                xpAwardedTargets = null;
            }

            foreach (var effectId in _template.ImbuedStatusEffects)
            {
                if (string.IsNullOrWhiteSpace(effectId))
                {
                    continue;
                }

                foreach (var agent in targets)
                {
                    if (agent == null || !agent.IsActive() || agent.Health < 1f || agent.IsFadingOut())
                    {
                        continue;
                    }

                    var component = agent.GetComponent<StatusEffectComponent>();
                    bool newlyApplied = component != null
                        && component.RunStatusEffect(effectId, triggererAgent, scaledDuration, true, OwnerSpellName);

                    if (newlyApplied && xpAwardedTargets != null && xpAwardedTargets.Add(agent.Index))
                    {
                        SotorSpellcraftHelper.GrantAbilityOutcomeXp(casterForXp, 10, OwnerIsSingleTarget);
                    }
                }
            }
        }

        private static MBList<Agent> NormalizeTriggeredTargets(IEnumerable<Agent> rawTargets)
        {
            var result = new MBList<Agent>();
            if (rawTargets == null)
            {
                return result;
            }

            var mission = Mission.Current;
            var seen = new HashSet<int>();
            foreach (var agent in rawTargets)
            {
                if (agent != null && agent.IsActive() && agent.Health >= 1f && !agent.IsFadingOut()
                    && (mission == null || mission.FindAgentWithIndex(agent.Index) == agent)
                    && seen.Add(agent.Index))
                {
                    result.Add(agent);
                }
            }

            return result;
        }

        private void SpawnVisuals(Vec3 position, Vec3 normal)
        {
            string prefab = _template?.BurstParticleEffectPrefab?.Trim();
            if (string.IsNullOrWhiteSpace(prefab) || prefab.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                var entity = GameEntity.CreateEmpty(Mission.Current.Scene, true, true, true);
                var identity = MatrixFrame.Identity;
                ParticleSystem.CreateParticleSystemAttachedToEntity(prefab, entity, ref identity);

                Vec3 fwd = normal;
                if (Math.Abs(fwd.x) + Math.Abs(fwd.y) + Math.Abs(fwd.z) < 0.0001f)
                {
                    fwd = Vec3.Forward;
                }

                var rot = Mat3.CreateMat3WithForward(fwd);
                var frame = new MatrixFrame(rot, position);
                entity.SetGlobalFrame(frame, true);
                GameEntityExtensions.FadeOut(entity, _template.SoundEffectLength, true);
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"TriggeredEffect.SpawnVisuals('{prefab}') failed: {ex.Message}");
            }
        }

        private void PlaySound(Vec3 position)
        {
            var id = _template?.SoundEffectId?.Trim();
            if (string.IsNullOrWhiteSpace(id) || id.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                int eventId = SoundEvent.GetEventIdFromString(id);
                if (eventId < 0)
                {
                    return;
                }
                Mission.Current.MakeSound(eventId, position, false, false, -1, -1);
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"TriggeredEffect.PlaySound('{id}') failed: {ex.Message}");
            }
        }
    }
}
