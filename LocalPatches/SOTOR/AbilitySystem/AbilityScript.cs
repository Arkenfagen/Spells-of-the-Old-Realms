using System;
using System.Collections.Generic;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem
{

    public abstract class AbilityScript : ScriptComponentBehavior
    {
        private Ability _ability;
        private Agent _casterAgent;
        private float _abilityLife = -1f;
        private float _timeSinceLastTick;
        private bool _lifeTimeExpired;
        private bool _hasTickedOnce;

        private bool _pendingDetonation;
        private Vec3 _pendingPosition;
        private Vec3 _pendingNormal;
        private bool _hasTriggered;
        private bool _hasCollided;
        private bool _canCollide;
        private readonly float _minArmingTimeForCollision = 0.1f;
        private Vec3 _previousFrameOrigin = Vec3.Zero;
        private GameEntity _entity;
        private SeekerController _controller;
        private MBList<Agent> _targetAgents;

        private SoundEvent _sound;
        private int _soundIndex = -1;
        private bool _soundStarted;

        public Agent CasterAgent => _casterAgent;
        public Ability Ability => _ability;
        public bool IsFading { get; private set; }
        public bool HasTickedOnce => _hasTickedOnce;
        protected bool CanCollide => _canCollide;

        public Vec3 CurrentGlobalPosition => GameEntity.GetGlobalFrame().origin;
        public Vec3 LastFrameGlobalPosition => _previousFrameOrigin;

        public void SetCasterAgent(Agent agent) => _casterAgent = agent;

        public void SetTargetSeeking(SotorTarget target, SeekerParameters parameters)
        {
            _controller = new SeekerController(target, parameters);
            SotorLog.Debug($"AbilityScript: seeking target '{target?.Agent?.Name}'.");
        }

        public void SetExplicitTargetAgents(MBList<Agent> agents)
        {
            _targetAgents = agents;
        }

        public virtual void Initialize(Ability ability, ref GameEntity entity)
        {
            _ability = ability;
            _entity = entity;
            SotorLog.Debug($"AbilityScript.Initialize for '{ability?.StringID}'.");
            InitializeSound();
        }

        private void InitializeSound()
        {
            var id = _ability?.Template?.SoundEffectToPlay?.Trim();
            if (string.IsNullOrEmpty(id) || id.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                _soundIndex = SoundEvent.GetEventIdFromString(id);
                if (_soundIndex < 0)
                {
                    SotorLog.Debug($"AbilityScript sound '{id}': not registered (eventId<0) for '{_ability.StringID}'.");
                    return;
                }

                _sound = SoundEvent.CreateEvent(_soundIndex, Scene);
                if (_sound == null || !_sound.IsValid)
                {
                    _sound = null;
                    _soundIndex = -1;
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"AbilityScript.InitializeSound('{id}') failed: {ex.Message}");
                _sound = null;
                _soundIndex = -1;
            }
        }

        private void UpdateSound(Vec3 position)
        {
            if (_sound == null)
            {
                return;
            }

            if (!_sound.IsValid)
            {
                if (_soundIndex < 0)
                {
                    _sound = null;
                    return;
                }
                _sound = SoundEvent.CreateEvent(_soundIndex, Scene);
                if (_sound == null || !_sound.IsValid)
                {
                    _sound = null;
                    _soundIndex = -1;
                    return;
                }
                _soundStarted = false;
            }

            _sound.SetPosition(position);

            if (_sound.IsPlaying())
            {
                return;
            }

            if (!_soundStarted)
            {
                _sound.Play();
                _soundStarted = true;
            }
            else if (_ability != null && _ability.Template.ShouldSoundLoopOverDuration)
            {
                _sound.Play();
            }
            else
            {
                _sound.Release();
                _sound = null;
            }
        }

        private void StopOrReleaseSpellSound()
        {
            if (_sound == null)
            {
                return;
            }
            try
            {
                if (_sound.IsValid)
                {
                    if (_sound.IsPlaying())
                    {
                        _sound.Stop();
                    }
                    else
                    {
                        _sound.Release();
                    }
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"AbilityScript.StopOrReleaseSpellSound failed: {ex.Message}");
            }
            _sound = null;
        }

        protected override void OnInit()
        {
            SetScriptComponentToTick(GetTickRequirement());
        }

        public override TickRequirement GetTickRequirement()
        {
            return TickRequirement.Tick;
        }

        protected virtual void OnBeforeTick(float dt) { }
        protected virtual void OnAfterTick(float dt) { }

        protected virtual bool ShouldMove()
        {
            if (_ability == null) return false;
            var t = _ability.Template.AbilityEffectType;
            return t == AbilityEffectType.Missile || t == AbilityEffectType.SeekerMissile
                || t == AbilityEffectType.Vortex || t == AbilityEffectType.Wind;
        }

        protected virtual MatrixFrame GetNextGlobalFrame(MatrixFrame oldFrame, float dt)
        {
            return oldFrame.Advance(_ability.Template.BaseMovementSpeed * dt);
        }

        protected override void OnTick(float dt)
        {
            if (_ability == null)
            {
                return;
            }

            if (!GameEntity.IsValid)
            {
                return;
            }

            if (Mission.Current == null || Mission.Current.MissionEnded)
            {
                Stop();
                return;
            }

            if (_pendingDetonation && _hasTickedOnce)
            {
                _pendingDetonation = false;
                HandleCollision(_pendingPosition, _pendingNormal);
                return;
            }

            OnBeforeTick(dt);
            _timeSinceLastTick += dt;
            UpdateLifeTime(dt);
            if (IsFading)
            {
                return;
            }

            var frame = GameEntity.GetGlobalFrame();

            if (_controller != null)
            {
                frame = _controller.CalculateRotatedFrame(frame, dt);
            }

            var template = _ability.Template;

            if (template.TriggerType == TriggerType.OnCollision && !template.Piercing && CollidedWithAgent())
            {
                HandleCollision(frame.origin, frame.origin.NormalizedCopy());
                return;
            }
            if (template.TriggerType == TriggerType.EveryTick && !_hasTriggered)
            {
                TriggerEffects(frame.origin, frame.origin.NormalizedCopy());
            }
            else if (template.TriggerType == TriggerType.EveryTick && _timeSinceLastTick > template.TickInterval)
            {
                _timeSinceLastTick = 0f;
                TriggerEffects(frame.origin, frame.origin.NormalizedCopy());
            }
            else if (template.TriggerType == TriggerType.TickOnce && _abilityLife > template.TickInterval && !_hasTriggered)
            {
                TriggerEffects(frame.origin, frame.origin.NormalizedCopy());
            }

            _hasTickedOnce = true;

            _previousFrameOrigin = frame.origin;

            if (ShouldMove())
            {
                var next = GetNextGlobalFrame(frame, dt);
                var ge = GameEntity;
                ge.SetGlobalFrame(next, true);

                try
                {
                    using (new TWSharedMutexWriteLock(Scene.PhysicsAndRayCastLock))
                    {
                        var shape = GameEntityPhysicsExtensions.GetBodyShape(GameEntity);
                        if (shape != null)
                        {
                            shape.ManualInvalidate();
                        }
                    }
                }
                catch {  }
            }

            UpdateSound(GameEntity.GetGlobalFrame().origin);

            OnAfterTick(dt);

            if (_lifeTimeExpired)
            {
                Stop();
            }
        }

        protected virtual bool CollidedWithAgent()
        {
            if (!_canCollide || _ability == null)
            {
                return false;
            }

            float collisionRadius = _ability.Template.Radius + 1f;
            var origin = GameEntity.GetGlobalFrame().origin;
            var nearby = Mission.Current.GetNearbyAgents(origin.AsVec2, collisionRadius, new MBList<Agent>());
            foreach (var agent in nearby)
            {
                if (agent == null || agent == _casterAgent)
                {
                    continue;
                }
                if (Math.Abs(origin.Z - agent.Position.Z) < collisionRadius)
                {
                    return true;
                }
            }
            return false;
        }

        protected override bool MovesEntity()
        {
            return true;
        }

#if BL13
        protected override void OnPhysicsCollision(ref PhysicsContact contact, WeakGameEntity entity0, WeakGameEntity entity1, bool isFirstShape)
#else
        protected override void OnPhysicsCollision(ref PhysicsContact contact, WeakGameEntity entity0, WeakGameEntity entity1)
#endif
        {
            if (_ability == null || _ability.Template.TriggerType != TriggerType.OnCollision || !_canCollide)
            {
                return;
            }

            var c = contact.ContactPair0.Contact0;

            if (_ability.Template.Piercing && this is MissileScript pierceScript)
            {
                Agent contacted = null;
                try
                {
                    Vec3 a = c.Position - c.Normal * 0.5f;
                    Vec3 b = c.Position + c.Normal * 0.5f;
                    int exclude = (_casterAgent != null && _casterAgent.Health > 0f) ? _casterAgent.Index : -1;
                    using (new TWSharedMutexReadLock(Scene.PhysicsAndRayCastLock))
                    {
                        contacted = Mission.Current.RayCastForClosestAgent(a, b, exclude, 1.0f, out _);
                    }
                }
                catch { }

                if (contacted != null)
                {
                    SotorLog.Info($"OnPhysicsCollision (pierce) '{_ability.StringID}' hit agent '{contacted.Name}' at {c.Position}.");
                    pierceScript.TryPierceAgent(contacted, c.Position, c.Normal);
                }
                else
                {

                    SotorLog.Info($"OnPhysicsCollision (pierce) '{_ability.StringID}' hit WORLD at {c.Position}; stopping.");
                    Stop();
                }
                return;
            }

            SotorLog.Debug($"OnPhysicsCollision '{_ability.StringID}' at {c.Position} (detonation queued for next tick).");
            _pendingDetonation = true;
            _pendingPosition = c.Position;
            _pendingNormal = c.Normal;
            _canCollide = false;
        }

        protected virtual void HandleCollision(Vec3 position, Vec3 normal)
        {
            if (_hasTickedOnce && !_hasCollided && position.IsValid && position.IsNonZero)
            {

                AI.SotorAimDiagnostics.LogImpact(CasterAgent, _ability, position, null, "detonated");

                TriggerEffects(position, normal);
                _hasCollided = true;
                Stop();
            }
        }

        protected bool TryGetWaterCrossing(Vec3 from, Vec3 to, out Vec3 crossPoint, out float crossDist)
        {
            crossPoint = default;
            crossDist = float.MaxValue;

            float waterLevel;
            using (new TWSharedMutexReadLock(Scene.PhysicsAndRayCastLock))
            {
                waterLevel = Mission.Current.Scene.GetWaterLevelAtPosition(from.AsVec2, true, true);
            }

            if (!(from.z > waterLevel) || !(to.z <= waterLevel))
            {
                return false;
            }

            float dz = from.z - to.z;
            if (dz <= 1e-4f)
            {
                return false;
            }

            float t = (from.z - waterLevel) / dz;
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;

            crossPoint = from + (to - from) * t;
            crossPoint.z = waterLevel;
            crossDist = (crossPoint - from).Length;
            return true;
        }

        protected void TriggerEffectsOnAgent(Agent agent, Vec3 position, Vec3 normal, float damageMultiplier = 1f)
        {
            if (agent == null)
            {
                return;
            }
            var one = new MBList<Agent> { agent };
            foreach (var effect in GetEffectsToTrigger())
            {
                effect?.Trigger(position, normal, _casterAgent, one, damageMultiplier);
            }
        }

        private void TriggerEffects(Vec3 position, Vec3 normal)
        {

            var targets = (_targetAgents != null && _targetAgents.Count > 0) ? _targetAgents : null;
            foreach (var effect in GetEffectsToTrigger())
            {
                effect?.Trigger(position, normal, _casterAgent, targets);
            }
            _hasTriggered = true;
        }

        protected virtual List<TriggeredEffect> GetEffectsToTrigger()
        {
            var list = new List<TriggeredEffect>();
            if (_ability == null)
            {
                return list;
            }

            var template = TriggeredEffectManager.GetTemplate(_ability.Template.TriggeredEffectID);
            if (template != null)
            {
                var tt = _ability.Template.AbilityTargetType;
                list.Add(new TriggeredEffect(template)
                {
                    OwnerIsSingleTarget = tt == AbilityTargetType.SingleEnemy
                        || tt == AbilityTargetType.SingleAlly
                        || tt == AbilityTargetType.Self,
                    OwnerSpellName = _ability.Template.Name,

                    OwnerShipTag = _ability.Template.ShipTag,
                    OwnerSpellTier = _ability.Template.SpellTier,

                    OwnerEffectType = _ability.Template.AbilityEffectType,
                });
            }
            else if (!string.IsNullOrEmpty(_ability.Template.TriggeredEffectID))
            {
                SotorLog.Warn($"AbilityScript: TriggeredEffectID '{_ability.Template.TriggeredEffectID}' not found.");
            }
            return list;
        }

        private void UpdateLifeTime(float dt)
        {
            if (_abilityLife < 0f)
            {
                _abilityLife = 0f;
            }
            else
            {
                _abilityLife += dt;
            }

            if (_ability != null && _abilityLife > _ability.Template.Duration && !IsFading)
            {
                _lifeTimeExpired = true;

                AI.SotorAimDiagnostics.LogImpact(CasterAgent, _ability, CurrentGlobalPosition, null, "EXPIRED, hit nothing");
            }

            float armTime = (_ability != null && _ability.Template.Piercing) ? 0.001f : _minArmingTimeForCollision;
            if (_abilityLife > armTime)
            {
                _canCollide = true;
            }
        }

        public void Stop()
        {
            if (IsFading)
            {
                return;
            }

            IsFading = true;
            if (_entity != null)
            {
                try
                {
                    GameEntityExtensions.FadeOut(_entity, 0.05f, true);
                }
                catch (Exception ex)
                {
                    SotorLog.Warn($"AbilityScript.Stop: FadeOut failed ({ex.GetType().Name}); entity may already be removed.");
                }
            }
        }

        protected override void OnRemoved(int removeReason)
        {
            StopOrReleaseSpellSound();
            _ability = null;
            _entity = null;
            _casterAgent = null;
        }
    }
}
