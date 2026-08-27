using System.Collections.Generic;
using SOTOR.Extensions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace SOTOR.AbilitySystem.Rivals
{

    public static class SotorRivalStanding
    {

        public const bool DecayEnabled = false;
        public const int GraceDays = 21;
        public const int Min = -100;
        public const int Max = 100;

        private static Dictionary<int, int> _tradition;
        private static Dictionary<int, float> _traditionDay;

        public static void Bind(Dictionary<int, int> tradition, Dictionary<int, float> traditionDay)
        {
            _tradition = tradition;
            _traditionDay = traditionDay;
        }

        public static bool IsReady => _tradition != null;

        public static int ClearAll()
        {
            int n = _tradition?.Count ?? 0;
            _tradition?.Clear();
            _traditionDay?.Clear();
            return n;
        }

        private static float NowDays()
        {

            return Campaign.Current != null ? (float)CampaignTime.Now.ToDays : 0f;
        }

        public static int Decayed(int stored, float lastDay, float nowDay)
        {
            if (!DecayEnabled) return stored;
            if (stored == 0) return 0;
            float elapsed = nowDay - lastDay;
            if (elapsed <= GraceDays) return stored;
            int steps = (int)(elapsed - GraceDays);
            if (stored > 0)
            {
                int v = stored - steps;
                return v < 0 ? 0 : v;
            }
            else
            {
                int v = stored + steps;
                return v > 0 ? 0 : v;
            }
        }

        private static bool FeatureOff => !SotorSettings.EnableRivalCasters;

        public static int GetTradition(Trad trad)
        {
            if (FeatureOff) return 0;
            if (trad == Trad.None || _tradition == null) return 0;
            int key = (int)trad;
            if (!_tradition.TryGetValue(key, out int stored)) return 0;
            float lastDay = _traditionDay.TryGetValue(key, out float d) ? d : 0f;
            int now = Decayed(stored, lastDay, NowDays());
            if (now != stored)
            {
                _tradition[key] = now;
                _traditionDay[key] = NowDays();
            }
            return now;
        }

        public static void ChangeTradition(Trad trad, int delta, bool silent = false, bool affectLords = false,
            bool spillToRivals = true)
        {
            if (FeatureOff) return;
            if (trad == Trad.None || _tradition == null || delta == 0) return;
            int before = GetTradition(trad);
            ApplyTradition(trad, delta);
            int lords = affectLords ? ApplyLordRelation(trad, delta) : 0;

            SotorLog.Info($"RivalStanding: {trad} {before} -> {GetTradition(trad)} ({delta:+#;-#;0})"
                          + (affectLords ? $", {lords} lord(s) moved by {LordShareOf(delta):+#;-#;0} ({SotorSettings.StandingLordSharePercent}% share)." : "."));

            foreach (var other in SotorTraditions.ClanTraditions)
            {
                if (!spillToRivals) break;
                if (other == trad) continue;
                int aff = SotorTraditions.Affinity(trad, other);
                if (aff < 0)
                {

                    int spill = -(delta * -aff) / 4;
                    if (spill != 0)
                    {
                        ApplyTradition(other, spill);
                        if (affectLords) ApplyLordRelation(other, spill);
                    }
                }
            }

            foreach (var other in SotorTraditions.MemberOnlyTraditions)
            {
                if (!spillToRivals) break;
                int aff = SotorTraditions.Affinity(trad, other);
                if (aff < 0)
                {
                    int spill = -(delta * -aff) / 4;
                    if (spill != 0)
                    {
                        ApplyTradition(other, spill);
                        if (affectLords) ApplyLordRelation(other, spill);
                    }
                }
            }

            if (!silent)
            {

                var tradition = SotorTraditionObject.For(trad);
                Notify(trad, tradition, delta, lords);
            }
        }

        public static void ApplyLearningInfluence(Trad learned, bool isLore)
        {

            if (FeatureOff) return;
            if (!IsReady || learned == Trad.None) return;

            int baseAmount = isLore
                ? SotorTraditions.LearnLoreStanding
                : SotorTraditions.LearnSpellStanding;

            var gained = new List<Trad>();
            var lost = new List<Trad>();
            int gainedLords = 0, lostLords = 0;

            foreach (var reader in SotorTraditions.AllTraditions)
            {
                int delta = SotorTraditions.LearnStandingDelta(reader, learned, baseAmount);
                if (delta == 0) continue;

                ChangeTradition(reader, delta, silent: true);

                int lords = ApplyLearningRelationToLords(reader, delta);
                if (delta > 0) { gained.Add(reader); gainedLords += lords; }
                else { lost.Add(reader); lostLords += lords; }
            }

            string gainedLine = NotifyLearningBatch(gained, gainedLords, rose: true);
            string lostLine = NotifyLearningBatch(lost, lostLords, rose: false);
            ShowStandingPopup(gainedLine, lostLine);

            SotorLog.Info($"RivalStanding: learning {(isLore ? "LORE" : "spell")} {learned} moved "
                          + $"{gained.Count} tradition(s) up ({gainedLords} lords) and "
                          + $"{lost.Count} down ({lostLords} lords).");
        }

        private static int LordShareOf(int standingDelta)
        {
            int pct = SotorSettings.StandingLordSharePercent;
            if (pct <= 0 || standingDelta == 0) return 0;

            int delta = standingDelta * pct / 100;
            if (delta == 0) delta = standingDelta > 0 ? 1 : -1;
            return delta;
        }

        private static int ScaleByLordShare(int delta)
        {
            int pct = SotorSettings.StandingLordSharePercent;
            if (pct <= 0 || delta == 0) return 0;
            if (pct >= 100) return delta;

            int scaled = delta * pct / 100;
            if (scaled == 0) scaled = delta > 0 ? 1 : -1;
            return scaled;
        }

        private static int ApplyLordRelation(Trad trad, int standingDelta)
        {
            if (trad == Trad.None || standingDelta == 0) return 0;

            int delta = LordShareOf(standingDelta);
            if (delta == 0) return 0;
            int moved = 0;
            int actuallyChanged = 0;

            foreach (var hero in Hero.AllAliveHeroes)
            {
                if (hero == null || hero == Hero.MainHero) continue;
                if (!hero.IsLord || hero.IsChild || hero.IsNotable) continue;
                if (hero.Clan == Clan.PlayerClan) continue;
                if (!hero.IsAbilityUser()) continue;
                if (SotorRivalSeeder.SocialTradition(hero) != trad) continue;

                int before = (int)hero.GetRelationWithPlayer();

                ChangeRelationAction.ApplyPlayerRelation(hero, delta, affectRelatives: false, showQuickNotification: false);

                int after = (int)hero.GetRelationWithPlayer();
                if (after != before) actuallyChanged++;
                moved++;

                SotorLog.Info($"  RivalStanding lord: {hero.Name} ({trad}, {hero.Clan?.Name?.ToString() ?? "no clan"}) "
                              + $"relation {before} -> {after} (asked {delta:+#;-#;0}"
                              + (after - before != delta ? $", GOT {after - before:+#;-#;0}" : string.Empty) + ").");
            }

            SotorLog.Info($"RivalStanding: {trad} lord pass, {moved} matched the order, {actuallyChanged} actually moved "
                          + $"(asked {delta:+#;-#;0} each).");
            return moved;
        }

        private static int ApplyLearningRelationToLords(Trad reader, int traditionDelta)
        {
            int count = 0;
            int actuallyChanged = 0;
            foreach (var hero in Hero.AllAliveHeroes)
            {
                if (hero == null || hero == Hero.MainHero) continue;
                if (!hero.IsLord || hero.IsChild || hero.IsNotable) continue;
                if (hero.Clan == Clan.PlayerClan) continue;
                if (!hero.IsAbilityUser()) continue;
                if (SotorRivalSeeder.SocialTradition(hero) != reader) continue;

                count++;
                int level = SotorRivalSeeder.HeroCasterLevel(hero, hero.Clan?.Tier ?? 0);

                int delta = ScaleByLordShare(SotorTraditions.LearnRelationForLord(traditionDelta, level));
                int before = (int)hero.GetRelationWithPlayer();

                ChangeRelationAction.ApplyPlayerRelation(hero, delta, affectRelatives: false, showQuickNotification: false);

                int after = (int)hero.GetRelationWithPlayer();
                if (after != before) actuallyChanged++;

                SotorLog.Info($"  RivalStanding lord: {hero.Name} ({reader}, lvl {level}) "
                              + $"relation {before} -> {after} (asked {delta:+#;-#;0}"
                              + (after - before != delta ? $", GOT {after - before:+#;-#;0}" : string.Empty) + ").");
            }

            if (count > 0)
            {
                SotorLog.Info($"RivalStanding: {reader} learning pass, {count} matched the order, "
                              + $"{actuallyChanged} actually moved (tradition delta {traditionDelta:+#;-#;0}).");
            }
            return count;
        }

        private static string NotifyLearningBatch(List<Trad> traditions, int lordCount, bool rose)
        {
            if (traditions == null || traditions.Count == 0) return null;

            var names = new List<string>();
            foreach (var t in traditions)
            {
                var obj = SotorTraditionObject.For(t);
                names.Add(obj != null ? obj.Name.ToString() : t.ToString());
            }

            var line = rose
                ? SotorText.GetObject("sotor_standing_learn_gained")
                : SotorText.GetObject("sotor_standing_learn_lost");
            line.SetTextVariable("TRADITIONS", JoinNames(names, MaxNamesShown));
            line.SetTextVariable("LORD_COUNT", lordCount);
            string rendered = line.ToString();

            SotorLog.Info($"RivalStanding: learning {(rose ? "RAISED" : "LOWERED")} standing with "
                          + $"[{string.Join(",", names)}] across {lordCount} lord(s).");
            return rendered;
        }

        private static void ShowStandingPopup(string gainedLine, string lostLine)
        {
            if (string.IsNullOrEmpty(gainedLine) && string.IsNullOrEmpty(lostLine)) return;

            var body = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(gainedLine)) body.Append(gainedLine);
            if (!string.IsNullOrEmpty(lostLine))
            {

                if (body.Length > 0) body.Append(' ');
                body.Append(lostLine);
            }

            try
            {

                SotorRibbon.Show(new TextObject(body.ToString()), 5000);
                SotorLog.Info("RivalStanding: showed the standing banner.");
            }
            catch (System.Exception ex)
            {

                SotorLog.Error($"RivalStanding: standing banner failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private const int MaxNamesShown = 2;

        private static string JoinNames(List<string> names, int max)
        {
            if (names.Count == 0) return string.Empty;
            if (names.Count == 1) return names[0];
            if (names.Count == 2) return names[0] + " and " + names[1];

            var sb = new System.Text.StringBuilder();
            if (names.Count <= max)
            {
                for (int i = 0; i < names.Count; i++)
                {
                    if (i > 0) sb.Append(i == names.Count - 1 ? " and " : ", ");
                    sb.Append(names[i]);
                }
                return sb.ToString();
            }

            for (int i = 0; i < max; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(names[i]);
            }
            sb.Append(" and ").Append(names.Count - max).Append(" others");
            return sb.ToString();
        }

        private static void ApplyTradition(Trad trad, int delta)
        {
            int key = (int)trad;
            int cur = GetTradition(trad);
            _tradition[key] = Clamp(cur + delta);
            _traditionDay[key] = NowDays();
        }

        private static int Clamp(int v) => v < Min ? Min : (v > Max ? Max : v);

        private static void Notify(Trad trad, SotorTraditionObject tradition, int delta, int lords)
        {
            var name = tradition != null ? tradition.Name : new TextObject(trad.ToString());
            bool rose = delta > 0;

            TextObject line = lords > 0
                ? SotorText.GetObject(rose ? "sotor_standing_tradition_rose" : "sotor_standing_tradition_fell")
                : SotorText.GetObject(rose ? "sotor_standing_tradition_rose_only" : "sotor_standing_tradition_fell_only");

            line.SetTextVariable("TRADITION", name);

            line.SetTextVariable("AMOUNT", System.Math.Abs(delta));
            line.SetTextVariable("LORD_COUNT", lords);

            line.SetTextVariable("LORD_AMOUNT", System.Math.Abs(LordShareOf(delta)));

            string text = line.ToString();

            SotorRibbon.Show(new TextObject(text), 4000);
        }
    }
}
