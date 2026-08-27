using System;
using SOTOR.AbilitySystem.AI;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem.Missions
{

    public class SotorDuelWizardAIComponent : WizardAIComponent
    {
        private bool _reported;

        public SotorDuelWizardAIComponent(Agent agent)
            : base(agent)
        {
        }

        public override void OnTick(float dt)
        {
            try
            {
                base.OnTick(dt);
            }
            catch (Exception ex)
            {
                if (!_reported)
                {
                    _reported = true;
                    SotorLog.Error("DuelWizardAI: the apprentice's casting tick threw and was swallowed to keep "
                                   + "the duel alive. He may stop casting from here. If this is inside the shared "
                                   + "casting AI it is a real bug there, not a duel quirk:\n"
                                   + $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }
    }
}
