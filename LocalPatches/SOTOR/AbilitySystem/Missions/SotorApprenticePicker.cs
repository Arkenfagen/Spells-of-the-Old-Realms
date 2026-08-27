using System.Collections.Generic;
using SOTOR.AbilitySystem.Rivals;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace SOTOR.AbilitySystem.Missions
{

    public static class SotorApprenticePicker
    {

        private static readonly string[] SkeletonApprentices =
        {
            "sotor_grave_guardian",
            "sotor_grave_guard_seneschal",
        };

        public static CharacterObject PickFor(Hero master, out bool isSkeleton)
        {
            isSkeleton = false;
            if (master == null) return null;

            if (SotorRivalSeeder.SocialTradition(master) == Trad.Necromancy)
            {
                var bones = PickSkeleton(master);
                if (bones != null)
                {
                    isSkeleton = true;
                    SotorLog.Info($"ApprenticePicker: {master.Name} is a necromancer, so his apprentice is the "
                                  + $"skeleton '{bones.StringId}' (tier {bones.Tier}).");
                    return bones;
                }

                SotorLog.Warn($"ApprenticePicker: {master.Name} is a necromancer but no skeleton troop resolved; "
                              + "falling back to his culture's elite.");
            }

            var apprentice = PickCultureElite(master);
            if (apprentice != null)
            {
                SotorLog.Info($"ApprenticePicker: {master.Name} ({master.Culture?.StringId ?? "no culture"}) fields "
                              + $"'{apprentice.StringId}' (tier {apprentice.Tier}, level {apprentice.Level}, "
                              + $"melee={IsMelee(apprentice)}).");
            }
            else
            {
                SotorLog.Error($"ApprenticePicker: could not resolve ANY apprentice for {master.Name}.");
            }
            return apprentice;
        }

        private static CharacterObject PickSkeleton(Hero master)
        {

            int pick = System.Math.Abs(master.StringId?.GetHashCode() ?? 0) % SkeletonApprentices.Length;
            for (int i = 0; i < SkeletonApprentices.Length; i++)
            {
                var c = MBObjectManager.Instance?.GetObject<CharacterObject>(
                    SkeletonApprentices[(pick + i) % SkeletonApprentices.Length]);
                if (c != null) return c;
            }
            return null;
        }

        private static CharacterObject PickCultureElite(Hero master)
        {
            var culture = master.Culture;
            if (culture == null) return null;

            var elite = CollectTree(culture.EliteBasicTroop);
            var best = Best(elite, meleeOnly: true) ?? Best(elite, meleeOnly: false);
            if (best != null) return best;

            var basic = CollectTree(culture.BasicTroop);
            return Best(basic, meleeOnly: true) ?? Best(basic, meleeOnly: false);
        }

        private static List<CharacterObject> CollectTree(CharacterObject root)
        {
            var found = new List<CharacterObject>();
            if (root == null) return found;

            var seen = new HashSet<string>();
            var queue = new Queue<CharacterObject>();
            queue.Enqueue(root);
            seen.Add(root.StringId);

            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                found.Add(c);

                var targets = c.UpgradeTargets;
                if (targets == null) continue;
                foreach (var t in targets)
                {
                    if (t == null || !seen.Add(t.StringId)) continue;
                    queue.Enqueue(t);
                }
            }
            return found;
        }

        private static CharacterObject Best(List<CharacterObject> pool, bool meleeOnly)
        {
            CharacterObject best = null;
            foreach (var c in pool)
            {
                if (c == null || c.IsHero) continue;
                if (meleeOnly && !IsMelee(c)) continue;
                if (best == null || c.Tier > best.Tier || (c.Tier == best.Tier && c.Level > best.Level))
                {
                    best = c;
                }
            }
            return best;
        }

        private static bool IsMelee(CharacterObject c) => !c.IsRanged && !c.IsMounted;
    }
}
