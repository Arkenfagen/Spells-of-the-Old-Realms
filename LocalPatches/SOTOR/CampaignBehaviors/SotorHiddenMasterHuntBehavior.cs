using System.Collections.Generic;
using SOTOR.AbilitySystem;
using SOTOR.AbilitySystem.Rivals;
using SOTOR.Extensions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace SOTOR.CampaignBehaviors
{

    public class SotorHiddenMasterHuntBehavior : CampaignBehaviorBase
    {

        private const float WhisperChance = 0.25f;

        private List<string> _clanLeads = new List<string>();
        private List<string> _suspects = new List<string>();

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("_sotorHmClanLeads", ref _clanLeads);
            dataStore.SyncData("_sotorHmSuspects", ref _suspects);
            if (_clanLeads == null) _clanLeads = new List<string>();
            if (_suspects == null) _suspects = new List<string>();

        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            AddDialogs(starter);
            LogDiscoverySurvey();
        }

        private void LogDiscoverySurvey()
        {
            try
            {
                if (!SotorSettings.EnableRivalCasters || !SotorRivalReveal.IsReady) return;

                var masters = new List<Hero>();
                foreach (var hero in Hero.AllAliveHeroes)
                {
                    if (IsDiscoverableMaster(hero)) masters.Add(hero);
                }
                if (masters.Count == 0)
                {
                    SotorLog.Info("HiddenMasterHunt survey: no unrevealed hidden masters left in the world.");
                    return;
                }

                SotorLog.Info($"HiddenMasterHunt survey: {masters.Count} unrevealed hidden master(s).");
                foreach (var master in masters)
                {
                    string clanName = master.Clan?.Name?.ToString() ?? "no clan";
                    string realm = master.MapFaction?.Name?.ToString() ?? "no kingdom";
                    SotorLog.Info($"  MASTER {master.Name} [{clanName}, {realm}] "
                                  + $"{SotorRivalSeeder.MemberOnlyTraditionForHero(master)}");

                    var kin = new List<string>();
                    if (master.Clan != null)
                    {
                        foreach (var hero in master.Clan.Heroes)
                        {
                            if (hero == null || hero == master || !hero.IsAlive) continue;
                            kin.Add(hero.Name.ToString());
                        }
                    }
                    SotorLog.Info($"    confirm outright ({kin.Count}): "
                                  + (kin.Count == 0 ? "nobody" : string.Join(", ", kin)));

                    var whisperers = new List<string>();
                    foreach (var hero in Hero.AllAliveHeroes)
                    {
                        if (hero == null || hero == Hero.MainHero || !hero.IsLord) continue;
                        if (hero.Clan != null && hero.Clan == Clan.PlayerClan) continue;
                        if (hero.MapFaction != master.MapFaction || hero.Clan == master.Clan) continue;
                        if (SotorRivalSeeder.DiscoveryRoll(hero) >= WhisperChance) continue;
                        if (FindKingdomMaster(hero) != master) continue;
                        whisperers.Add(hero.Name.ToString());
                    }
                    SotorLog.Info($"    whisper his clan ({whisperers.Count}): "
                                  + (whisperers.Count == 0 ? "nobody" : string.Join(", ", whisperers)));
                }

                if (_clanLeads.Count > 0)
                {
                    SotorLog.Info($"  leads already held ({_clanLeads.Count}): {string.Join(", ", _clanLeads)}");
                }
            }
            catch (System.Exception ex)
            {
                SotorLog.Warn($"HiddenMasterHunt survey failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void AddDialogs(CampaignGameStarter starter)
        {

            starter.AddPlayerLine("sotor_hm_interrogate", "hero_main_options", "sotor_hm_interrogate_reply",
                SotorText.Get("sotor_hm_interrogate"),
                IsInterrogatableNonWizardCaptive, null, 105);
            starter.AddPlayerLine("sotor_hm_interrogate_alt", "prisoner_recruit_start_player", "sotor_hm_interrogate_reply",
                SotorText.Get("sotor_hm_interrogate"),
                IsInterrogatableNonWizardCaptive, null, 105);

            starter.AddDialogLine("sotor_hm_captive_bargain", "sotor_hm_interrogate_reply", "sotor_hm_bargain_fork",
                SotorText.Get("sotor_hm_captive_bargain"),
                CaptiveHasKnowledge, null, 120);

            starter.AddDialogLine("sotor_hm_captive_nothing_root", "sotor_hm_interrogate_reply", "sotor_prisoner_teach_fork",
                SotorText.Get("sotor_hm_captive_nothing"),
                SotorPrisonerTeachBehavior.IsCapturedWizardWithLore, null, 95);
            starter.AddDialogLine("sotor_hm_captive_nothing", "sotor_hm_interrogate_reply", "close_window",
                SotorText.Get("sotor_hm_captive_nothing"),
                null, null, 90);

            starter.AddPlayerLine("sotor_hm_bargain_accept", "sotor_hm_bargain_fork", "sotor_hm_bargain_tell",
                SotorText.Get("sotor_hm_bargain_accept"),
                null, null, 110);
            starter.AddPlayerLine("sotor_hm_bargain_refuse_root", "sotor_hm_bargain_fork", "sotor_prisoner_teach_fork",
                SotorText.Get("sotor_hm_bargain_refuse"),
                SotorPrisonerTeachBehavior.IsCapturedWizardWithLore, null, 105);
            starter.AddPlayerLine("sotor_hm_bargain_refuse", "sotor_hm_bargain_fork", "close_window",
                SotorText.Get("sotor_hm_bargain_refuse"),
                null, null, 100);

            starter.AddDialogLine("sotor_hm_captive_confirms", "sotor_hm_bargain_tell", "close_window",
                SotorText.Get("sotor_hm_captive_confirms"),
                CaptiveCanConfirm, OnCaptiveConfirms, 120);
            starter.AddDialogLine("sotor_hm_captive_whisper", "sotor_hm_bargain_tell", "close_window",
                SotorText.Get("sotor_hm_captive_whisper"),
                CaptiveHasWhisper, OnCaptiveWhisper, 110);

            starter.AddPlayerLine("sotor_hm_confront_ask", "hero_main_options", "sotor_hm_confront_reply",
                SotorText.Get("sotor_hm_confront_ask"),
                CanConfront, null, 104);

            starter.AddDialogLine("sotor_hm_confront_admit", "sotor_hm_confront_reply", "hero_main_options",
                SotorText.Get("sotor_hm_confront_admit"),
                null, OnConfrontAdmitted, 110);
        }

        public static bool IsInterrogatableCaptiveNow()
        {
            if (!SotorSettings.EnableRivalCasters) return false;
            var hero = Hero.OneToOneConversationHero;
            if (hero == null || hero == Hero.MainHero) return false;
            var ch = CharacterObject.OneToOneConversationCharacter;
            if (ch == null || !MobileParty.MainParty.PrisonRoster.Contains(ch)) return false;
            return AnyUnrevealedMasterExists();
        }

        private static bool IsInterrogatableNonWizardCaptive()
        {
            return IsInterrogatableCaptiveNow()
                   && !SotorPrisonerTeachBehavior.IsCapturedWizardWithLore();
        }

        private static bool IsDiscoverableMaster(Hero hero)
        {
            if (hero == null || hero == Hero.MainHero) return false;
            if (hero.Clan != null && hero.Clan == Clan.PlayerClan) return false;
            if (!SotorRivalReveal.IsReady) return false;
            return SotorRivalSeeder.IsHiddenMaster(hero) && !SotorRivalReveal.IsRevealed(hero);
        }

        private static bool AnyUnrevealedMasterExists()
        {
            if (!SotorRivalReveal.IsReady) return false;
            foreach (var hero in Hero.AllAliveHeroes)
            {
                if (IsDiscoverableMaster(hero)) return true;
            }
            return false;
        }

        private static Hero FindClanMaster(Hero captive)
        {
            if (captive?.Clan == null || !SotorRivalReveal.IsReady) return null;
            foreach (var hero in captive.Clan.Heroes)
            {
                if (hero == null || hero == captive || !hero.IsAlive) continue;
                if (IsDiscoverableMaster(hero)) return hero;
            }
            return null;
        }

        private static Hero FindKingdomMaster(Hero captive)
        {
            if (captive?.MapFaction == null || !SotorRivalReveal.IsReady) return null;
            Hero best = null;
            foreach (var hero in Hero.AllAliveHeroes)
            {
                if (!IsDiscoverableMaster(hero)) continue;
                if (hero.MapFaction != captive.MapFaction || hero.Clan == captive.Clan) continue;
                if (best == null || string.CompareOrdinal(hero.StringId, best.StringId) < 0) best = hero;
            }
            return best;
        }

        private bool CaptiveHasKnowledge()
        {
            return CaptiveCanConfirm() || CaptiveHasWhisper();
        }

        private bool CaptiveCanConfirm()
        {
            var captive = Hero.OneToOneConversationHero;
            var master = FindClanMaster(captive);
            if (master == null) return false;
            MBTextManager.SetTextVariable("MASTER", master.Name);
            MBTextManager.SetTextVariable("LORE", TraditionName(master));
            return true;
        }

        private void OnCaptiveConfirms()
        {
            var captive = Hero.OneToOneConversationHero;
            var master = FindClanMaster(captive);
            if (master == null) return;

            if (!_suspects.Contains(master.StringId))
            {
                _suspects.Add(master.StringId);

                SotorRivalReveal.MarkKnownToPlayer(master);
                Announce("sotor_hm_suspect_ribbon", master);
            }
            SotorLog.Info($"HiddenMasterHunt: captive {captive.Name} NAMED clan-mate {master.Name} "
                          + $"as the hidden master of their house. Suspicion recorded; confrontation unlocked.");
            PayTheBargain(captive);
        }

        private bool CaptiveHasWhisper()
        {
            var captive = Hero.OneToOneConversationHero;
            if (captive == null) return false;
            if (SotorRivalSeeder.DiscoveryRoll(captive) >= WhisperChance) return false;
            var master = FindKingdomMaster(captive);
            if (master?.Clan == null) return false;
            MBTextManager.SetTextVariable("CLAN", master.Clan.Name);
            return true;
        }

        private void OnCaptiveWhisper()
        {
            var captive = Hero.OneToOneConversationHero;
            var master = FindKingdomMaster(captive);
            if (master?.Clan == null) return;
            if (!_clanLeads.Contains(master.Clan.StringId))
            {
                _clanLeads.Add(master.Clan.StringId);
                var t = SotorText.GetObject("sotor_hm_lead_ribbon");
                t.SetTextVariable("CLAN", master.Clan.Name);
                ShowBothChannels(t.ToString());
            }
            SotorLog.Info($"HiddenMasterHunt: captive {captive.Name} whispered a lead on clan "
                          + $"{master.Clan.Name} (master {master.Name}).");
            PayTheBargain(captive);
        }

        private static void PayTheBargain(Hero captive)
        {
            if (captive == null) return;
            EndCaptivityAction.ApplyByReleasedByChoice(captive, Hero.MainHero);
            SotorLog.Info($"HiddenMasterHunt: released {captive.Name} as the price of what he knew.");
        }

        private bool CanConfront()
        {
            if (!SotorSettings.EnableRivalCasters || !SotorRivalReveal.IsReady) return false;
            var hero = Hero.OneToOneConversationHero;
            if (!IsDiscoverableMaster(hero)) return false;

            return _suspects.Contains(hero.StringId);
        }

        private void OnConfrontAdmitted()
        {
            var master = Hero.OneToOneConversationHero;
            if (master == null) return;
            if (SotorRivalReveal.Reveal(master))
            {
                Announce("sotor_hm_reveal_ribbon", master);
                SotorLog.Info($"HiddenMasterHunt: {master.Name} admitted to being a hidden "
                              + $"{SotorRivalSeeder.MemberOnlyTraditionForHero(master)} master when confronted.");
            }
        }

        private static TextObject TraditionName(Hero master)
        {
            var trad = SotorRivalSeeder.MemberOnlyTraditionForHero(master);
            var obj = SotorTraditionObject.For(trad);
            return obj != null ? obj.Name : new TextObject(trad.ToString());
        }

        private static void Announce(string stringId, Hero master)
        {
            var t = SotorText.GetObject(stringId);
            t.SetTextVariable("NAME", master.Name);
            t.SetTextVariable("TRADITION", TraditionName(master));
            ShowBothChannels(t.ToString());
        }

        private static void ShowBothChannels(string line)
        {
            SotorRibbon.Show(new TextObject(line), 4000);
            InformationManager.DisplayMessage(new InformationMessage(line));
        }
    }
}
