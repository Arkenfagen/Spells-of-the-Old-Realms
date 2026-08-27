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

        public AbilityEffectType OwnerEffectType { get; set; } = AbilityEffectType.Missile;

        public TriggeredEffect(TriggeredEffectTemplate template)
        {
            _template = template;
        }

        public void Trigger(Vec3 position, Vec3 normal, Agent triggererAgent, MBList<Agent> targets = null, float damageMultiplier = 1f)
        {
            try
            {
                TriggerCore(position, normal, triggererAgent, targets, damageMultiplier);
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"TriggeredEffect.Trigger failed (effect='{_template?.StringID}', "
                            + $"caster='{triggererAgent?.Name}', pos={position}): {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void LogBlastGeometry(Vec3 position, Agent triggererAgent,
            TaleWorlds.CampaignSystem.Hero triggererHero,
            MBList<Agent> targets, float radius, float radiusFactor, bool preTargeted)
        {
            try
            {
                if (radius <= 0f || _template == null || _template.DamageAmount <= 0) return;
                if (triggererAgent == null || triggererAgent != Mission.Current?.MainAgent) return;

                bool verbose = SotorLog.MinLevel <= SotorLog.Level.Debug;

                int count = targets?.Count ?? 0;
                float farthestFlat = 0f, farthest3d = 0f;
                string farthestName = "none";
                int beyond = 0;
                if (targets != null)
                {
                    foreach (var a in targets)
                    {
                        if (a == null) continue;
                        float flat = a.Position.AsVec2.Distance(position.AsVec2);
                        float d3 = a.Position.Distance(position);
                        if (flat > farthestFlat)
                        {
                            farthestFlat = flat;
                            farthest3d = d3;
                            farthestName = a.Name ?? "?";
                        }
                        if (flat > radius + 0.01f) beyond++;
                    }
                }

                int missionAgents = Mission.Current?.Agents?.Count ?? -1;
                if (verbose)
                SotorLog.Debug(
                    $"BLAST '{_template.StringID}' (spell='{OwnerSpellName ?? "?"}'): "
                    + $"templateRadius={_template.Radius} gearFactor={radiusFactor:0.###} finalRadius={radius:0.##} "
                    + $"targetType={_template.TargetType} preTargeted={preTargeted} "
                    + $"hit={count}/{missionAgents} agents, beyondRadius={beyond}, "
                    + $"farthest='{farthestName}' flat={farthestFlat:0.#}m true3d={farthest3d:0.#}m");

                if (beyond > 0)
                {
                    SotorLog.Warn($"BLAST '{_template.StringID}': {beyond} target(s) are OUTSIDE the "
                                  + $"{radius:0.##}m radius - the gather is not honouring it.");
                }

                if (!verbose) return;

                float heroFactor = SOTOR.Items.SotorItemTraitCampaign.GetSpellRadiusFactor(triggererHero);
                bool arena = AbilityMissionModeHelper.IsArenaOrTournamentMission(Mission.Current);
                SotorLog.Debug(
                    $"BLAST radius sources: arena={arena} appliedFromWornGear={radiusFactor:0.###} "
                    + $"heroSavedLoadout={heroFactor:0.###}"
                    + (Math.Abs(radiusFactor - heroFactor) > 0.001f
                        ? "  (differs: loaner or changed kit - the worn factor wins by design)"
                        : ""));
                if (radiusFactor > 1.001f)
                {
                    SotorLog.Debug("BLAST worn gear says: "
                                   + SOTOR.Items.SotorItemExtensions.DescribeWornStat(
                                       triggererAgent, SOTOR.Items.SotorItemTraitStatType.SpellRadius));
                }

                float wornDmg = SOTOR.Items.SotorItemExtensions.SumArmorDamageBonus(
                    triggererAgent, _template.DamageType);
                SotorLog.Debug(
                    $"BLAST damage sources: type={_template.DamageType} wornArmourBonus=+{wornDmg:0.###}"
                    + (wornDmg != 0f ? "  (applies to every victim of this cast before their resists)" : ""));
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"BLAST diagnostic failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void TriggerCore(Vec3 position, Vec3 normal, Agent triggererAgent, MBList<Agent> targets, float damageMultiplier)
        {
            if (_template == null || triggererAgent == null || !triggererAgent.IsActive())
            {
                return;
            }

            float radius = _template.Radius;
            float radiusFactor = 1f;

            var triggererHero = SOTOR.Extensions.AgentExtensions.GetHero(triggererAgent);
            if (triggererHero != null && radius > 0f)
            {
                float wornPct = SOTOR.Items.SotorItemExtensions.SumWornStat(
                    triggererAgent, SOTOR.Items.SotorItemTraitStatType.SpellRadius);
                radiusFactor = wornPct != 0f ? Math.Max(0.1f, 1f + wornPct / 100f) : 1f;
                radius *= radiusFactor;
            }

            bool preTargeted = targets != null;

            if (targets == null)
            {
                targets = new MBList<Agent>();
                switch (_template.TargetType)
                {
                    case TargetType.Self:
                        targets.Add(triggererAgent);
                        break;

                    case TargetType.Enemy:
                        targets = triggererAgent.Team != null
                            ? Mission.Current.GetNearbyEnemyAgents(position.AsVec2, radius, triggererAgent.Team, targets)
                            : Mission.Current.GetNearbyAgents(position.AsVec2, radius, targets);
                        break;
                    case TargetType.Friendly:
                        targets = triggererAgent.Team != null
                            ? Mission.Current.GetNearbyAllyAgents(position.AsVec2, radius, triggererAgent.Team, targets)
                            : Mission.Current.GetNearbyAgents(position.AsVec2, radius, targets);
                        break;
                    default:
                        targets = Mission.Current.GetNearbyAgents(position.AsVec2, radius, targets);
                        break;
                }
            }

            targets = NormalizeTriggeredTargets(targets);

            LogBlastGeometry(position, triggererAgent, triggererHero, targets, radius, radiusFactor, preTargeted);

            PlaySound(position);

            if (_template.DamageAmount > 0)
            {
                int min = (int)(_template.DamageAmount * (1f - _template.DamageVariance) * damageMultiplier);
                int max = (int)(_template.DamageAmount * (1f + _template.DamageVariance) * damageMultiplier);

                SotorDamageHelper.DamageAgents(targets, min, max, triggererAgent, _template,
                    _template.HasShockWave, position, OwnerIsSingleTarget, OwnerSpellName, radius,
                    OwnerEffectType, OwnerSpellTier);
            }

            TryDamageShip(position, triggererAgent, targets, damageMultiplier);

            float scaledDuration = _template.ImbuedStatusEffectDuration;
            if (_template.ImbuedStatusEffectDuration > 1.5f
                && SOTOR.Extensions.AgentExtensions.GetHero(triggererAgent) is TaleWorlds.CampaignSystem.Hero casterHero)
            {
                scaledDuration *= SotorSpellcraftHelper.GetSpellDurationFactor(casterHero);
            }

            ApplyStatusEffects(targets, triggererAgent, scaledDuration);

            NotePracticeStatusEffects(targets, triggererAgent);

            RunTriggeredScript(position, triggererAgent, targets, scaledDuration);

            SpawnVisuals(position, normal);
        }

        private void NotePracticeStatusEffects(MBList<Agent> targets, Agent triggererAgent)
        {
            if (targets == null || targets.Count == 0 || triggererAgent == null) return;
            if (string.IsNullOrEmpty(OwnerSpellName)) return;

            try
            {
                int allies = 0, enemies = 0;
                foreach (var a in targets)
                {
                    if (a == null || !a.IsHuman || !a.IsActive()) continue;
                    if (a.IsEnemyOf(triggererAgent)) enemies++;
                    else if (a != triggererAgent) allies++;
                }

                AbilitySystem.Rivals.SotorPracticeTracker.NoteAllyBuffed(triggererAgent, OwnerSpellName, allies);
                AbilitySystem.Rivals.SotorPracticeTracker.NoteEnemyAfflicted(triggererAgent, OwnerSpellName, enemies);
            }
            catch (System.Exception ex)
            {
                SotorLog.Warn($"Practice: status-effect counting failed harmlessly: {ex.GetType().Name}: {ex.Message}");
            }
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
                if (float.IsNaN(fwd.x) || float.IsNaN(fwd.y) || float.IsNaN(fwd.z)
                    || float.IsInfinity(fwd.x) || float.IsInfinity(fwd.y) || float.IsInfinity(fwd.z)
                    || Math.Abs(fwd.x) + Math.Abs(fwd.y) + Math.Abs(fwd.z) < 0.0001f)
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
