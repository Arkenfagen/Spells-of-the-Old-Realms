using System;
using SOTOR.AbilitySystem.Crosshairs;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem
{
    public abstract class Ability
    {

        private int _coolDownLeft = 0;
        private float _cooldownEndTime;

        public string StringID { get; }

        public AbilityTemplate Template { get; }

        public AbilityCrosshair Crosshair { get; private set; }

        public bool RequiresTargeting => Template.AbilityTargetType != AbilityTargetType.Self;

        public virtual bool IsThrownWeapon => false;

        protected Ability(AbilityTemplate template)
        {
            StringID = template.StringID;
            Template = template;
        }

        public void SetCrosshair(AbilityCrosshair crosshair)
        {
            Crosshair = crosshair;
        }

        public virtual bool IsDisabled(Agent casterAgent, out TextObject disabledReason)
        {
            disabledReason = new TextObject("{=sotor_ability_enabled}Enabled");
            if (IsOnCooldown())
            {
                disabledReason = new TextObject("{=sotor_ability_on_cooldown}On cooldown");
                return true;
            }

            if (Template != null
                && Template.AbilityEffectType == AbilityEffectType.Summoning
                && AbilityMissionModeHelper.IsArenaOrTournamentMission(Mission.Current))
            {
                disabledReason = SotorText.GetObject("sotor_no_summons_in_arena");
                return true;
            }

            return false;
        }

        public bool CanCast(Agent casterAgent, out TextObject disabledReason)
        {
            return !IsDisabled(casterAgent, out disabledReason);
        }

        public bool IsOnCooldown()
        {
            if (Mission.Current == null)
            {
                return false;
            }
            float remaining = _cooldownEndTime - Mission.Current.CurrentTime;
            if (remaining <= 0f)
            {
                _coolDownLeft = 0;
                return false;
            }
            _coolDownLeft = (int)Math.Ceiling(remaining);
            return true;
        }

        public int GetCoolDownLeft()
        {
            IsOnCooldown();
            return _coolDownLeft;
        }

        public void SetCoolDown(int cooldownTime)
        {
            if (Mission.Current == null)
            {
                return;
            }
            _coolDownLeft = cooldownTime;
            _cooldownEndTime = Mission.Current.CurrentTime + _coolDownLeft + 0.8f;
        }

        public void ReduceCooldown(float seconds)
        {
            if (Mission.Current == null || seconds <= 0f)
            {
                return;
            }
            float remaining = _cooldownEndTime - Mission.Current.CurrentTime;
            if (remaining <= 0f)
            {
                return;
            }
            _cooldownEndTime -= Math.Min(seconds, remaining);
            IsOnCooldown();
        }

        public bool TryCast(Agent casterAgent, out TextObject failureReason)
        {
            return TryCast(casterAgent, null, out failureReason);
        }

        public virtual bool TryCast(Agent casterAgent, SotorTarget preferredTarget, out TextObject failureReason)
        {
            failureReason = null;

            if (casterAgent == null || Mission.Current == null || Mission.Current.Scene == null)
            {
                failureReason = new TextObject("{=sotor_cast_no_context}No mission context.");
                SotorLog.Warn($"TryCast {StringID}: no caster/mission/scene.");
                return false;
            }

            if (IsDisabled(casterAgent, out failureReason))
            {

                SotorLog.Info($"TryCast {StringID}: blocked for {DescribeCaster(casterAgent)}: "
                              + $"{failureReason?.ToString() ?? "disabled"} (cooldown {GetCoolDownLeft()}s).");
                return false;
            }

            if (!IsThrownWeapon)
            {
                SotorCastAnimation.PlayRelease(casterAgent, Template?.AnimationActionName);
            }

            Rivals.SotorPracticeTracker.NoteCast(casterAgent, StringID);

            try
            {
                var scene = Mission.Current.Scene;

                MatrixFrame frame = GetSpawnFrame(casterAgent);

                GameEntity parent = GameEntity.CreateEmpty(scene, false, true, false);
                parent.SetGlobalFrame(frame, true);

                if (Template.TriggerType == TriggerType.OnCollision)
                {
                    AddPhysics(parent);
                }

                {
                    var casterFrame = casterAgent.Frame;
                    var eye = casterAgent.GetEyeGlobalPosition();
                    var localOffset = casterFrame.TransformToLocal(frame.origin - casterFrame.origin);
                    var fwd = frame.rotation.f;
                    float horiz = (float)Math.Sqrt(fwd.x * fwd.x + fwd.y * fwd.y);
                    float elevationDeg = (float)(Math.Atan2(fwd.z, horiz) * 180.0 / Math.PI);
                    SotorLog.Debug(
                        $"Spawn {StringID} ({Template.AbilityEffectType}): casterPos={casterFrame.origin} eye={eye} " +
                        $"spawnOrigin={frame.origin} localOffset(r,f,u)={localOffset} " +
                        $"lookForward={fwd} lookElevation={elevationDeg:0.0}deg " +
                        $"spawnZ-casterZ={frame.origin.z - casterFrame.origin.z:0.00} spawnZ-eyeZ={frame.origin.z - eye.z:0.00}");
                }

                AI.SotorAimDiagnostics.LogCastGeometry(casterAgent, this, frame);

                string scriptTypeName = GetScriptTypeName();
                parent.CreateAndAddScriptComponent(scriptTypeName, false);
                var script = parent.GetFirstScriptOfType<AbilityScript>();
                if (script == null)
                {
                    failureReason = new TextObject("{=sotor_cast_no_script}Ability script not attached.");
                    SotorLog.Warn($"TryCast {StringID}: '{scriptTypeName}' not attached (type not registered?).");
                    return false;
                }

                var prefabName = Template.ParticleEffectPrefab;
                GameEntity child = null;
                if (!string.IsNullOrEmpty(prefabName) && prefabName != "none")
                {
                    child = GameEntity.Instantiate(scene, prefabName, true, true, string.Empty);
                    if (child == null)
                    {
                        SotorLog.Warn($"TryCast {StringID}: Instantiate('{prefabName}') returned null; continuing without VFX child.");
                    }
                    else
                    {
                        parent.AddChild(child, false);
                    }
                }

                script.Initialize(this, ref parent);
                script.SetCasterAgent(casterAgent);

                var explicitTarget = ResolveExplicitTarget(casterAgent, preferredTarget);
                if (explicitTarget != null)
                {
                    var list = new MBList<Agent> { explicitTarget };
                    script.SetExplicitTargetAgents(list);
                }

                if (Template.SeekerParameters != null)
                {

                    var target = (preferredTarget != null && preferredTarget.IsValid)
                        ? preferredTarget
                        : (AiBrainTarget(casterAgent) ?? FindNearestEnemyTarget(casterAgent));
                    if (target != null)
                    {
                        script.SetTargetSeeking(target, Template.SeekerParameters);
                        SotorLog.Info($"TryCast {StringID}: homing at '{target.Agent?.Name}' (source={(preferredTarget != null ? "crosshair" : "auto")}).");

                        AI.SotorAimDiagnostics.LogSeekerTarget(casterAgent, this, target.Agent);
                    }
                }

                parent.CallScriptCallbacks(true);

                SotorLog.Info(
                    $"TryCast {StringID}: spawned via {scriptTypeName} | effect={Template.AbilityEffectType} " +
                    $"prefab='{prefabName}' origin={frame.origin}");
            }
            catch (Exception ex)
            {
                failureReason = new TextObject("{=sotor_cast_spawn_failed}Cast spawn failed.");
                SotorLog.Error($"TryCast {StringID}: spawn EXCEPTION: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                return false;
            }

            SetCoolDown(Template.CoolDown);

            OnCastSucceeded(casterAgent);

            SotorLog.Info($"TryCast {StringID}: cast OK by {DescribeCaster(casterAgent)}; "
                          + $"cooldown started ({Template.CoolDown}s).");
            return true;
        }

        private static string DescribeCaster(Agent casterAgent)
        {
            try
            {
                if (casterAgent == null) return "(null caster)";
                string name = casterAgent.Name ?? "(unnamed)";
                if (casterAgent.IsMainAgent) return name + " [PLAYER]";
                var team = casterAgent.Team;
                if (team == null) return name + " [AI, no team]";
                return name + (team.IsPlayerTeam || team.IsPlayerAlly ? " [ALLY-AI]" : " [ENEMY-AI]");
            }
            catch
            {
                return "(caster describe failed)";
            }
        }

        protected virtual void OnCastSucceeded(Agent casterAgent)
        {
        }

        private void AddPhysics(GameEntity entity)
        {
            try
            {
                using (new TWSharedMutexWriteLock(Scene.PhysicsAndRayCastLock))
                {
                    GameEntityPhysicsExtensions.AddSphereAsBody(entity, Vec3.Zero, Template.Radius, (BodyFlags)65552);
                    if (Template.UseGravity)
                    {
                        GameEntityPhysicsExtensions.AddPhysics(
                            entity, 1f, entity.CenterOfMass, GameEntityPhysicsExtensions.GetBodyShape(entity),
                            Vec3.Zero, Vec3.Zero, PhysicsMaterial.GetFromName("missile"), false, -1);
                    }
                }
                SotorLog.Info($"AddPhysics {StringID}: sphere body radius={Template.Radius} (gravity={Template.UseGravity}).");
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"AddPhysics {StringID} failed: {ex.Message}; relying on raycast/proximity collision.");
            }
        }

        private string GetScriptTypeName()
        {
            switch (Template.AbilityEffectType)
            {
                case AbilityEffectType.Missile:
                case AbilityEffectType.SeekerMissile:
                    return nameof(MissileScript);
                case AbilityEffectType.Heal:
                    return nameof(HealScript);
                case AbilityEffectType.Augment:
                case AbilityEffectType.TacticalReposition:
                    return nameof(AugmentScript);
                case AbilityEffectType.Wind:
                    return nameof(WindScript);
                case AbilityEffectType.Vortex:
                    return nameof(VortexScript);
                case AbilityEffectType.Blast:
                    return nameof(BlastScript);
                case AbilityEffectType.Bombardment:

                    return nameof(BombardmentScript);
                case AbilityEffectType.Hex:

                    return nameof(AugmentScript);
                case AbilityEffectType.MindControl:
                    return nameof(MindControlScript);
                default:

                    SotorLog.Debug($"TryCast {StringID}: no dedicated script for {Template.AbilityEffectType}; using AugmentScript.");
                    return nameof(AugmentScript);
            }
        }

        private MatrixFrame GetSpawnFrame(Agent casterAgent)
        {
            MatrixFrame frame = casterAgent.LookFrame;

            if (!casterAgent.IsPlayerControlled && TryCalculateAiCastFrame(casterAgent, ref frame))
            {
                return frame;
            }

            switch (Template.AbilityEffectType)
            {
                case AbilityEffectType.Missile:
                case AbilityEffectType.SeekerMissile:
                    frame.origin = casterAgent.GetEyeGlobalPosition();

                    if (casterAgent.IsPlayerControlled && Crosshair != null
                        && Crosshair.TryGetCameraAimDirection(out var aimDir))
                    {
                        var rot = Mat3.CreateMat3WithForward(aimDir);
                        frame.rotation = rot;
                        SotorLog.Debug($"GetSpawnFrame {StringID}: camera-ray aim dir={aimDir} (was LookFrame).");
                    }
                    return frame;

                case AbilityEffectType.Wind:

                    if (Crosshair != null)
                    {
                        frame = Crosshair.Frame;
                    }
                    return frame;

                case AbilityEffectType.Blast:

                    if (Crosshair != null)
                    {
                        frame = Crosshair.Frame;
                        frame.origin.z += 1f;
                    }
                    return frame;

                case AbilityEffectType.Vortex:

                    if (Crosshair != null)
                    {
                        frame = new MatrixFrame(casterAgent.Frame.rotation, Crosshair.Position);
                    }
                    return frame;

                case AbilityEffectType.Bombardment:

                    if (Crosshair != null)
                    {
                        frame = new MatrixFrame(Mat3.Identity, Crosshair.Position);
                        frame.origin.z += Template.Offset;
                    }
                    return frame;

                case AbilityEffectType.Hex:

                    if (Crosshair != null)
                    {
                        frame = new MatrixFrame(Mat3.Identity, Crosshair.Position);
                        if (StringID == "CurseOfMidnightWind")
                        {
                            frame.origin.z -= 1.5f;
                        }
                    }
                    else
                    {
                        frame.origin = casterAgent.GetChestGlobalPosition();
                    }
                    return frame;

                case AbilityEffectType.MindControl:

                    if (Crosshair != null)
                    {
                        frame = new MatrixFrame(Mat3.Identity, Crosshair.Position);
                    }
                    else
                    {
                        frame.origin = casterAgent.GetChestGlobalPosition();
                    }
                    return frame;

                case AbilityEffectType.Heal:
                case AbilityEffectType.Augment:
                default:

                    if (Crosshair != null)
                    {
                        frame = new MatrixFrame(Mat3.Identity, Crosshair.Position);
                    }
                    else
                    {
                        frame.origin = casterAgent.GetChestGlobalPosition();
                    }
                    return frame;
            }
        }

        private static SotorTarget AiBrainTarget(Agent casterAgent)
        {
            if (casterAgent == null || casterAgent.IsPlayerControlled) return null;
            var agent = casterAgent.GetComponent<AI.WizardAIComponent>()?.CurrentCastingBehavior?.CurrentTarget?.Agent;
            if (agent == null) return null;
            var target = new SotorTarget { Agent = agent };
            return target.IsValid ? target : null;
        }

        private bool TryCalculateAiCastFrame(Agent casterAgent, ref MatrixFrame frame)
        {
            var behavior = casterAgent.GetComponent<AI.WizardAIComponent>()?.CurrentCastingBehavior;
            var target = behavior?.CurrentTarget;
            if (target == null)
            {
                return false;
            }

            Vec3 aim = target.GetPositionPrioritizeCalculated();
            if (aim == Vec3.Invalid)
            {
                return false;
            }

            switch (Template.AbilityEffectType)
            {
                case AbilityEffectType.Missile:
                case AbilityEffectType.SeekerMissile:

                    frame = frame.Elevate(casterAgent.GetEyeGlobalHeight()).Advance(Template.Offset);
                    frame.rotation = Mat3.CreateMat3WithForward((aim - frame.origin).NormalizedCopy());
                    break;

                case AbilityEffectType.Blast:

                    frame = new MatrixFrame(frame.rotation, aim).Advance(-Template.Offset).Elevate(1f);
                    break;

                case AbilityEffectType.Wind:
                case AbilityEffectType.Vortex:

                    frame = new MatrixFrame(casterAgent.Frame.rotation, aim);
                    break;

                default:

                    frame = new MatrixFrame(Mat3.Identity, aim);
                    break;
            }

            if (Template.AbilityTargetType == AbilityTargetType.GroundAtPosition && Mission.Current?.Scene != null)
            {
                frame.origin.z = Mission.Current.Scene.GetGroundHeightAtPosition(frame.origin, (BodyFlags)544321929);
                if (Template.AbilityEffectType == AbilityEffectType.Bombardment)
                {

                    frame.origin.z += Template.Offset;
                }
            }

            return true;
        }

        private Agent ResolveExplicitTarget(Agent casterAgent, SotorTarget preferredTarget)
        {
            if (Template.AbilityTargetType == AbilityTargetType.Self)
            {
                return casterAgent;
            }
            if (Template.AbilityTargetType == AbilityTargetType.SingleAlly
                || Template.AbilityTargetType == AbilityTargetType.SingleEnemy)
            {
                if (preferredTarget != null && preferredTarget.IsValid)
                {
                    return preferredTarget.Agent;
                }
            }
            return null;
        }

        private SotorTarget FindNearestEnemyTarget(Agent casterAgent)
        {
            var mission = Mission.Current;
            if (mission == null || casterAgent?.Team == null)
            {
                return null;
            }

            float range = Template.MaxDistance > 0f ? Template.MaxDistance : 100f;
            var origin = casterAgent.GetEyeGlobalPosition();
            var lookDir = casterAgent.LookDirection;
            lookDir.z = 0f;
            lookDir = lookDir.NormalizedCopy();

            var nearby = new MBList<Agent>();
            nearby = mission.GetNearbyEnemyAgents(origin.AsVec2, range, casterAgent.Team, nearby);

            Agent best = null;
            float bestDist = float.MaxValue;
            foreach (var agent in nearby)
            {
                if (agent == null || !agent.IsActive() || agent.Health <= 0f || !agent.IsEnemyOf(casterAgent))
                {
                    continue;
                }

                var to = agent.CollisionCapsuleCenter - origin;
                var toFlat = to;
                toFlat.z = 0f;
                if (Vec3.DotProduct(lookDir, toFlat.NormalizedCopy()) <= 0f)
                {
                    continue;
                }

                float d = to.Length;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = agent;
                }
            }

            return best != null ? new SotorTarget { Agent = best } : null;
        }

        public void TickCastingState()
        {
        }
    }
}
