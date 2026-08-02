using System.Collections.Generic;
using SOTOR.Extensions;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem.StatusEffects
{

    public class StatusEffectMissionLogic : MissionLogic
    {

        private const float PermanentAuraDuration = 100000f;

        private readonly HashSet<Agent> _captainAurasApplied = new HashSet<Agent>();

        public override void OnAgentCreated(Agent agent)
        {
            if (agent != null && agent.IsHuman)
            {
                agent.AddComponent(new StatusEffectComponent(agent));
                ApplySelfPerkAuras(agent);
            }
        }

        private static void ApplySelfPerkAuras(Agent agent)
        {
            var hero = agent.GetHero();
            if (hero == null)
            {
                return;
            }

            if (SotorPerks.Dampener != null && hero.GetPerkValue(SotorPerks.Dampener))
            {
                agent.GetComponent<StatusEffectComponent>()?.RunStatusEffect("dampener_ward_save", agent, PermanentAuraDuration, false);
                SotorLog.Info($"Dampener: applied 5% ward-save aura to {hero.Name}.");
            }
        }

        private void ApplyCaptainPerkAuras(Agent agent)
        {
            if (_captainAurasApplied.Contains(agent))
            {
                return;
            }

            var captain = agent.Formation?.Captain;
            if (captain == null)
            {
                return;
            }

            _captainAurasApplied.Add(agent);

            var captainHero = captain.GetHero();
            if (captainHero == null)
            {
                return;
            }

            var component = agent.GetComponent<StatusEffectComponent>();
            if (component == null)
            {
                return;
            }

            if (SotorPerks.ArcaneLink != null && captainHero.GetPerkValue(SotorPerks.ArcaneLink))
            {
                component.RunStatusEffect("arcanelink_magic_10", agent, PermanentAuraDuration, false);
                SotorLog.Debug($"ArcaneLink: +10% dmg aura on '{agent.Name}' (captain {captainHero.Name}).");
            }
            if (SotorPerks.Dampener != null && captainHero.GetPerkValue(SotorPerks.Dampener))
            {
                component.RunStatusEffect("dampener_formation_spellres", agent, PermanentAuraDuration, false);
                SotorLog.Debug($"Dampener: -30% spell-dmg formation aura on '{agent.Name}' (captain {captainHero.Name}).");
            }
        }

        public override void OnMissionTick(float dt)
        {
            var all = Mission.Current?.AllAgents;
            if (all == null) return;

            foreach (var agent in (List<Agent>)all)
            {
                if (agent == null) continue;

                if (agent.IsHuman && !_captainAurasApplied.Contains(agent))
                {
                    ApplyCaptainPerkAuras(agent);
                }

                var component = agent.GetComponent<StatusEffectComponent>();
                if (component != null && component.NeedsStatusEffectTick)
                {
                    component.OnTick(dt);
                }
            }

            SotorSpellDamageLog.FlushExpired(Mission.Current);
        }

        public override void OnRemoveBehavior()
        {
            SotorSpellDamageLog.Reset();
            base.OnRemoveBehavior();
        }
    }
}
