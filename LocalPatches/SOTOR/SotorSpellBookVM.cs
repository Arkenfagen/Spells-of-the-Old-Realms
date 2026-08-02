using System;
using System.Collections.Generic;
using System.Linq;
using SOTOR.AbilitySystem;
using SOTOR.Extensions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Library;

namespace SOTOR
{
    public class SotorSpellBookVM : ViewModel
    {
        private readonly Action _closeAction;
        private Hero _hero;

        private System.Collections.Generic.List<Hero> _cycleHeroes = new System.Collections.Generic.List<Hero>();
        private int _cycleIndex;
        private string _titleText = "SpellBook";
        private MBBindingList<SotorLoreObjectVM> _loreObjects;
        private MBBindingList<SotorLoreObjectVM> _loreObjectsLeft;
        private MBBindingList<SotorLoreObjectVM> _loreObjectsRight;
        private SotorLoreObjectVM _currentLore;
        private SotorSpellcraftBarVM _spellcraftBar;
        private CharacterViewModel _characterPortrait;

        private readonly Dictionary<string, bool> _stagedSelections = new Dictionary<string, bool>();
        private readonly HashSet<string> _stagedUnlocks = new HashSet<string>();

        private readonly HashSet<string> _stagedPurchases = new HashSet<string>();

        public SotorSpellBookVM(Action closeAction)
        {
            _closeAction = closeAction;

            _cycleHeroes = SOTOR.Extensions.ExtendedInfoSystem.ExtendedInfoManager.GetSpellcasterPartyHeroes();
            _cycleIndex = 0;
            _hero = (_cycleHeroes.Count > 0) ? _cycleHeroes[0] : Hero.MainHero;
            BuildForHero();
        }

        private void BuildForHero()
        {
            _stagedSelections.Clear();
            _stagedUnlocks.Clear();
            _stagedPurchases.Clear();

            _spellcraftBar = new SotorSpellcraftBarVM(_hero);

            _spellcraftBar.OnStagingChanged = RefreshAllStates;

            _spellcraftBar.StagedOwnedLoreTitlesProvider = GetStagedOwnedLoreTitles;

            _spellcraftBar.StagedOwnedSpellProvider = IsSpellPurchased;
            OnPropertyChanged(nameof(SpellcraftBar));

            BuildCharacterPortrait();
            InitializeLoreObjects();
            OnPropertyChanged(nameof(CanCycleHeroes));
            OnPropertyChanged(nameof(GoldText));
            OnPropertyChanged(nameof(HeroName));
        }

        private void SwitchToHeroAt(int index)
        {
            if (_cycleHeroes == null || _cycleHeroes.Count <= 1)
            {
                return;
            }

            _cycleIndex = ((index % _cycleHeroes.Count) + _cycleHeroes.Count) % _cycleHeroes.Count;
            _hero = _cycleHeroes[_cycleIndex];
            _spellcraftBar?.RevertChanges();
            BuildForHero();
        }

        public void ExecuteSelectNextHero() => SwitchToHeroAt(_cycleIndex + 1);
        public void ExecuteSelectPreviousHero() => SwitchToHeroAt(_cycleIndex - 1);

        [DataSourceProperty]
        public bool CanCycleHeroes => _cycleHeroes != null && _cycleHeroes.Count > 1;

        private System.Collections.Generic.List<string> GetStagedOwnedLoreTitles()
        {
            var titles = new System.Collections.Generic.List<string>();
            foreach (var id in SotorLores.AllShownLores)
            {
                if (IsLoreOwned(id))
                {
                    titles.Add(SotorLores.Display.TryGetValue(id, out var d) ? d.Title : id);
                }
            }
            return titles;
        }

        private int StagedCasterLevel() =>
            _spellcraftBar != null
                ? (int)_spellcraftBar.GetStagedCastingLevel()
                : (int)SotorSpellcraftHelper.GetCastingLevel(_hero);

        public bool IsLoreOwned(string loreId)
        {
            if (loreId == null) return false;
            if (_stagedUnlocks.Contains(loreId)) return true;
            var info = _hero?.GetExtendedInfo();
            return info != null && info.HasLore(loreId);
        }

        public bool IsLoreLocked(string loreId) => !IsLoreOwned(loreId);
        public bool IsUnlockStaged(string loreId) => _stagedUnlocks.Contains(loreId);

        private bool IsLibrarianActive()
        {
            var lib = AbilitySystem.SotorPerks.Librarian;
            if (lib == null) return false;
            if (_spellcraftBar != null && _spellcraftBar.IsPerkSelectedStaged(lib)) return true;
            return _hero != null && _hero.GetPerkValue(lib);
        }

        public int LorePrice(string loreId) => ApplyLibrarian(SotorLores.GetPrice(loreId));

        private int StagedSpend()
        {
            int total = 0;
            foreach (var l in _stagedUnlocks) total += LorePrice(l);
            foreach (var a in _stagedPurchases) total += SpellPriceById(a);
            return total;
        }

        private bool CanAfford(int extra) => (Hero.MainHero?.Gold ?? 0) >= StagedSpend() + extra;

        public bool CanAffordLore(string loreId) => CanAfford(LorePrice(loreId));

        public int HeroGold => Hero.MainHero?.Gold ?? 0;

        [DataSourceProperty]
        public string GoldText => (HeroGold - StagedSpend()).ToString("N0");

        public bool MeetsCasterLevelForLore(string loreId)
        {
            var required = SotorLores.GetRequiredCasterLevel(loreId);
            return required == SpellCastingLevel.None || StagedCasterLevel() >= (int)required;
        }

        public string LoreUnlockBlockReason(string loreId)
        {
            if (!MeetsCasterLevelForLore(loreId))
            {
                return $"Requires {SotorLores.GetRequiredCasterLevel(loreId)} casting level";
            }
            if (!CanAffordLore(loreId)) return "Not enough gold";
            return "";
        }

        public void StageUnlockLore(string loreId)
        {
            if (loreId == null || IsLoreOwned(loreId)) return;
            if (!CanAffordLore(loreId))
            {
                SotorLog.Info($"Spellbook: cannot afford lore '{loreId}' ({LorePrice(loreId)} gold, have {HeroGold}, already staged {StagedSpend()}).");
                return;
            }
            _stagedUnlocks.Add(loreId);
            SotorLog.Info($"Spellbook: STAGED unlock of lore '{loreId}' ({LorePrice(loreId)} gold).");
            RefreshAllStates();
        }

        private static AbilityTemplate TemplateById(string abilityId) => AbilityFactory.GetTemplate(abilityId);

        public int SpellPriceById(string abilityId) => ApplyLibrarian(SotorSpellcraftHelper.GetSpellBaseGoldCost(TemplateById(abilityId)));
        public int SpellPrice(AbilityTemplate t) => ApplyLibrarian(SotorSpellcraftHelper.GetSpellBaseGoldCost(t));

        private int ApplyLibrarian(int cost) => IsLibrarianActive() ? (int)(cost * 0.5f) : cost;

        public bool IsSpellPurchased(string abilityId)
        {
            if (abilityId == null) return false;
            if (_stagedPurchases.Contains(abilityId)) return true;
            var info = _hero?.GetExtendedInfo();
            return info != null && info.HasSpell(abilityId);
        }

        public bool IsPurchaseStaged(string abilityId) => _stagedPurchases.Contains(abilityId);

        public bool CanBuySpell(string abilityId, string loreId, int spellTier)
        {
            if (IsLoreLocked(loreId) || IsSpellPurchased(abilityId)) return false;
            return StagedCasterLevel() >= spellTier;
        }

        public bool CanAffordSpell(string abilityId) => CanAfford(SpellPriceById(abilityId));

        public string SpellBuyBlockReason(string abilityId, string loreId, int spellTier)
        {
            if (IsLoreLocked(loreId)) return "Unlock the lore first";
            if (IsSpellPurchased(abilityId)) return "";
            if (StagedCasterLevel() < spellTier) return "Caster level too low";
            if (!CanAffordSpell(abilityId)) return "Not enough gold";
            return "";
        }

        public void StageBuySpell(string abilityId, string loreId, int spellTier)
        {
            if (abilityId == null || !CanBuySpell(abilityId, loreId, spellTier)) return;
            if (!CanAffordSpell(abilityId))
            {
                SotorLog.Info($"Spellbook: cannot afford spell '{abilityId}' ({SpellPriceById(abilityId)} gold, have {HeroGold}, already staged {StagedSpend()}).");
                return;
            }
            _stagedPurchases.Add(abilityId);
            SotorLog.Info($"Spellbook: STAGED purchase of spell '{abilityId}' ({SpellPriceById(abilityId)} gold).");
            RefreshAllStates();
        }

        public bool IsSpellSelectedStaged(string abilityId)
        {
            if (_stagedSelections.TryGetValue(abilityId, out var v)) return v;
            var info = _hero?.GetExtendedInfo();
            return info != null && info.IsAbilitySelected(abilityId);
        }

        public bool IsSpellEquippable(string abilityId, string loreId) =>
            IsLoreOwned(loreId) && IsSpellPurchased(abilityId);

        public void ToggleSpellStaged(string abilityId, string loreId)
        {
            if (abilityId == null || !IsSpellEquippable(abilityId, loreId)) return;
            _stagedSelections[abilityId] = !IsSpellSelectedStaged(abilityId);
            SotorLog.Info($"Spellbook: STAGED spell '{abilityId}' selected={_stagedSelections[abilityId]}.");
            RefreshAllStates();
        }

        private void RefreshAllStates()
        {
            if (_loreObjects != null)
            {
                foreach (var lore in _loreObjects)
                {
                    lore.RefreshFromState();
                    if (lore.SpellList != null)
                    {
                        foreach (var spell in lore.SpellList) spell.RefreshFromState();
                    }
                }
            }

            _spellcraftBar?.RefreshValues();

            OnPropertyChanged(nameof(GoldText));
        }

        private void CommitBookChanges()
        {
            var info = _hero?.GetExtendedInfo();
            if (info == null) { _stagedUnlocks.Clear(); _stagedPurchases.Clear(); _stagedSelections.Clear(); return; }

            foreach (var loreId in _stagedUnlocks)
            {
                int price = LorePrice(loreId);
                if (price > 0) Hero.MainHero.ChangeHeroGold(-price);
                info.AddLore(loreId);
                SotorLog.Info($"Spellbook: COMMITTED unlock lore '{loreId}' (−{price} gold).");
            }

            foreach (var abilityId in _stagedPurchases)
            {
                int price = SpellPriceById(abilityId);
                if (price > 0) Hero.MainHero.ChangeHeroGold(-price);
                info.AddSpell(abilityId);
                if (!_hero.HasAbility(abilityId)) _hero.AddAbility(abilityId);
                SotorLog.Info($"Spellbook: COMMITTED purchase spell '{abilityId}' (−{price} gold; now castable).");
            }

            foreach (var kv in _stagedSelections)
            {
                bool want = kv.Value;
                bool has = info.IsAbilitySelected(kv.Key);
                if (want && !has) info.AddSelectedAbility(kv.Key);
                else if (!want && has) info.RemoveSelectedAbility(kv.Key);
            }
            if (_stagedSelections.Count > 0)
                SotorLog.Info($"Spellbook: COMMITTED {_stagedSelections.Count} spell selection change(s).");

            _stagedUnlocks.Clear();
            _stagedPurchases.Clear();
            _stagedSelections.Clear();
        }

        private void RevertBookChanges()
        {
            bool had = _stagedUnlocks.Count > 0 || _stagedPurchases.Count > 0 || _stagedSelections.Count > 0;
            _stagedUnlocks.Clear();
            _stagedPurchases.Clear();
            _stagedSelections.Clear();
            if (had) SotorLog.Info("Spellbook: reverted staged unlocks + purchases + spell selections.");
            RefreshAllStates();
        }

        [DataSourceProperty]
        public SotorSpellcraftBarVM SpellcraftBar
        {
            get => _spellcraftBar;
            set
            {
                if (value == _spellcraftBar)
                {
                    return;
                }

                _spellcraftBar = value;
                OnPropertyChangedWithValue(value, nameof(SpellcraftBar));
            }
        }

        [DataSourceProperty]
        public CharacterViewModel CharacterPortrait
        {
            get => _characterPortrait;
            set
            {
                if (value == _characterPortrait)
                {
                    return;
                }

                _characterPortrait = value;
                OnPropertyChangedWithValue(value, nameof(CharacterPortrait));
            }
        }

        private void BuildCharacterPortrait()
        {
            try
            {
                if (_hero?.CharacterObject == null)
                {
                    return;
                }
                var vm = new CharacterViewModel(CharacterViewModel.StanceTypes.None);
                vm.FillFrom(_hero.CharacterObject);
                if (_hero.BattleEquipment != null)
                {

                    var onFoot = _hero.BattleEquipment.Clone();
                    onFoot[EquipmentIndex.Horse] = default(EquipmentElement);
                    onFoot[EquipmentIndex.HorseHarness] = default(EquipmentElement);
                    vm.SetEquipment(onFoot);
                }
                vm.MountCreationKey = null;
                CharacterPortrait = vm;
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SpellBook: BuildCharacterPortrait failed: {ex.Message}");
            }
        }

        [DataSourceProperty]
        public string HeroName => _hero?.Name?.ToString() ?? "";

        [DataSourceProperty]
        public string TitleText
        {
            get => _titleText;
            set
            {
                if (value == _titleText)
                {
                    return;
                }

                _titleText = value;
                OnPropertyChangedWithValue(value, nameof(TitleText));
            }
        }

        [DataSourceProperty]
        public MBBindingList<SotorLoreObjectVM> LoreObjects
        {
            get => _loreObjects;
            set
            {
                if (value == _loreObjects)
                {
                    return;
                }

                _loreObjects = value;
                OnPropertyChangedWithValue(value, nameof(LoreObjects));
            }
        }

        [DataSourceProperty]
        public MBBindingList<SotorLoreObjectVM> LoreObjectsLeft
        {
            get => _loreObjectsLeft;
            set { if (value != _loreObjectsLeft) { _loreObjectsLeft = value; OnPropertyChangedWithValue(value, nameof(LoreObjectsLeft)); } }
        }

        [DataSourceProperty]
        public MBBindingList<SotorLoreObjectVM> LoreObjectsRight
        {
            get => _loreObjectsRight;
            set { if (value != _loreObjectsRight) { _loreObjectsRight = value; OnPropertyChangedWithValue(value, nameof(LoreObjectsRight)); } }
        }

        [DataSourceProperty]
        public SotorLoreObjectVM CurrentLore
        {
            get => _currentLore;
            set
            {
                if (value == _currentLore)
                {
                    return;
                }

                _currentLore = value;
                OnPropertyChangedWithValue(value, nameof(CurrentLore));
            }
        }

        public void ExecuteClose()
        {
            _closeAction?.Invoke();
        }

        public void ExecuteDone()
        {
            _spellcraftBar?.CommitChanges();
            CommitBookChanges();
            _closeAction?.Invoke();
        }

        public void ExecuteCancel()
        {
            _spellcraftBar?.RevertChanges();
            RevertBookChanges();
            _closeAction?.Invoke();
        }

        public void ExecuteReset()
        {
            _spellcraftBar?.RevertChanges();
            RevertBookChanges();
        }

        private void InitializeLoreObjects()
        {
            var hero = _hero;
            LoreObjects = new MBBindingList<SotorLoreObjectVM>();

            foreach (var loreId in SotorLores.AllShownLores)
            {

                var templates = AbilityFactory.GetTemplatesByLore(loreId);
                var spells = new MBBindingList<SotorSpellItemVM>();
                foreach (var template in templates)
                {
                    spells.Add(new SotorSpellItemVM(this, hero, template, loreId));
                }

                var display = SotorLores.Display.TryGetValue(loreId, out var d)
                    ? d
                    : new SotorLores.LoreDisplay { LoreId = loreId, Title = loreId, SymbolSprite = "minormagic_symbol" };

                LoreObjects.Add(new SotorLoreObjectVM(
                    this,
                    SelectLoreObject,
                    display.Title,
                    display.SymbolSprite,
                    spells,
                    loreId));

                SotorLog.Info($"Spellbook tab built: '{display.Title}' ({loreId}) — {spells.Count} spell(s), locked={IsLoreLocked(loreId)}.");
            }

            LoreObjectsLeft = new MBBindingList<SotorLoreObjectVM>();
            LoreObjectsRight = new MBBindingList<SotorLoreObjectVM>();
            foreach (var lore in LoreObjects)
            {
                if (lore.IsRightSide) LoreObjectsRight.Add(lore);
                else LoreObjectsLeft.Add(lore);
            }

            if (LoreObjects.Count > 0)
            {
                SelectLoreObject(LoreObjects[0]);
            }

            SotorLog.Info($"Spellbook initialized with {LoreObjects.Count} lore tab(s) ({LoreObjectsLeft.Count} left, {LoreObjectsRight.Count} right).");
        }

        private void SelectLoreObject(SotorLoreObjectVM lore)
        {
            foreach (var loreObject in LoreObjects)
            {
                loreObject.IsSelected = loreObject == lore;
                loreObject.IsVisible = loreObject != lore;
            }

            CurrentLore = lore;
        }
    }
}
