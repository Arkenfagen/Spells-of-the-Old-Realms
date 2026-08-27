using System.Collections.Generic;
using SOTOR.Extensions.ExtendedInfoSystem;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace SOTOR.Extensions
{
    public static class CharacterObjectExtensions
    {
        public static List<string> GetAbilities(this BasicCharacterObject characterObject)
        {
            return new List<string>();
        }

        public static List<string> GetAttributes(this BasicCharacterObject characterObject)
        {
            return new List<string>();
        }
    }

    public static class HeroExtensions
    {
        public static string GetInfoKey(this Hero hero)
        {
            return ((MBObjectBase)hero).StringId;
        }

        public static HeroExtendedInfo GetExtendedInfo(this Hero hero)
        {
            return ExtendedInfoManager.Instance?.GetHeroInfoFor(hero.GetInfoKey());
        }

        public static bool HasAbility(this Hero hero, string abilityId)
        {
            var info = hero.GetExtendedInfo();
            return info != null && info.AllAbilities.Contains(abilityId);
        }

        public static void AddAbility(this Hero hero, string abilityId)
        {
            var info = hero.GetExtendedInfo();
            if (info != null && !info.AcquiredAbilities.Contains(abilityId))
            {
                info.AcquiredAbilities.Add(abilityId);
            }
        }

        public static void AddAttribute(this Hero hero, string attribute)
        {
            var info = hero.GetExtendedInfo();
            if (info != null && !info.AcquiredAttributes.Contains(attribute))
            {
                info.AcquiredAttributes.Add(attribute);
            }
        }

        public static void RemoveAttribute(this Hero hero, string attribute)
        {
            var info = hero.GetExtendedInfo();
            info?.AcquiredAttributes.Remove(attribute);
        }

        public static bool HasAttribute(this Hero hero, string attribute)
        {
            var info = hero.GetExtendedInfo();
            return info != null && info.AllAttributes.Contains(attribute);
        }

        public static bool IsAbilityUser(this Hero hero)
        {
            return hero.HasAttribute("AbilityUser");
        }

        public static float GetWindsOfMagic(this Hero hero)
        {
            return hero.GetExtendedInfo()?.WindsOfMagic ?? 0f;
        }

        public static float GetMaxWindsOfMagic(this Hero hero)
        {
            return hero.GetExtendedInfo()?.MaxWindsOfMagic ?? 0f;
        }

        public static void AddWindsOfMagic(this Hero hero, float amount, bool allowOverMax = false)
        {
            hero.GetExtendedInfo()?.AddWindsOfMagic(amount, allowOverMax);
        }

        public static void SetWindsOfMagic(this Hero hero, float amount)
        {
            hero.GetExtendedInfo()?.SetWindsOfMagic(amount);
        }

        public static int GetEffectiveWindsCostForSpell(this Hero hero, AbilitySystem.AbilityTemplate template)
        {
            int baseCost = template?.WindsOfMagicCost ?? 0;
            if (hero == null || baseCost <= 0)
            {
                return baseCost;
            }

            float factor = 1f;
            if (AbilitySystem.SotorPerks.OverCaster != null && hero.GetPerkValue(AbilitySystem.SotorPerks.OverCaster))
            {
                factor += 0.3f;
            }
            if (AbilitySystem.SotorPerks.EfficientSpellCaster != null && hero.GetPerkValue(AbilitySystem.SotorPerks.EfficientSpellCaster))
            {
                factor -= 0.3f;
            }

            return System.Math.Max(0, (int)System.Math.Round(baseCost * factor));
        }
    }
}
