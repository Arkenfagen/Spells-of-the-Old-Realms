using System;
using NavalDLC.Missions;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.NavalPhysics;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.ShipActuators;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Objects.Usables;

namespace SOTOR.AbilitySystem
{

    internal static class SotorNavalBridge
    {

        public const float ImpactHullMult = 50f;
        public const float FlamingFireMult = 40f;

        public const float FlamingHullMult = 5f;
        public const float EnergyHullMult = 8f;
        public const float EnergyFireMult = 25f;

        public const float ImpactSailMult = 25f;
        public const float FlamingSailMult = 22f;
        public const float EnergySailMult = 12f;
        public static float SailMult(string tag)
        {
            switch (tag)
            {
                case "impact": return ImpactSailMult;
                case "flaming": return FlamingSailMult;
                case "energy": return EnergySailMult;
                default: return 0f;
            }
        }

        public static float GetTierWeight(int tier)
        {
            switch (tier)
            {
                case 1: return 0.6f;
                case 2: return 0.8f;
                case 4: return 1.2f;
                default: return 1.0f;
            }
        }

        public static bool IsNavalMission(Mission mission)
        {
            return mission != null && mission.IsNavalBattle;
        }

        private static readonly System.Collections.Generic.List<MissionShip> _ablazeScratch = new System.Collections.Generic.List<MissionShip>(8);

        public static void CollectAgentsOnAblazeDecks(Mission mission, System.Collections.Generic.List<int> outAgentIndices)
        {
            if (outAgentIndices == null)
            {
                return;
            }
            try
            {
                if (!IsNavalMission(mission))
                {
                    return;
                }
                var shipsLogic = mission.GetMissionBehavior<NavalShipsLogic>();
                if (shipsLogic == null)
                {
                    return;
                }

                var allShips = shipsLogic.AllShips;
                if (allShips == null)
                {
                    return;
                }

                _ablazeScratch.Clear();
                for (int s = 0; s < allShips.Count; s++)
                {
                    var ship = allShips[s];
                    if (ship != null && !ship.IsSinking && ship.FireHitPoints <= 0f)
                    {
                        _ablazeScratch.Add(ship);
                    }
                }
                if (_ablazeScratch.Count == 0)
                {
                    return;
                }

                var agents = mission.Agents;
                for (int i = 0; i < agents.Count; i++)
                {
                    Agent a = agents[i];
                    if (a == null || !a.IsActive() || a.Health < 1f || !a.IsHuman)
                    {
                        continue;
                    }
                    if (a.IsInWater())
                    {
                        continue;
                    }
                    for (int s = 0; s < _ablazeScratch.Count; s++)
                    {
                        if (_ablazeScratch[s].GetIsAgentOnShip(a, true))
                        {
                            outAgentIndices.Add(a.Index);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorNavalBridge.CollectAgentsOnAblazeDecks failed ({ex.GetType().Name}): {ex.Message}");
            }
        }

        public const int EscapeNone = 0;
        public const int EscapeSwimEnemy = 1;
        public const int EscapeSwimFriendly = 2;
        public const int EscapeRunEnemy = 3;
        public const int EscapeRunFriendly = 4;

        private const float MaxSwimDistance = 90f;
        private const float MaxSwimDistanceSq = MaxSwimDistance * MaxSwimDistance;

        private const float FriendlyBiasMult = 0.6f;

        public static bool TryGetEscapeTarget(Agent agent, out Vec3 deckPoint, out int targetShipIndex,
                                              out int quality, out bool needsSwim, out Vec3 waterEntryPoint,
                                              bool alreadyCommitted = false)
        {
            deckPoint = Vec3.Zero;
            targetShipIndex = -1;
            quality = EscapeNone;
            needsSwim = false;
            waterEntryPoint = Vec3.Zero;
            try
            {
                if (agent == null || !agent.IsActive())
                {
                    return false;
                }
                var comp = agent.GetComponent<AgentNavalComponent>();
                MissionShip myShip = comp?.SteppedShip;
                Team myTeam = agent.Team;

                if (!alreadyCommitted && myShip == null && !agent.IsInWater())
                {
                    return false;
                }

                if (myShip != null && !IsShipDoomed(myShip))
                {
                    return false;
                }

                if (myShip != null && !ShipTeamMatches(myShip, myTeam))
                {
                    return false;
                }

                MissionShip best = null;
                int bestQuality = EscapeNone;

                if (myShip != null)
                {
                    var connected = myShip.GetShipsConnectedWithBridges();
                    if (connected != null)
                    {
                        for (int i = 0; i < connected.Count; i++)
                        {
                            var s = connected[i];
                            if (s == null || IsShipDoomed(s))
                            {
                                continue;
                            }
                            int q = ShipTeamMatches(s, myTeam) ? EscapeRunFriendly : EscapeRunEnemy;
                            if (q > bestQuality)
                            {
                                bestQuality = q;
                                best = s;
                            }
                        }
                    }
                }

                if (bestQuality < EscapeRunEnemy)
                {
                    var shipsLogic = Mission.Current?.GetMissionBehavior<NavalShipsLogic>();
                    var allShips = shipsLogic?.AllShips;
                    Vec3 from = agent.Position;

                    float bestEffDistSq = MaxSwimDistanceSq;
                    if (allShips != null)
                    {
                        for (int i = 0; i < allShips.Count; i++)
                        {
                            var s = allShips[i];
                            if (s == null || s == myShip || IsShipDoomed(s))
                            {
                                continue;
                            }

                            if (!ShipHasUsableNet(s))
                            {
                                continue;
                            }
                            float dsq = s.GlobalFrame.origin.DistanceSquared(from);
                            if (dsq > MaxSwimDistanceSq)
                            {
                                continue;
                            }
                            bool friendly = ShipTeamMatches(s, myTeam);
                            float effDsq = friendly ? dsq * (FriendlyBiasMult * FriendlyBiasMult) : dsq;
                            if (effDsq < bestEffDistSq)
                            {
                                bestEffDistSq = effDsq;
                                bestQuality = friendly ? EscapeSwimFriendly : EscapeSwimEnemy;
                                best = s;
                                needsSwim = true;
                            }
                        }
                    }
                }

                if (best == null)
                {
                    return false;
                }

                best.GetWorldPositionOnDeck(out var wp);
                Vec3 pt = wp.GetNavMeshVec3();
                if (!pt.IsValid || !pt.IsNonZero)
                {
                    return false;
                }
                deckPoint = pt;
                targetShipIndex = best.Index;
                quality = bestQuality;

                if (bestQuality >= EscapeRunEnemy)
                {
                    needsSwim = false;
                }
                else if (needsSwim)
                {

                    if (!TryGetWaterEntryPoint(agent, myShip, pt, out waterEntryPoint))
                    {
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorNavalBridge.TryGetEscapeTarget failed ({ex.GetType().Name}): {ex.Message}");
                return false;
            }
        }

        public static bool TryBoardViaClimbingNet(Agent agent, int targetShipIndex)
        {
            try
            {
                if (agent == null || !agent.IsActive())
                {
                    return false;
                }
                var shipsLogic = Mission.Current?.GetMissionBehavior<NavalShipsLogic>();
                if (shipsLogic == null || !shipsLogic.GetShipWithShipIndex(targetShipIndex, out MissionShip ship) || ship == null)
                {
                    return false;
                }
                var nets = ship.ClimbingMachines;
                if (nets == null || nets.Count == 0)
                {
                    return false;
                }

                Vec3 me = agent.Position;

                ClimbingMachine bestNet = null;
                StandingPoint bestSp = null;
                bool bestFree = false;
                float bestDistSq = float.MaxValue;
                for (int i = 0; i < nets.Count; i++)
                {
                    var net = nets[i];
                    var sp = (net as UsableMachine)?.PilotStandingPoint;
                    if (sp == null)
                    {
                        continue;
                    }
                    Vec3 spPos = ((ScriptComponentBehavior)sp).GameEntity.GlobalPosition;
                    float dsq = spPos.DistanceSquared(me);

                    bool free = !sp.HasUser || sp.UserAgent == agent;

                    if (bestSp == null || (free && !bestFree) || (free == bestFree && dsq < bestDistSq))
                    {
                        bestNet = net; bestSp = sp; bestFree = free; bestDistSq = dsq;
                    }
                }
                if (bestNet == null || bestSp == null)
                {
                    return false;
                }

                Vec3 basePos = ((ScriptComponentBehavior)bestSp).GameEntity.GlobalPosition;
                if (bestFree && basePos.DistanceSquared(me) < 6.25f)
                {
                    if (!agent.IsUsingGameObject)
                    {
                        agent.UseGameObject(bestSp, -1);
                    }
                }
                else
                {

                    agent.SetTargetPosition(basePos.AsVec2);
                }
                return true;
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorNavalBridge.TryBoardViaClimbingNet failed ({ex.GetType().Name}): {ex.Message}");
                return false;
            }
        }

        private static ActionIndexCache? _escapeJumpAnim;
        private static bool _escapeJumpResolved;

        public static bool MakeAgentJumpOverboard(Agent agent, Vec3 towardPoint)
        {
            try
            {
                if (agent == null || !agent.IsActive())
                {
                    return false;
                }
                var comp = agent.GetComponent<AgentNavalComponent>();
                if (comp == null)
                {
                    return false;
                }

                if (comp.SteppedShip == null)
                {
                    return false;
                }

                Vec3 dir = towardPoint - agent.Position;
                dir.z = 0f;
                if (dir.LengthSquared < 0.01f)
                {
                    dir = agent.LookDirection;
                    dir.z = 0f;
                }
                dir = dir.NormalizedCopy();

                if (agent.IsUsingGameObject)
                {
                    agent.StopUsingGameObject(true);
                }

#if BL13
                comp.SetupAgentToJumpOffABurningShip();
#else
                comp.SetupAgentToAbandonShip();
#endif

                if (!_escapeJumpResolved)
                {
                    _escapeJumpResolved = true;
                    try { _escapeJumpAnim = ActionIndexCache.Create("act_escape_jump"); } catch { _escapeJumpAnim = null; }
                }
                if (_escapeJumpAnim.HasValue && _escapeJumpAnim.Value.Index != ActionIndexCache.act_none.Index)
                {
                    var jump = _escapeJumpAnim.Value;
                    agent.SetActionChannel(0, jump, false, 0UL, 0f, 1f, -0.2f, 0.4f, 0f, false, -0.2f, 0, true);
                }

                Vec3 jumpTo = agent.Position + dir * 10f;
                Vec2 xy = jumpTo.AsVec2;
                agent.SetTargetPositionAndDirection(in xy, in dir);
                agent.ClearTargetFrame();
                return true;
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorNavalBridge.MakeAgentJumpOverboard failed ({ex.GetType().Name}): {ex.Message}");
                return false;
            }
        }

        private static bool TryGetWaterEntryPoint(Agent agent, MissionShip myShip, Vec3 targetDeck, out Vec3 water)
        {
            water = Vec3.Zero;
            try
            {
                Vec3 from = agent.Position;
                Vec2 dir = (targetDeck.AsVec2 - from.AsVec2);
                if (dir.Length < 0.01f)
                {
                    return false;
                }
                dir = dir.Normalized();

                var scene = Mission.Current?.Scene;
                if (scene == null)
                {
                    return false;
                }

                for (float dist = 6f; dist <= 22f; dist += 4f)
                {
                    Vec2 p2 = from.AsVec2 + dir * dist;
                    float waterZ = scene.GetWaterLevelAtPosition(p2, true, true);
                    float groundZ = scene.GetGroundHeightAtPosition(new Vec3(p2.x, p2.y, waterZ), (BodyFlags)544321929);

                    if (waterZ >= groundZ - 0.25f)
                    {
                        water = new Vec3(p2.x, p2.y, waterZ);
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public static int GetShipClimbingNetCount(int shipIndex)
        {
            try
            {
                var shipsLogic = Mission.Current?.GetMissionBehavior<NavalShipsLogic>();
                if (shipsLogic == null || !shipsLogic.GetShipWithShipIndex(shipIndex, out MissionShip ship) || ship == null)
                {
                    return -1;
                }
                return ship.ClimbingMachines?.Count ?? 0;
            }
            catch { return -1; }
        }

        public static bool IsShipIndexDoomed(int shipIndex)
        {
            try
            {
                var shipsLogic = Mission.Current?.GetMissionBehavior<NavalShipsLogic>();
                if (shipsLogic == null || !shipsLogic.GetShipWithShipIndex(shipIndex, out MissionShip ship) || ship == null)
                {
                    return true;
                }
                return IsShipDoomed(ship);
            }
            catch { return true; }
        }

        private static bool ShipHasUsableNet(MissionShip ship)
        {
            var nets = ship?.ClimbingMachines;
            return nets != null && nets.Count > 0;
        }

        private static bool IsShipDoomed(MissionShip ship)
        {
            if (ship == null) return false;

#if BL13
            return ship.IsSinking || ship.FireHitPoints <= 0f;
#else
            return ship.IsSinking || ship.BeingAbandoned || ship.FireHitPoints <= 0f;
#endif
        }

        private static bool ShipTeamMatches(MissionShip ship, Team team)
        {
            if (ship == null || team == null)
            {
                return false;
            }
            var st = ship.Team;
            return st != null && st == team;
        }

        public static bool IsAgentShipDoomed(Agent agent)
        {
            try
            {
                var comp = agent?.GetComponent<AgentNavalComponent>();
                MissionShip deck = comp?.SteppedShip;
                if (!IsShipDoomed(deck))
                {
                    return false;
                }

                return ShipTeamMatches(deck, agent.Team);
            }
            catch
            {
                return false;
            }
        }

        public static bool IsAgentOnSafeShip(Agent agent)
        {
            try
            {
                if (agent == null || !agent.IsActive()) return false;

                var comp = agent.GetComponent<AgentNavalComponent>();
                var stepped = comp?.SteppedShip;
                if (stepped != null && !IsShipDoomed(stepped)) return true;

                if (agent.IsInWater()) return false;
                if (agent.IsUsingGameObject) return false;

                var comp2 = agent.GetComponent<AgentNavalComponent>();
                var landedDeck = comp2?.SteppedShip;
                if (landedDeck == null || IsShipDoomed(landedDeck)) return false;
                var shipsLogic = Mission.Current?.GetMissionBehavior<NavalShipsLogic>();
                var allShips = shipsLogic?.AllShips;
                if (allShips != null)
                {
                    for (int i = 0; i < allShips.Count; i++)
                    {
                        var s = allShips[i];
                        if (s == null || IsShipDoomed(s)) continue;
                        if (s.GetIsAgentOnShip(agent, true)) return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public static void ApplyShipDamage(
            Mission mission, Agent caster, Vec3 position, string shipTag,
            float scaledBase, int nearbyAgentHintIndex, DamageType logElement, string spellName)
        {
            try
            {
                if (!IsNavalMission(mission) || string.IsNullOrEmpty(shipTag))
                {
                    return;
                }

                var shipsLogic = mission.GetMissionBehavior<NavalShipsLogic>();
                if (shipsLogic == null)
                {
                    return;
                }

                MissionShip ship = ResolveShip(mission, shipsLogic, position, caster, nearbyAgentHintIndex);
                if (ship == null)
                {

                    return;
                }

                if (ship.IsSinking)
                {
                    return;
                }

                bool wasAblaze = ship.FireHitPoints <= 0f;

                float hullMult = 0f, fireMult = 0f;
                switch (shipTag.ToLowerInvariant())
                {
                    case "impact":
                        hullMult = ImpactHullMult;
                        break;
                    case "flaming":
                        hullMult = FlamingHullMult;
                        fireMult = FlamingFireMult;
                        break;
                    case "energy":
                        hullMult = EnergyHullMult;
                        fireMult = EnergyFireMult;
                        break;
                    default:
                        return;
                }

                if (hullMult > 0f)
                {
                    float hullDmg = scaledBase * hullMult;
                    if (hullDmg > 0f)
                    {
                        ship.DealCollisionDamage(null, false, position, hullDmg);

                        bool sank = ship.HitPoints <= 0f || ship.IsSinking;
                        if (sank && !ship.IsSinking)
                        {
                            ship.SetSinkingState(NavalPhysics.SinkingState.Sinking);
                        }
                        if (sank)
                        {

                            SotorSpellDamageLog.BookShipEvent(caster, logElement, spellName, "sends the ship to the depths!");
                            return;
                        }
                        SotorSpellDamageLog.BookShipEvent(caster, logElement, spellName, "smashes into the hull");
                    }
                }

                if (fireMult > 0f)
                {
                    float fireDmg = scaledBase * fireMult;
                    if (fireDmg > 0f)
                    {
                        ship.DealFireDamage(fireDmg);
                        bool nowAblaze = ship.FireHitPoints <= 0f;

                        if (nowAblaze && !wasAblaze)
                        {
                            SotorSpellDamageLog.BookShipEvent(caster, logElement, spellName, "sets the ship ablaze!");
                        }
                        else if (!nowAblaze)
                        {
                            SotorSpellDamageLog.BookShipEvent(caster, logElement, spellName, "scorches the ship");
                        }

                        TryDriveVisualFire(ship, position, fireDmg > 40f, nowAblaze);
                    }
                }

                float sailDmg = scaledBase * SailMult(shipTag);
                if (sailDmg > 0f)
                {
                    TryDamageNearestSail(ship, caster, position, sailDmg);
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorNavalBridge.ApplyShipDamage failed ({ex.GetType().Name}): {ex.Message}");
            }
        }

        private const float FootprintEdgeMargin = 1.15f;
        private const float DeckEdgeAgentDistSq = 9f;

        private static MissionShip ResolveShip(
            Mission mission, NavalShipsLogic shipsLogic, Vec3 position, Agent caster, int nearbyAgentHintIndex)
        {
            Vec2 xy = position.AsVec2;

            MissionShip onDeck = null;
            float bestCentreDistSq = float.MaxValue;
            var allShips = shipsLogic.AllShips;
            if (allShips != null)
            {
                for (int i = 0; i < allShips.Count; i++)
                {
                    MissionShip s = allShips[i];
                    if (s == null || s.IsSinking)
                    {
                        continue;
                    }
                    bool inside = PointOverShip(s, xy);
                    if (!inside)
                    {
                        continue;
                    }
                    float dsq = s.GlobalFrame.origin.AsVec2.DistanceSquared(xy);
                    if (dsq < bestCentreDistSq)
                    {
                        bestCentreDistSq = dsq;
                        onDeck = s;
                    }
                }
            }
            if (onDeck != null)
            {
                return onDeck;
            }

            if (nearbyAgentHintIndex >= 0)
            {
                Agent hinted = mission.FindAgentWithIndex(nearbyAgentHintIndex);
                if (hinted != null && hinted.IsActive()
                    && hinted.Position.AsVec2.DistanceSquared(xy) <= DeckEdgeAgentDistSq)
                {
                    MissionShip s = ShipUnderAgent(hinted);
                    if (s != null && !s.IsSinking)
                    {
                        return s;
                    }
                }
            }

            return null;
        }

        private static bool PointOverShip(MissionShip ship, Vec2 xy)
        {
            try
            {
                MatrixFrame frame = ship.GlobalFrame;
                Vec2[] quad = ship.CalculateBoundingXYGlobalPlaneFromLocal(in frame);
                if (quad == null || quad.Length < 4)
                {
                    return false;
                }
                if (MBMath.CheckPointInsidePolygon(in quad[0], in quad[1], in quad[2], in quad[3], in xy))
                {
                    return true;
                }

                if (FootprintEdgeMargin > 1f)
                {
                    Vec2 c = (quad[0] + quad[1] + quad[2] + quad[3]) * 0.25f;
                    Vec2 g0 = c + (quad[0] - c) * FootprintEdgeMargin;
                    Vec2 g1 = c + (quad[1] - c) * FootprintEdgeMargin;
                    Vec2 g2 = c + (quad[2] - c) * FootprintEdgeMargin;
                    Vec2 g3 = c + (quad[3] - c) * FootprintEdgeMargin;
                    if (MBMath.CheckPointInsidePolygon(in g0, in g1, in g2, in g3, in xy))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static MissionShip ShipUnderAgent(Agent agent)
        {
            if (agent == null)
            {
                return null;
            }
            var comp = agent.GetComponent<AgentNavalComponent>();
            if (comp == null)
            {
                return null;
            }
            return comp.SteppedShip ?? comp.FormationShip;
        }

        private static void TryDamageNearestSail(MissionShip ship, Agent caster, Vec3 position, float sailDmg)
        {
            try
            {
                if (ship.ShipSailState == MissionShip.SailState.Destroyed)
                {
                    return;
                }

                var sails = ship.Sails;
                if (sails == null || sails.Count == 0)
                {

#if BL13
                    ship.DealDamageToSails(caster, sailDmg, null);
#else
                    ship.DealDamageToSails(caster, sailDmg, sailDmg, null);
#endif
                    return;
                }

                MissionSail target = null;
                float bestDistSq = float.MaxValue;
                var meshes = ship.SailMeshEntities;
                if (meshes != null && meshes.Count == sails.Count)
                {
                    for (int i = 0; i < meshes.Count; i++)
                    {
                        var e = meshes[i];
                        if (e == null)
                        {
                            continue;
                        }
                        float dsq = e.GetGlobalFrame().origin.DistanceSquared(position);
                        if (dsq < bestDistSq)
                        {
                            bestDistSq = dsq;
                            target = sails[i];
                        }
                    }
                }
                if (target == null)
                {
                    target = sails[0];
                }

#if BL13
                ship.DealDamageToSails(caster, sailDmg, target);
#else
                ship.DealDamageToSails(caster, sailDmg, sailDmg, target);
#endif
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorNavalBridge.TryDamageNearestSail failed ({ex.GetType().Name}): {ex.Message}");
            }
        }

        private static bool _fireReflectResolved;
        private static System.Reflection.MethodInfo _getFirstScriptRecursiveClosed;
        private static System.Reflection.MethodInfo _registerBlow;
        private static System.Reflection.MethodInfo _startFire;
        private static System.Reflection.PropertyInfo _scbGameEntityProp;

        private static void ResolveFireReflection()
        {
            _fireReflectResolved = true;
            try
            {

                var navalAsm = typeof(MissionShip).Assembly;
                var sbsType = navalAsm.GetType("ShipBurningSystem", false)
                              ?? System.Array.Find(navalAsm.GetTypes(), t => t.Name == "ShipBurningSystem");
                if (sbsType == null)
                {
                    return;
                }
                _registerBlow = sbsType.GetMethod("RegisterBlow", new[] { typeof(Vec3) });
                _startFire = sbsType.GetMethod("StartFire", System.Type.EmptyTypes);

                var weakType = typeof(TaleWorlds.Engine.WeakGameEntity);
                var openGeneric = System.Array.Find(weakType.GetMethods(),
                    m => m.Name == "GetFirstScriptOfTypeRecursive" && m.IsGenericMethodDefinition
                         && m.GetParameters().Length == 0);
                if (openGeneric != null)
                {
                    _getFirstScriptRecursiveClosed = openGeneric.MakeGenericMethod(sbsType);
                }

                _scbGameEntityProp = typeof(TaleWorlds.Engine.ScriptComponentBehavior).GetProperty("GameEntity");
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorNavalBridge: visual-fire reflection resolve failed ({ex.GetType().Name}): {ex.Message}. Flames will be cosmetic-only via the game's own spiral.");
            }
        }

        private static void TryDriveVisualFire(MissionShip ship, Vec3 position, bool bigHit, bool nowAblaze)
        {
            try
            {
                if (!_fireReflectResolved)
                {
                    ResolveFireReflection();
                }
                if (_getFirstScriptRecursiveClosed == null || _scbGameEntityProp == null)
                {
                    return;
                }

                object weakEntity = _scbGameEntityProp.GetValue(ship);
                if (weakEntity == null)
                {
                    return;
                }
                object burningSystem = _getFirstScriptRecursiveClosed.Invoke(weakEntity, null);
                if (burningSystem == null)
                {
                    return;
                }

                if (nowAblaze && _startFire != null)
                {
                    _startFire.Invoke(burningSystem, null);
                }
                else if (bigHit && _registerBlow != null)
                {
                    _registerBlow.Invoke(burningSystem, new object[] { position });
                }
            }
            catch (Exception ex)
            {

                SotorLog.Warn($"SotorNavalBridge.TryDriveVisualFire failed ({ex.GetType().Name}): {ex.Message}");
            }
        }

        public static int GetSummonedAgentNavalBinding(Agent agent)
        {
            try
            {
                if (agent == null)
                {
                    return 0;
                }
                var comp = agent.GetComponent<AgentNavalComponent>();
                if (comp == null)
                {
                    return 0;
                }
                bool bound = comp.SteppedShip != null || comp.FormationShip != null;
                return bound ? 1 : 2;
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorNavalBridge.GetSummonedAgentNavalBinding failed ({ex.GetType().Name}): {ex.Message}");
                return 0;
            }
        }

        public static System.Reflection.MethodInfo ResolveNavalOnCombatHitMethod()
        {
            try
            {
                var navalAsm = typeof(MissionShip).Assembly;
                var mgrType = navalAsm.GetType("NavalDLC.CharacterDevelopment.NavalSkillLevellingManager", false)
                              ?? System.Array.Find(navalAsm.GetTypes(), t => t.Name == "NavalSkillLevellingManager");
                if (mgrType == null)
                {
                    return null;
                }

                return System.Array.Find(mgrType.GetMethods(
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance),
                    m => m.Name == "OnCombatHit");
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorNavalBridge.ResolveNavalOnCombatHitMethod failed ({ex.GetType().Name}): {ex.Message}");
                return null;
            }
        }

    }
}
