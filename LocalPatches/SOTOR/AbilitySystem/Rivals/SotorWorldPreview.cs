using System;
using System.Collections.Generic;
using SOTOR.Extensions;
using TaleWorlds.CampaignSystem;

namespace SOTOR.AbilitySystem.Rivals
{

    public static class SotorWorldPreview
    {

        private struct Saved
        {
            public bool Enable, Rulers, Minor;
            public int Lords, Wanderers, Forbidden;
            public int MinTier;
            public string Seed;
            public bool ByCulture;
        }

        public static string Predict(
            bool enable, int lordShare, int wandererShare, int forbiddenChance,
            int minClanTier, bool includeRulers, bool includeMinorFactions, string worldSeed,
            bool loreByCulture)
        {
            if (Campaign.Current == null)
            {
                return SotorText.Rendered("sotor_mcm_no_campaign",
                    "No campaign is loaded. Start or load a game first.");
            }

            var saved = new Saved
            {
                Enable = SotorSettings.EnableRivalCasters,
                Rulers = SotorSettings.RivalIncludeRulers,
                Minor = SotorSettings.RivalIncludeMinorFactions,
                Lords = SotorSettings.RivalCasterLordShare,
                Wanderers = SotorSettings.RivalCasterWandererShare,
                Forbidden = SotorSettings.RivalMemberOnlyLoreClanChance,
                MinTier = SotorSettings.RivalMinClanTierForCaster,
                Seed = SotorSettings.RivalWorldSeed,
                ByCulture = SotorSettings.RivalLoreByCulture,
            };

            try
            {

                SotorRivalOverrides.Reload();

                SotorSettings.EnableRivalCasters = enable;
                SotorSettings.RivalIncludeRulers = includeRulers;
                SotorSettings.RivalIncludeMinorFactions = includeMinorFactions;
                SotorSettings.RivalCasterLordShare = lordShare;
                SotorSettings.RivalCasterWandererShare = wandererShare;
                SotorSettings.RivalMemberOnlyLoreClanChance = forbiddenChance;
                SotorSettings.RivalMinClanTierForCaster = minClanTier;
                SotorSettings.RivalWorldSeed = worldSeed ?? string.Empty;

                SotorSettings.RivalLoreByCulture = loreByCulture;

                SotorBloodlineMemo.Rebuild();
                return Build(enable);
            }
            catch (Exception ex)
            {
                SotorLog.Error($"WorldPreview failed: {ex.GetType().Name}: {ex.Message}");
                return SotorText.Rendered("sotor_mcm_report_failed",
                    "Could not read the campaign. See the SOTOR log for details.");
            }
            finally
            {
                SotorSettings.EnableRivalCasters = saved.Enable;
                SotorSettings.RivalIncludeRulers = saved.Rulers;
                SotorSettings.RivalIncludeMinorFactions = saved.Minor;
                SotorSettings.RivalCasterLordShare = saved.Lords;
                SotorSettings.RivalCasterWandererShare = saved.Wanderers;
                SotorSettings.RivalMemberOnlyLoreClanChance = saved.Forbidden;
                SotorSettings.RivalMinClanTierForCaster = saved.MinTier;
                SotorSettings.RivalWorldSeed = saved.Seed;
                SotorSettings.RivalLoreByCulture = saved.ByCulture;
                SotorBloodlineMemo.Rebuild();
            }
        }

        private static string Build(bool enable)
        {

            var seedLine = SotorText.GetObject(SotorSettings.HasWorldSeed
                ? "sotor_mcm_report_seed_custom"
                : "sotor_mcm_report_seed_default");
            seedLine.SetTextVariable("SEED", SotorRivalSeeder.WorldSeedText());

            var lines = new List<string> { seedLine.ToString() };

            if (!enable)
            {
                lines.Add("");
                lines.Add(SotorText.Rendered("sotor_mcm_preview_disabled"));
                return string.Join("\n", lines);
            }

            int lords = 0, hiddenMasters = 0, wanderers = 0;

            int inherited = 0;
            var byTradition = new Dictionary<Trad, int>();
            var hiddenByTradition = new Dictionary<Trad, int>();

            foreach (var clan in Clan.All)
            {
                if (!SotorRivalSeeder.IsCasterEligibleClan(clan)) continue;

                var trad = SotorRivalSeeder.DeriveClanTradition(clan);
                foreach (var hero in clan.AliveLords)
                {
                    if (!SotorRivalSeeder.IsSeedCandidateLord(hero)) continue;
                    if (!SotorRivalSeeder.HeroIsCasterPublic(hero)) continue;
                    lords++;
                    if (!SotorRivalSeeder.IsGeneticFounder(hero)) inherited++;
                    byTradition.TryGetValue(trad, out int n);
                    byTradition[trad] = n + 1;
                }

                if (clan.Tier >= 5
                    && SotorRivalSeeder.ClanHasMemberOnlyMaster(clan, SotorSettings.RivalMemberOnlyLoreClanChance))
                {
                    int eligible = 0;
                    foreach (var hero in clan.AliveLords)
                    {
                        if (SotorRivalSeeder.IsSeedCandidateLord(hero)) eligible++;
                        if (eligible >= 2) break;
                    }
                    if (eligible > 0)
                    {
                        hiddenMasters += eligible;
                        var mt = SotorRivalSeeder.MemberOnlyTraditionFor(clan);
                        hiddenByTradition.TryGetValue(mt, out int hn);
                        hiddenByTradition[mt] = hn + eligible;
                    }
                }
            }

            foreach (var hero in Hero.AllAliveHeroes)
            {
                if (hero == null || !hero.IsWanderer || hero.IsNotable || hero.IsChild) continue;
                if (hero.CompanionOf != null || hero.Clan != null) continue;
                if (SotorRivalSeeder.WandererIsCaster(hero, SotorSettings.RivalCasterWandererShare)) wanderers++;
            }

            foreach (var trad in SotorTraditions.MemberOnlyTraditions)
            {
                hiddenByTradition.TryGetValue(trad, out int have);
                if (have <= 0) hiddenMasters++;
            }

            if (inherited > 0)
            {
                var blood = SotorText.GetObject("sotor_mcm_preview_bloodline");
                blood.SetTextVariable("EXTRA", inherited);
                lines.Add("");
                lines.Add(blood.ToString());
            }

            var counts = SotorText.GetObject("sotor_mcm_preview_counts");
            counts.SetTextVariable("LORDS", lords);
            counts.SetTextVariable("HIDDEN", hiddenMasters);
            counts.SetTextVariable("TAVERN", wanderers);

            var knobs = SotorText.GetObject("sotor_mcm_report_knobs");
            knobs.SetTextVariable("LORDSHARE", $"{SotorSettings.RivalCasterLordShare:0.#}");
            knobs.SetTextVariable("WANDERERSHARE", $"{SotorSettings.RivalCasterWandererShare:0.#}");
            knobs.SetTextVariable("FORBIDDEN", $"{SotorSettings.RivalMemberOnlyLoreClanChance:0.#}");
            knobs.SetTextVariable("MINTIER", SotorSettings.RivalMinClanTierForCaster);

            lines.Add("");
            lines.Add(counts.ToString());
            lines.Add("");
            lines.Add(knobs.ToString());

            if (byTradition.Count > 0)
            {
                lines.Add("");
                foreach (var trad in SotorTraditions.AllTraditions)
                {
                    if (!byTradition.TryGetValue(trad, out int n) || n <= 0) continue;
                    var obj = SotorTraditionObject.For(trad);
                    lines.Add($"  {(obj != null ? obj.Name.ToString() : trad.ToString())}: {n}");
                }
            }

            lines.Add("");
            lines.Add(SotorText.Rendered("sotor_mcm_preview_footer"));
            return string.Join("\n", lines);
        }
    }
}
