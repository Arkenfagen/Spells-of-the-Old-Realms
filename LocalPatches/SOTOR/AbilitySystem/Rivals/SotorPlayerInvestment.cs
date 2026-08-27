using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace SOTOR.AbilitySystem.Rivals
{

    public static class SotorPlayerInvestment
    {
        private static List<string> _invested;

        public static void Bind(List<string> invested) => _invested = invested;

        public static bool IsReady => _invested != null;

        public static bool WasInvestedIn(Hero hero)
        {
            return hero != null && _invested != null && _invested.Contains(hero.StringId);
        }

        public static void Record(Hero hero)
        {
            if (hero == null || _invested == null) return;
            if (_invested.Contains(hero.StringId)) return;
            _invested.Add(hero.StringId);
            SotorLog.Info($"PlayerInvestment: {hero.Name} recorded as player-taught; a world rebuild will not strip him.");
        }
    }
}
