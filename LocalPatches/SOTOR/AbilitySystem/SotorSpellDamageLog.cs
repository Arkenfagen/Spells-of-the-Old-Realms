using System.Collections.Generic;
using SOTOR.Extensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem
{

    public static class SotorSpellDamageLog
    {

        private const float FlushDelay = 2.0f;

        private sealed class Session
        {
            public Agent Caster;
            public string SpellName;
            public DamageType PrimaryDamageType = DamageType.Physical;
            public float LastBookTime;

            public int TotalDamage;
            public readonly HashSet<int> DamagedTargets = new HashSet<int>();
            public int Kills;

            public int TotalHealing;
            public readonly HashSet<int> HealedTargets = new HashSet<int>();

            public int TotalFriendlyFire;
            public readonly HashSet<int> FriendlyTargets = new HashSet<int>();
            public int FriendlyKills;
        }

        private static readonly Dictionary<string, Session> _sessions = new Dictionary<string, Session>();
        private static Mission _boundMission;

        private static string SessionKey(Agent caster, string spellName)
        {
            return caster.Index + "|" + (string.IsNullOrEmpty(spellName) ? "(unnamed)" : spellName);
        }

        public static void BookHit(Agent caster, Agent victim, DamageType damageType, int amount, bool killed, string spellName)
        {
            if (amount <= 0 || caster == null || victim == null || caster == victim) return;
            if (!SotorSettings.EnableSpellDamageLog) return;
            if (!IsPlayerOrMainParty(caster)) return;

            var session = GetOrCreate(caster, spellName);
            if (victim.IsEnemyOf(caster))
            {
                session.PrimaryDamageType = damageType;
                session.TotalDamage += amount;
                session.DamagedTargets.Add(victim.Index);
                if (killed) session.Kills++;
            }
            else
            {
                session.TotalFriendlyFire += amount;
                session.FriendlyTargets.Add(victim.Index);
                if (killed) session.FriendlyKills++;
            }
        }

        public static void BookHeal(Agent caster, Agent target, int amount, string spellName)
        {
            if (amount <= 0 || caster == null || target == null) return;
            if (!SotorSettings.EnableSpellDamageLog) return;
            if (!IsPlayerOrMainParty(caster)) return;

            var session = GetOrCreate(caster, spellName);
            session.TotalHealing += amount;
            session.HealedTargets.Add(target.Index);
        }

        private const float ShipEventThrottle = 3.0f;
        private static readonly Dictionary<string, float> _shipEventTimes = new Dictionary<string, float>();

        public static void BookShipEvent(Agent caster, DamageType damageType, string spellName, string message)
        {
            if (caster == null || string.IsNullOrEmpty(message)) return;
            if (!SotorSettings.EnableSpellDamageLog) return;
            if (!IsPlayerOrMainParty(caster)) return;

            RebindIfMissionChanged();
            string spell = string.IsNullOrEmpty(spellName) ? "Spell" : spellName;
            string key = caster.Index + "|" + spell + "|" + message;
            float now = MissionTime();
            if (_shipEventTimes.TryGetValue(key, out float last) && now - last < ShipEventThrottle)
            {
                return;
            }
            _shipEventTimes[key] = now;

            string icon = GetDamageTypeIcon(damageType);
            Post($"{icon} {spell} {message}", GetDamageTypeColor(damageType));
        }

        private static Session GetOrCreate(Agent caster, string spellName)
        {
            RebindIfMissionChanged();
            var key = SessionKey(caster, spellName);
            if (!_sessions.TryGetValue(key, out var session))
            {
                session = new Session { Caster = caster, SpellName = spellName };
                _sessions[key] = session;
            }
            session.LastBookTime = MissionTime();
            return session;
        }

        public static void FlushExpired(Mission mission)
        {
            if (mission == null) return;
            RebindIfMissionChanged();
            if (_sessions.Count == 0) return;

            float now = mission.CurrentTime;
            List<string> ready = null;
            foreach (var kv in _sessions)
            {
                if (now - kv.Value.LastBookTime >= FlushDelay)
                {
                    (ready ?? (ready = new List<string>())).Add(kv.Key);
                }
            }

            if (ready == null) return;
            foreach (var key in ready)
            {
                if (_sessions.TryGetValue(key, out var session))
                {
                    Emit(session);
                    _sessions.Remove(key);
                }
            }
        }

        public static void Reset()
        {
            _sessions.Clear();
            _shipEventTimes.Clear();
            _boundMission = null;
        }

        private static void RebindIfMissionChanged()
        {
            var current = Mission.Current;
            if (!ReferenceEquals(current, _boundMission))
            {
                _sessions.Clear();
                _shipEventTimes.Clear();
                _boundMission = current;
            }
        }

        private static float MissionTime()
        {
            var m = Mission.Current;
            return m != null ? m.CurrentTime : 0f;
        }

        private static bool IsPlayerOrMainParty(Agent caster)
        {
            if (caster == null) return false;
            if (caster == Agent.Main) return true;
            var hero = caster.GetHero();
            return hero != null && hero.PartyBelongedTo == MobileParty.MainParty;
        }

        private static void Emit(Session session)
        {
            string spellName = string.IsNullOrEmpty(session.SpellName) ? "Spell" : session.SpellName;

            if (session.TotalDamage > 0 && session.DamagedTargets.Count > 0)
            {
                DisplayAggregateSpellDamage(session.PrimaryDamageType, session.TotalDamage,
                    session.DamagedTargets.Count, session.Kills, spellName);
            }
            if (session.TotalHealing > 0 && session.HealedTargets.Count > 0)
            {
                DisplayAggregateSpellHealing(session.TotalHealing, session.HealedTargets.Count, spellName);
            }
            if (session.TotalFriendlyFire > 0 && session.FriendlyTargets.Count > 0)
            {
                DisplayAggregateSpellFriendlyFire(session.TotalFriendlyFire, session.FriendlyTargets.Count,
                    session.FriendlyKills, spellName);
            }
        }

        private static void DisplayAggregateSpellDamage(DamageType damageType, int totalDamage, int agentsAffected, int agentsKilled, string spellName)
        {
            string icon = GetDamageTypeIcon(damageType);
            string typeText = GetDamageTypeText(damageType);
            string target = agentsAffected == 1 ? "target" : "targets";
            string msg = agentsKilled > 0
                ? $"{icon} {spellName} dealt {totalDamage} {typeText} damage to {agentsAffected} {target}, {agentsKilled} eliminated"
                : $"{icon} {spellName} dealt {totalDamage} {typeText} damage to {agentsAffected} {target}";
            Post(msg, GetDamageTypeColor(damageType));
        }

        private static void DisplayAggregateSpellHealing(int totalHealing, int agentsAffected, string spellName)
        {
            string target = agentsAffected == 1 ? "target" : "targets";
            string msg = $"<img src=\"heart_icon\"/> {spellName} healed {totalHealing} health to {agentsAffected} {target}";
            Post(msg, Colors.Green);
        }

        private static void DisplayAggregateSpellFriendlyFire(int totalDamage, int agentsAffected, int agentsKilled, string spellName)
        {

            string target = agentsAffected == 1 ? "ally" : "allies";
            string msg = agentsKilled > 0
                ? $"<img src=\"screamingskull_icon\"/> {spellName} hit {agentsAffected} {target} for {totalDamage} friendly fire damage, {agentsKilled} killed"
                : $"<img src=\"screamingskull_icon\"/> {spellName} hit {agentsAffected} {target} for {totalDamage} friendly fire damage";
            Post(msg, Colors.Magenta);
        }

        private static void Post(string message, Color color)
        {
            try
            {
                InformationManager.DisplayMessage(new InformationMessage(message.TrimStart(), color));
            }
            catch (System.Exception ex)
            {
                SotorLog.Warn($"SotorSpellDamageLog.Post failed: {ex.Message}");
            }
        }

        private static Color GetDamageTypeColor(DamageType damageType)
        {
            switch (damageType)
            {
                case DamageType.Fire: return Colors.Red;
                case DamageType.Holy: return Colors.Yellow;
                case DamageType.Lightning: return Color.FromUint(5745663u);
                case DamageType.Magical: return Colors.Cyan;
                case DamageType.Frost: return Color.FromUint(8909823u);
                default: return Colors.White;
            }
        }

        private static string GetDamageTypeIcon(DamageType damageType)
        {
            string part;
            switch (damageType)
            {
                case DamageType.Fire: part = "traits_fire_icon"; break;
                case DamageType.Holy: part = "traits_holy_icon"; break;
                case DamageType.Lightning: part = "traits_lightning_icon"; break;
                case DamageType.Magical: part = "traits_magic_icon"; break;
                case DamageType.Frost: part = "traits_frost_icon"; break;
                default: part = null; break;
            }
            return string.IsNullOrEmpty(part) ? "" : $"<img src=\"{part}\"/>";
        }

        private static string GetDamageTypeText(DamageType damageType)
        {
            switch (damageType)
            {
                case DamageType.Fire: return "Fire";
                case DamageType.Holy: return "Holy";
                case DamageType.Lightning: return "Lightning";
                case DamageType.Magical: return "Magical";
                case DamageType.Frost: return "Frost";
                default: return "Physical";
            }
        }
    }
}
