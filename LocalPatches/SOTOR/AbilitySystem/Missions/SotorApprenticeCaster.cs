using System.Collections.Generic;
using SOTOR.AbilitySystem.AI;
using SOTOR.Extensions;
using SOTOR.AbilitySystem.Rivals;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem.Missions
{

    public static class SotorApprenticeCaster
    {

        private static readonly List<string> CasterAttributes = new List<string> { "AbilityUser", "SpellCaster" };

        private class Loadout
        {
            public List<string> Abilities;
            public float MaxWinds;
            public SpellCastingLevel Level;
        }

        private static readonly Dictionary<int, Loadout> _byAgent = new Dictionary<int, Loadout>();

        public static void Clear() => _byAgent.Clear();

        public static SpellCastingLevel ApprenticeLevelFor(SpellCastingLevel masterLevel)
        {
            switch (masterLevel)
            {
                case SpellCastingLevel.Archmage: return SpellCastingLevel.Master;
                case SpellCastingLevel.Master: return SpellCastingLevel.Adept;
                case SpellCastingLevel.Adept: return SpellCastingLevel.Entry;
                default: return SpellCastingLevel.Entry;
            }
        }

        private static int MaxSpellTierFor(SpellCastingLevel level)
        {
            switch (level)
            {
                case SpellCastingLevel.Archmage:
                case SpellCastingLevel.Master: return 4;
                case SpellCastingLevel.Adept: return 3;

                default: return 2;
            }
        }

        private static int SpellcraftBandFor(SpellCastingLevel level)
        {
            switch (level)
            {
                case SpellCastingLevel.Archmage: return 6;
                case SpellCastingLevel.Master: return 4;
                case SpellCastingLevel.Adept: return 3;
                case SpellCastingLevel.Entry: return 2;
                default: return 1;
            }
        }

        public static bool Equip(Agent agent, Hero master)
        {
            if (agent == null || master == null) return false;

            var masterLevel = SotorSpellcraftHelper.GetCastingLevel(master);
            var level = ApprenticeLevelFor(masterLevel);
            int maxTier = MaxSpellTierFor(level);

            var trad = SotorRivalSeeder.SocialTradition(master);
            string loreId = SotorTraditions.LoreIdFor(trad);
            if (loreId == null)
            {
                SotorLog.Info($"ApprenticeCaster: {master.Name} has no social tradition; his apprentice fights without magic.");
                return false;
            }

            var abilities = new List<string>();
            foreach (var template in AbilityFactory.GetTemplatesByLore(loreId))
            {
                if (template?.StringID == null) continue;
                if (template.SpellTier > maxTier) continue;
                abilities.Add(template.StringID);
            }

            if (abilities.Count == 0)
            {
                SotorLog.Info($"ApprenticeCaster: no {loreId} spells at tier <= {maxTier}; apprentice fights without magic.");
                return false;
            }

            int spellcraft = SotorTraditions.SpellcraftForLevel(SpellcraftBandFor(level), 0.5f);
            float maxWinds = SotorSpellcraftHelper.BaseMaxWinds
                             + SotorSpellcraftHelper.MaxWindsPerSpellcraftPoint * spellcraft;

            _byAgent[agent.Index] = new Loadout { Abilities = abilities, MaxWinds = maxWinds, Level = level };

            try
            {
                if (agent.GetComponent<AbilityComponent>() == null)
                {
                    agent.AddComponent(new AbilityComponent(agent));
                }
                if (agent.GetComponent<WizardAIComponent>() == null)
                {

                    agent.AddComponent(new SotorDuelWizardAIComponent(agent));
                }
            }
            catch (System.Exception ex)
            {
                _byAgent.Remove(agent.Index);
                SotorLog.Error($"ApprenticeCaster: could not attach caster components: {ex.GetType().Name}: {ex.Message}");
                return false;
            }

            SotorApprenticeWinds.Initialize(agent, maxWinds);

            var detail = new List<string>();
            foreach (var id in abilities)
            {
                var t = AbilityFactory.GetTemplate(id);
                detail.Add($"{id}(T{t?.SpellTier ?? 0})");
            }
            SotorLog.Info($"ApprenticeCaster: '{agent.Name}' fights for {master.Name}. "
                          + $"master={masterLevel} -> apprentice={level} (tier cap {maxTier}), school={loreId}, "
                          + $"{abilities.Count} spell(s): [{string.Join(", ", detail)}]");

            var component = agent.GetComponent<AbilityComponent>();
            bool brain = agent.GetComponent<WizardAIComponent>() != null;
            SotorLog.Info($"ApprenticeCaster VERIFY '{agent.Name}': isAbilityUser={agent.IsAbilityUser()} "
                          + $"isSpellCaster={agent.IsSpellCaster()} selected={agent.GetSelectedAbilities().Count} "
                          + $"known={component?.KnownAbilitySystem.Count ?? 0} "
                          + $"current={component?.CurrentAbility?.StringID ?? "none"} brain={brain} "
                          + $"winds={SotorApprenticeWinds.Get(agent):0}/{maxWinds:0} "
                          + $"aiControlled={agent.IsAIControlled} human={agent.IsHuman}");

            try
            {
                foreach (var ability in component?.KnownAbilitySystem ?? new List<Ability>())
                {
                    var t = ability?.Template;
                    if (t == null) { SotorLog.Info("ApprenticeCaster LOADOUT: an ability with a NULL template."); continue; }
                    var eff = TriggeredEffectManager.GetTemplate(t.TriggeredEffectID);
                    SotorLog.Info($"ApprenticeCaster LOADOUT '{t.StringID}': type={ability.GetType().Name} "
                                  + $"effect={t.AbilityEffectType} target={t.AbilityTargetType} "
                                  + $"triggeredEffect='{t.TriggeredEffectID}' resolved={eff != null} "
                                  + $"blastRadius={(eff?.Radius ?? 0f):0.##} tickInterval={t.TickInterval} "
                                  + $"duration={t.Duration} winds={t.WindsOfMagicCost} "

                                  + $"range={t.MinDistance}-{t.MaxDistance}m");
                }
            }
            catch (System.Exception ex)
            {
                SotorLog.Warn($"ApprenticeCaster: loadout dump failed harmlessly: {ex.GetType().Name}: {ex.Message}");
            }

            if (component == null || component.KnownAbilitySystem.Count == 0 || !brain)
            {
                SotorLog.Error($"ApprenticeCaster: '{agent.Name}' was armed but cannot cast "
                               + $"(component={component != null}, known={component?.KnownAbilitySystem.Count ?? 0}, "
                               + $"brain={brain}). He will fight with steel only.");
            }
            return true;
        }

        private static Loadout For(Agent agent)
        {
            if (agent == null) return null;
            return _byAgent.TryGetValue(agent.Index, out var loadout) ? loadout : null;
        }

        public static List<string> AbilitiesFor(Agent agent)
        {
            var loadout = For(agent);
            return loadout == null ? null : new List<string>(loadout.Abilities);
        }

        public static List<string> AttributesFor(Agent agent)
        {
            return For(agent) == null ? null : new List<string>(CasterAttributes);
        }

        public static float MaxWindsFor(Agent agent) => For(agent)?.MaxWinds ?? 0f;

    }
}
