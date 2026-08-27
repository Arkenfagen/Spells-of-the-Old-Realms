using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;

namespace SOTOR.Extensions.ExtendedInfoSystem
{
    public class HeroExtendedInfo
    {

        public static float TestingMaxWindsOverride = -1f;

        public const float DefaultMaxWindsOfMagic = 100f;

        [SaveableField(0)]
        public List<string> AcquiredAbilities = new List<string>();

        [SaveableField(1)]
        public List<string> AcquiredAttributes = new List<string>();

        [SaveableField(2)]
        private List<string> _selectedAbilities;

        [SaveableField(3)]
        private CharacterObject _baseCharacter;

        [SaveableField(4)]
        private float _windsOfMagic;

        [SaveableField(5)]
        private bool _windsInitialized;

        [SaveableField(6)]
        public List<string> AcquiredLores = new List<string>();

        private void EnsureLores()
        {
            if (AcquiredLores == null) AcquiredLores = new List<string>();
        }

        public bool HasLore(string loreId)
        {
            EnsureLores();
            return loreId != null && AcquiredLores.Contains(loreId);
        }

        public void AddLore(string loreId)
        {
            EnsureLores();
            if (loreId != null && !AcquiredLores.Contains(loreId))
            {
                AcquiredLores.Add(loreId);
            }
        }

        public void RemoveLore(string loreId)
        {
            EnsureLores();
            AcquiredLores.Remove(loreId);
        }

        [SaveableField(7)]
        public List<string> AcquiredSpells = new List<string>();

        [SaveableField(8)]
        public double WindsCreditedHours;

        [SaveableField(9)]
        public List<string> KnownBlueprints = new List<string>();

        private void EnsureBlueprints()
        {
            if (KnownBlueprints == null) KnownBlueprints = new List<string>();
        }

        public bool HasBlueprint(string traitId)
        {
            EnsureBlueprints();
            return traitId != null && KnownBlueprints.Contains(traitId);
        }

        public void AddBlueprint(string traitId)
        {
            EnsureBlueprints();
            if (traitId != null && !KnownBlueprints.Contains(traitId))
            {
                KnownBlueprints.Add(traitId);
            }
        }

        public List<string> AllKnownBlueprints
        {
            get
            {
                EnsureBlueprints();
                return KnownBlueprints;
            }
        }

        private void EnsureSpells()
        {
            if (AcquiredSpells == null) AcquiredSpells = new List<string>();
        }

        public bool HasSpell(string abilityId)
        {
            EnsureSpells();
            return abilityId != null && AcquiredSpells.Contains(abilityId);
        }

        public void AddSpell(string abilityId)
        {
            EnsureSpells();
            if (abilityId != null && !AcquiredSpells.Contains(abilityId))
            {
                AcquiredSpells.Add(abilityId);
            }
        }

        public void RemoveSpell(string abilityId)
        {
            EnsureSpells();
            AcquiredSpells.Remove(abilityId);
        }

        public float MaxWindsOfMagic
        {
            get
            {
                if (TestingMaxWindsOverride >= 0f)
                {
                    return TestingMaxWindsOverride;
                }
                var hero = _baseCharacter?.HeroObject;
                return hero != null
                    ? AbilitySystem.SotorSpellcraftHelper.GetMaxWinds(hero)
                    : DefaultMaxWindsOfMagic;
            }
        }

        public float WindsOfMagic
        {
            get
            {
                EnsureWindsInitialized();
                return _windsOfMagic;
            }
        }

        private void EnsureWindsInitialized()
        {
            if (!_windsInitialized)
            {
                _windsInitialized = true;
                _windsOfMagic = MaxWindsOfMagic;
            }
        }

        public void AddWindsOfMagic(float amount, bool allowOverMax = false)
        {
            EnsureWindsInitialized();
            float v = Math.Max(0f, _windsOfMagic + amount);
            if (!allowOverMax)
            {
                v = Math.Min(MaxWindsOfMagic, v);
            }
            _windsOfMagic = v;
        }

        public void SetWindsOfMagic(float amount)
        {
            _windsInitialized = true;
            _windsOfMagic = Math.Max(0f, Math.Min(MaxWindsOfMagic, amount));
        }

        public List<string> AllAbilities
        {
            get
            {
                var list = new List<string>();
                if (_baseCharacter != null)
                {
                    list.AddRange(_baseCharacter.GetAbilities());
                }

                list.AddRange(AcquiredAbilities);
                return list.Distinct().ToList();
            }
        }

        public List<string> AllAttributes
        {
            get
            {
                var list = new List<string>();
                if (_baseCharacter != null)
                {
                    list.AddRange(_baseCharacter.GetAttributes());
                }

                list.AddRange(AcquiredAttributes);
                return list.Distinct().ToList();
            }
        }

        public List<string> SelectedAbilities => _selectedAbilities ?? new List<string>();

        public HeroExtendedInfo(CharacterObject character)
        {
            _baseCharacter = character;
            _selectedAbilities = new List<string>();
        }

        public void AddSelectedAbility(string abilityId)
        {
            if (!_selectedAbilities.Contains(abilityId))
            {
                _selectedAbilities.Add(abilityId);
            }
        }

        public void RemoveSelectedAbility(string abilityId)
        {
            _selectedAbilities.Remove(abilityId);
        }

        public void EnsureSelectedAbilityFirst(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId) || _selectedAbilities == null) return;
            if (_selectedAbilities.Count > 0 && _selectedAbilities[0] == abilityId) return;
            _selectedAbilities.Remove(abilityId);
            _selectedAbilities.Insert(0, abilityId);
        }

        public void ToggleSelectedAbility(string abilityId)
        {
            if (IsAbilitySelected(abilityId))
            {
                RemoveSelectedAbility(abilityId);
            }
            else if (AllAbilities.Contains(abilityId))
            {
                AddSelectedAbility(abilityId);
            }
        }

        public bool IsAbilitySelected(string abilityId)
        {
            return _selectedAbilities.Contains(abilityId);
        }
    }
}
