using System.Collections.Generic;
using SOTOR.Extensions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace SOTOR.AbilitySystem.Rivals
{

    public class SotorTraditionObject : MBObjectBase
    {
        private static List<SotorTraditionObject> _all;

        public Trad Tradition { get; private set; }

        public TextObject Name => SotorText.GetObject(SotorTraditions.NameKey(Tradition), FallbackName(Tradition));

        public TextObject Description =>
            SotorText.GetObject(SotorTraditions.DescriptionKey(Tradition), FallbackDescription(Tradition));

        public bool IsMemberOnly => SotorTraditions.IsMemberOnly(Tradition);

        public static IReadOnlyList<SotorTraditionObject> All => _all ?? (IReadOnlyList<SotorTraditionObject>)new List<SotorTraditionObject>();

        public static void EnsureCreated()
        {
            if (_all != null) return;
            var list = new List<SotorTraditionObject>();
            foreach (var t in SotorTraditions.AllTraditions)
            {
                string id = SotorTraditions.ObjectStringId(t);
                if (id == null) continue;

                list.Add(new SotorTraditionObject
                {
                    StringId = id,
                    Tradition = t,
                });
            }
            _all = list;
        }

        public static SotorTraditionObject Find(string stringId)
        {
            EnsureCreated();
            if (string.IsNullOrEmpty(stringId)) return null;
            for (int i = 0; i < _all.Count; i++)
            {
                if (_all[i].StringId == stringId) return _all[i];
            }
            return null;
        }

        public static SotorTraditionObject For(Trad t)
        {
            return Find(SotorTraditions.ObjectStringId(t));
        }

        public List<Hero> CurrentPractitioners()
        {
            var result = new List<Hero>();
            bool memberOnly = IsMemberOnly;
            foreach (var hero in Hero.AllAliveHeroes)
            {
                if (hero == null || !hero.IsAbilityUser()) continue;
                if (memberOnly)
                {
                    if (!SotorRivalReveal.IsRevealed(hero)) continue;
                    if (!SotorRivalSeeder.TeachableTraditions(hero).Contains(Tradition)) continue;
                }
                else
                {
                    if (SotorRivalSeeder.SocialTradition(hero) != Tradition) continue;
                }
                result.Add(hero);
            }
            return result;
        }

        public string EncyclopediaLink => SotorTraditionEncyclopediaPage.PageIdentifier + "-" + StringId;

        private static string FallbackName(Trad t)
        {
            switch (t)
            {
                case Trad.Fire: return "Pyromancers";
                case Trad.Heavens: return "Astromancers";
                case Trad.Light: return "Hierophants";
                case Trad.Life: return "Druids";
                case Trad.Beasts: return "Shamans";
                case Trad.Metal: return "Alchemists";
                case Trad.Death: return "Spirit Magisters";
                case Trad.Necromancy: return "Necromancers";
                case Trad.Dark: return "Doomweavers";
                case Trad.High: return "Loremasters";
                default: return "Arcane Tradition";
            }
        }

        private static string FallbackDescription(Trad t)
        {
            return "An arcane tradition of " + FallbackName(t) + ".";
        }
    }
}
