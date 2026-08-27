using System.Collections.Generic;
using System.Linq;
using SOTOR.AbilitySystem.Rivals;
using SOTOR.Extensions;
using SOTOR.Items;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace SOTOR.CampaignBehaviors
{

    public class SotorPrisonerTeachBehavior : CampaignBehaviorBase
    {

        private enum PressMode { None, Lore, Spell }

        private PressMode _mode;
        private Trad _offeredLore = Trad.None;
        private string _offeredLoreId;
        private string _offeredSpellId;

        private struct SpellSlot
        {
            public string SpellId;
            public string LoreId;
            public Trad Lore;
        }

        private readonly List<SpellSlot> _spellSlots = new List<SpellSlot>();

        private const int SpellSlotCount = 12;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {

        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            AddDialogs(starter);
        }

        private void AddDialogs(CampaignGameStarter starter)
        {

            starter.AddPlayerLine("sotor_prisoner_teach_ask", "hero_main_options", "sotor_prisoner_teach_reply",
                SotorText.Get("sotor_prisoner_teach_ask"),
                IsCapturedWizardWithLore, ResetPressState, 110);

            starter.AddPlayerLine("sotor_prisoner_teach_ask_alt", "prisoner_recruit_start_player",
                "sotor_prisoner_teach_reply",
                SotorText.Get("sotor_prisoner_teach_ask"),
                IsCapturedWizardWithLore, ResetPressState, 110);

            starter.AddDialogLine("sotor_prisoner_teach_nothing", "sotor_prisoner_teach_reply", "close_window",
                SotorText.Get("sotor_prisoner_teach_nothing"),
                HasNothingToGive, null, 200);

            starter.AddDialogLine("sotor_prisoner_teach_reply", "sotor_prisoner_teach_reply", "sotor_prisoner_teach_fork",
                SotorText.Get("sotor_prisoner_teach_reply"),
                SetOfferText, null, 100);

            starter.AddPlayerLine("sotor_prisoner_teach_take", "sotor_prisoner_teach_fork", "sotor_prisoner_teach_lesson",
                SotorText.Get("sotor_prisoner_teach_take"),
                CanPressForLore, OnPressForLore, 110);

            starter.AddPlayerLine("sotor_prisoner_teach_take_spell", "sotor_prisoner_teach_fork", "sotor_prisoner_spell_pick",
                SotorText.Get("sotor_prisoner_teach_take_spell"),
                CanPressForSpell, null, 100);

            starter.AddPlayerLine("sotor_prisoner_teach_take_blueprint", "sotor_prisoner_teach_fork", "sotor_prisoner_blueprint_pick",
                SotorText.Get("sotor_prisoner_teach_take_blueprint"),
                null, null, 95);

            starter.AddPlayerLine("sotor_prisoner_teach_take_info", "sotor_prisoner_teach_fork", "sotor_hm_interrogate_reply",
                SotorText.Get("sotor_hm_interrogate"),
                SotorHiddenMasterHuntBehavior.IsInterrogatableCaptiveNow, null, 93);

            starter.AddPlayerLine("sotor_prisoner_teach_release", "sotor_prisoner_teach_fork", "sotor_prisoner_teach_done_free",
                SotorText.Get("sotor_prisoner_teach_release"),
                null, null, 90);
            starter.AddPlayerLine("sotor_prisoner_teach_never", "sotor_prisoner_teach_fork", "close_window",
                SotorText.Get("sotor_prisoner_teach_never"),
                null, null, 80);

            starter.AddDialogLine("sotor_prisoner_spell_list", "sotor_prisoner_spell_pick", "sotor_prisoner_spell_choose",
                SotorText.Get("sotor_prisoner_spell_list"),
                BuildSpellSlots, null, 100);

            for (int i = 0; i < SpellSlotCount; i++)
            {
                int slot = i;
                starter.AddPlayerLine("sotor_prisoner_spell_pick_" + slot, "sotor_prisoner_spell_choose",
                    "sotor_prisoner_teach_lesson",
                    "{SOTOR_PRISONER_SPELL_" + slot + "}",
                    () => SpellSlotExists(slot), () => PickSpellSlot(slot), 100 - slot);
            }

            starter.AddPlayerLine("sotor_prisoner_spell_none", "sotor_prisoner_spell_choose", "hero_main_options",
                SotorText.Get("sotor_prisoner_spell_none"), null, ResetPressState, 10);

            starter.AddDialogLine("sotor_prisoner_blueprint_nothing", "sotor_prisoner_blueprint_pick", "sotor_prisoner_teach_fork",
                SotorText.Get("sotor_prisoner_blueprint_nothing"),
                BlueprintShelfEmpty, null, 200);

            starter.AddDialogLine("sotor_prisoner_blueprint_list", "sotor_prisoner_blueprint_pick",
                "sotor_prisoner_blueprint_lesson",
                SotorText.Get("sotor_prisoner_blueprint_list"),
                null, OpenBlueprintPicker, 100);

            starter.AddDialogLine("sotor_prisoner_blueprint_done", "sotor_prisoner_blueprint_lesson", "close_window",
                "{SOTOR_PRISONER_BLURB}",
                () => _pickerAnswer == PickerAnswer.Taken, OnLessonFinished, 100);

            starter.AddDialogLine("sotor_prisoner_blueprint_cancelled", "sotor_prisoner_blueprint_lesson",
                "sotor_prisoner_teach_fork",
                SotorText.Get("sotor_prisoner_blueprint_cancelled"),
                () => _pickerAnswer == PickerAnswer.Cancelled, null, 90);

            starter.AddDialogLine("sotor_prisoner_teach_done_take", "sotor_prisoner_teach_lesson", "sotor_prisoner_teach_blurb",
                SotorText.Get("sotor_prisoner_teach_done_take"),
                null, OnCoerceLore, 100);

            starter.AddDialogLine("sotor_prisoner_teach_blurb", "sotor_prisoner_teach_blurb", "close_window",
                "{SOTOR_PRISONER_BLURB}",
                null, OnLessonFinished, 100);

            starter.AddDialogLine("sotor_prisoner_teach_done_free", "sotor_prisoner_teach_done_free", "close_window",
                SotorText.Get("sotor_prisoner_teach_done_free"),
                null, OnReleaseUntaught, 100);
        }

        public static bool IsCapturedWizardWithLore()
        {
            if (!SotorSettings.EnableRivalCasters) return false;
            var hero = Hero.OneToOneConversationHero;
            if (hero == null || hero == Hero.MainHero) return false;
            if (!hero.IsLord || !hero.IsAbilityUser()) return false;
            var ch = CharacterObject.OneToOneConversationCharacter;
            if (ch == null || !MobileParty.MainParty.PrisonRoster.Contains(ch)) return false;

            if (SotorRivalSeeder.IsHiddenMaster(hero)
                && (!SotorRivalReveal.IsReady || !SotorRivalReveal.IsRevealed(hero)))
            {
                return false;
            }

            return true;
        }

        private bool HasNothingToGive()
        {
            var master = Hero.OneToOneConversationHero;
            if (master == null) return false;
            SetLineVariables(master);
            bool anything = PickCoercibleLore(master, out _, out _) || CountTeachableSpells(master) > 0
                            || CollectCoercibleBlueprints(master).Count > 0;
            if (!anything)
            {
                SotorLog.Info($"RivalPrisonerTeach: {master.Name} has nothing left to give; refusing.");
            }
            return !anything;
        }

        private bool CanPressForLore()
        {
            var hero = Hero.OneToOneConversationHero;
            return hero != null && PickCoercibleLore(hero, out _, out _);
        }

        private bool CanPressForSpell()
        {
            var hero = Hero.OneToOneConversationHero;
            if (hero == null || CountTeachableSpells(hero) == 0) return false;
            return !PickCoercibleLore(hero, out _, out _);
        }

        private bool PickCoercibleLore(Hero master, out Trad lore, out string loreId)
        {
            lore = Trad.None;
            loreId = null;
            var info = Hero.MainHero.GetExtendedInfo();
            Trad best = Trad.None;
            foreach (var t in SotorRivalSeeder.CoercibleTraditions(master))
            {
                string id = SotorTraditions.LoreIdFor(t);
                if (id == null) continue;
                if (info != null && info.HasLore(id)) continue;
                if (best == Trad.None || SotorTraditions.Rarity(t) > SotorTraditions.Rarity(best))
                {
                    best = t;
                    lore = t;
                    loreId = id;
                }
            }
            return best != Trad.None;
        }

        private List<SpellSlot> CollectTeachableSpells(Hero master)
        {
            var result = new List<SpellSlot>();
            var info = Hero.MainHero.GetExtendedInfo();
            if (info == null) return result;
            var masterInfo = master.GetExtendedInfo();
            if (masterInfo == null) return result;

            foreach (var t in SotorRivalSeeder.CoercibleTraditions(master))
            {
                string loreId = SotorTraditions.LoreIdFor(t);
                if (loreId == null) continue;
                if (!info.HasLore(loreId)) continue;

                foreach (var template in AbilitySystem.AbilityFactory.GetTemplatesByLore(loreId))
                {
                    string spellId = template?.StringID;
                    if (spellId == null) continue;
                    if (info.HasSpell(spellId) || Hero.MainHero.HasAbility(spellId)) continue;
                    if (!masterInfo.HasSpell(spellId)) continue;

                    result.Add(new SpellSlot { SpellId = spellId, LoreId = loreId, Lore = t });
                }
            }
            return result;
        }

        private int CountTeachableSpells(Hero master)
        {
            return CollectTeachableSpells(master).Count;
        }

        private List<SotorItemTrait> CollectCoercibleBlueprints(Hero master)
        {
            var result = new List<SotorItemTrait>();
            if (master == null) return result;

            var lores = new List<string>();
            foreach (var t in SotorRivalSeeder.CoercibleTraditions(master))
            {
                string id = SotorTraditions.LoreIdFor(t);
                if (id != null && !lores.Contains(id)) lores.Add(id);
            }
            if (lores.Count == 0) return result;

            var known = new HashSet<string>();
            var roster = MobileParty.MainParty?.MemberRoster;
            if (roster != null)
            {
                for (int i = 0; i < roster.Count; i++)
                {
                    var ch = roster.GetCharacterAtIndex(i);
                    var hero = ch != null && ch.IsHero ? ch.HeroObject : null;
                    var info = hero?.GetExtendedInfo();
                    if (info == null) continue;
                    foreach (var trait in SotorItemTraitManager.CraftableTraits)
                    {
                        if (info.HasBlueprint(trait.ItemTraitStringId)) known.Add(trait.ItemTraitStringId);
                    }
                }
            }

            foreach (var trait in SotorItemTraitManager.CraftableTraits)
            {
                if (!trait.HasLoreRequirement || !lores.Contains(trait.RequiredLore)) continue;

                if (!SotorBlueprintBookBehavior.HeroMeetsGate(master, trait)) continue;
                if (known.Contains(trait.ItemTraitStringId)) continue;
                result.Add(trait);
            }
            result.Sort((a, b) => a.LearnThreshold.CompareTo(b.LearnThreshold));
            return result;
        }

        private bool CanPressForBlueprint()
        {
            var hero = Hero.OneToOneConversationHero;
            return hero != null && CollectCoercibleBlueprints(hero).Count > 0;
        }

        private bool BlueprintShelfEmpty()
        {
            var hero = Hero.OneToOneConversationHero;
            if (hero == null) return false;
            SetLineVariables(hero);
            return !CanPressForBlueprint();
        }

        private enum PickerAnswer { None, Taken, Cancelled }
        private PickerAnswer _pickerAnswer;

        private Hero _pickerCaptive;

        private void OpenBlueprintPicker()
        {
            var master = Hero.OneToOneConversationHero;
            if (master == null) return;

            var candidates = CollectCoercibleBlueprints(master);
            if (candidates.Count == 0) return;

            _pickerCaptive = master;
            _pickerAnswer = PickerAnswer.None;
            MBTextManager.SetTextVariable("MASTER", master.Name);

            var elements = new List<InquiryElement>();
            foreach (var trait in candidates)
            {

                var label = SotorText.GetObject("sotor_prisoner_blueprint_option");
                label.SetTextVariable("TRAIT", trait.ItemTraitName);
                label.SetTextVariable("LORE", SotorUnlockBlurb.LoreTitle(trait.RequiredLore));
                var book = SotorBlueprintBookBehavior.BookFor(trait.ItemTraitStringId);
                elements.Add(new InquiryElement(trait.ItemTraitStringId, label.ToString(),
                    book != null ? new ItemImageIdentifier(book) : null,
                    true, trait.ItemTraitDescription));
            }

            SotorLog.Info($"RivalPrisonerTeach: formula picker for captive {master.Name} = "
                          + $"[{string.Join(",", candidates.ConvertAll(t => t.ItemTraitStringId))}].");

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                SotorText.Rendered("sotor_master_shop_title"),
                SotorText.Rendered("sotor_prisoner_blueprint_list_text"),
                elements, true, 1, 1,
                SotorText.Rendered("sotor_str_accept"), SotorText.Rendered("sotor_str_cancel"),
                selected =>
                {
                    var pick = selected?.FirstOrDefault();
                    if (pick?.Identifier is string traitId) TakeCoercedBlueprint(traitId);
                    else CancelBlueprintPicker();
                },
                _ => CancelBlueprintPicker()), true, false);
        }

        private void CancelBlueprintPicker()
        {
            if (_pickerCaptive != null)
            {
                SotorLog.Info($"RivalPrisonerTeach: the player closed {_pickerCaptive.Name}'s formula picker "
                              + "without taking anything; he keeps his secrets and his cell.");
            }
            _pickerCaptive = null;
            _pickerAnswer = PickerAnswer.Cancelled;
            ResumeConversation();
        }

        private static void ResumeConversation()
        {
            try
            {
                Campaign.Current?.ConversationManager?.ContinueConversation();
            }
            catch (System.Exception ex)
            {
                SotorLog.Error($"RivalPrisonerTeach: could not resume the conversation after the formula "
                               + $"picker: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void TakeCoercedBlueprint(string traitId)
        {
            var captive = _pickerCaptive;
            _pickerCaptive = null;

            var trait = SotorItemTraitManager.GetTrait(traitId);
            var info = Hero.MainHero.GetExtendedInfo();
            if (captive == null || trait == null || info == null) return;

            info.AddBlueprint(traitId);

            var lore = SotorTraditions.TradForLore(trait.RequiredLore);
            if (SotorRivalStanding.IsReady)
            {

                SotorRivalStanding.ChangeTradition(lore,
                    -System.Math.Abs(SotorTraditions.LearnSpellStanding), silent: false, affectLords: true);
            }

            ChangeRelationAction.ApplyPlayerRelation(captive, -10, affectRelatives: true, showQuickNotification: true);

            if (SotorCoercionRecord.IsReady && SotorCoercionRecord.Record(captive))
            {
                SotorLog.Info($"RivalPrisonerTeach: {captive.Name} will no longer teach the player willingly.");
            }

            var learned = SotorText.GetObject("sotor_blueprint_learned");
            learned.SetTextVariable("HERO", Hero.MainHero.Name);
            learned.SetTextVariable("TRAIT", trait.ItemTraitName);
            SotorRibbon.Show(learned, SotorRibbon.DefaultMs, Hero.MainHero);

            var blurb = SotorText.GetObject("sotor_ransom_blueprint");
            blurb.SetTextVariable("TRAIT", trait.ItemTraitName);
            MBTextManager.SetTextVariable("SOTOR_PRISONER_BLURB", blurb.ToString());

            SotorLog.Info($"RivalPrisonerTeach: coerced the formula '{traitId}' ({lore}) from captive "
                          + $"{captive.Name}; he speaks and then walks.");

            _pickerAnswer = PickerAnswer.Taken;
            ResumeConversation();
        }

        private bool BuildSpellSlots()
        {
            var master = Hero.OneToOneConversationHero;
            if (master == null) return false;
            SetLineVariables(master);

            _spellSlots.Clear();
            var all = CollectTeachableSpells(master);
            if (all.Count == 0) return false;

            if (all.Count > SpellSlotCount)
            {
                SotorLog.Info($"RivalPrisonerTeach: {master.Name} could yield {all.Count} spells, showing the first {SpellSlotCount}.");
            }

            for (int i = 0; i < all.Count && i < SpellSlotCount; i++)
            {
                _spellSlots.Add(all[i]);

                var label = SotorText.GetObject("sotor_teach_spell_option");
                label.SetTextVariable("SPELL", SotorUnlockBlurb.SpellTitle(all[i].SpellId));
                label.SetTextVariable("LORE", SotorUnlockBlurb.LoreTitle(all[i].LoreId));
                MBTextManager.SetTextVariable("SOTOR_PRISONER_SPELL_" + i, label.ToString());
            }

            SotorLog.Info($"RivalPrisonerTeach: spell list for captive {master.Name} = "
                          + $"[{string.Join(",", all.ConvertAll(x => x.SpellId))}].");
            return true;
        }

        private bool SpellSlotExists(int index) => index >= 0 && index < _spellSlots.Count;

        private bool SetOfferText()
        {
            var master = Hero.OneToOneConversationHero;
            if (master == null) return false;
            SetLineVariables(master);

            if (PickCoercibleLore(master, out _, out string loreId))
            {
                MBTextManager.SetTextVariable("LORE_NAME", SotorUnlockBlurb.LoreTitle(loreId));
                return true;
            }

            var spells = CollectTeachableSpells(master);
            if (spells.Count > 0)
            {
                MBTextManager.SetTextVariable("LORE_NAME", SotorUnlockBlurb.LoreTitle(spells[0].LoreId));
                return true;
            }

            var blueprints = CollectCoercibleBlueprints(master);
            if (blueprints.Count == 0) return false;
            MBTextManager.SetTextVariable("LORE_NAME", SotorUnlockBlurb.LoreTitle(blueprints[0].RequiredLore));
            return true;
        }

        private void ResetPressState()
        {
            _mode = PressMode.None;
            _offeredLore = Trad.None;
            _offeredLoreId = null;
            _offeredSpellId = null;
            _spellSlots.Clear();

            _pickerAnswer = PickerAnswer.None;
            _pickerCaptive = null;
        }

        private void OnPressForLore()
        {
            var master = Hero.OneToOneConversationHero;
            if (master == null) return;
            if (!PickCoercibleLore(master, out _offeredLore, out _offeredLoreId)) return;
            _mode = PressMode.Lore;
            _offeredSpellId = null;
        }

        private void PickSpellSlot(int index)
        {
            if (!SpellSlotExists(index)) return;
            var slot = _spellSlots[index];
            _mode = PressMode.Spell;
            _offeredSpellId = slot.SpellId;
            _offeredLore = slot.Lore;
            _offeredLoreId = slot.LoreId;
        }

        private void OnCoerceLore()
        {
            var master = Hero.OneToOneConversationHero;
            if (master == null || _mode == PressMode.None) return;

            string tookSpellId = null;
            if (_mode == PressMode.Lore)
            {

                if (_offeredLoreId == null) return;
                GrantLoreOnly(Hero.MainHero, _offeredLoreId);
            }
            else
            {
                if (_offeredSpellId == null) return;
                tookSpellId = _offeredSpellId;
                GrantSingleSpell(Hero.MainHero, tookSpellId);
            }

            if (SotorRivalStanding.IsReady)
            {
                int taken = _mode == PressMode.Lore
                    ? SotorTraditions.LearnLoreStanding
                    : SotorTraditions.LearnSpellStanding;
                SotorRivalStanding.ChangeTradition(_offeredLore, -System.Math.Abs(taken), silent: false, affectLords: true);
            }

            ChangeRelationAction.ApplyPlayerRelation(master, -10, affectRelatives: true, showQuickNotification: true);

            if (SotorCoercionRecord.IsReady && SotorCoercionRecord.Record(master))
            {
                SotorLog.Info($"RivalPrisonerTeach: {master.Name} will no longer teach the player willingly.");
            }

            string blurbId = tookSpellId != null
                ? "sotor_unlock_spell_" + tookSpellId
                : "sotor_ransom_lore_" + _offeredLoreId;
            SotorUnlockBlurb.Publish("SOTOR_PRISONER_BLURB", blurbId, master, _offeredLoreId, tookSpellId);

            SotorLog.Info($"RivalPrisonerTeach: coerced {(tookSpellId ?? _offeredLoreId)} "
                          + $"({_offeredLore}) from captive {master.Name}, mode={_mode}, blurb='{blurbId}'.");
        }

        private void OnLessonFinished()
        {
            var master = Hero.OneToOneConversationHero;
            if (master == null) return;
            ReleasePrisoner(master);
            SotorLog.Info($"RivalPrisonerTeach: released {master.Name} after the lesson.");
            ResetPressState();
        }

        private void OnReleaseUntaught()
        {
            var master = Hero.OneToOneConversationHero;
            if (master == null) return;

            ChangeRelationAction.ApplyPlayerRelation(master, 5, affectRelatives: false, showQuickNotification: true);
            ReleasePrisoner(master);
            SotorLog.Info($"RivalPrisonerTeach: released {master.Name} untaught.");
            ResetPressState();
        }

        private static void ReleasePrisoner(Hero master)
        {
            EndCaptivityAction.ApplyByReleasedByChoice(master, Hero.MainHero);
        }

        private static void SetLineVariables(Hero master)
        {
            SotorText.SetPlayerVariables();
            if (master != null) MBTextManager.SetTextVariable("MASTER", master.Name);
        }

        private static void GrantLoreOnly(Hero hero, string loreId)
        {
            if (hero == null || loreId == null) return;
            hero.AddAttribute("AbilityUser");
            hero.AddAttribute("SpellCaster");
            var info = hero.GetExtendedInfo();
            if (info == null || info.HasLore(loreId)) return;
            info.AddLore(loreId);
        }

        private static void GrantSingleSpell(Hero hero, string abilityId)
        {
            if (hero == null || abilityId == null) return;
            var info = hero.GetExtendedInfo();
            if (info == null) return;
            if (!info.HasSpell(abilityId)) info.AddSpell(abilityId);
            if (!hero.HasAbility(abilityId)) hero.AddAbility(abilityId);
        }
    }
}
