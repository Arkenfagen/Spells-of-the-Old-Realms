using System.Collections.Generic;
using SOTOR;
using SOTOR.Extensions;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem
{
    public class AbilityComponent : AgentComponent
    {
        private Ability _currentAbility;
        private readonly List<Ability> _knownAbilitySystem = new List<Ability>();

        public bool LastCastWasQuickCast { get; set; }

        public List<Ability> KnownAbilitySystem => _knownAbilitySystem;

        public List<AbilityTemplate> GetKnownAbilityTemplates()
        {
            var templates = new List<AbilityTemplate>(_knownAbilitySystem.Count);
            foreach (var ability in _knownAbilitySystem)
            {
                templates.Add(ability.Template);
            }
            return templates;
        }

        public Ability CurrentAbility
        {
            get => _currentAbility;
            set => _currentAbility = value;
        }

        public AbilityComponent(Agent agent)
            : base(agent)
        {
            foreach (var abilityId in agent.GetSelectedAbilities())
            {
                var ability = AbilityFactory.CreateNew(abilityId, agent);
                if (ability != null)
                {
                    _knownAbilitySystem.Add(ability);
                }
                else
                {
                    SotorLog.Warn($"Failed to create ability '{abilityId}' for agent.");
                }
            }

            if (_knownAbilitySystem.Count > 0)
            {
                SelectAbility(0);
            }

            var heroKey = agent.GetHero()?.GetInfoKey() ?? "no-hero";
            SotorLog.Info(
                $"AbilityComponent created. hero={heroKey} main={agent.IsMainAgent} known={_knownAbilitySystem.Count} current={CurrentAbility?.StringID ?? "none"}");
        }
        public void SelectAbility(Ability ability)
        {
            if (_knownAbilitySystem.Contains(ability))
            {
                CurrentAbility = ability;
            }
        }

        public void SelectAbility(int index)
        {
            if (_knownAbilitySystem.Count > 0 && index >= 0 && index < _knownAbilitySystem.Count)
            {
                CurrentAbility = _knownAbilitySystem[index];
            }
        }

        public override void OnTick(float dt)
        {
            foreach (var ability in _knownAbilitySystem)
            {
                ability.TickCastingState();
            }
        }
    }
}
