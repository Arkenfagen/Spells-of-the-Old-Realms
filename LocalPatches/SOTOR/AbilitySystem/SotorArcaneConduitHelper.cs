using System;
using SOTOR.Extensions;
using SOTOR.Extensions.ExtendedInfoSystem;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem
{

    public static class SotorArcaneConduitHelper
    {
        public const string AbilityId = "ArcaneConduit";

        public const string AbilityDisplayName = "Arcane Conduit";

        public const string WindsRegenStatusId = "arcane_conduit_winds_reg";
        public const string SlowStatusId = "arcane_conduit_slow";
        public const string VulnerabilityStatusId = "arcane_conduit_res_debuff";
        public const string DamageBuffStatusId = "arcane_conduit_dmg_buff";

        private static readonly float[] RegenPercentByPieces = { 0.02f, 0.025f, 0.03f, 0.035f, 0.04f };
        private const float MinRegenPerSec = 1f;

        public static int GetPieces(Hero hero)
        {
            if (hero == null)
            {
                return 0;
            }
            var level = SotorSpellcraftHelper.GetCastingLevel(hero);
            return CastingLevelToPieces(level);
        }

        public static int CastingLevelToPieces(SpellCastingLevel level)
        {
            switch (level)
            {
                case SpellCastingLevel.Entry: return 1;
                case SpellCastingLevel.Adept: return 2;
                case SpellCastingLevel.Master: return 3;
                case SpellCastingLevel.Archmage: return 4;
                default: return 0;
            }
        }

        public static string GetSpellbookLabel(SpellCastingLevel stagedLevel)
        {
            int uses = 1 + CastingLevelToPieces(stagedLevel);

            var label = SotorText.GetObject(uses == 1
                ? "sotor_sb_lbl_conduit_one"
                : "sotor_sb_lbl_conduit_many");
            label.SetTextVariable("COUNT", uses);
            return label.ToString();
        }

        public static string GetSpellbookValue(Hero hero, SpellCastingLevel stagedLevel)
        {
            int pieces = CastingLevelToPieces(stagedLevel);

            int regenPct = (int)System.Math.Round(GetRegenPercent(pieces) * 100f);

            const string nbsp = " ";
            var parts = new System.Collections.Generic.List<string> { regenPct + "%/s" };

            if (pieces < 4)
            {
                int slowPct = pieces >= 2 ? 25 : 50;
                int weakPct = pieces >= 1 ? 25 : 75;
                parts.Add("Slow" + nbsp + slowPct + "%");
                parts.Add("Weak" + nbsp + weakPct + "%");
            }

            if (pieces >= 3)
            {
                parts.Add("Dmg" + nbsp + "+30%");
            }

            return string.Join(" · ", parts);
        }

        public static float GetRegenPercent(int pieces)
        {
            int i = pieces < 0 ? 0 : (pieces >= RegenPercentByPieces.Length ? RegenPercentByPieces.Length - 1 : pieces);
            return RegenPercentByPieces[i];
        }

        public static float GetWindsRegenPerSec(Hero hero)
        {
            float maxWinds = hero?.GetExtendedInfo()?.MaxWindsOfMagic
                ?? (hero != null ? SotorSpellcraftHelper.GetMaxWinds(hero) : 100f);
            return ComputeRegenPerSec(GetPieces(hero), maxWinds);
        }

        public static float ComputeRegenPerSec(int pieces, float maxWinds)
        {
            return Math.Max(MinRegenPerSec, maxWinds * GetRegenPercent(pieces));
        }

        public static float GetSelfSlow(Hero hero)
        {
            int pieces = GetPieces(hero);
            if (pieces >= 4) return 0f;
            if (pieces >= 2) return -0.25f;
            return -0.5f;
        }

        public static float GetVulnerability(Hero hero)
        {
            int pieces = GetPieces(hero);
            if (pieces >= 4) return 0f;
            if (pieces >= 1) return -0.25f;
            return -0.75f;
        }

        public static float GetSpellDamageBonus(Hero hero)
        {
            return GetPieces(hero) >= 3 ? 0.30f : 0f;
        }

        public const float BaseChannelDuration = 10f;
        public static float GetChannelDuration(Hero hero)
        {
            return BaseChannelDuration * SotorSpellcraftHelper.GetSpellDurationFactor(hero);
        }

        public static int GetUsesPerBattle(Hero hero)
        {
            return 1 + GetPieces(hero);
        }

        public static int GetCooldown(Hero hero)
        {
            return GetPieces(hero) >= 4 ? 45 : 90;
        }

        public static bool ResetsOtherCooldowns(Hero hero)
        {
            return GetPieces(hero) >= 4;
        }
    }
}
