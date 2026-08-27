using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem
{

    public class SotorSpellGoreMissionLogic : MissionLogic
    {

        public enum Outcome { None, Gib, Shatter }

        private const int PoolSize = 20;
        private const int GibPartsPerSet = 14;
        private const int BonePartsPerSet = 8;

        private static int _goreThisTick;

        private static int _gibCount, _shatterCount, _droppedCount;

        private static SotorSpellGoreMissionLogic _instance;

        private GameEntity[][] _gibPool;
        private GameEntity[][] _bonePool;
        private int _gibIndex;
        private int _boneIndex;
        private bool _poolsReady;

        private static readonly string[] GibParts =
        {
            "sotor_exploded_head_001",
            "sotor_exploded_arms_001",
            "sotor_exploded_arms_002",
            "sotor_exploded_legs_002",
            "sotor_exploded_legs_003",
            "sotor_exploded_flesh_pieces_001",
            "sotor_exploded_flesh_pieces_002",
            "sotor_exploded_flesh_pieces_003",
            "sotor_exploded_limb_pieces_001",
            "sotor_exploded_limb_pieces_002",
            "sotor_exploded_limb_pieces_003",
            "sotor_exploded_limb_pieces_001",
            "sotor_exploded_limb_pieces_002",
            "sotor_exploded_limb_pieces_003",
        };

        private static readonly string[] BoneParts =
        {
            "sotor_bone_skull",
            "sotor_bone_shoulder",
            "sotor_bone_shoulder",
            "sotor_bone_hands",
            "sotor_bone_hands",
            "sotor_bone_feets",
            "sotor_bone_feets",
            "sotor_bone_skull",
        };

        public override void AfterStart()
        {
            _instance = this;
            _goreThisTick = 0;

            if (!SotorSettings.EnableSpellGore) return;

            if (Mission.CombatType != Mission.MissionCombatType.Combat)
            {
                SotorLog.Debug($"Spell gore: skipped (combatType={(int)Mission.CombatType}, not a real fight).");
                return;
            }

            try
            {
                _gibPool = BuildPool(GibParts, GibPartsPerSet, swapSlotZeroFor: "sotor_exploded_torso_001");
                _bonePool = BuildPool(BoneParts, BonePartsPerSet, swapSlotZeroFor: null);
                _poolsReady = _gibPool != null || _bonePool != null;
                SotorLog.Info($"Spell gore: pools ready (gib={_gibPool != null}, bone={_bonePool != null}).");
            }
            catch (Exception ex)
            {
                _poolsReady = false;
                SotorLog.Warn($"Spell gore: pool construction failed, gore disabled this mission: {ex.Message}");
            }
        }

        private GameEntity[][] BuildPool(string[] parts, int perSet, string swapSlotZeroFor)
        {
            var scene = Mission.Current?.Scene;
            if (scene == null) return null;

            var pool = new GameEntity[PoolSize][];
            for (int i = 0; i < PoolSize; i++)
            {
                pool[i] = new GameEntity[perSet];
                for (int j = 0; j < perSet && j < parts.Length; j++)
                {
                    string prefab = (j == 0 && swapSlotZeroFor != null && i % 2 != 0)
                        ? swapSlotZeroFor
                        : parts[j];

                    try
                    {
                        pool[i][j] = InstantiateParked(scene, prefab);
                    }
                    catch (Exception ex)
                    {
                        pool[i][j] = null;
                        SotorLog.Warn($"Spell gore: pooling '{prefab}' (set {i}, slot {j}) failed: {ex.Message}");
                    }
                }
            }
            return pool;
        }

        private GameEntity InstantiateParked(Scene scene, string prefabName)
        {
            var entity = GameEntity.Instantiate(scene, prefabName, false, true, "");
            if (entity == null)
            {

                SotorLog.Warn($"Spell gore: prefab '{prefabName}' not found - check Prefabs/ and Assets/ deployed.");
                return null;
            }

            Vec3 parkPos;
            try
            {
                parkPos = new Vec3(
                    Mission.Current.GetFormationSpawnPosition(Mission.Current.PlayerTeam, FormationClass.Infantry),
                    1f, -1f);
            }
            catch
            {
                parkPos = Vec3.Zero;
            }
            parkPos += Vec3.Up * 50f;
            var identity = Mat3.Identity;
            var frame = new MatrixFrame(identity, parkPos);
            entity.SetGlobalFrame(frame, true);

            var shape = entity.GetBodyShape();
            if (shape == null)
            {
                SotorLog.Warn($"Spell gore: '{prefabName}' has no physics shape (hull not loaded) - "
                            + "skipping it rather than handing native a null. Check sotor_gib_shapes.tpac deployed.");
                entity.Remove(0);
                return null;
            }

            using (new TWSharedMutexWriteLock(Scene.PhysicsAndRayCastLock))
            {
                entity.AddPhysics(entity.Mass, entity.CenterOfMass, shape,
                    Vec3.Zero, Vec3.Zero, PhysicsMaterial.GetFromName("flesh"), false, -1);
            }
            entity.SetAlpha(0f);
            return entity;
        }

        public override void OnClearScene() => ClearPools();

        protected override void OnEndMission()
        {
            ClearPools();
            _instance = null;
        }

        private void ClearPools()
        {
            ReleasePool(_gibPool);
            ReleasePool(_bonePool);
            _gibPool = null;
            _bonePool = null;
            _poolsReady = false;
        }

        private static void ReleasePool(GameEntity[][] pool)
        {
            if (pool == null) return;
            foreach (var set in pool)
            {
                if (set == null) continue;
                for (int i = 0; i < set.Length; i++)
                {
                    set[i]?.Remove(0);
                    set[i] = null;
                }
            }
        }

        public static void TickGoreBudget() => _goreThisTick = 0;

        public static void ReportGore()
        {
            if (_gibCount == 0 && _shatterCount == 0 && _droppedCount == 0) return;
            SotorLog.Debug($"Spell gore: {_gibCount} gib, {_shatterCount} shatter, "
                         + $"{_droppedCount} dropped (limit {System.Math.Min(SotorSettings.SpellGoreAtOnce, SotorSettings.SpellDeathsAtOnce)}"
                         + $"/tick, gore={SotorSettings.SpellGoreAtOnce} deaths={SotorSettings.SpellDeathsAtOnce}).");
            _gibCount = _shatterCount = _droppedCount = 0;
        }

        public static Outcome Roll(Agent victim, Agent damager, int damage,
                                   AbilityEffectType effectType, int spellTier)
        {
            if (!SotorSettings.EnableSpellGore) return Outcome.None;
            if (victim == null || !victim.IsHuman) return Outcome.None;

            if (victim.IsMainAgent || victim.IsHero) return Outcome.None;

            float gib = GibBaseChance(effectType);
            if (gib <= 0f) return Outcome.None;

            float healthLimit = victim.HealthLimit > 1f ? victim.HealthLimit : 100f;
            float overkill = damage / healthLimit;
            if (overkill > 1f) overkill = 1f;
            if (overkill <= 0f) return Outcome.None;

            float tier = TierFactor(spellTier);

            bool skeleton = SkeletonVoice.IsSkeleton(victim);
            float scale = skeleton ? SotorSettings.SpellGoreShatterScale : SotorSettings.SpellGoreGibScale;

            float roll = gib * tier * overkill * scale;
            if (roll > 0f && MBRandom.RandomFloat < roll) return skeleton ? Outcome.Shatter : Outcome.Gib;

            return Outcome.None;
        }

        private static float GibBaseChance(AbilityEffectType t)
        {
            switch (t)
            {
                case AbilityEffectType.Bombardment:
                case AbilityEffectType.Blast:
                case AbilityEffectType.ArtilleryPlacement:
                    return 0.35f;
                case AbilityEffectType.Projectile:
                case AbilityEffectType.Missile:
                case AbilityEffectType.SeekerMissile:
                    return 0.15f;
                case AbilityEffectType.Wind:
                case AbilityEffectType.Vortex:

                    return 0.08f;
                default:
                    return 0f;
            }
        }

        private static float TierFactor(int tier)
        {
            switch (tier)
            {
                case 0: return 0.5f;
                case 1: return 0.75f;
                case 2: return 1.0f;
                case 3: return 1.25f;
                default: return 1.5f;
            }
        }

        public static bool Execute(Outcome outcome, Agent victim, Vec3 impactPosition, Vec3 blowDirection)
        {
            if (outcome == Outcome.None || victim == null) return false;
            var self = _instance;
            if (self == null || !self._poolsReady) return false;

            int limit = SotorSettings.SpellGoreAtOnce;
            int ceiling = SotorSettings.SpellDeathsAtOnce;
            if (limit > ceiling) limit = ceiling;
            if (_goreThisTick >= limit)
            {
                _droppedCount++;
                return false;
            }

            try
            {
                switch (outcome)
                {
                    case Outcome.Gib:
                        _gibCount++;
                        return self.Burst(victim, self._gibPool, ref self._gibIndex, bloody: true);
                    case Outcome.Shatter:
                        _shatterCount++;
                        return self.Burst(victim, self._bonePool, ref self._boneIndex, bloody: false);
                }
            }
            catch (Exception ex)
            {

                SotorLog.Warn($"Spell gore: {outcome} on '{victim?.Name}' failed: {ex.Message}");
            }
            return false;
        }

        private bool Burst(Agent victim, GameEntity[][] pool, ref int index, bool bloody)
        {
            if (pool == null) return false;

            var frame = victim.Frame;
            var spawnFrame = frame.Elevate(1f);

            if (bloody)
            {
                RunParticle(victim.GetChestGlobalPosition(), "blood_explosion");
                Mission.Current.MakeSound(
                    SoundEvent.GetEventIdFromString("sotor_blood_explosion"),
                    victim.Position, false, true, -1, -1);
            }
            else
            {

                SkeletonVoice.PlayRattle(victim);
            }

            victim.AgentVisuals?.SetVisible(false);
            MoveParts(pool, ref index, spawnFrame);
            _goreThisTick++;
            return true;
        }

        private void MoveParts(GameEntity[][] pool, ref int index, MatrixFrame frame)
        {
            if (index >= PoolSize) index = 0;
            var set = pool[index];
            index++;
            if (set == null) return;

            for (int i = 0; i < set.Length; i++)
            {
                var part = set[i];
                if (part == null) continue;

                var dir = RandomDirection(3f);
                bool live;
                using (new TWSharedMutexWriteLock(Scene.PhysicsAndRayCastLock))
                {
                    live = RestorePhysics(part);
                    if (live)
                    {
                        part.SetGlobalFrame(frame, true);
                        part.ApplyLocalImpulseToDynamicBody(Vec3.Up * -1f, dir * 25f);
                    }
                }

                part.SetAlpha(live ? 1f : 0f);
            }
        }

        private static bool RestorePhysics(GameEntity part)
        {
            if (part.HasDynamicRigidBody()) return true;
            var shape = part.GetBodyShape();
            if (part.HasPhysicsBody()) part.RemovePhysics(false);
            if (shape == null) return false;
            part.AddPhysics(part.Mass, part.CenterOfMass, shape, Vec3.Zero, Vec3.Zero,
                PhysicsMaterial.GetFromName("flesh"), false, -1);
            return part.HasDynamicRigidBody();
        }

        private static Vec3 RandomDirection(float deviation)
        {
            return new Vec3(
                MBRandom.RandomFloatRanged(-deviation, deviation),
                MBRandom.RandomFloatRanged(-deviation, deviation),
                1f, -1f);
        }

        private static void RunParticle(Vec3 position, string particleId)
        {
            var holder = GameEntity.CreateEmpty(Mission.Current.Scene, true, true, true);
            var identity = MatrixFrame.Identity;

            ParticleSystem.CreateParticleSystemAttachedToEntity(particleId, holder, ref identity);
            var rot = Mat3.CreateMat3WithForward(Vec3.Zero);
            var frame = new MatrixFrame(rot, position);
            holder.SetGlobalFrame(frame, true);
        }
    }
}
