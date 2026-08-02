using System.Collections.Generic;
using System.Linq;
using SOTOR.AbilitySystem.StatusEffects;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem.TriggeredScripts
{

    public class SpiritLeech : ITriggeredScript
    {
        private const string HealEffectId = "spirit_leech_heal";

        public void OnTrigger(Vec3 position, Agent triggeredByAgent, IEnumerable<Agent> triggeredAgents, float duration, TriggeredEffectTemplate template, string originSpell)
        {
            if (triggeredByAgent == null || !triggeredByAgent.IsActive())
            {
                return;
            }

            var list = triggeredAgents?.Where(a => a != null && a.IsActive()).ToList();
            if (list == null || list.Count == 0)
            {
                return;
            }

            Agent best = list.FirstOrDefault(a => a.Character is CharacterObject c && c.IsHero)
                         ?? list.OrderByDescending(a => (a.Character as CharacterObject)?.Level ?? 0).First();

            int tier = (best.Character as CharacterObject)?.Tier ?? 1;
            if (tier < 1)
            {
                tier = 1;
            }

            float healDuration = tier * duration;

            var casterComp = triggeredByAgent.GetComponent<StatusEffectComponent>();
            if (casterComp != null)
            {
                casterComp.RunStatusEffect(HealEffectId, triggeredByAgent, healDuration, append: true, originSpellName: originSpell);
                SotorLog.Info(
                    $"SpiritLeech: '{triggeredByAgent.Name}' drains '{best.Name}' (tier {tier}) -> heals self " +
                    $"'{HealEffectId}' for {healDuration:0.0}s.");
            }
        }
    }
}
