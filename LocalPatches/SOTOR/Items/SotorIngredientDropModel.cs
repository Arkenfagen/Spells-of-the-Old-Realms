using System;
using System.Collections.Generic;
using SOTOR.AbilitySystem.Rivals;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace SOTOR.Items
{

    public static class SotorIngredientDropModel
    {

        private const float WeightLowTier = 0.5f;
        private const float WeightMidTier = 1.0f;
        private const float WeightHighTier = 2.0f;
        private const float WeightHero = 5.0f;

        private const float WeightBandit = 1.5f;

        private const float SecondaryLaneShare = 0.4f;

        private const float HideoutBattleMultiplier = 3.0f;

        public static SotorIngredientType TerrainLane(TerrainType terrain)
        {
            switch (terrain)
            {

                case TerrainType.Forest:
                case TerrainType.RuralArea:
                    return SotorIngredientType.AmberCrystal;

                case TerrainType.Desert:
                case TerrainType.Dune:
                case TerrainType.Mountain:
                case TerrainType.Canyon:
                case TerrainType.Cliff:
                    return SotorIngredientType.GemStone;

                case TerrainType.Steppe:
                case TerrainType.Swamp:
                    return SotorIngredientType.WarpstoneDust;

                case TerrainType.Snow:
                case TerrainType.Water:
                case TerrainType.CoastalSea:
                case TerrainType.OpenSea:
                case TerrainType.Lake:
                case TerrainType.River:
                case TerrainType.NonNavigableRiver:
                case TerrainType.Fording:
                case TerrainType.Beach:
                case TerrainType.Bridge:
                case TerrainType.UnderBridge:
                    return SotorIngredientType.BlessedWater;

                default:
                    return SotorIngredientType.GemStone;
            }
        }

        private static readonly SotorIngredientType[] PlainReagents =
        {
            SotorIngredientType.AmberCrystal,
            SotorIngredientType.GemStone,
            SotorIngredientType.WarpstoneDust,
        };

        public const float PlainRegionSize = 150f;

        public static SotorIngredientType PlainLane(Vec2 position)
        {
            float nx = position.X / PlainRegionSize;
            float ny = position.Y / PlainRegionSize;
            uint seed = WorldSeedSalt();

            int best = 0;
            float bestValue = float.MinValue;
            for (int i = 0; i < PlainReagents.Length; i++)
            {
                float v = ValueNoise(nx, ny, seed + (uint)(i * 0x9E3779B9));
                if (v > bestValue) { bestValue = v; best = i; }
            }
            return PlainReagents[best];
        }

        private static uint _worldSalt;
        private static string _worldSaltFor;

        private static uint WorldSeedSalt()
        {
            string text = SotorRivalSeeder.WorldSeedText() ?? "";
            if (_worldSaltFor == text) return _worldSalt;
            uint h = 2166136261u;
            foreach (char c in text) h = unchecked((h ^ c) * 16777619u);
            _worldSaltFor = text;
            _worldSalt = Avalanche(h);
            return _worldSalt;
        }

        private static uint Avalanche(uint h)
        {
            h ^= h >> 16; h = unchecked(h * 0x85ebca6bu);
            h ^= h >> 13; h = unchecked(h * 0xc2b2ae35u);
            h ^= h >> 16;
            return h;
        }

        private static float LatticeValue(int x, int y, uint salt)
        {
            uint h = 2166136261u;
            h = unchecked((h ^ (uint)x) * 16777619u);
            h = unchecked((h ^ (uint)y) * 16777619u);
            h = unchecked((h ^ salt) * 16777619u);
            return (Avalanche(h) & 0xFFFFFF) / (float)0x1000000;
        }

        private static float ValueNoise(float x, float y, uint salt)
        {
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            float fx = x - x0, fy = y - y0;
            float sx = fx * fx * (3f - 2f * fx);
            float sy = fy * fy * (3f - 2f * fy);

            float n00 = LatticeValue(x0, y0, salt);
            float n10 = LatticeValue(x0 + 1, y0, salt);
            float n01 = LatticeValue(x0, y0 + 1, salt);
            float n11 = LatticeValue(x0 + 1, y0 + 1, salt);

            float top = n00 + (n10 - n00) * sx;
            float bottom = n01 + (n11 - n01) * sx;
            return top + (bottom - top) * sy;
        }

        public const float SnowLyingThreshold = 0.55f;

        public static float SnowAt(Vec2 position)
        {
            try
            {
                Campaign.Current.Models.MapWeatherModel.GetSnowAndRainDataForPosition(
                    position, CampaignTime.Now, out float snow, out float _);
                return snow;
            }
            catch (Exception) { return 0f; }
        }

        public static SotorIngredientType LoreLane(Trad tradition)
        {
            switch (tradition)
            {
                case Trad.Beasts:
                case Trad.Life:
                    return SotorIngredientType.AmberCrystal;
                case Trad.Metal:
                case Trad.Fire:
                case Trad.Heavens:
                    return SotorIngredientType.GemStone;
                case Trad.Dark:
                case Trad.Necromancy:
                case Trad.Death:
                    return SotorIngredientType.WarpstoneDust;
                case Trad.Light:
                case Trad.High:
                    return SotorIngredientType.BlessedWater;
                default:
                    return SotorIngredientType.Invalid;
            }
        }

        public const float OpenGroundYield = 0.5f;

        public static SotorIngredientType PrimaryLaneFor(PartyBase party, TerrainType battleTerrain,
                                                        Vec2 battlePosition, out float yieldMultiplier)
        {
            yieldMultiplier = 1f;

            if (SnowAt(battlePosition) > SnowLyingThreshold) return SotorIngredientType.BlessedWater;

            var bandit = party?.MobileParty?.PartyComponent as BanditPartyComponent;
            var home = bandit?.Hideout?.Settlement;
            if (home != null)
            {
                try
                {
                    var pos = home.Position;
                    var homeTerrain = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(pos.Face);

                    if (homeTerrain == TerrainType.Plain)
                    {
                        yieldMultiplier = OpenGroundYield;
                        return PlainLane(pos.ToVec2());
                    }
                    return TerrainLane(homeTerrain);
                }
                catch (Exception) {  }
            }
            if (battleTerrain == TerrainType.Plain)
            {
                yieldMultiplier = OpenGroundYield;
                return PlainLane(battlePosition);
            }
            return TerrainLane(battleTerrain);
        }

        public static TerrainType BattleTerrain(MapEvent mapEvent)
        {
            try { return mapEvent != null ? mapEvent.EventTerrainType : TerrainType.Plain; }
            catch (Exception) { return TerrainType.Plain; }
        }

        public static float BattleMultiplier(MapEvent mapEvent)
        {
            try { return mapEvent != null && mapEvent.IsHideoutBattle ? HideoutBattleMultiplier : 1f; }
            catch (Exception) { return 1f; }
        }

        public static float BodyWeight(CharacterObject character)
        {
            if (character == null) return 0f;
            if (character.IsHero) return WeightHero;
            if (character.Culture != null && character.Culture.IsBandit) return WeightBandit;
            if (character.Tier >= 5) return WeightHighTier;
            if (character.Tier >= 3) return WeightMidTier;
            return WeightLowTier;
        }

        public static float GetDropFactor(CharacterObject character, SotorIngredientType ingredient,
                                          SotorIngredientType primaryLane, Trad partyLore = Trad.None,
                                          float primaryYield = 1f)
        {
            if (character == null) return 0f;
            float factor = 0f;
            var culture = character.Culture;
            bool isBandit = culture != null && culture.IsBandit;

            var lane = partyLore != Trad.None
                ? partyLore
                : (culture != null ? SotorCultureTraditions.DealtTraditionFor(culture) : Trad.None);
            var hero = character.IsHero ? character.HeroObject : null;
            float weight = BodyWeight(character);

            if (ingredient == primaryLane) factor += weight * primaryYield;
            if (ingredient == LoreLane(lane)) factor += weight * SecondaryLaneShare;

            switch (ingredient)
            {
                case SotorIngredientType.ArcaneScroll:
                    if (hero != null && hero.IsLord && hero.Clan != null
                        && SotorRivalSeeder.DeriveClanTradition(hero.Clan) != Trad.None)
                    {

                        factor += (SotorSettings.EnableRivalCasters && SotorRivalSeeder.HeroIsCasterPublic(hero)) ? 30f : 8f;
                    }
                    break;

                case SotorIngredientType.WarpstoneDust:
                    if (culture != null && culture.StringId == "sotor_skeleton") factor += 2.5f;
                    if (hero != null && CasterOfLane(hero, Trad.Dark, Trad.Necromancy)) factor += 2.5f;

                    if (isBandit) factor += 1.25f;
                    break;

                case SotorIngredientType.AmberCrystal:
                    if (hero != null && CasterOfLane(hero, Trad.Beasts, Trad.Life)) factor += 5f;
                    break;

                case SotorIngredientType.BlessedWater:
                    if (hero != null && CasterOfLane(hero, Trad.Light, Trad.Life)) factor += 5f;
                    break;

                case SotorIngredientType.DragonBlood:

                    if (hero != null && hero.IsLord) factor += DragonBloodScore(hero);
                    break;
            }
            return factor;
        }

        public static float SettlementScrollScore(Settlement settlement)
        {
            if (settlement == null) return 0f;
            try
            {

                if (settlement.IsVillage && settlement.Village != null)
                {

                    float hearth = Math.Max(0f, settlement.Village.Hearth);
                    return 20f * (1f + hearth / 600f);
                }
                float prosperity = settlement.Town != null ? Math.Max(0f, settlement.Town.Prosperity) : 0f;
                if (settlement.IsTown)

                    return 200f * (1f + prosperity / SotorBookShelf.RichProsperity);
                if (settlement.IsCastle)

                    return 80f * (1f + prosperity / SotorBookShelf.MidProsperity);
            }
            catch (Exception) { }
            return 0f;
        }

        private static readonly SotorIngredientType[] BodyLaneReagents =
        {
            SotorIngredientType.AmberCrystal, SotorIngredientType.GemStone,
            SotorIngredientType.WarpstoneDust, SotorIngredientType.BlessedWater,
        };

        public const float SettlementBodyKeep = 0.55f;

        public static float MeanRandom(SotorIngredientType ingredient)
            => (0.2f + RandomBandMax(ingredient)) / 2f;

        public static void ShiftBodyValueToScrolls(Dictionary<SotorIngredientType, float> scores,
                                                   float keepFraction)
        {
            if (scores == null) return;
            float movedUnits = 0f;
            foreach (var t in BodyLaneReagents)
            {
                if (!scores.TryGetValue(t, out float s) || s <= 0f) continue;
                float lost = s * (1f - keepFraction);
                scores[t] = s - lost;
                movedUnits += lost * DropAmplitude(t) * MeanRandom(t);
            }
            if (movedUnits <= 0f) return;

            float perScroll = DropAmplitude(SotorIngredientType.ArcaneScroll)
                              * MeanRandom(SotorIngredientType.ArcaneScroll);
            if (perScroll <= 0f) return;
            scores[SotorIngredientType.ArcaneScroll] =
                (scores.TryGetValue(SotorIngredientType.ArcaneScroll, out float sc) ? sc : 0f)
                + movedUnits / perScroll;
        }

        public static float DragonBloodScore(Hero hero)
        {
            int tier = hero != null && hero.Clan != null ? hero.Clan.Tier : 0;
            if (tier < 0) tier = 0;
            return 8f + tier * 6f;
        }

        public static Trad PartyLore(PartyBase party)
        {
            try
            {
                if (party == null) return Trad.None;

                var clan = party.MobileParty?.ActualClan ?? party.Owner?.Clan ?? party.Settlement?.OwnerClan;
                if (clan != null && !clan.IsBanditFaction)
                    return SotorRivalSeeder.DeriveClanTradition(clan);

                var home = (party.MobileParty?.PartyComponent as BanditPartyComponent)?.HomeSettlement;
                if (home == null) return Trad.None;

                var owner = home.OwnerClan;
                if (owner != null)
                    return owner.IsBanditFaction ? Trad.None : SotorRivalSeeder.DeriveClanTradition(owner);

                return home.Culture != null ? SotorCultureTraditions.DealtTraditionFor(home.Culture) : Trad.None;
            }
            catch (Exception) { return Trad.None; }
        }

        private static bool CasterOfLane(Hero hero, Trad a, Trad b)
        {
            if (hero?.Clan == null || !SotorRivalSeeder.HeroIsCasterPublic(hero)) return false;
            var trad = SotorRivalSeeder.DeriveClanTradition(hero.Clan);
            return trad == a || trad == b;
        }

        public static int CalculateResultAmount(float dropScore, SotorIngredientType ingredient,
                                                float percentageOfLoot, float ratePercent = 100f)
        {
            if (dropScore <= 0f) return 0;
            return (int)(dropScore * DropAmplitude(ingredient) * RandomMultiplier(ingredient)
                         * (percentageOfLoot / 100f) * (ratePercent / 100f) + 0.5f);
        }

        public static float DropAmplitude(SotorIngredientType ingredient)
        {
            switch (ingredient)
            {
                case SotorIngredientType.DragonBlood: return 0.05f;
                case SotorIngredientType.AmberCrystal: return 0.07f;
                case SotorIngredientType.WarpstoneDust: return 0.0375f;
                case SotorIngredientType.GemStone: return 0.10f;
                case SotorIngredientType.BlessedWater: return 0.06f;
                default: return 0.1f;
            }
        }

        public static float RandomBandMax(SotorIngredientType ingredient)
        {
            switch (ingredient)
            {
                case SotorIngredientType.ArcaneScroll: return 1.5f;
                case SotorIngredientType.DragonBlood: return 2f;
                case SotorIngredientType.AmberCrystal: return 1.5f;
                case SotorIngredientType.WarpstoneDust: return 3f;
                case SotorIngredientType.GemStone: return 1f;
                default: return 2f;
            }
        }

        private static float RandomMultiplier(SotorIngredientType ingredient)
        {
            return MBRandom.RandomFloatRanged(0.2f, RandomBandMax(ingredient));
        }

        public static float RecycleFraction(ItemObject item)
        {
            return item != null && item.IsCraftedByPlayer ? 0.33f : 0.5f;
        }
    }
}
