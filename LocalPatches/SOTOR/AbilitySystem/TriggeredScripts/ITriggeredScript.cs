using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem.TriggeredScripts
{

    public interface ITriggeredScript
    {

        void OnTrigger(Vec3 position, Agent triggeredByAgent, IEnumerable<Agent> triggeredAgents, float duration, TriggeredEffectTemplate template, string originSpell);
    }
}
