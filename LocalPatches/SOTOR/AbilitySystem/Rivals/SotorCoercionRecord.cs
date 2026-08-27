using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace SOTOR.AbilitySystem.Rivals
{

    public static class SotorCoercionRecord
    {
        private static List<string> _coerced;

        public static void Bind(List<string> coerced) => _coerced = coerced;

        public static bool IsReady => _coerced != null;

        public static void ClearAll() => _coerced?.Clear();

        public static bool WasCoerced(Hero hero)
        {
            return hero != null && _coerced != null && _coerced.Contains(hero.StringId);
        }

        public static bool Record(Hero hero)
        {
            if (hero == null || _coerced == null) return false;
            if (_coerced.Contains(hero.StringId)) return false;
            _coerced.Add(hero.StringId);
            return true;
        }

        public static void Forget(Hero hero)
        {
            if (hero != null) _coerced?.Remove(hero.StringId);
        }
    }
}
