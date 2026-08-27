using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using SOTOR.AbilitySystem;

namespace SOTOR.Items
{

    public static class SotorSkillGate
    {

        public static SkillObject Resolve(string id)
        {
            switch (id)
            {
                case "OneHanded": return DefaultSkills.OneHanded;
                case "TwoHanded": return DefaultSkills.TwoHanded;
                case "Polearm": return DefaultSkills.Polearm;
                case "Bow": return DefaultSkills.Bow;
                case "Crossbow": return DefaultSkills.Crossbow;
                case "Throwing": return DefaultSkills.Throwing;
                case "Riding": return DefaultSkills.Riding;
                case "Athletics": return DefaultSkills.Athletics;
                case "Crafting": return DefaultSkills.Crafting;
                case "Scouting": return DefaultSkills.Scouting;
                case "Medicine": return DefaultSkills.Medicine;
                case "Charm": return DefaultSkills.Charm;
                case "Spellcraft": return SotorSkills.Spellcraft;
                default: return null;
            }
        }

        public static int BestValue(Hero hero, SotorItemTrait trait)
        {
            if (hero == null || trait == null || !trait.HasSkillRequirement) return 0;
            int best = 0;
            foreach (var id in trait.RequiredSkillIds)
            {
                var skill = Resolve(id);
                if (skill == null) continue;
                int value = hero.GetSkillValue(skill);
                if (value > best) best = value;
            }
            return best;
        }

        public static bool Meets(Hero hero, SotorItemTrait trait)
        {
            return trait != null && trait.HasSkillRequirement
                && BestValue(hero, trait) >= trait.SkillThreshold;
        }

        public static string RequirementText(SotorItemTrait trait)
        {
            if (trait == null) return "";
            if (trait.HasSkillRequirement)
            {
                string skills = null;
                foreach (var id in trait.RequiredSkillIds)
                {
                    var skill = Resolve(id);
                    string name = skill?.Name?.ToString() ?? id;
                    if (skills == null)
                    {
                        skills = name;
                    }
                    else
                    {
                        var joined = SotorText.GetObject("sotor_skill_gate_or");
                        joined.SetTextVariable("A", skills);
                        joined.SetTextVariable("B", name);
                        skills = joined.ToString();
                    }
                }
                var text = SotorText.GetObject("sotor_trait_req_skill");
                text.SetTextVariable("SKILLS", skills ?? "");
                text.SetTextVariable("THRESHOLD", trait.SkillThreshold);
                return text.ToString();
            }
            if (trait.HasLoreRequirement)
            {
                var text = SotorText.GetObject("sotor_trait_req_lore");
                text.SetTextVariable("LORE", SotorLores.TitleFor(trait.RequiredLore));
                text.SetTextVariable("THRESHOLD", trait.LearnThreshold);
                return text.ToString();
            }
            return "";
        }

        public static string NeedsLine(SotorItemTrait trait)
        {
            string requirement = RequirementText(trait);
            if (string.IsNullOrEmpty(requirement)) return "";
            var text = SotorText.GetObject("sotor_trait_needs_line");
            text.SetTextVariable("REQ", requirement);
            return text.ToString();
        }
    }
}
