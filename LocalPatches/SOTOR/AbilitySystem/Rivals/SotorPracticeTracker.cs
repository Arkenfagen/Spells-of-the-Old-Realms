using System;
using SOTOR.Quests;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem.Rivals
{

    public static class SotorPracticeTracker
    {

        public enum Objective
        {
            Kills,
            HealedHitPoints,
            AlliesBuffed,
            EnemiesAfflicted,
            Casts,
        }

        private static readonly int[] KillTargets = { 5, 5, 10, 25, 50 };

        private static readonly int[] HealTargets = { 600, 600, 1000, 1600, 2400 };

        private static readonly int[] BuffTargets = { 50, 50, 80, 120, 150 };

        private static readonly int[] AfflictTargets = { 25, 25, 40, 60, 75 };

        private static readonly int[] CastTargets = { 5, 5, 5, 5, 5 };

        private static int Tiered(int[] table, int tier)
        {
            if (tier < 0) tier = 0;
            if (tier >= table.Length) tier = table.Length - 1;
            return table[tier];
        }

        public static Objective ObjectiveFor(AbilityTemplate template)
        {
            if (template == null) return Objective.Casts;

            var effect = string.IsNullOrEmpty(template.TriggeredEffectID)
                ? null
                : TriggeredEffectManager.GetTemplate(template.TriggeredEffectID);

            if (effect != null && effect.DamageAmount > 0) return Objective.Kills;

            switch (template.AbilityEffectType)
            {
                case AbilityEffectType.Heal:
                    return Objective.HealedHitPoints;
                case AbilityEffectType.Augment:
                    return Objective.AlliesBuffed;
                case AbilityEffectType.Hex:
                    return Objective.EnemiesAfflicted;
                default:
                    return Objective.Casts;
            }
        }

        public static int TargetFor(AbilityTemplate template)
        {
            int tier = template?.SpellTier ?? 1;
            switch (ObjectiveFor(template))
            {
                case Objective.Kills: return Tiered(KillTargets, tier);
                case Objective.HealedHitPoints: return Tiered(HealTargets, tier);
                case Objective.AlliesBuffed: return Tiered(BuffTargets, tier);
                case Objective.EnemiesAfflicted: return Tiered(AfflictTargets, tier);
                default: return Tiered(CastTargets, tier);
            }
        }

        public static string TaskStringId(Objective objective)
        {
            switch (objective)
            {
                case Objective.Kills: return "sotor_practice_task_kills";
                case Objective.HealedHitPoints: return "sotor_practice_task_healed";
                case Objective.AlliesBuffed: return "sotor_practice_task_buffed";
                case Objective.EnemiesAfflicted: return "sotor_practice_task_afflicted";
                default: return "sotor_practice_task_casts";
            }
        }

        public static string CounterStringId(Objective objective)
        {
            switch (objective)
            {
                case Objective.Kills: return "sotor_practice_counter_kills";
                case Objective.HealedHitPoints: return "sotor_practice_counter_healed";
                case Objective.AlliesBuffed: return "sotor_practice_counter_buffed";
                case Objective.EnemiesAfflicted: return "sotor_practice_counter_afflicted";
                default: return "sotor_practice_counter_casts";
            }
        }

        private static bool IsPlayerCaster(Agent caster)
        {
            if (caster == null || Campaign.Current == null) return false;
            if (caster.IsMainAgent) return true;
            return Extensions.AgentExtensions.GetHero(caster) == Hero.MainHero;
        }

        private static SotorPracticeQuest QuestFor(Agent caster, string spellId)
        {
            if (string.IsNullOrEmpty(spellId) || !IsPlayerCaster(caster)) return null;
            return SotorPracticeQuest.ActiveForSpell(spellId);
        }

        public static void NoteKill(Agent caster, string spellId)
        {
            QuestFor(caster, spellId)?.Advance(1);
        }

        public static void NoteHealed(Agent caster, string spellId, int amount)
        {
            if (amount <= 0) return;
            QuestFor(caster, spellId)?.Advance(amount);
        }

        public static void NoteAllyBuffed(Agent caster, string spellId, int allies)
        {
            if (allies <= 0) return;
            QuestFor(caster, spellId)?.Advance(allies);
        }

        public static void NoteEnemyAfflicted(Agent caster, string spellId, int enemies)
        {
            if (enemies <= 0) return;
            QuestFor(caster, spellId)?.Advance(enemies);
        }

        public static void NoteCast(Agent caster, string spellId)
        {
            var quest = QuestFor(caster, spellId);
            if (quest == null) return;

            if (quest.Objective != Objective.Casts) return;
            quest.Advance(1);
        }
    }
}
