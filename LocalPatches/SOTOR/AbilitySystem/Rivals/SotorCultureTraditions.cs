using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace SOTOR.AbilitySystem.Rivals
{

    public static class SotorCultureTraditions
    {

        private const uint SaltCultureDeal = 0x5A17000Eu;

        private static readonly Dictionary<string, Trad> _map = new Dictionary<string, Trad>();
        private static string _fingerprint;

        private static string Fingerprint()
        {
            return SotorRivalSeeder.WorldSeedText()
                   + "|" + (SotorSettings.RivalLoreByCulture ? "1" : "0")
                   + "|" + SotorRivalOverrides.CulturePinCount
                   + "|" + SotorRivalOverrides.CulturePinFingerprint;
        }

        public static void Invalidate()
        {
            _map.Clear();
            _fingerprint = null;
        }

        public static Trad TraditionFor(CultureObject culture)
        {
            if (culture == null || string.IsNullOrEmpty(culture.StringId)) return Trad.None;

            var pinned = SotorRivalOverrides.CultureTraditionPin(culture);
            if (pinned != Trad.None) return pinned;

            if (!SotorSettings.RivalLoreByCulture) return Trad.None;

            EnsureBuilt();
            return _map.TryGetValue(culture.StringId, out var t) ? t : Trad.None;
        }

        public static Trad DealtTraditionFor(CultureObject culture)
        {
            if (culture == null || string.IsNullOrEmpty(culture.StringId)) return Trad.None;
            var pinned = SotorRivalOverrides.CultureTraditionPin(culture);
            if (pinned != Trad.None) return pinned;
            if (SotorRivalOverrides.IsMundaneCultureById(culture.StringId)) return Trad.None;
            EnsureBuilt();
            return _map.TryGetValue(culture.StringId, out var t) ? t : Trad.None;
        }

        private static void EnsureBuilt()
        {
            string fp = Fingerprint();
            if (_fingerprint == fp && _map.Count > 0) return;
            Build(fp);
        }

        private static void Build(string fingerprint)
        {
            _map.Clear();
            _fingerprint = fingerprint;
            if (Campaign.Current == null) return;

            var majors = new List<string>();
            var minors = new List<string>();
            try
            {
                var seen = new HashSet<string>();
                var isMajor = new Dictionary<string, bool>();
                foreach (var clan in Clan.All)
                {
                    if (clan == null || !SotorRivalSeeder.IsCasterEligibleClan(clan)) continue;
                    var id = clan.Culture?.StringId;
                    if (string.IsNullOrEmpty(id)) continue;
                    seen.Add(id);

                    bool major = !clan.IsMinorFaction;
                    isMajor[id] = isMajor.TryGetValue(id, out bool prev) ? (prev || major) : major;
                }

                foreach (var id in seen)
                {

                    if (SotorRivalOverrides.IsMundaneCultureById(id)) continue;
                    if (isMajor.TryGetValue(id, out bool m) && m) majors.Add(id);
                    else minors.Add(id);
                }
            }
            catch
            {
                return;
            }

            majors.Sort(string.CompareOrdinal);
            minors.Sort(string.CompareOrdinal);

            var cultures = new List<string>(majors);
            cultures.AddRange(minors);

            var order = new List<Trad>(SotorTraditions.ClanTraditions);
            Shuffle(order);

            for (int i = 0; i < cultures.Count; i++)
            {

                _map[cultures[i]] = order[i % order.Count];
            }

            foreach (var id in cultures)
            {
                var want = SotorRivalOverrides.CultureTraditionPinById(id);
                if (want == Trad.None) continue;

                var have = _map[id];
                if (have == want) continue;

                string holder = null;
                foreach (var other in cultures)
                {
                    if (other != id && _map[other] == want) { holder = other; break; }
                }
                if (holder != null) _map[holder] = have;
                _map[id] = want;
            }

            var parts = new List<string>();
            foreach (var kv in _map) parts.Add(kv.Key + "=" + kv.Value);
            parts.Sort(string.CompareOrdinal);
            SotorLog.Info($"CultureLores: dealt {_map.Count} culture(s) ({majors.Count} major, {minors.Count} minor) "
                          + $"[{string.Join(" ", parts.ToArray())}] (seed {SotorRivalSeeder.WorldSeedText()}).");
        }

        private static void Shuffle(List<Trad> list)
        {
            uint seed = (uint)(SotorRivalSeeder.WorldSeedText() ?? "").GetDeterministicHashCode() ^ SaltCultureDeal;
            for (int i = list.Count - 1; i > 0; i--)
            {
                float r = MBRandom.RandomFloatWithSeed((uint)i ^ seed, seed);
                int j = (int)(r * (i + 1));
                if (j < 0) j = 0;
                if (j > i) j = i;
                var tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}
