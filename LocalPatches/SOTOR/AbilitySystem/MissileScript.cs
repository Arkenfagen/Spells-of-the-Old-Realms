using System.Collections.Generic;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem
{

    public class MissileScript : AbilityScript
    {
        private const float MinimumSweepDistanceSquared = 0.0001f;

        private const int WorldBodyFlags = 79617;

        private float AgentSweepThickness =>
            Ability != null && Ability.Template.Radius > 0.4f ? Ability.Template.Radius : 0.4f;

        private const float PierceSweepThickness = 1.5f;

        private readonly HashSet<int> _piercedAgents = new HashSet<int>();

        private float _pierceTravelled;

        private const float HeadshotMultiplier = 2f;

        private const float HeadZoneBelowEye = 0.25f;

        private static string D(float d) => d >= float.MaxValue * 0.5f ? "none" : d.ToString("0.0");

        protected override void OnAfterTick(float dt)
        {
            if (!CanCollide || IsFading || Ability.Template.TriggerType != TriggerType.OnCollision)
            {
                return;
            }

            if (Ability.Template.Piercing)
            {
                PierceTick();
                return;
            }

            Vec3 from = LastFrameGlobalPosition;
            Vec3 to = CurrentGlobalPosition;
            Vec3 travel = to - from;
            if (travel.LengthSquared <= MinimumSweepDistanceSquared)
            {
                return;
            }

            Vec3 dir = travel.NormalizedCopy();
            float travelLen = travel.Length;

            int excludeIndex = (CasterAgent.Health <= 0f) ? -1 : CasterAgent.Index;
            Agent agentHit;
            float agentDist;
            using (new TWSharedMutexReadLock(Scene.PhysicsAndRayCastLock))
            {
                agentHit = Mission.Current.RayCastForClosestAgent(from, to, excludeIndex, AgentSweepThickness, out agentDist);
            }

            if (agentHit != null)
            {
                var mount = CasterAgent.MountAgent;
                if (mount != null && agentHit.Index == mount.Index)
                {
                    agentHit = null;
                    agentDist = float.MaxValue;
                }
            }
            if (agentHit == null)
            {
                agentDist = float.MaxValue;
            }

            float worldDist = float.MaxValue;
            Vec3 worldPos = default;
            bool worldHit;
            using (new TWSharedMutexReadLock(Scene.PhysicsAndRayCastLock))
            {
                worldHit = Mission.Current.Scene.RayCastForClosestEntityOrTerrain(
                    from, to, out float d, out Vec3 hp, out WeakGameEntity he, 0.01f, (BodyFlags)WorldBodyFlags);
                if (worldHit)
                {
                    worldDist = d;
                    worldPos = hp;
                }
            }

            float waterDist = float.MaxValue;
            Vec3 waterPos = default;
            bool waterHit = TryGetWaterCrossing(from, to, out waterPos, out waterDist);

            bool hasAgent = agentDist <= travelLen;
            bool hasWorld = worldHit && worldDist <= travelLen;
            bool hasWater = waterHit && waterDist <= travelLen;

            if (!hasAgent && !hasWorld && !hasWater)
            {
                return;
            }

            if (hasAgent && (!hasWorld || agentDist <= worldDist) && (!hasWater || agentDist <= waterDist))
            {
                Vec3 pos = from + dir * agentDist;
                SotorLog.Info($"MissileScript '{Ability.StringID}': agent hit '{agentHit.Name}' at {pos} (agentDist={D(agentDist)}, worldDist={D(worldDist)}, waterDist={D(waterDist)}).");
                AI.SotorAimDiagnostics.LogImpact(CasterAgent, Ability, pos, agentHit, "agent");
                HandleCollision(pos, -dir);
            }
            else if (hasWorld && (!hasWater || worldDist <= waterDist))
            {
                Vec3 pos = worldPos.IsValid && worldPos.IsNonZero ? worldPos : from + dir * worldDist;
                SotorLog.Info($"MissileScript '{Ability.StringID}': world/terrain hit at {pos} (worldDist={D(worldDist)}, agentDist={D(agentDist)}, waterDist={D(waterDist)}).");
                AI.SotorAimDiagnostics.LogImpact(CasterAgent, Ability, pos, null, "world/terrain");
                HandleCollision(pos, -dir);
            }
            else
            {
                SotorLog.Info($"MissileScript '{Ability.StringID}': water-surface hit at {waterPos} (waterDist={D(waterDist)}, agentDist={D(agentDist)}, worldDist={D(worldDist)}).");
                AI.SotorAimDiagnostics.LogImpact(CasterAgent, Ability, waterPos, null, "water");
                HandleCollision(waterPos, Vec3.Up);
            }
        }

        private void PierceTick()
        {
            Vec3 from = LastFrameGlobalPosition;
            Vec3 to = CurrentGlobalPosition;
            Vec3 travel = to - from;
            if (travel.LengthSquared <= MinimumSweepDistanceSquared)
            {
                return;
            }

            Vec3 dir = travel.NormalizedCopy();
            float travelLen = travel.Length;

            _pierceTravelled += travelLen;
            float maxRange = (Ability?.Template?.MaxDistance ?? 50f) + 10f;
            if (_pierceTravelled > maxRange)
            {
                Stop();
                return;
            }

            float worldDist = float.MaxValue;
            Vec3 worldPos = default;
            bool worldHit;
            using (new TWSharedMutexReadLock(Scene.PhysicsAndRayCastLock))
            {
                worldHit = Mission.Current.Scene.RayCastForClosestEntityOrTerrain(
                    from, to, out float d, out Vec3 hp, out WeakGameEntity he, 0.01f, (BodyFlags)WorldBodyFlags);
                if (worldHit)
                {
                    worldDist = d;
                    worldPos = hp;
                }
            }

            bool waterHit = TryGetWaterCrossing(from, to, out Vec3 waterPos, out float waterDist);

            float reach = travelLen;
            if (worldHit && worldDist < reach) reach = worldDist;
            if (waterHit && waterDist < reach) reach = waterDist;
            Vec3 hitTo = from + dir * reach;

            int found = 0;
            for (int i = 0; i < 32; i++)
            {
                Agent hit;
                float agentDist;
                int exclude = (CasterAgent != null && CasterAgent.Health > 0f) ? CasterAgent.Index : -1;
                using (new TWSharedMutexReadLock(Scene.PhysicsAndRayCastLock))
                {
                    hit = Mission.Current.RayCastForClosestAgent(from, hitTo, exclude, PierceSweepThickness, out agentDist);
                }
                if (hit == null || _piercedAgents.Contains(hit.Index))
                {

                    break;
                }
                if (hit == CasterAgent || hit.IsFriendOf(CasterAgent) || !hit.IsActive())
                {
                    break;
                }

                Vec3 hitPos = from + dir * MBMath.ClampFloat(agentDist, 0f, reach);
                TryPierceAgent(hit, hitPos, -dir);
                found++;
            }

            if (worldHit && worldDist <= travelLen && (!waterHit || worldDist <= waterDist))
            {
                Vec3 pos = worldPos.IsValid && worldPos.IsNonZero ? worldPos : from + dir * worldDist;
                SotorLog.Info($"MissileScript '{Ability.StringID}': pierce ended on world hit at {pos} (pierced total={_piercedAgents.Count}).");
                Stop();
            }

            else if (waterHit && waterDist <= travelLen)
            {
                SotorLog.Info($"MissileScript '{Ability.StringID}': pierce ended at water surface {waterPos} (pierced total={_piercedAgents.Count}).");
                Stop();
            }
        }

        public void TryPierceAgent(Agent agent, Vec3 hitPos, Vec3 normal)
        {
            if (agent == null || !agent.IsActive() || agent == CasterAgent || agent.IsFriendOf(CasterAgent))
            {
                return;
            }
            if (!_piercedAgents.Add(agent.Index))
            {
                return;
            }

            bool headshot = false;
            try
            {
                float eyeZ = agent.GetEyeGlobalPosition().Z;
                if (hitPos.Z >= eyeZ - HeadZoneBelowEye)
                {
                    headshot = true;
                }
            }
            catch {  }

            float mult = headshot ? HeadshotMultiplier : 1f;
            SotorLog.Info($"MissileScript '{Ability.StringID}': PIERCED '{agent.Name}'{(headshot ? " HEADSHOT x2" : "")} at {hitPos} (pierced so far={_piercedAgents.Count}).");
            TriggerEffectsOnAgent(agent, hitPos, normal, mult);
        }
    }
}
