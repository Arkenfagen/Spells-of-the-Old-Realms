using System.Collections.Generic;
using HarmonyLib;
using SOTOR.AbilitySystem;
using SOTOR.Extensions;
using SOTOR.Extensions.ExtendedInfoSystem;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace SOTOR.AbilitySystem.Rivals
{

    public static class SotorRivalSeeder
    {

        private static readonly HashSet<string> AiUnsafeSpells = new HashSet<string>
        {

            "AmberSpearThrown",

            "BironasTimewarp",
            SotorArcaneConduitHelper.AbilityId,
        };

        public static bool IsAiSafeSpell(string abilityId) => !AiUnsafeSpells.Contains(abilityId);

        private static uint GameSalt()
        {
            if (SotorSettings.HasWorldSeed)
            {
                return (uint)SotorSettings.RivalWorldSeed.Trim().GetDeterministicHashCode();
            }
            return CampaignSalt();
        }

        public static string CampaignSeedText()
        {
            return Campaign.Current == null ? string.Empty : CampaignSalt().ToString("X8");
        }

        private static uint CampaignSalt()
        {
            var id = Campaign.Current?.UniqueGameId;
            return id == null ? 0u : (uint)id.GetDeterministicHashCode();
        }

        public static string WorldSeedText()
        {
            return SotorSettings.HasWorldSeed
                ? SotorSettings.RivalWorldSeed.Trim()
                : GameSalt().ToString("X8");
        }

        private static float HeroRoll(Hero hero, uint salt)
        {
            if (!SotorSettings.HasWorldSeed)
            {
                return hero.RandomFloatWithSeed(salt);
            }
            uint s1 = (uint)(hero.StringId ?? "").GetDeterministicHashCode() ^ salt;
            return MBRandom.RandomFloatWithSeed(s1, GameSalt());
        }

        public static float DiscoveryRoll(Hero hero)
        {
            return hero == null ? 1f : HeroRoll(hero, 0x00D15C0Fu);
        }

        private static float ClanRoll(Clan clan, uint salt)
        {
            uint s1 = (uint)clan.StringId.GetDeterministicHashCode() ^ salt;
            uint s2 = GameSalt();
            return MBRandom.RandomFloatWithSeed(s1, s2);
        }

        public static Trad DeriveClanTradition(Clan clan)
        {
            var pin = SotorRivalOverrides.ClanTraditionPin(clan);
            if (pin != Trad.None) return pin;

            var byCulture = SotorCultureTraditions.TraditionFor(clan?.Culture);
            if (byCulture != Trad.None) return byCulture;

            return SotorTraditions.ClanTraditionFromRoll(ClanRoll(clan, SotorTraditions.SaltClanTradition));
        }

        public static string SeededLoreFor(Hero hero, Clan clan)
        {
            string pin = SotorRivalOverrides.LordLorePin(hero);
            if (pin != null && !SotorTraditions.IsMemberOnly(SotorTraditions.TradForLore(pin))) return pin;
            return clan == null ? null : SotorTraditions.LoreIdFor(DeriveClanTradition(clan));
        }

        public static bool HasMemberOnlyPin(Hero hero)
        {
            string pin = SotorRivalOverrides.LordLorePin(hero);
            return pin != null && SotorTraditions.IsMemberOnly(SotorTraditions.TradForLore(pin))
                   && SotorRivalOverrides.LordCasterPin(hero) != false;
        }

        public static Trad SocialTradition(Hero hero)
        {
            if (hero == null) return Trad.None;

            if (!SotorSettings.EnableRivalCasters) return Trad.None;

            if (IsHiddenMaster(hero))
            {
                if (!SotorRivalReveal.IsReady || !SotorRivalReveal.IsRevealed(hero))
                {
                    return Trad.None;
                }
                foreach (var t in TeachableTraditions(hero))
                {
                    if (SotorTraditions.IsMemberOnly(t)) return t;
                }
            }

            var clan = hero.Clan;
            if (clan != null && clan != Clan.PlayerClan && IsCasterEligibleClan(clan))
            {
                var houseTrad = DeriveClanTradition(clan);

                if (hero.IsAbilityUser())
                {
                    var info = hero.GetExtendedInfo();
                    string houseLoreId = SotorTraditions.LoreIdFor(houseTrad);
                    bool knowsHouseSchool = info != null && houseLoreId != null && info.HasLore(houseLoreId);
                    if (!knowsHouseSchool)
                    {
                        var owned = HighestOwnedTradition(hero);
                        if (owned != Trad.None) return owned;
                    }
                }

                return houseTrad;
            }
            return HighestOwnedTradition(hero);
        }

        public static Trad HighestOwnedTradition(Hero hero)
        {
            var info = hero?.GetExtendedInfo();
            if (info == null) return Trad.None;
            Trad best = Trad.None;
            foreach (var loreId in info.AcquiredLores)
            {
                var t = SotorTraditions.TradForLore(loreId);
                if (t == Trad.None) continue;
                if (best == Trad.None || SotorTraditions.Rarity(t) > SotorTraditions.Rarity(best))
                {
                    best = t;
                }
            }
            return best;
        }

        public static bool IsHiddenMaster(Hero hero)
        {
            if (hero == null || !hero.IsAbilityUser()) return false;
            foreach (var t in TeachableTraditions(hero))
            {
                if (SotorTraditions.IsMemberOnly(t)) return true;
            }
            return false;
        }

        public static List<Trad> TeachableTraditions(Hero hero)
        {
            var list = new List<Trad>();
            var info = hero?.GetExtendedInfo();
            if (info == null) return list;
            foreach (var loreId in info.AcquiredLores)
            {
                var t = SotorTraditions.TradForLore(loreId);
                if (t != Trad.None && !list.Contains(t)) list.Add(t);
            }
            return list;
        }

        public static List<Trad> CoercibleTraditions(Hero hero)
        {
            var all = TeachableTraditions(hero);
            if (!IsHiddenMaster(hero)) return all;

            var forbiddenOnly = new List<Trad>();
            foreach (var t in all)
            {
                if (SotorTraditions.IsMemberOnly(t)) forbiddenOnly.Add(t);
            }

            return forbiddenOnly.Count > 0 ? forbiddenOnly : all;
        }

        public static bool IsCasterEligibleClan(Clan clan)
        {
            if (clan == null || clan.IsEliminated) return false;
            if (clan == Clan.PlayerClan) return false;
            if (clan.IsBanditFaction) return false;

            if (SotorRivalOverrides.IsMundaneClan(clan)) return false;
            if (!SotorRivalOverrides.HasClanPin(clan) && SotorRivalOverrides.IsMundaneCulture(clan.Culture)) return false;
            if (!SotorSettings.RivalIncludeMinorFactions && clan.IsMinorFaction) return false;
            if (clan.Tier < SotorSettings.RivalMinClanTierForCaster) return false;
            return true;
        }

        public static bool HeroIsCasterPublic(Hero hero)
        {
            bool? pin = SotorRivalOverrides.LordCasterPin(hero);
            if (pin.HasValue) return pin.Value;
            return SotorBloodlineMemo.IsCaster(hero);
        }

        public static bool PassesBaseCasterRoll(Hero hero)
        {
            if (hero == null) return false;
            return SotorTraditions.IsCasterFromRoll(
                HeroRoll(hero, SotorTraditions.SaltIsCaster), SotorSettings.RivalCasterLordShare);
        }

        public const float SeasonedCasterAge = 35f;

        public static int HeroCasterLevel(Hero hero, int ceiling)
        {

            int pinned = SotorRivalOverrides.LordLevelPin(hero);
            if (pinned > 0) return pinned;

            if (HoldsMemberOnlyLore(hero)) return SotorTraditions.MaxCasterLevel;

            bool oldEnough = hero.Age >= SeasonedCasterAge;
            bool cleverEnough = GetIntelligence(hero) >= 5;

            return SotorTraditions.CasterLevelFromScore(ceiling, oldEnough, cleverEnough,
                SotorSettings.RivalPowerShift);
        }

        private static int GetIntelligence(Hero hero)
        {
            try
            {
                return hero.GetAttributeValue(DefaultCharacterAttributes.Intelligence);
            }
            catch
            {
                return 0;
            }
        }

        public static bool IsSeedCandidateLord(Hero hero)
        {
            if (hero == null || !hero.IsAlive || hero.IsChild || hero.IsNotable) return false;
            if (hero.IsHumanPlayerCharacter) return false;
            if (!hero.IsLord) return false;
            var clan = hero.Clan;
            if (clan == null || clan == Clan.PlayerClan) return false;
            if (!IsCasterEligibleClan(clan)) return false;
            if (!SotorSettings.RivalIncludeRulers && hero.IsFactionLeader) return false;
            return true;
        }

        public static void GrantLoreToHero(Hero hero, string loreId, bool onlyAiSafe, int casterLevel = 0,
                                           bool allowStrip = true)
        {
            if (hero == null || loreId == null) return;
            var mgr = ExtendedInfoManager.Instance;
            if (mgr == null) return;

            hero.AddAttribute("AbilityUser");
            hero.AddAttribute("SpellCaster");
            var info = hero.GetExtendedInfo();
            if (info == null) return;

            if (!info.HasLore(loreId))
            {
                info.AddLore(loreId);
            }

            int stripped = 0;
            foreach (var template in AbilityFactory.GetTemplatesByLore(loreId))
            {
                string id = template?.StringID;
                if (id == null) continue;
                if (onlyAiSafe && !IsAiSafeSpell(id)) continue;

                if (casterLevel > 0 && !SotorTraditions.KnowsSpellTier(casterLevel, template.SpellTier))
                {

                    if (!allowStrip) continue;
                    if (info.HasSpell(id) || hero.HasAbility(id))
                    {
                        info.RemoveSpell(id);
                        info.AcquiredAbilities.Remove(id);
                        info.RemoveSelectedAbility(id);
                        stripped++;
                    }
                    continue;
                }

                if (!info.HasSpell(id)) info.AddSpell(id);
                if (!hero.HasAbility(id)) hero.AddAbility(id);
                info.AddSelectedAbility(id);
            }

            if (stripped > 0)
            {
                SotorLog.Info($"RivalCaster: stripped {stripped} above-tier spell(s) from {hero.Name} "
                              + $"(caster level {casterLevel} in {loreId}).");
            }

            EnsureCasterSkillAndPerks(hero, casterLevel);
        }

        public static void EnsureCasterSkillAndPerks(Hero hero, int casterLevel, bool allowDemote = false)
        {
            if (hero == null) return;

            bool memberOnly = HoldsMemberOnlyLore(hero);
            if (!memberOnly && casterLevel <= 0) return;

            try
            {
                var skill = SotorSkills.Spellcraft;
                if (skill == null || hero.HeroDeveloper == null) return;

                int target = memberOnly
                    ? SotorTraditions.SpellcraftMax

                    : SotorTraditions.SpellcraftForLevel(casterLevel, HeroRoll(hero, SotorTraditions.SaltSpellcraft));

                if (hero.GetSkillValue(skill) < target)
                {
                    hero.HeroDeveloper.SetInitialSkillLevel(skill, target);
                }
                else if (allowDemote && target < hero.GetSkillValue(skill) && IsDemotable(hero))
                {

                    int before = hero.GetSkillValue(skill);
                    hero.HeroDeveloper.SetInitialSkillLevel(skill, target);
                    int pulled = StripUnearnedCasterPerks(hero, target);
                    SotorLog.Info($"RivalCaster: {hero.Name} demoted {before} -> {target} Spellcraft "
                                  + $"({pulled} tier perk(s) removed) - world power dial.");
                }

                hero.HeroDeveloper.DevelopCharacterStats();

                if (memberOnly) EnsureArchmage(hero);
            }
            catch (System.Exception ex)
            {

                SotorLog.Error($"RivalCaster: skill/perk grant failed for {hero.Name}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        public static bool HoldsMemberOnlyLore(Hero hero)
        {
            var info = hero?.GetExtendedInfo();
            if (info == null) return false;
            return info.HasLore(SotorLores.DarkMagic) || info.HasLore(SotorLores.HighMagic);
        }

        private static bool IsDemotable(Hero hero)
        {
            if (hero == null || hero == Hero.MainHero) return false;
            if (Clan.PlayerClan != null
                && (hero.Clan == Clan.PlayerClan || hero.CompanionOf == Clan.PlayerClan)) return false;
            if (HoldsMemberOnlyLore(hero)) return false;
            if (SotorRivalOverrides.LordLevelPin(hero) > 0) return false;
            return true;
        }

        private static System.Reflection.MethodInfo _setPerkValue;
        private static bool _setPerkValueLookupDone;

        private static int StripUnearnedCasterPerks(Hero hero, int newSkillValue)
        {
            if (hero == null) return 0;
            if (!_setPerkValueLookupDone)
            {
                _setPerkValue = AccessTools.Method(typeof(Hero), "SetPerkValueInternal");
                _setPerkValueLookupDone = true;
                if (_setPerkValue == null)
                    SotorLog.Warn("RivalCaster: Hero.SetPerkValueInternal not found; caster perks cannot be demoted.");
            }
            if (_setPerkValue == null) return 0;

            var tiers = new[]
            {
                SotorPerks.EntrySpells, SotorPerks.AdeptSpells, SotorPerks.MasterSpells, SotorPerks.Archmage,
            };
            int removed = 0;
            foreach (var perk in tiers)
            {
                if (perk == null || !hero.GetPerkValue(perk)) continue;
                if (newSkillValue >= perk.RequiredSkillValue) continue;
                _setPerkValue.Invoke(hero, new object[] { perk, false });
                removed++;
            }
            return removed;
        }

        public static void EnsureArchmage(Hero hero)
        {
            if (hero?.HeroDeveloper == null) return;
            var tiers = new[]
            {
                SotorPerks.EntrySpells, SotorPerks.AdeptSpells, SotorPerks.MasterSpells, SotorPerks.Archmage,
            };
            int granted = 0;
            foreach (var perk in tiers)
            {
                if (perk == null) continue;
                if (hero.GetPerkValue(perk)) continue;
                hero.HeroDeveloper.AddPerk(perk);
                granted++;
            }
            if (granted > 0)
            {
                SotorLog.Info($"RivalCaster: {hero.Name} holds a member-only lore, forced to Archmage "
                              + $"({granted} tier perk(s) granted, Spellcraft {hero.GetSkillValue(SotorSkills.Spellcraft)}).");
            }
        }

        public static string CountSpells(Hero hero, string loreId, int casterLevel = 0)
        {
            var info = hero?.GetExtendedInfo();
            if (info == null || loreId == null) return "?";
            int have = 0, eligible = 0, total = 0;
            foreach (var template in AbilityFactory.GetTemplatesByLore(loreId))
            {
                string id = template?.StringID;
                if (id == null || !IsAiSafeSpell(id)) continue;
                total++;
                if (casterLevel > 0 && !SotorTraditions.KnowsSpellTier(casterLevel, template.SpellTier)) continue;
                eligible++;
                if (info.HasSpell(id)) have++;
            }

            return have + "/" + eligible + " of " + total;
        }

        public static bool HasLore(Hero hero, string loreId)
        {
            var info = hero?.GetExtendedInfo();
            return info != null && loreId != null && info.HasLore(loreId);
        }

        public static bool AlreadySeeded(Hero hero, string loreId, int casterLevel = 0)
        {
            var info = hero?.GetExtendedInfo();
            if (info == null || loreId == null) return false;
            if (!info.HasLore(loreId)) return false;

            foreach (var template in AbilityFactory.GetTemplatesByLore(loreId))
            {
                string id = template?.StringID;
                if (id == null || !IsAiSafeSpell(id)) continue;
                bool allowed = casterLevel <= 0 || SotorTraditions.KnowsSpellTier(casterLevel, template.SpellTier);
                bool has = info.HasSpell(id);

                if (allowed && !has) return false;

                if (!allowed && has) return false;
            }
            return true;
        }

        public static bool ClanHasMemberOnlyMaster(Clan clan, float chancePercent)
        {
            if (clan == null || chancePercent <= 0f)
            {
                return false;
            }
            return ClanRoll(clan, SotorTraditions.SaltMemberOnlyClan) < chancePercent / 100f;
        }

        public static bool HeroIsMemberOnlyMaster(Hero hero, float chancePercent)
        {

            if (HasMemberOnlyPin(hero)) return true;
            if (SotorRivalOverrides.LordCasterPin(hero) == false) return false;

            if (hero == null || chancePercent <= 0f)
            {
                return false;
            }
            return HeroRoll(hero, SotorTraditions.SaltMemberOnlyClan) < chancePercent / 100f;
        }

        public static bool IsSeededMemberOnlyMaster(Hero hero)
        {
            if (hero == null) return false;
            if (HasMemberOnlyPin(hero)) return true;
            var clan = hero.Clan;
            if (clan == null || clan.Tier < 5 || !IsCasterEligibleClan(clan)) return false;
            return HeroIsMemberOnlyMaster(hero, SotorSettings.RivalMemberOnlyLoreClanChance);
        }

        public static Trad MemberOnlyTraditionForHero(Hero hero)
        {
            if (HasMemberOnlyPin(hero))
            {
                return SotorTraditions.TradForLore(SotorRivalOverrides.LordLorePin(hero));
            }
            return SotorTraditions.MemberOnlyFromRoll(HeroRoll(hero, SotorTraditions.SaltMemberOnlyWhich));
        }

        public static Trad MemberOnlyTraditionFor(Clan clan)
        {
            return SotorTraditions.MemberOnlyFromRoll(ClanRoll(clan, SotorTraditions.SaltMemberOnlyWhich));
        }

        public static bool IsGeneticFounder(Hero hero)
        {
            if (hero == null) return true;
            try
            {
                var start = Campaign.Current?.Models?.CampaignTimeModel?.CampaignStartTime;
                if (start == null) return true;
                return hero.BirthDay.ToDays <= start.Value.ToDays;
            }
            catch
            {

                return true;
            }
        }

        public static SotorGenotype FounderGenotype(Hero hero)
        {
            if (hero == null) return new SotorGenotype(false, false);

            if (hero.IsHumanPlayerCharacter) return new SotorGenotype(true, true);

            if (PassesBaseCasterRoll(hero)) return new SotorGenotype(true, true);

            float share = SotorSettings.RivalCasterLordShare / 100f;
            if (share <= 0f) return new SotorGenotype(false, false);
            if (share > 1f) share = 1f;

            double p = System.Math.Sqrt(share);
            double carriers = 2.0 * p * (1.0 - p);
            double failures = 1.0 - share;
            double carrierShareOfFailures = failures > 0.0001 ? carriers / failures : 0.0;

            bool carrier = HeroRoll(hero, SotorTraditions.SaltAlleleA) < carrierShareOfFailures;
            if (!carrier) return new SotorGenotype(false, false);

            bool aIsM = HeroRoll(hero, SotorTraditions.SaltAlleleB) < 0.5f;
            return new SotorGenotype(aIsM, !aIsM);
        }

        public static bool InheritAllele(Hero child, Hero parent, SotorGenotype parentGenes)
        {
            if (child == null) return false;
            uint salt = SotorTraditions.SaltInheritance
                ^ (uint)((parent?.StringId ?? "").GetDeterministicHashCode());
            return HeroRoll(child, salt) < 0.5f ? parentGenes.A : parentGenes.B;
        }

        public const int WandererMaxCasterLevel = 3;

        public static int SpellGrantLevel(Hero hero)
        {
            int spellcraft = 0;
            try
            {
                var skill = SotorSkills.Spellcraft;
                if (skill != null) spellcraft = hero.GetSkillValue(skill);
            }
            catch
            {
                spellcraft = 0;
            }

            if (spellcraft >= 200) return 4;
            if (spellcraft >= 100) return 2;
            return 1;
        }

        public static int WandererCasterLevel(Hero hero)
        {

            int level = GetIntelligence(hero) - 1 + SotorSettings.RivalPowerShift;
            if (level < 1) level = 1;
            if (level > WandererMaxCasterLevel) level = WandererMaxCasterLevel;
            return level;
        }

        public static void GrantLoreOnlyToWanderer(Hero hero, string loreId)
        {
            if (hero == null || loreId == null) return;
            var mgr = ExtendedInfoManager.Instance;
            if (mgr == null) return;
            hero.AddAttribute("AbilityUser");
            hero.AddAttribute("SpellCaster");
            var info = hero.GetExtendedInfo();
            if (info == null) return;
            if (!info.HasLore(loreId))
            {
                info.AddLore(loreId);
            }
        }

        private static float WandererRoll(Hero hero, uint salt)
        {
            string key = hero?.Template?.StringId ?? hero?.StringId ?? "";
            uint s1 = (uint)key.GetDeterministicHashCode() ^ salt;
            return MBRandom.RandomFloatWithSeed(s1, GameSalt());
        }

        public static bool WandererIsCaster(Hero hero, float sharePercent)
        {
            return SotorTraditions.IsCasterFromRoll(WandererRoll(hero, SotorTraditions.SaltWandererIsCaster), sharePercent);
        }

        public static Trad WandererTradition(Hero hero)
        {
            return SotorTraditions.ClanTraditionFromRoll(WandererRoll(hero, SotorTraditions.SaltWandererTradition));
        }

        private const string SkeletonTroopId = "sotor_skeleton_warrior";

        public static bool IsNecromancerArmyLord(Hero hero)
        {
            if (hero == null || !hero.IsLord || !hero.IsAbilityUser()) return false;
            var clan = hero.Clan;
            if (clan == null || clan == Clan.PlayerClan || !IsCasterEligibleClan(clan)) return false;
            return DeriveClanTradition(clan) == Trad.Necromancy;
        }

        public static int SeedSkeletonContingent(Hero hero)
        {
            if (!IsNecromancerArmyLord(hero)) return 0;
            var party = hero.PartyBelongedTo;
            if (party == null) return 0;
            var troop = MBObjectManager.Instance.GetObject<CharacterObject>(SkeletonTroopId);
            if (troop == null) return 0;

            int level = HeroCasterLevel(hero, hero.Clan.Tier);
            int target = System.Math.Min(6 + level * 4, 30);
            int have = party.MemberRoster.GetTroopCount(troop);
            int add = target - have;
            if (add <= 0) return 0;
            party.MemberRoster.AddToCounts(troop, add);
            return add;
        }
    }
}
