using System.Collections.Generic;
using SOTOR.Extensions;
using TaleWorlds.CampaignSystem;

namespace SOTOR.AbilitySystem.Rivals
{

    public static class SotorBloodlineMemo
    {
        private static readonly Dictionary<string, SotorGenotype> _genes = new Dictionary<string, SotorGenotype>();
        private static bool _built;

        public static void Invalidate()
        {
            _genes.Clear();
            _built = false;
        }

        public static void Rebuild()
        {
            Invalidate();
            if (Campaign.Current == null) return;

            var all = new List<Hero>();
            foreach (var h in Hero.AllAliveHeroes) if (h != null) all.Add(h);
            foreach (var h in Hero.DeadOrDisabledHeroes) if (h != null) all.Add(h);

            all.Sort((a, b) =>
            {
                int cmp = a.BirthDay.ToDays.CompareTo(b.BirthDay.ToDays);
                return cmp != 0 ? cmp : string.CompareOrdinal(a.StringId, b.StringId);
            });

            foreach (var hero in all)
            {
                _genes[hero.StringId] = Compute(hero);
            }
            _built = true;
        }

        private static SotorGenotype Compute(Hero hero)
        {
            if (SotorRivalSeeder.IsGeneticFounder(hero)) return SotorRivalSeeder.FounderGenotype(hero);

            var father = hero.Father;
            var mother = hero.Mother;

            if (father == null && mother == null) return SotorRivalSeeder.FounderGenotype(hero);

            var fg = LookupGenes(father);
            var mg = LookupGenes(mother);

            return new SotorGenotype(
                SotorRivalSeeder.InheritAllele(hero, father, fg),
                SotorRivalSeeder.InheritAllele(hero, mother, mg));
        }

        private static SotorGenotype LookupGenes(Hero parent)
        {
            if (parent == null) return new SotorGenotype(false, false);
            return _genes.TryGetValue(parent.StringId, out var g)
                ? g
                : SotorRivalSeeder.FounderGenotype(parent);
        }

        public static int CasterParentCount(Hero hero)
        {
            if (hero == null) return 0;
            int n = 0;
            if (LookupGenes(hero.Father).IsCaster) n++;
            if (LookupGenes(hero.Mother).IsCaster) n++;
            return n;
        }

        public static SotorGenotype GenesOf(Hero hero)
        {
            if (hero == null) return new SotorGenotype(false, false);
            if (_built && _genes.TryGetValue(hero.StringId, out var g)) return g;
            return Compute(hero);
        }

        public static bool IsCaster(Hero hero) => GenesOf(hero).IsCaster;
    }
}
