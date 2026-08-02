using System;
using System.Linq;
using SOTOR.AbilitySystem;
using SOTOR.Extensions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper.PerkSelection;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace SOTOR
{

    public class SotorSpellcraftBarVM : ViewModel
    {
        private readonly Hero _hero;

        private string _skillName = "Spellcraft";
        private int _skillLevel;
        private int _focus;
        private int _maxFocus = 5;
        private int _unspentFocus;
        private string _attributeName = "Intelligence";
        private int _attributeValue;
        private int _unspentAttribute;
        private string _castingLevelText = "";
        private int _skillXpProgress;
        private int _skillXpForNext;
        private MBBindingList<PerkVM> _perks;
        private int _fullLearningRateLevel;
        private float _learningRate;
        private bool _canLearnSkill = true;
        private MBBindingList<SotorStatItemVM> _derivedStats;

        private PerkSelectionVM _perkSelection;

        private int _pendingFocus;

        public Action OnStagingChanged;

        public Func<System.Collections.Generic.List<string>> StagedOwnedLoreTitlesProvider;

        public Func<string, bool> StagedOwnedSpellProvider;

        public SotorSpellcraftBarVM(Hero hero)
        {
            _hero = hero;
            _derivedStats = new MBBindingList<SotorStatItemVM>();
            if (_hero?.HeroDeveloper != null)
            {

                _perkSelection = new PerkSelectionVM(_hero.HeroDeveloper, _ => OnPerkStagingChanged(), OnPerkStagingChanged);
            }
            BuildPerks();
            RefreshValues();
        }

        private void OnPerkStagingChanged()
        {
            RefreshValues();
            OnStagingChanged?.Invoke();
        }

        [DataSourceProperty]
        public PerkSelectionVM PerkSelection
        {
            get => _perkSelection;
            set { if (value != _perkSelection) { _perkSelection = value; OnPropertyChangedWithValue(value, nameof(PerkSelection)); } }
        }

        public bool HasPendingChanges => (_perkSelection != null && _perkSelection.IsAnyPerkSelected()) || _pendingFocus > 0;

        public void CommitChanges()
        {
            var dev = _hero?.HeroDeveloper;
            var skill = SotorSkills.Spellcraft;
            if (dev == null || skill == null)
            {
                return;
            }

            if (_pendingFocus > 0)
            {
                int toAdd = _pendingFocus;
                _pendingFocus = 0;
                for (int i = 0; i < toAdd && dev.UnspentFocusPoints > 0 && dev.CanAddFocusToSkill(skill); i++)
                {
                    dev.AddFocus(skill, 1, checkUnspentFocusPoints: true);
                }
            }

            _perkSelection?.ApplySelectedPerks();
            SotorLog.Info($"Spellbook: committed staged perks + focus (Spellcraft {_hero.GetSkillValue(skill)}).");
            RefreshValues();
        }

        public void RevertChanges()
        {
            _perkSelection?.ResetSelectedPerks();
            if (_perkSelection != null)
            {
                _perkSelection.IsActive = false;
            }
            _pendingFocus = 0;
            RefreshValues();
        }

        [DataSourceProperty]
        public MBBindingList<SotorStatItemVM> DerivedStats
        {
            get => _derivedStats;
            set { if (value != _derivedStats) { _derivedStats = value; OnPropertyChangedWithValue(value, nameof(DerivedStats)); } }
        }

        public SpellCastingLevel GetStagedCastingLevel()
        {
            if (_hero == null)
            {
                return SpellCastingLevel.None;
            }

            var perks = AbilitySystem.SotorPerks.Instance;
            if (perks != null)
            {
                if (AbilitySystem.SotorPerks.Archmage != null && IsPerkSelected(AbilitySystem.SotorPerks.Archmage))
                {
                    return SpellCastingLevel.Archmage;
                }
                if (AbilitySystem.SotorPerks.MasterSpells != null && IsPerkSelected(AbilitySystem.SotorPerks.MasterSpells))
                {
                    return SpellCastingLevel.Master;
                }
                if (AbilitySystem.SotorPerks.AdeptSpells != null && IsPerkSelected(AbilitySystem.SotorPerks.AdeptSpells))
                {
                    return SpellCastingLevel.Adept;
                }
                if (AbilitySystem.SotorPerks.EntrySpells != null && IsPerkSelected(AbilitySystem.SotorPerks.EntrySpells))
                {
                    return SpellCastingLevel.Entry;
                }
                return SpellCastingLevel.Minor;
            }

            return SotorSpellcraftHelper.GetCastingLevel(_hero);
        }

        private string GetStagedCastingLevelText() => GetStagedCastingLevel().ToString();

        private float GetStagedCasterPerkDamageFactor()
        {
            float factor = 1f;
            if (AbilitySystem.SotorPerks.OverCaster != null && IsPerkSelected(AbilitySystem.SotorPerks.OverCaster))
            {
                factor += 0.2f;
            }
            if (AbilitySystem.SotorPerks.EfficientSpellCaster != null && IsPerkSelected(AbilitySystem.SotorPerks.EfficientSpellCaster))
            {
                factor -= 0.2f;
            }
            if (AbilitySystem.SotorPerks.Dampener != null && IsPerkSelected(AbilitySystem.SotorPerks.Dampener))
            {
                factor -= 0.15f;
            }
            return factor;
        }

        private void RefreshDerivedStats()
        {
            if (_derivedStats == null || _hero == null)
            {
                return;
            }

            _derivedStats.Clear();
            const string windsIcon = "winds_icon_45";

            var rows = new System.Collections.Generic.List<SotorStatItemVM>();

            rows.Add(new SotorStatItemVM("Spell Casting Level:", GetStagedCastingLevelText()));

            float winds = _hero.GetWindsOfMagic();
            float maxWinds = _hero.GetMaxWindsOfMagic();
            float rechargeRate = SOTOR.Extensions.ExtendedInfoSystem.ExtendedInfoManager.GetWindsRechargePerHour(_hero);
            rows.Add(new SotorStatItemVM("Current Winds of Magic:", ((int)Math.Round(winds)).ToString(), windsIcon));
            rows.Add(new SotorStatItemVM("Maximum Winds of Magic:", ((int)Math.Round(maxWinds)).ToString(), windsIcon));
            rows.Add(new SotorStatItemVM("Winds of Magic Recharge:", rechargeRate.ToString("0.00") + " / hour", windsIcon));

            float effFactor = SotorSpellcraftHelper.GetSpellDamageFactor(_hero) * GetStagedCasterPerkDamageFactor();
            int effPct = (int)Math.Round((effFactor - 1f) * 100f);
            rows.Add(new SotorStatItemVM("Spell Effectiveness:", (effPct >= 0 ? "+" : "") + effPct + "%"));
            int durPct = (int)Math.Round((SotorSpellcraftHelper.GetSpellDurationFactor(_hero) - 1f) * 100f);
            rows.Add(new SotorStatItemVM("Spell Duration:", (durPct >= 0 ? "+" : "") + durPct + "%"));

            if (SOTOR.SotorSettings.EnableArcaneConduit)
            {
                var acLevel = GetStagedCastingLevel();
                rows.Add(new SotorStatItemVM(
                    AbilitySystem.SotorArcaneConduitHelper.GetSpellbookLabel(acLevel),
                    AbilitySystem.SotorArcaneConduitHelper.GetSpellbookValue(_hero, acLevel)));
            }

            AddSpellModifierPerkRows(rows, windsIcon);

            AddSelfAllyPerkRows(rows, windsIcon);

            System.Collections.Generic.List<string> loreTitles = StagedOwnedLoreTitlesProvider?.Invoke();
            if (loreTitles == null)
            {
                var owned = _hero.GetExtendedInfo()?.AcquiredLores ?? new System.Collections.Generic.List<string>();
                loreTitles = owned
                    .Select(id => SotorLores.Display.TryGetValue(id, out var d) ? d.Title : id)
                    .ToList();
            }
            rows.Add(new SotorStatItemVM("Known Magic Lores:",
                loreTitles.Count > 0 ? string.Join(", ", loreTitles) : "None"));

            bool ownsMindControl = StagedOwnedSpellProvider != null
                ? StagedOwnedSpellProvider("MindControl")
                : (_hero?.GetExtendedInfo()?.HasSpell("MindControl") == true);
            if (ownsMindControl)
            {
                int mcPct = (int)System.Math.Round(AbilitySystem.SotorMindControlHelper.GetBaseChance(_hero) * 100f);
                rows.Add(new SotorStatItemVM("Base Mind Control Chance:", mcPct + "%"));
            }
            string necroTitle = SotorLores.Display.TryGetValue(SotorLores.LoreOfNecromancy, out var nd) ? nd.Title : "Lore of Necromancy";
            if (loreTitles.Contains(necroTitle))
            {
                int levelsAboveEntry = (int)GetStagedCastingLevel() - (int)SpellCastingLevel.Entry;
                int reductionPct = levelsAboveEntry > 0 ? levelsAboveEntry * 20 : 0;
                rows.Add(new SotorStatItemVM("Skeleton Troop Weight:",
                    reductionPct > 0 ? "-" + reductionPct + "%" : "0%"));
            }

            for (int i = rows.Count - 1; i >= 0; i--)
            {
                _derivedStats.Add(rows[i]);
            }

            OnPropertyChanged(nameof(DerivedStats));
        }

        private void AddSpellModifierPerkRows(System.Collections.Generic.List<SotorStatItemVM> rows, string windsIcon)
        {
            if (AbilitySystem.SotorPerks.Instance == null)
            {
                return;
            }

            if (AbilitySystem.SotorPerks.OverCaster != null && IsPerkSelected(AbilitySystem.SotorPerks.OverCaster))
            {
                rows.Add(new SotorStatItemVM("Spell Winds Cost:", "+30%", windsIcon));
            }
            else if (AbilitySystem.SotorPerks.EfficientSpellCaster != null && IsPerkSelected(AbilitySystem.SotorPerks.EfficientSpellCaster))
            {
                rows.Add(new SotorStatItemVM("Spell Winds Cost:", "-30%", windsIcon));
            }

            if (AbilitySystem.SotorPerks.Dampener != null && IsPerkSelected(AbilitySystem.SotorPerks.Dampener))
            {
                rows.Add(new SotorStatItemVM("Ward Save:", "5%"));
            }
        }

        private void AddSelfAllyPerkRows(System.Collections.Generic.List<SotorStatItemVM> rows, string windsIcon)
        {
            if (AbilitySystem.SotorPerks.Instance == null)
            {
                return;
            }

            if (AbilitySystem.SotorPerks.Selfish != null && IsPerkSelected(AbilitySystem.SotorPerks.Selfish))
            {
                rows.Add(new SotorStatItemVM("Self Spell Damage:", "-90%"));
            }

            if (AbilitySystem.SotorPerks.WellControlled != null && IsPerkSelected(AbilitySystem.SotorPerks.WellControlled))
            {
                rows.Add(new SotorStatItemVM("Friendly Spell Damage:", "-30%"));
            }

            if (AbilitySystem.SotorPerks.Catalyst != null && IsPerkSelected(AbilitySystem.SotorPerks.Catalyst))
            {
                rows.Add(new SotorStatItemVM("Catalyst (legendary gear):", "+5 / item", windsIcon));
            }
        }

        private void BuildPerks()
        {
            _perks = new MBBindingList<PerkVM>();
            if (_hero == null || AbilitySystem.SotorPerks.Instance == null)
            {
                return;
            }

            var list = new[]
            {
                AbilitySystem.SotorPerks.EntrySpells,
                AbilitySystem.SotorPerks.Selfish, AbilitySystem.SotorPerks.WellControlled,
                AbilitySystem.SotorPerks.AdeptSpells,
                AbilitySystem.SotorPerks.Librarian, AbilitySystem.SotorPerks.StoryTeller,
                AbilitySystem.SotorPerks.OverCaster, AbilitySystem.SotorPerks.EfficientSpellCaster,
                AbilitySystem.SotorPerks.MasterSpells,
                AbilitySystem.SotorPerks.Improvision, AbilitySystem.SotorPerks.Catalyst,
                AbilitySystem.SotorPerks.Dampener, AbilitySystem.SotorPerks.ArcaneLink,
                AbilitySystem.SotorPerks.TrueTransmutation,
            };

            foreach (var perk in list)
            {
                if (perk == null)
                {
                    continue;
                }

                var alt = perk.AlternativePerk == null
                    ? PerkVM.PerkAlternativeType.NoAlternative
                    : (string.CompareOrdinal(perk.StringId, perk.AlternativePerk.StringId) < 0
                        ? PerkVM.PerkAlternativeType.FirstAlternative
                        : PerkVM.PerkAlternativeType.SecondAlternative);

                var vm = new PerkVM(
                    perk,
                    IsPerkAvailable(perk),
                    alt,
                    OnStartPerkSelection,
                    _ => { },
                    IsPerkSelected,
                    IsPreviousPerkSelected);

                vm.PerkId = "SPPerks\\" + perk.StringId.Replace("Sotor", "");
                DiagnoseSprite(vm.PerkId);
                _perks.Add(vm);
            }
        }

        private static readonly System.Collections.Generic.HashSet<string> _diagnosed = new System.Collections.Generic.HashSet<string>();
        private static void DiagnoseSprite(string perkId)
        {
            if (!_diagnosed.Add(perkId))
            {
                return;
            }
            try
            {
                var sd = TaleWorlds.Engine.GauntletUI.UIResourceManager.SpriteData;
                var sp = sd?.GetSprite(perkId);
                var known = sd?.GetSprite("fireball_icon");
                int cats = sd?.SpriteCategories?.Count ?? -1;
                bool hasUiSotor = sd?.SpriteCategories?.ContainsKey("ui_sotor") ?? false;
                bool hasPerks = sd?.SpriteCategories?.ContainsKey("ui_sotor_perks") ?? false;
                bool perksLoaded = hasPerks && (sd.SpriteCategories["ui_sotor_perks"].IsLoaded);
                SotorLog.Info(
                    $"SpriteDiag: '{perkId}' -> {(sp != null ? $"FOUND ({sp.Width}x{sp.Height} hasTex={sp.Texture != null})" : "NULL")}; " +
                    $"fireball_icon -> {(known != null ? "FOUND" : "NULL")}; categories={cats} ui_sotor={hasUiSotor} " +
                    $"ui_sotor_perks={hasPerks} perksLoaded={perksLoaded}.");
            }
            catch (System.Exception ex)
            {
                SotorLog.Warn($"SpriteDiag failed: {ex.Message}");
            }
        }

        private bool IsPerkAvailable(PerkObject perk)
        {
            var skill = SotorSkills.Spellcraft;
            return _hero != null && skill != null && _hero.GetSkillValue(skill) >= (int)perk.RequiredSkillValue;
        }

        private bool IsPerkSelected(PerkObject perk) =>
            _hero != null && (_hero.GetPerkValue(perk) || (_perkSelection != null && _perkSelection.IsPerkSelected(perk)));

        public bool IsPerkSelectedStaged(PerkObject perk) => IsPerkSelected(perk);

        private bool IsPreviousPerkSelected(PerkObject perk)
        {
            var sc = SotorSkills.Spellcraft;
            if (perk == null || sc == null)
            {
                return false;
            }

            var below = PerkObject.All.Where(p => p.Skill == sc && p.RequiredSkillValue < perk.RequiredSkillValue).ToList();
            if (below.Count == 0)
            {
                return true;
            }

            var prev = below.OrderByDescending(p => p.RequiredSkillValue).First();
            if (IsPerkSelected(prev))
            {
                return true;
            }
            return prev.AlternativePerk != null && IsPerkSelected(prev.AlternativePerk);
        }

        private void OnStartPerkSelection(PerkVM perk)
        {
            var p0 = perk?.Perk;
            if (_hero?.HeroDeveloper == null || p0 == null || _perkSelection == null)
            {
                return;
            }

            if (IsPerkSelected(p0))
            {
                return;
            }

            if (!IsPerkAvailable(p0) || !IsPreviousPerkSelected(p0))
            {
                SotorLog.Info($"Spellbook: perk '{p0.StringId}' not selectable (avail={IsPerkAvailable(p0)} prevSel={IsPreviousPerkSelected(p0)}).");
                return;
            }

            _perkSelection.SetCurrentSelectionPerk(perk);
        }

        [DataSourceProperty]
        public MBBindingList<PerkVM> Perks
        {
            get => _perks;
            set { if (value != _perks) { _perks = value; OnPropertyChangedWithValue(value, nameof(Perks)); } }
        }

        [DataSourceProperty]
        public float LearningRate
        {
            get => _learningRate;
            set { if (value != _learningRate) { _learningRate = value; OnPropertyChangedWithValue(value, nameof(LearningRate)); } }
        }

        [DataSourceProperty]
        public bool CanLearnSkill
        {
            get => _canLearnSkill;
            set { if (value != _canLearnSkill) { _canLearnSkill = value; OnPropertyChangedWithValue(value, nameof(CanLearnSkill)); } }
        }

        [DataSourceProperty]
        public int CurrentFocusLevel => _focus;

        [DataSourceProperty]
        public string CurrentLearningRateText => "Learning Rate: x " + _learningRate.ToString("0.00");

        [DataSourceProperty]
        public string FocusPointsText => "Focus Points";

        [DataSourceProperty]
        public int CurrentSkillXP => _skillXpProgress;

        [DataSourceProperty]
        public int XpRequiredForNextLevel => _skillXpForNext > 0 ? _skillXpForNext : 1;

        [DataSourceProperty]
        public string ProgressText => _skillXpProgress + " / " + XpRequiredForNextLevel + " XP";

        [DataSourceProperty]
        public int FullLearningRateLevel
        {
            get => _fullLearningRateLevel;
            set { if (value != _fullLearningRateLevel) { _fullLearningRateLevel = value; OnPropertyChangedWithValue(value, nameof(FullLearningRateLevel)); } }
        }

        public sealed override void RefreshValues()
        {
            base.RefreshValues();

            var skill = SotorSkills.Spellcraft;
            var dev = _hero?.HeroDeveloper;
            if (_hero == null || skill == null || dev == null)
            {
                return;
            }

            int stagedFocus = dev.GetFocus(skill) + _pendingFocus;

            SkillLevel = _hero.GetSkillValue(skill);
            Focus = stagedFocus;
            UnspentFocus = Math.Max(0, dev.UnspentFocusPoints - _pendingFocus);
            AttributeValue = _hero.GetAttributeValue(DefaultCharacterAttributes.Intelligence);
            UnspentAttribute = dev.UnspentAttributePoints;
            CastingLevelText = SotorSpellcraftHelper.GetCastingLevel(_hero).ToString();

            try
            {
                var model = Campaign.Current?.Models?.CharacterDevelopmentModel;
                if (model != null)
                {
                    SkillXpProgress = dev.GetSkillXpProgress(skill);
                    SkillXpForNext = model.GetXpRequiredForSkillLevel(SkillLevel + 1)
                                     - model.GetXpRequiredForSkillLevel(SkillLevel);
                }
            }
            catch
            {

            }

            try
            {
                var model = Campaign.Current?.Models?.CharacterDevelopmentModel;
                if (model != null && skill != null)
                {
                    FullLearningRateLevel = (int)Math.Round(
                        model.CalculateLearningLimit(_hero.CharacterAttributes, stagedFocus, skill).ResultNumber);

                    LearningRate = model.CalculateLearningRate(_hero.CharacterAttributes, stagedFocus, SkillLevel, skill).ResultNumber;
                    CanLearnSkill = SkillLevel < FullLearningRateLevel;
                }
            }
            catch
            {

            }

            OnPropertyChanged(nameof(CurrentFocusLevel));
            OnPropertyChanged(nameof(CurrentLearningRateText));
            OnPropertyChanged(nameof(CurrentSkillXP));
            OnPropertyChanged(nameof(XpRequiredForNextLevel));
            OnPropertyChanged(nameof(ProgressText));

            if (_perks != null)
            {
                foreach (var perk in _perks)
                {
                    perk.RefreshState();
                }
            }

            RefreshDerivedStats();
        }

        public void ExecuteAddFocus()
        {
            var skill = SotorSkills.Spellcraft;
            var dev = _hero?.HeroDeveloper;
            SotorLog.Info($"CLICKDIAG focus: FIRED unspent={dev?.UnspentFocusPoints ?? -1} pending={_pendingFocus} focus={(skill != null && dev != null ? dev.GetFocus(skill) : -1)}.");
            if (skill == null || dev == null)
            {
                return;
            }

            int unspentAfterStaged = dev.UnspentFocusPoints - _pendingFocus;
            int stagedFocus = dev.GetFocus(skill) + _pendingFocus;
            int maxPerSkill = Campaign.Current?.Models?.CharacterDevelopmentModel?.MaxFocusPerSkill ?? 5;

            if (unspentAfterStaged > 0 && stagedFocus < maxPerSkill)
            {
                _pendingFocus++;
                SotorLog.Info($"Spellbook: STAGED +1 focus (pending={_pendingFocus}).");
                RefreshValues();
            }
        }

        public void ExecuteAddAttribute()
        {
            var dev = _hero?.HeroDeveloper;
            if (dev == null)
            {
                return;
            }

            if (dev.UnspentAttributePoints > 0)
            {
                dev.AddAttribute(DefaultCharacterAttributes.Intelligence, 1, checkUnspentPoints: true);
                SotorLog.Info($"Spellbook: +1 Intelligence (now {_hero.GetAttributeValue(DefaultCharacterAttributes.Intelligence)}, unspent {dev.UnspentAttributePoints}).");
                RefreshValues();
            }
        }

        [DataSourceProperty]
        public string SkillName
        {
            get => _skillName;
            set { if (value != _skillName) { _skillName = value; OnPropertyChangedWithValue(value, nameof(SkillName)); } }
        }

        [DataSourceProperty]
        public int SkillLevel
        {
            get => _skillLevel;
            set { if (value != _skillLevel) { _skillLevel = value; OnPropertyChangedWithValue(value, nameof(SkillLevel)); OnPropertyChanged(nameof(SkillLevelText)); } }
        }

        [DataSourceProperty]
        public string SkillLevelText => _skillLevel.ToString();

        [DataSourceProperty]
        public int Focus
        {
            get => _focus;
            set { if (value != _focus) { _focus = value; OnPropertyChangedWithValue(value, nameof(Focus)); } }
        }

        [DataSourceProperty]
        public int MaxFocus
        {
            get => _maxFocus;
            set { if (value != _maxFocus) { _maxFocus = value; OnPropertyChangedWithValue(value, nameof(MaxFocus)); } }
        }

        [DataSourceProperty]
        public int UnspentFocus
        {
            get => _unspentFocus;
            set { if (value != _unspentFocus) { _unspentFocus = value; OnPropertyChangedWithValue(value, nameof(UnspentFocus)); OnPropertyChanged(nameof(CanAddFocus)); } }
        }

        [DataSourceProperty]
        public bool CanAddFocus => _unspentFocus > 0 && _focus < _maxFocus;

        [DataSourceProperty]
        public string AttributeName
        {
            get => _attributeName;
            set { if (value != _attributeName) { _attributeName = value; OnPropertyChangedWithValue(value, nameof(AttributeName)); } }
        }

        [DataSourceProperty]
        public int AttributeValue
        {
            get => _attributeValue;
            set { if (value != _attributeValue) { _attributeValue = value; OnPropertyChangedWithValue(value, nameof(AttributeValue)); OnPropertyChanged(nameof(AttributeValueText)); } }
        }

        [DataSourceProperty]
        public string AttributeValueText => _attributeValue.ToString();

        [DataSourceProperty]
        public int UnspentAttribute
        {
            get => _unspentAttribute;
            set { if (value != _unspentAttribute) { _unspentAttribute = value; OnPropertyChangedWithValue(value, nameof(UnspentAttribute)); OnPropertyChanged(nameof(CanAddAttribute)); } }
        }

        [DataSourceProperty]
        public bool CanAddAttribute => _unspentAttribute > 0;

        [DataSourceProperty]
        public string CastingLevelText
        {
            get => _castingLevelText;
            set { if (value != _castingLevelText) { _castingLevelText = value; OnPropertyChangedWithValue(value, nameof(CastingLevelText)); } }
        }

        [DataSourceProperty]
        public int SkillXpProgress
        {
            get => _skillXpProgress;
            set { if (value != _skillXpProgress) { _skillXpProgress = value; OnPropertyChangedWithValue(value, nameof(SkillXpProgress)); } }
        }

        [DataSourceProperty]
        public int SkillXpForNext
        {
            get => _skillXpForNext;
            set { if (value != _skillXpForNext) { _skillXpForNext = value; OnPropertyChangedWithValue(value, nameof(SkillXpForNext)); } }
        }
    }
}
