using System.Collections.Generic;
using SOTOR.AbilitySystem.Rivals;
using SOTOR.Extensions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace SOTOR.CampaignBehaviors
{

    public class SotorRivalBehavior : CampaignBehaviorBase
    {

        private bool _schema1;

        private Dictionary<int, int> _traditionStanding = new Dictionary<int, int>();
        private Dictionary<int, float> _traditionStandingDay = new Dictionary<int, float>();

        private List<string> _lordPairSeeded = new List<string>();

        private List<string> _revealedMasters = new List<string>();

        private List<string> _coercedMasters = new List<string>();

        private List<string> _playerInvestedHeroes = new List<string>();

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.OnNewGameCreatedPartialFollowUpEvent.AddNonSerializedListener(this, OnNewGameCreatedPartialFollowUp);
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
            CampaignEvents.OnHeroUnregisteredEvent.AddNonSerializedListener(this, OnHeroUnregistered);
            CampaignEvents.OnPlayerMetHeroEvent.AddNonSerializedListener(this, OnPlayerMetHero);
            CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);
            CampaignEvents.HeroComesOfAgeEvent.AddNonSerializedListener(this, OnHeroComesOfAge);
            CampaignEvents.HeroPrisonerReleased.AddNonSerializedListener(this, OnHeroPrisonerReleased);
            CampaignEvents.OnMissionEndedEvent.AddNonSerializedListener(this, OnMissionEnded);
        }

        private void OnMissionEnded(IMission mission)
        {
            if (!SotorSettings.EnableRivalCasters) return;

            if (!SotorBattleAllyTally.PlayerWon)
            {
                SotorBattleAllyTally.Clear();
            }

            if (SotorBattleAllyTally.HasAny)
            {
                foreach (var ally in SotorBattleAllyTally.Take())
                {

                    SotorRivalStanding.ChangeTradition(ally.Tradition,
                        SotorTraditions.AssistCasterStanding, silent: true, affectLords: true,
                        spillToRivals: false);

                    var obj = SotorTraditionObject.For(ally.Tradition);
                    var line = SotorText.GetObject("sotor_standing_fought_alongside");
                    line.SetTextVariable("AMOUNT", SotorTraditions.AssistCasterStanding);
                    line.SetTextVariable("TRADITION",
                        obj != null ? obj.Name : new TextObject(ally.Tradition.ToString()));
                    line.SetTextVariable("NAME", ally.CasterName ?? string.Empty);
                    SotorRibbon.Show(line, 4000);

                    SotorLog.Info($"RivalStanding: fought alongside {ally.Tradition} ({ally.CasterName}).");
                }
            }

            if (!SotorRivalReveal.IsReady || !SotorRivalReveal.HasPendingAnnouncements) return;

            foreach (var hero in SotorRivalReveal.TakePendingAnnouncements())
            {
                if (hero == null) continue;
                var revealed = SotorRivalSeeder.SocialTradition(hero);
                var obj = SotorTraditionObject.For(revealed);

                var t = SotorText.GetObject("sotor_reveal_battle");
                t.SetTextVariable("NAME", hero.Name);
                t.SetTextVariable("TRADITION", obj != null ? obj.Name : new TextObject(revealed.ToString()));

                string line = t.ToString();
                SotorRibbon.Show(new TextObject(line), 4000);
                InformationManager.DisplayMessage(new InformationMessage(line));
                SotorLog.Info($"RivalReveal: announced {hero.Name} as {revealed} on the campaign map.");
            }
        }

        private void OnWeeklyTick()
        {
            if (!SotorSettings.EnableRivalCasters) return;

            SeedWorld("weekly");
        }

        private void OnHeroComesOfAge(Hero hero)
        {
            if (!SotorSettings.EnableRivalCasters || hero == null) return;

            float today = (float)CampaignTime.Now.ToDays;
            if (_lastComingOfAgeSeedDay > 0f && today - _lastComingOfAgeSeedDay < 1f) return;
            _lastComingOfAgeSeedDay = today;

            SeedWorld("came-of-age");
        }

        private float _lastComingOfAgeSeedDay;

        private void OnPlayerMetHero(Hero hero)
        {
            if (!SotorSettings.EnableRivalCasters || hero == null || hero == Hero.MainHero) return;
            if (!hero.IsLord || !hero.IsAbilityUser()) return;
            var kingdom = hero.Clan?.Kingdom;
            if (kingdom == null) return;

            var aTrad = SotorRivalSeeder.SocialTradition(hero);
            if (aTrad == Trad.None) return;

            foreach (var clan in kingdom.Clans)
            {
                foreach (var other in clan.AliveLords)
                {
                    if (other == hero || other == Hero.MainHero) continue;
                    if (!other.HasMet || !other.IsAbilityUser()) continue;
                    if (!PairIsNew(hero, other)) continue;
                    var bTrad = SotorRivalSeeder.SocialTradition(other);
                    if (bTrad == Trad.None) continue;

                    int delta = SotorTraditions.LordPairRelationDelta(
                        aTrad, bTrad, SotorSettings.RivalStrongTraditionRelations);
                    if (delta != 0)
                    {
                        ChangeRelationAction.ApplyRelationChangeBetweenHeroes(hero, other, delta, showQuickNotification: false);
                    }
                }
            }
        }

        private bool PairIsNew(Hero a, Hero b)
        {
            string key = string.CompareOrdinal(a.StringId, b.StringId) < 0
                ? a.StringId + "|" + b.StringId
                : b.StringId + "|" + a.StringId;
            if (_lordPairSeeded.Contains(key))
            {
                return false;
            }
            _lordPairSeeded.Add(key);
            return true;
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("SotorRivalData1", ref _schema1);

            dataStore.SyncData("SotorTraditionStanding", ref _traditionStanding);
            dataStore.SyncData("SotorTraditionStandingDay", ref _traditionStandingDay);
            dataStore.SyncData("SotorLordPairSeeded", ref _lordPairSeeded);
            dataStore.SyncData("SotorRevealedMasters", ref _revealedMasters);
            dataStore.SyncData("SotorCoercedMasters", ref _coercedMasters);
            dataStore.SyncData("SotorPlayerInvestedHeroes", ref _playerInvestedHeroes);

            if (_traditionStanding == null) _traditionStanding = new Dictionary<int, int>();
            if (_traditionStandingDay == null) _traditionStandingDay = new Dictionary<int, float>();
            if (_lordPairSeeded == null) _lordPairSeeded = new List<string>();
            if (_revealedMasters == null) _revealedMasters = new List<string>();

            if (_coercedMasters == null) _coercedMasters = new List<string>();

            if (_playerInvestedHeroes == null) _playerInvestedHeroes = new List<string>();
            BindStanding();
        }

        private void BindStanding()
        {
            SotorRivalStanding.Bind(_traditionStanding, _traditionStandingDay);
            SotorRivalReveal.Bind(_revealedMasters);
            SotorCoercionRecord.Bind(_coercedMasters);
            SotorPlayerInvestment.Bind(_playerInvestedHeroes);
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            BindStanding();
            SeedWorld("session-launched");
        }

        private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
        {
            SotorRivalReveal.Forget(victim);

            if (!SotorSettings.EnableRivalCasters) return;
            if (detail != KillCharacterAction.KillCharacterActionDetail.Executed) return;
            if (killer != Hero.MainHero || victim == null || !victim.IsAbilityUser()) return;

            var trad = SotorRivalSeeder.SocialTradition(victim);
            if (trad == Trad.None) return;

            SotorRivalStanding.ChangeTradition(trad,
                SotorTraditions.ExecuteCasterStanding, silent: false, affectLords: true);
            SotorLog.Info($"RivalStanding: player executed {victim.Name} of {trad}.");
        }

        private void OnHeroPrisonerReleased(Hero prisoner, PartyBase party, IFaction capturerFaction, EndCaptivityDetail detail, bool showNotification)
        {
            if (!SotorSettings.EnableRivalCasters) return;
            if (detail != EndCaptivityDetail.ReleasedByChoice) return;
            if (prisoner == null || !prisoner.IsAbilityUser()) return;
            if (capturerFaction == null || capturerFaction != Clan.PlayerClan.MapFaction) return;

            var trad = SotorRivalSeeder.SocialTradition(prisoner);
            if (trad == Trad.None) return;

            SotorRivalStanding.ChangeTradition(trad,
                SotorTraditions.FreeCasterStanding, silent: false, affectLords: true);
            SotorLog.Info($"RivalStanding: player released {prisoner.Name} of {trad}.");
        }

        private void OnHeroUnregistered(Hero hero)
        {
            SotorRivalReveal.Forget(hero);
        }

        private void OnNewGameCreatedPartialFollowUp(CampaignGameStarter starter, int index)
        {

            if (index == 99)
            {
                BindStanding();
                SeedWorld("new-game");
            }
        }

        private void LogPlayerArcaneState(string reason)
        {
            var info = Hero.MainHero?.GetExtendedInfo();
            if (info == null)
            {
                SotorLog.Info($"PlayerArcane ({reason}): no extended info yet.");
                return;
            }
            SotorLog.Info($"PlayerArcane ({reason}): lores=[{string.Join(",", info.AcquiredLores ?? new List<string>())}] "
                          + $"spells={info.AcquiredSpells?.Count ?? 0} "
                          + $"spellcraft={Hero.MainHero.GetSkillValue(AbilitySystem.SotorSkills.Spellcraft)}.");
        }

        public static string RegenerateWorld()
        {
            if (Campaign.Current == null) return null;
            var behavior = Campaign.Current.GetCampaignBehavior<SotorRivalBehavior>();
            if (behavior == null) return null;

            var wiped = AbilitySystem.Rivals.SotorRivalReset.Run();
            behavior.SeedWorld("settings-changed");

            var t = SotorText.GetObject("sotor_mcm_apply_done");
            t.SetTextVariable("REMOVED", wiped.Heroes);
            return t.ToString();
        }

        public static string RivalSettingsFingerprint()
        {
            return string.Join("|", new[]
            {
                SotorSettings.EnableRivalCasters.ToString(),
                SotorSettings.RivalCasterLordShare.ToString(),
                SotorSettings.RivalCasterWandererShare.ToString(),
                SotorSettings.RivalMemberOnlyLoreClanChance.ToString(),
                SotorSettings.RivalMinClanTierForCaster.ToString(),
                SotorSettings.RivalIncludeRulers.ToString(),
                SotorSettings.RivalIncludeMinorFactions.ToString(),
                SotorSettings.RivalWorldSeed ?? string.Empty,
            });
        }

        public static string LastBuiltFingerprint { get; private set; }

        private void SeedWorld(string reason)
        {
            if (!SotorSettings.EnableRivalCasters)
            {
                return;
            }
            if (Campaign.Current == null)
            {
                return;
            }

            bool routine = reason == "weekly" || reason == "came-of-age";

            try
            {

                AbilitySystem.Rivals.SotorRivalOverrides.Reload();

                if (!routine && !string.IsNullOrEmpty(AbilitySystem.Rivals.SotorRivalOverrides.LastLoadError))
                {
                    var warn = SotorText.GetObject("sotor_overrides_broken");
                    warn.SetTextVariable("ERROR", AbilitySystem.Rivals.SotorRivalOverrides.LastLoadError);
                    InformationManager.DisplayMessage(new InformationMessage(warn.ToString(), Colors.Red));
                }

                AbilitySystem.Rivals.SotorBloodlineMemo.Rebuild();

                if (!routine) LogPlayerArcaneState(reason);

                ReconcileOrphanedCasters();
                ReconcileOrphanedMasters();

                int lordCasters = SeedLords(quiet: routine);
                int memberOnly = SeedMemberOnlyMasters();

                int pinned = SeedPinnedLords();
                int houseSchools = GiveHiddenMastersTheirHouseSchool();
                int archmages = EnforceMemberOnlyArchmages();

                if (!routine) { LogCasterLevelSpread(); LogTraditionSpread(); }
                int wanderers = SeedWandererCasters(routine);
                EnsureWandererSkill();
                int undead = SeedNecromancerArmies();

                if (!routine) AbilitySystem.Rivals.SotorRivalOverrides.WriteLookupFile();

                if (routine && lordCasters == 0 && memberOnly == 0 && pinned == 0 && houseSchools == 0
                    && archmages == 0 && wanderers == 0 && undead == 0)
                {
                    LastBuiltFingerprint = RivalSettingsFingerprint();
                    return;
                }

                SotorLog.Info(
                    $"RivalCasters seeded ({reason}): {lordCasters} lord caster(s), {memberOnly} hidden Dark/High master(s), " +
                    $"{pinned} from player pins, " +
                    $"{archmages} forced to Archmage, {houseSchools} given a house school, {wanderers} tavern caster(s), {undead} skeleton(s) into necromancer parties. " +
                    $"LordShare={SotorSettings.RivalCasterLordShare}% " +
                    $"WandererShare={SotorSettings.RivalCasterWandererShare}% MemberOnlyChance={SotorSettings.RivalMemberOnlyLoreClanChance}%.");
                LastBuiltFingerprint = RivalSettingsFingerprint();
            }
            catch (System.Exception ex)
            {
                SotorLog.Error($"RivalCasters seeding failed ({reason}): {ex.GetType().Name}: {ex.Message}");
            }
        }

        private int SeedLords(bool quiet = false)
        {
            int count = 0;

            int clansSeen = 0, clansEligible = 0, lordsSeen = 0, notCandidate = 0, notCaster = 0, already = 0;

            int[] casterByParents = new int[3];
            int[] candidatesByParents = new int[3];

            foreach (var clan in Clan.All)
            {
                clansSeen++;
                if (!SotorRivalSeeder.IsCasterEligibleClan(clan))
                {
                    continue;
                }
                clansEligible++;
                var trad = SotorRivalSeeder.DeriveClanTradition(clan);
                string loreId = SotorTraditions.LoreIdFor(trad);
                if (loreId == null)
                {
                    continue;
                }

                foreach (var hero in clan.AliveLords)
                {
                    lordsSeen++;
                    if (!SotorRivalSeeder.IsSeedCandidateLord(hero))
                    {
                        notCandidate++;
                        continue;
                    }
                    int parents = AbilitySystem.Rivals.SotorBloodlineMemo.CasterParentCount(hero);
                    candidatesByParents[parents]++;

                    if (!SotorRivalSeeder.HeroIsCasterPublic(hero))
                    {
                        notCaster++;
                        continue;
                    }
                    casterByParents[parents]++;

                    string heroLoreId = SotorRivalSeeder.SeededLoreFor(hero, clan);
                    var heroTrad = SotorTraditions.TradForLore(heroLoreId);
                    if (heroLoreId == null) continue;

                    int level = SotorRivalSeeder.HeroCasterLevel(hero, clan.Tier);

                    SotorRivalSeeder.EnsureCasterSkillAndPerks(hero, level, allowDemote: true);

                    int grantLevel = SotorRivalSeeder.SpellGrantLevel(hero);

                    if (SotorRivalSeeder.AlreadySeeded(hero, heroLoreId, grantLevel))
                    {
                        already++;
                        continue;
                    }
                    SotorRivalSeeder.GrantLoreToHero(hero, heroLoreId, onlyAiSafe: true, casterLevel: grantLevel);

                    SotorLog.Info($"RivalCaster: {hero.Name} of {clan.Name} -> {heroTrad} (level {level}, clanTier {clan.Tier}, "
                                  + $"spells={SotorRivalSeeder.CountSpells(hero, heroLoreId, grantLevel)}, "
                                  + $"spellcraft={hero.GetSkillValue(AbilitySystem.SotorSkills.Spellcraft)}).");
                    count++;
                }
            }

            if (!quiet || count > 0)
            {
                SotorLog.Info($"RivalCaster funnel: clans={clansSeen} eligible={clansEligible} lords={lordsSeen} "
                              + $"notCandidate={notCandidate} failedCasterRoll={notCaster} alreadySeeded={already} granted={count}.");

                {
                    SotorLog.Info("RivalCaster bloodline: "
                        + $"0 parents {casterByParents[0]}/{candidatesByParents[0]}, "
                        + $"1 parent {casterByParents[1]}/{candidatesByParents[1]}, "
                        + $"2 parents {casterByParents[2]}/{candidatesByParents[2]} "
                        + $"(allele freq {System.Math.Sqrt(SotorSettings.RivalCasterLordShare / 100f):0.###}).");
                }
            }

            return count;
        }

        private int SeedMemberOnlyMasters()
        {
            float chance = SotorSettings.RivalMemberOnlyLoreClanChance;

            int count = 0;
            int eligible = 0;
            var perTradition = new Dictionary<Trad, int>();

            foreach (var clan in Clan.All)
            {
                if (chance <= 0f) break;
                if (!SotorRivalSeeder.IsCasterEligibleClan(clan) || clan.Tier < 5)
                {
                    continue;
                }

                foreach (var hero in clan.AliveLords)
                {
                    if (!SotorRivalSeeder.IsSeedCandidateLord(hero))
                    {
                        continue;
                    }
                    eligible++;

                    if (!SotorRivalSeeder.HeroIsMemberOnlyMaster(hero, chance))
                    {
                        continue;
                    }

                    var memberTrad = SotorRivalSeeder.MemberOnlyTraditionForHero(hero);
                    string loreId = SotorTraditions.LoreIdFor(memberTrad);
                    if (loreId == null || SotorRivalSeeder.AlreadySeeded(hero, loreId))
                    {
                        continue;
                    }
                    if (HasOtherMemberOnlyLore(hero, memberTrad))
                    {
                        continue;
                    }

                    if (!hero.IsAbilityUser())
                    {
                        string houseLoreId = SotorRivalSeeder.SeededLoreFor(hero, clan);
                        if (houseLoreId != null)
                        {
                            int houseLevel = SotorRivalSeeder.HeroCasterLevel(hero, clan.Tier);
                            SotorRivalSeeder.GrantLoreToHero(hero, houseLoreId, onlyAiSafe: true, casterLevel: houseLevel);
                        }
                    }

                    SotorRivalSeeder.GrantLoreToHero(hero, loreId, onlyAiSafe: true);
                    count++;
                    perTradition[memberTrad] = PerTraditionCount(perTradition, memberTrad) + 1;
                }
            }

            SotorLog.Info($"RivalCaster member-only roll: {eligible} eligible lord(s) in tier-5+ clans at "
                          + $"{chance}%, {count} rolled in before the floor.");

            foreach (var trad in SotorTraditions.MemberOnlyTraditions)
            {
                if (PerTraditionCount(perTradition, trad) > 0) continue;
                if (CountMemberOnlyMastersInWorld(trad) > 0) continue;
                if (SeedOneMemberOnlyMaster(trad)) count++;
            }

            return count;
        }

        private int SeedPinnedLords()
        {
            if (AbilitySystem.Rivals.SotorRivalOverrides.LordPinCount == 0
                && AbilitySystem.Rivals.SotorRivalOverrides.ClanPinCount == 0)
            {
                return 0;
            }

            int changed = 0;
            try
            {
                foreach (var hero in Hero.AllAliveHeroes)
                {
                    var pin = AbilitySystem.Rivals.SotorRivalOverrides.FindLordPin(hero);
                    if (pin == null) continue;
                    pin.Matched = true;

                    if (hero.IsChild) continue;

                    if (pin.Caster == false)
                    {
                        if (hero.IsAbilityUser()
                            && !SOTOR.Extensions.ExtendedInfoSystem.ExtendedInfoManager.IsPlayerSideCaster(hero)
                            && !AbilitySystem.Rivals.SotorPlayerInvestment.WasInvestedIn(hero))
                        {
                            int removed = AbilitySystem.Rivals.SotorRivalReset.StripHero(hero);
                            if (removed > 0)
                            {
                                changed++;
                                SotorLog.Info($"RivalCaster pin: {hero.Name} is pinned caster=\"false\"; took "
                                              + $"back {removed} lore(s).");
                            }
                        }
                        continue;
                    }

                    if (!SotorRivalSeeder.HeroIsCasterPublic(hero))
                    {
                        if (pin.LoreId != null || pin.Level > 0)
                        {
                            SotorLog.Info($"RivalCaster pin '{pin.IdOrName}': {hero.Name} sets lore/level but is "
                                          + "not a caster under this seed - add caster=\"true\" to force it.");
                        }
                        continue;
                    }

                    var clan = hero.Clan;
                    bool memberOnlyPin = SotorRivalSeeder.HasMemberOnlyPin(hero);

                    string everydayLoreId = SotorRivalSeeder.SeededLoreFor(hero, clan);
                    if (everydayLoreId == null && !memberOnlyPin)
                    {
                        SotorLog.Warn($"RivalCaster pin '{pin.IdOrName}': {hero.Name} has no clan to take a "
                                      + "lore from - add lore=\"...\" to the pin.");
                        continue;
                    }

                    int level = SotorRivalSeeder.HeroCasterLevel(hero, clan?.Tier ?? 3);
                    if (everydayLoreId != null)
                    {

                        SotorRivalSeeder.EnsureCasterSkillAndPerks(hero, level, allowDemote: true);
                        int grantLevel = SotorRivalSeeder.SpellGrantLevel(hero);
                        if (!SotorRivalSeeder.AlreadySeeded(hero, everydayLoreId, grantLevel))
                        {
                            SotorRivalSeeder.GrantLoreToHero(hero, everydayLoreId, onlyAiSafe: true, casterLevel: grantLevel);
                            changed++;
                            SotorLog.Info($"RivalCaster pin: {hero.Name} of {clan?.Name?.ToString() ?? "(clanless)"} -> "
                                          + $"{SotorTraditions.TradForLore(everydayLoreId)} (level {level}, "
                                          + $"spells={SotorRivalSeeder.CountSpells(hero, everydayLoreId, grantLevel)}, "
                                          + $"spellcraft={hero.GetSkillValue(AbilitySystem.SotorSkills.Spellcraft)}).");
                        }
                    }

                    if (memberOnlyPin && !SotorRivalSeeder.HasLore(hero, pin.LoreId))
                    {
                        SotorRivalSeeder.GrantLoreToHero(hero, pin.LoreId, onlyAiSafe: true);
                        changed++;
                        SotorLog.Info($"RivalCaster pin: {hero.Name} granted member-only "
                                      + $"{SotorTraditions.TradForLore(pin.LoreId)} (hidden master by player pin).");
                    }
                }

                foreach (var missed in AbilitySystem.Rivals.SotorRivalOverrides.UnmatchedLordPins())
                {
                    SotorLog.Warn($"RivalCaster pin '{missed.IdOrName}' matched no living hero - check it against "
                                  + "sotor_overrides_lookup.txt. Dead heroes, respawned wanderers and the player's "
                                  + "own clan all resolve to nobody.");
                }
                foreach (var missed in AbilitySystem.Rivals.SotorRivalOverrides.UnmatchedClanPins())
                {
                    SotorLog.Warn($"RivalCaster clan pin '{missed}' matched no clan - check it against "
                                  + "sotor_overrides_lookup.txt.");
                }
            }
            catch (System.Exception ex)
            {
                SotorLog.Error($"RivalCaster pinned-lord pass failed: {ex.GetType().Name}: {ex.Message}");
            }
            return changed;
        }

        private int ReconcileOrphanedCasters()
        {
            int stripped = 0, heroes = 0, trimmedHeroes = 0, trimmedLores = 0;
            try
            {
                foreach (var hero in Hero.AllAliveHeroes)
                {
                    if (hero == null || hero == Hero.MainHero || !hero.IsLord) continue;
                    if (!hero.IsAbilityUser()) continue;
                    if (SOTOR.Extensions.ExtendedInfoSystem.ExtendedInfoManager.IsPlayerSideCaster(hero)) continue;
                    if (AbilitySystem.Rivals.SotorPlayerInvestment.WasInvestedIn(hero)) continue;
                    if (SotorRivalSeeder.IsHiddenMaster(hero)) continue;

                    var clan = hero.Clan;
                    if (clan == null || clan == Clan.PlayerClan) continue;

                    bool mundane = AbilitySystem.Rivals.SotorRivalOverrides.IsMundaneClan(clan)
                                   || (!AbilitySystem.Rivals.SotorRivalOverrides.HasClanPin(clan)
                                       && AbilitySystem.Rivals.SotorRivalOverrides.IsMundaneCulture(clan.Culture));

                    bool pinnedCaster = AbilitySystem.Rivals.SotorRivalOverrides.LordCasterPin(hero) == true;

                    if (mundane && !pinnedCaster)
                    {
                        int took = AbilitySystem.Rivals.SotorRivalReset.StripHero(hero);
                        if (took > 0)
                        {
                            heroes++;
                            stripped += took;
                            SotorLog.Info($"RivalCaster mundane: took back {took} lore(s) from {hero.Name} "
                                          + $"({clan.Name}) - pinned to have no magic.");
                        }
                        continue;
                    }

                    if (!pinnedCaster && !SotorRivalSeeder.IsCasterEligibleClan(clan)) continue;

                    string houseLoreId = SotorRivalSeeder.SeededLoreFor(hero, clan);
                    var info = hero.GetExtendedInfo();
                    if (info == null || houseLoreId == null) continue;

                    if (SotorRivalSeeder.HeroIsCasterPublic(hero))
                    {
                        int extra = TrimLoresOtherThan(hero, info, houseLoreId);
                        if (extra > 0)
                        {
                            trimmedHeroes++;
                            trimmedLores += extra;
                        }
                        continue;
                    }

                    if (info.HasLore(houseLoreId))
                    {

                    }

                    int before = info.AcquiredLores?.Count ?? 0;
                    int removed = AbilitySystem.Rivals.SotorRivalReset.StripHero(hero);
                    if (removed > 0)
                    {
                        heroes++;
                        stripped += removed;
                    }
                    else if (before > 0)
                    {
                        heroes++;
                    }
                }

                if (heroes > 0)
                {
                    SotorLog.Info($"RivalCaster reconcile: took back magic from {heroes} orphaned lord(s) "
                                  + $"({stripped} lore(s)) who no longer pass the caster roll under this world "
                                  + "seed. They were left over from an earlier seeding.");
                }
                if (trimmedHeroes > 0)
                {
                    SotorLog.Info($"RivalCaster reconcile: trimmed {trimmedLores} extra lore(s) from "
                                  + $"{trimmedHeroes} caster(s) who also knew their own house school. These were "
                                  + "held over from a tradition their clan no longer teaches.");
                }
            }
            catch (System.Exception ex)
            {
                SotorLog.Error($"RivalCaster reconcile failed: {ex.GetType().Name}: {ex.Message}");
            }
            return heroes;
        }

        private static int TrimLoresOtherThan(Hero hero,
            SOTOR.Extensions.ExtendedInfoSystem.HeroExtendedInfo info, string keepLoreId)
        {
            int trimmed = 0;
            foreach (var loreId in new List<string>(info.AcquiredLores ?? new List<string>()))
            {
                if (loreId == keepLoreId || loreId == AbilitySystem.SotorLores.MinorMagic) continue;

                foreach (var template in AbilitySystem.AbilityFactory.GetTemplatesByLore(loreId))
                {
                    string id = template?.StringID;
                    if (id == null) continue;
                    if (info.HasSpell(id)) info.RemoveSpell(id);
                    info.AcquiredAbilities.Remove(id);
                    info.RemoveSelectedAbility(id);
                }

                info.RemoveLore(loreId);
                trimmed++;
                SotorLog.Info($"RivalCaster trim: removed '{loreId}' from {hero.Name} "
                              + $"(clan teaches '{keepLoreId}').");
            }
            return trimmed;
        }

        private int ReconcileOrphanedMasters()
        {
            int demoted = 0, trimmed = 0;
            try
            {
                foreach (var hero in Hero.AllAliveHeroes)
                {
                    if (hero == null || hero == Hero.MainHero || !hero.IsLord) continue;
                    if (!SotorRivalSeeder.IsHiddenMaster(hero)) continue;
                    if (SOTOR.Extensions.ExtendedInfoSystem.ExtendedInfoManager.IsPlayerSideCaster(hero)) continue;
                    if (AbilitySystem.Rivals.SotorPlayerInvestment.WasInvestedIn(hero)) continue;

                    var clan = hero.Clan;
                    if (clan == null || clan == Clan.PlayerClan) continue;

                    var info = hero.GetExtendedInfo();
                    if (info == null) continue;

                    float chance = SotorSettings.RivalMemberOnlyLoreClanChance;
                    bool stillRolls = SotorRivalSeeder.HasMemberOnlyPin(hero)
                                      || (clan.Tier >= 5
                                          && SotorRivalSeeder.IsCasterEligibleClan(clan)
                                          && SotorRivalSeeder.HeroIsMemberOnlyMaster(hero, chance));
                    if (!stillRolls)
                    {
                        AbilitySystem.Rivals.SotorRivalReset.StripHero(hero);
                        demoted++;
                        continue;
                    }

                    var keepTrad = SotorRivalSeeder.MemberOnlyTraditionForHero(hero);
                    string keepMemberOnly = SotorTraditions.LoreIdFor(keepTrad);
                    string keepHouse = SotorRivalSeeder.SeededLoreFor(hero, clan);

                    foreach (var loreId in new List<string>(info.AcquiredLores ?? new List<string>()))
                    {
                        if (loreId == keepMemberOnly || loreId == keepHouse) continue;
                        info.RemoveLore(loreId);
                        trimmed++;
                    }
                }

                if (demoted > 0 || trimmed > 0)
                {
                    SotorLog.Info($"RivalCaster reconcile (masters): demoted {demoted} lord(s) who no longer roll "
                                  + $"as hidden masters under this seed, and trimmed {trimmed} stale lore(s) from "
                                  + "the ones who still do.");
                }
            }
            catch (System.Exception ex)
            {
                SotorLog.Error($"RivalCaster master reconcile failed: {ex.GetType().Name}: {ex.Message}");
            }
            return demoted;
        }

        private void LogTraditionSpread()
        {
            try
            {
                var byTrad = new Dictionary<Trad, int>();
                foreach (var hero in Hero.AllAliveHeroes)
                {
                    if (hero == null || hero == Hero.MainHero || !hero.IsLord || !hero.IsAbilityUser()) continue;
                    var trad = SotorRivalSeeder.SocialTradition(hero);
                    if (trad == Trad.None) continue;
                    byTrad[trad] = (byTrad.TryGetValue(trad, out int n) ? n : 0) + 1;
                }

                var parts = new List<string>();
                foreach (var trad in SotorTraditions.AllTraditions)
                {
                    parts.Add($"{trad}={(byTrad.TryGetValue(trad, out int n) ? n : 0)}");
                }
                SotorLog.Info("RivalCaster tradition spread: " + string.Join(" ", parts));

                int mismatch = 0;
                var mismatchByTrad = new Dictionary<Trad, int>();
                foreach (var hero in Hero.AllAliveHeroes)
                {
                    if (hero == null || hero == Hero.MainHero || !hero.IsLord || !hero.IsAbilityUser()) continue;
                    var clan = hero.Clan;
                    if (clan == null || clan == Clan.PlayerClan || !SotorRivalSeeder.IsCasterEligibleClan(clan)) continue;
                    if (SotorRivalSeeder.IsHiddenMaster(hero)) continue;

                    string houseLoreId = SotorRivalSeeder.SeededLoreFor(hero, clan);
                    var info = hero.GetExtendedInfo();
                    if (houseLoreId == null || info == null) continue;

                    bool missingHouse = !info.HasLore(houseLoreId);
                    int extras = 0;
                    foreach (var loreId in info.AcquiredLores ?? new List<string>())
                    {
                        if (loreId == houseLoreId || loreId == AbilitySystem.SotorLores.MinorMagic) continue;
                        extras++;
                    }
                    if (!missingHouse && extras == 0) continue;

                    mismatch++;
                    var shown = SotorRivalSeeder.SocialTradition(hero);
                    mismatchByTrad[shown] = (mismatchByTrad.TryGetValue(shown, out int m) ? m : 0) + 1;
                    if (extras > 0)
                    {
                        SotorLog.Warn($"RivalCaster carried-over: {hero.Name} knows "
                                      + $"[{string.Join(",", info.AcquiredLores ?? new List<string>())}] "
                                      + $"but house '{clan.Name}' teaches '{houseLoreId}'.");
                    }
                }

                if (mismatch > 0)
                {
                    var mparts = new List<string>();
                    foreach (var kv in mismatchByTrad) mparts.Add($"{kv.Key}={kv.Value}");
                    SotorLog.Warn($"RivalCaster CARRIED-OVER lords: {mismatch} caster(s) do NOT know their own "
                                  + $"house school and are being listed under a lore they kept from an earlier "
                                  + $"seeding [{string.Join(" ", mparts)}]. A clean world should show none.");
                }
                else
                {
                    SotorLog.Info("RivalCaster carried-over lords: none - every caster knows his clan lore.");
                }
            }
            catch (System.Exception ex)
            {
                SotorLog.Warn($"Tradition spread log failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void LogCasterLevelSpread()
        {
            var byLevel = new Dictionary<int, int>();
            int masters = 0, archmages = 0, casters = 0;
            foreach (var hero in Hero.AllAliveHeroes)
            {
                if (hero == null || hero == Hero.MainHero || !hero.IsLord || !hero.IsAbilityUser()) continue;
                casters++;
                int lv = SotorRivalSeeder.HeroCasterLevel(hero, hero.Clan?.Tier ?? 0);
                byLevel[lv] = (byLevel.TryGetValue(lv, out int n) ? n : 0) + 1;

                int sc = hero.GetSkillValue(AbilitySystem.SotorSkills.Spellcraft);
                if (sc >= 300) archmages++;
                else if (sc >= 200) masters++;
            }

            var parts = new List<string>();
            for (int lv = 1; lv <= SotorTraditions.MaxCasterLevel; lv++)
            {
                parts.Add($"L{lv}:{(byLevel.TryGetValue(lv, out int n) ? n : 0)}");
            }

            foreach (var hero in Hero.AllAliveHeroes)
            {
                if (hero == null || hero == Hero.MainHero || !SotorRivalSeeder.IsHiddenMaster(hero)) continue;
                var hInfo = hero.GetExtendedInfo();
                SotorLog.Info($"RivalCaster hidden master: {hero.Name} "
                              + $"lores=[{string.Join(",", hInfo?.AcquiredLores ?? new List<string>())}] "
                              + $"spells={hInfo?.AcquiredSpells?.Count ?? 0} "
                              + $"revealed={SotorRivalReveal.IsRevealed(hero)} "
                              + $"publicTradition={SotorRivalSeeder.SocialTradition(hero)}.");
            }

            string dial = SotorSettings.RivalPowerShift != 0
                ? $" powerShift={SotorSettings.RivalPowerShift:+0;-0}" : "";
            SotorLog.Info($"RivalCaster levels: {casters} caster lord(s) {string.Join(" ", parts)}{dial} "
                          + $"| Spellcraft >=200 (Master): {masters}, >=300 (Archmage): {archmages}. "
                          + "Before the 2026-08-02 rewrite this read L1:138 L2:76 L3:2 with ZERO Masters.");
        }

        private int GiveHiddenMastersTheirHouseSchool()
        {
            int repaired = 0;
            foreach (var hero in Hero.AllAliveHeroes)
            {
                if (hero == null || hero == Hero.MainHero || hero.Clan == Clan.PlayerClan) continue;
                if (!SotorRivalSeeder.IsHiddenMaster(hero)) continue;

                var clan = hero.Clan;
                if (clan == null || !SotorRivalSeeder.IsCasterEligibleClan(clan)) continue;

                string houseLoreId = SotorRivalSeeder.SeededLoreFor(hero, clan);
                if (houseLoreId == null) continue;

                int level = SotorRivalSeeder.HeroCasterLevel(hero, clan.Tier);
                if (SotorRivalSeeder.AlreadySeeded(hero, houseLoreId, level)) continue;

                SotorRivalSeeder.GrantLoreToHero(hero, houseLoreId, onlyAiSafe: true, casterLevel: level);
                repaired++;
                SotorLog.Info($"RivalCaster: hidden master {hero.Name} also given his clan lore "
                              + $"{SotorTraditions.TradForLore(houseLoreId)}.");
            }
            return repaired;
        }

        private int EnforceMemberOnlyArchmages()
        {
            int changed = 0;
            foreach (var hero in Hero.AllAliveHeroes)
            {
                if (hero == null || hero == Hero.MainHero || hero.Clan == Clan.PlayerClan) continue;
                if (!SotorRivalSeeder.HoldsMemberOnlyLore(hero)) continue;

                bool wasArchmage = AbilitySystem.SotorPerks.Archmage != null
                    && hero.GetPerkValue(AbilitySystem.SotorPerks.Archmage);
                int before = hero.GetSkillValue(AbilitySystem.SotorSkills.Spellcraft);

                foreach (var loreId in new[] { AbilitySystem.SotorLores.DarkMagic, AbilitySystem.SotorLores.HighMagic })
                {
                    var info = hero.GetExtendedInfo();
                    if (info == null || !info.HasLore(loreId)) continue;
                    if (!SotorRivalSeeder.AlreadySeeded(hero, loreId))
                    {
                        SotorRivalSeeder.GrantLoreToHero(hero, loreId, onlyAiSafe: true);
                    }
                }

                SotorRivalSeeder.EnsureCasterSkillAndPerks(hero, 0);

                bool nowArchmage = AbilitySystem.SotorPerks.Archmage != null
                    && hero.GetPerkValue(AbilitySystem.SotorPerks.Archmage);
                int after = hero.GetSkillValue(AbilitySystem.SotorSkills.Spellcraft);
                if (!wasArchmage && nowArchmage || after != before) changed++;
            }
            return changed;
        }

        private static int PerTraditionCount(Dictionary<Trad, int> map, Trad t)
        {
            return map != null && map.TryGetValue(t, out int n) ? n : 0;
        }

        private static int CountMemberOnlyMastersInWorld(Trad trad)
        {
            string loreId = SotorTraditions.LoreIdFor(trad);
            if (loreId == null) return 0;
            int n = 0;
            foreach (var hero in Hero.AllAliveHeroes)
            {
                if (hero == null || hero == Hero.MainHero) continue;
                if (!hero.IsLord || hero.IsChild || hero.IsNotable) continue;
                if (hero.Clan == Clan.PlayerClan) continue;
                if (SotorRivalSeeder.HasLore(hero, loreId)) n++;
            }
            return n;
        }

        private static bool HasOtherMemberOnlyLore(Hero hero, Trad granting)
        {
            foreach (var other in SotorTraditions.MemberOnlyTraditions)
            {
                if (other == granting) continue;
                string otherLore = SotorTraditions.LoreIdFor(other);
                if (otherLore != null && SotorRivalSeeder.HasLore(hero, otherLore)) return true;
            }
            return false;
        }

        private bool SeedOneMemberOnlyMaster(Trad trad)
        {
            string loreId = SotorTraditions.LoreIdFor(trad);
            if (loreId == null) return false;

            Hero best = null;
            int bestScore = -1;
            foreach (var clan in Clan.All)
            {
                if (!SotorRivalSeeder.IsCasterEligibleClan(clan) || clan.Tier < 5) continue;
                foreach (var hero in clan.AliveLords)
                {
                    if (!SotorRivalSeeder.IsSeedCandidateLord(hero)) continue;
                    if (SotorRivalSeeder.AlreadySeeded(hero, loreId)) continue;
                    if (HasOtherMemberOnlyLore(hero, trad)) continue;

                    int score = (hero.IsAbilityUser() ? 1000 : 0)
                                + clan.Tier * 10 + SotorRivalSeeder.HeroCasterLevel(hero, clan.Tier);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = hero;
                    }
                }
            }

            if (best == null)
            {

                SotorLog.Info($"RivalCasters: no eligible host found for the {trad} floor; that tradition has no master this campaign.");
                return false;
            }

            if (!best.IsAbilityUser())
            {
                var houseTrad = SotorRivalSeeder.DeriveClanTradition(best.Clan);
                string houseLoreId = SotorTraditions.LoreIdFor(houseTrad);
                if (houseLoreId != null)
                {
                    int houseLevel = SotorRivalSeeder.HeroCasterLevel(best, best.Clan?.Tier ?? 0);
                    SotorRivalSeeder.GrantLoreToHero(best, houseLoreId, onlyAiSafe: true, casterLevel: houseLevel);
                }
            }

            SotorRivalSeeder.GrantLoreToHero(best, loreId, onlyAiSafe: true);
            SotorLog.Info($"RivalCasters: {trad} had no master from the clan rolls, seeded the floor onto {best.Name} (clan tier {best.Clan?.Tier}).");
            return true;
        }

        private void EnsureWandererSkill()
        {
            foreach (var hero in Hero.AllAliveHeroes)
            {
                if (hero == null || !hero.IsWanderer || hero.IsNotable || hero.IsChild) continue;

                if (Extensions.ExtendedInfoSystem.ExtendedInfoManager.IsPlayerSideCaster(hero)) continue;

                var info = hero.GetExtendedInfo();
                if (info == null || info.AcquiredLores == null || info.AcquiredLores.Count == 0) continue;

                int level = SotorRivalSeeder.WandererCasterLevel(hero);
                int before = hero.GetSkillValue(AbilitySystem.SotorSkills.Spellcraft);

                SotorRivalSeeder.EnsureCasterSkillAndPerks(hero, level, allowDemote: true);

                foreach (var loreId in new List<string>(info.AcquiredLores))
                {

                    SotorRivalSeeder.GrantLoreToHero(hero, loreId, onlyAiSafe: true,
                                                     casterLevel: SotorRivalSeeder.SpellGrantLevel(hero),
                                                     allowStrip: false);
                }

                int after = hero.GetSkillValue(AbilitySystem.SotorSkills.Spellcraft);
                if (after != before)
                {
                    SotorLog.Info($"RivalCaster: tavern caster {hero.Name} set to level {level} "
                                  + $"(spellcraft {before} -> {after}).");
                }
            }
        }

        private int SeedWandererCasters(bool quiet = false)
        {
            float share = SotorSettings.RivalCasterWandererShare;
            if (share <= 0f)
            {
                return 0;
            }

            int count = 0;
            foreach (var hero in Hero.AllAliveHeroes)
            {
                if (hero == null || !hero.IsWanderer || hero.IsNotable || hero.IsChild)
                {
                    continue;
                }

                if (hero.CompanionOf != null || hero.Clan != null)
                {
                    continue;
                }
                if (!SotorRivalSeeder.WandererIsCaster(hero, share))
                {
                    continue;
                }
                var trad = SotorRivalSeeder.WandererTradition(hero);
                string loreId = SotorTraditions.LoreIdFor(trad);

                if (loreId == null || SotorRivalSeeder.HasLore(hero, loreId))
                {
                    continue;
                }

                SotorRivalSeeder.GrantLoreOnlyToWanderer(hero, loreId);
                count++;
            }

            if (!quiet)
            {
                var names = new List<string>();
                foreach (var hero in Hero.AllAliveHeroes)
                {
                    if (hero == null || !hero.IsWanderer || hero.IsNotable || hero.IsChild) continue;
                    if (hero.CompanionOf != null || hero.Clan != null) continue;
                    if (!hero.IsAbilityUser()) continue;
                    var t = SotorRivalSeeder.SocialTradition(hero);
                    if (t == Trad.None) continue;
                    string where = hero.CurrentSettlement?.Name?.ToString() ?? "travelling";
                    names.Add($"{hero.Name} ({t}, {where})");
                }
                if (names.Count > 0)
                {
                    SotorLog.Info("RivalCaster tavern casters: " + string.Join("; ", names));
                }
            }

            return count;
        }

        private int SeedNecromancerArmies()
        {
            if (!SotorSettings.EnableSkeletonArmies)
            {
                return 0;
            }
            int total = 0;
            foreach (var hero in Hero.AllAliveHeroes)
            {
                if (SotorRivalSeeder.IsNecromancerArmyLord(hero))
                {
                    total += SotorRivalSeeder.SeedSkeletonContingent(hero);
                }
            }
            ReclaimOrphanedSkeletons();
            return total;
        }

        private void ReclaimOrphanedSkeletons()
        {
            int parties = 0, removed = 0;
            try
            {
                foreach (var hero in Hero.AllAliveHeroes)
                {
                    if (hero == null || hero == Hero.MainHero || !hero.IsLord) continue;
                    if (hero.Clan != null && hero.Clan == Clan.PlayerClan) continue;

                    var party = hero.PartyBelongedTo;
                    if (party == null || party == MobileParty.MainParty) continue;
                    if (party.LeaderHero != hero) continue;

                    if (SotorRivalSeeder.IsNecromancerArmyLord(hero)) continue;

                    if (AbilitySystem.SkeletonUpkeep.PartyHasNecromancer(party.Party)) continue;

                    int taken = 0;
                    foreach (var element in new List<TroopRosterElement>(party.MemberRoster.GetTroopRoster()))
                    {
                        var ch = element.Character;
                        if (ch == null || ch.IsHero || !AbilitySystem.SkeletonUpkeep.IsSkeletonChar(ch)) continue;

                        int n = element.Number;
                        if (n <= 0) continue;
                        party.MemberRoster.AddToCounts(ch, -n);
                        taken += n;
                    }

                    if (taken > 0)
                    {
                        parties++;
                        removed += taken;
                        SotorLog.Info($"RivalCaster skeleton reclaim: {taken} undead crumbled in '{party.Name}' "
                                      + $"({hero.Name}) - nobody there can command them.");
                    }
                }

                if (parties > 0)
                {
                    SotorLog.Info($"RivalCaster skeleton reclaim: {removed} undead removed from {parties} "
                                  + "party(ies) left over from an earlier seeding.");
                }
            }
            catch (System.Exception ex)
            {
                SotorLog.Error($"RivalCaster skeleton reclaim failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
