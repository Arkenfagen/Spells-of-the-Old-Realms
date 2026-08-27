using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace SOTOR.AbilitySystem.Rivals
{

    public static class SotorRivalReveal
    {
        private static List<string> _revealed;

        private static readonly List<Hero> _pendingAnnouncements = new List<Hero>();

        public static void Bind(List<string> revealed) => _revealed = revealed;

        public static void ClearAll() => _revealed?.Clear();

        public static bool IsReady => _revealed != null;

        public static bool IsRevealed(Hero hero)
        {
            return hero != null && _revealed != null && _revealed.Contains(hero.StringId);
        }

        public static bool Reveal(Hero hero)
        {
            if (hero == null || _revealed == null) return false;
            if (_revealed.Contains(hero.StringId))
            {
                return false;
            }
            _revealed.Add(hero.StringId);
            MarkKnownToPlayer(hero);
            return true;
        }

        public static void MarkKnownToPlayer(Hero hero)
        {
            if (hero == null || hero == Hero.MainHero) return;
            if (hero.IsKnownToPlayer) return;
            hero.IsKnownToPlayer = true;
        }

        public static void Forget(Hero hero)
        {
            if (hero != null) _revealed?.Remove(hero.StringId);
        }

        public static void QueueAnnouncement(Hero hero)
        {
            if (hero == null) return;
            if (_pendingAnnouncements.Contains(hero)) return;
            _pendingAnnouncements.Add(hero);
        }

        public static bool HasPendingAnnouncements => _pendingAnnouncements.Count > 0;

        public static List<Hero> TakePendingAnnouncements()
        {
            var copy = new List<Hero>(_pendingAnnouncements);
            _pendingAnnouncements.Clear();
            return copy;
        }
    }
}
