using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem.StatusEffects
{

    public class StatusEffect
    {
        public Agent ApplierAgent;
        public float CurrentDuration;

        public string OriginSpellName;

        public float? EffectValueOverride;

        public StatusEffectTemplate Template { get; }

        public StatusEffect(StatusEffectTemplate template, Agent applierAgent)
        {
            Template = template;
            ApplierAgent = applierAgent;
        }
    }
}
