using System.Collections.Generic;
using SOTOR.AbilitySystem;
using SOTOR;
using SOTOR.Extensions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;

namespace SOTOR.Extensions.ExtendedInfoSystem
{
    public class ExtendedInfoManager : CampaignBehaviorBase
    {
        private static ExtendedInfoManager _instance;

        private Dictionary<string, HeroExtendedInfo> _heroInfos = new Dictionary<string, HeroExtendedInfo>();

        public static ExtendedInfoManager Instance => _instance;

        public ExtendedInfoManager()
        {
            _instance = this;
        }

        private const float WindsRechargePerHour = 2f;

        private float _lastLoggedPlayerRate = float.NaN;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.OnNewGameCreatedPartialFollowUpEvent.AddNonSerializedListener(this, OnNewGameCreatedPartialFollowUp);
            CampaignEvents.HeroCreated.AddNonSerializedListener(this, OnHeroCreated);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);

            CampaignEvents.NewCompanionAdded.AddNonSerializedListener(this, OnNewCompanionAdded);
        }

        private void OnHourlyTick()
        {
            foreach (var hero in Hero.AllAliveHeroes)
            {
                if (hero == null || hero.IsNotable)
                {
                    continue;
                }

                if (!_heroInfos.TryGetValue(hero.GetInfoKey(), out var info))
                {
                    continue;
                }

                if (info.AllAttributes.Contains("SpellCaster"))
                {
                    float factor = GetArmorRechargeFactor(hero, out float armorWeight);

                    float rate = (WindsRechargePerHour + SOTOR.AbilitySystem.SotorSpellcraftHelper.GetWindsRechargeSkillBonus(hero)) * factor * GetCatalystTownFactor(hero);

                    CreditWindsUpTo(hero, CampaignTime.Now);

                    if (hero.IsHumanPlayerCharacter && System.Math.Abs(rate - _lastLoggedPlayerRate) > 0.001f)
                    {
                        _lastLoggedPlayerRate = rate;
                        float penalty = System.Math.Max(0f, (armorWeight - 5f) / 15f);
                        SotorLog.Info(
                            $"Winds regen rate changed: {hero.Name} armorWt={armorWeight:0.#}lb " +
                            $"penalty={penalty:0.##} factor={factor:0.##} -> +{rate:0.##}/hr " +
                            $"({info.WindsOfMagic:0}/{info.MaxWindsOfMagic:0}).");
                    }
                }
            }
        }

        public static bool IsPlayerSideCaster(Hero hero)
        {
            if (hero == null) return false;
            if (hero.IsHumanPlayerCharacter) return true;
            if (hero.Clan != null && hero.Clan == Clan.PlayerClan) return true;
            if (hero.PartyBelongedTo != null && hero.PartyBelongedTo == MobileParty.MainParty) return true;
            return false;
        }

        private static float GetArmorRechargeFactor(Hero hero, out float armorWeight)
        {
            armorWeight = 0f;
            var equipment = hero?.BattleEquipment;
            if (equipment == null)
            {
                return 1f;
            }

            armorWeight = equipment.GetTotalWeightOfArmor(true);

            if (!IsPlayerSideCaster(hero))
            {
                return 1f;
            }

            float penalty = System.Math.Max(0f, (armorWeight - 5f) / 15f);

            penalty *= SOTOR.SotorSettings.ArmorWomRechargeEffectMultiplier;
            return System.Math.Max(0f, 1f - penalty);
        }

        public static float GetWindsRechargePerHour(Hero hero)
        {
            return (WindsRechargePerHour + SOTOR.AbilitySystem.SotorSpellcraftHelper.GetWindsRechargeSkillBonus(hero))
                   * GetArmorRechargeFactor(hero, out _) * GetCatalystTownFactor(hero);
        }

        public static float CreditWindsUpTo(Hero hero, CampaignTime now)
        {
            var info = hero?.GetExtendedInfo();
            if (info == null || !info.AllAttributes.Contains("SpellCaster")) return 0f;

            double nowHours = now.ToHours;
            double last = info.WindsCreditedHours;

            if (last <= 0.0 || last > nowHours)
            {

                info.WindsCreditedHours = nowHours;
                return 0f;
            }

            float hours = (float)(nowHours - last);
            if (hours <= 0f)
            {
                return 0f;
            }

            float rate = GetWindsRechargePerHour(hero);
            info.WindsCreditedHours = nowHours;
            if (rate <= 0f)
            {

                return 0f;
            }

            float before = info.WindsOfMagic;
            info.AddWindsOfMagic(rate * hours);
            return info.WindsOfMagic - before;
        }

        private static float GetCatalystTownFactor(Hero hero)
        {
            var perk = SOTOR.AbilitySystem.SotorPerks.Catalyst;
            if (perk == null || hero == null || !hero.GetPerkValue(perk))
            {
                return 1f;
            }
            var settlement = hero.PartyBelongedTo?.CurrentSettlement;
            return (settlement != null && settlement.IsTown) ? 1.2f : 1f;
        }

        public HeroExtendedInfo GetHeroInfoFor(string heroId)
        {
            return _heroInfos.TryGetValue(heroId, out var info) ? info : null;
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            InitializeHeroes();
            EnsureStarterSpellSetup();
        }

        private void OnNewGameCreatedPartialFollowUp(CampaignGameStarter starter, int index)
        {
            if (index == 98)
            {
                InitializeHeroes();
                EnsureStarterSpellSetup();
            }
        }

        private void OnHeroCreated(Hero hero, bool bornNaturally)
        {
            EnsureHeroInfo(hero);
        }

        private void OnNewCompanionAdded(Hero hero)
        {
            if (hero == null || !SotorSettings.EnableCompanionSpellcasters)
            {
                return;
            }
            EnsureCasterSetupFor(hero);
        }

        private void InitializeHeroes()
        {
            foreach (var hero in Hero.AllAliveHeroes)
            {
                EnsureHeroInfo(hero);
            }
        }

        private void EnsureHeroInfo(Hero hero)
        {
            if (hero == null || hero.IsNotable)
            {
                return;
            }

            var key = hero.GetInfoKey();
            if (!_heroInfos.ContainsKey(key))
            {
                _heroInfos.Add(key, new HeroExtendedInfo(hero.CharacterObject));
            }
        }

        public static System.Collections.Generic.List<Hero> GetSpellcasterPartyHeroes()
        {
            var list = new System.Collections.Generic.List<Hero>();
            var main = Hero.MainHero;
            if (main == null)
            {
                return list;
            }
            list.Add(main);

            if (!SotorSettings.EnableCompanionSpellcasters)
            {
                return list;
            }

            var clan = Clan.PlayerClan;
            if (clan != null)
            {
                foreach (var h in clan.Heroes)
                {
                    if (h != null && h.IsAlive && !h.IsNotable && !h.IsChild && !list.Contains(h))
                    {
                        list.Add(h);
                    }
                }
                foreach (var h in clan.Companions)
                {
                    if (h != null && h.IsAlive && !h.IsNotable && !h.IsChild && !list.Contains(h))
                    {
                        list.Add(h);
                    }
                }
            }

            if (_instance != null)
            {
                foreach (var h in list)
                {
                    _instance.EnsureCasterSetupFor(h);
                }
            }
            return list;
        }

        private void EnsureStarterSpellSetup()
        {

            foreach (var h in GetSpellcasterPartyHeroes())
            {
                EnsureCasterSetupFor(h);
            }
        }

        private void EnsureCasterSetupFor(Hero hero)
        {
            if (hero == null)
            {
                return;
            }

            EnsureHeroInfo(hero);
            hero.AddAttribute("AbilityUser");
            hero.AddAttribute("SpellCaster");

            var info = hero.GetExtendedInfo();

            var ownedLores = info?.AcquiredLores ?? new System.Collections.Generic.List<string>();
            int granted = 0;
            foreach (var loreId in ownedLores)
            {
                foreach (var template in AbilityFactory.GetTemplatesByLore(loreId))
                {
                    if (info != null && info.HasSpell(template.StringID) && !hero.HasAbility(template.StringID))
                    {
                        hero.AddAbility(template.StringID);
                        granted++;
                    }
                }
            }
            SotorLog.Info($"Granted {granted} purchased spell(s) across {ownedLores.Count} owned lore(s).");

            const string arcaneConduitId = AbilitySystem.SotorArcaneConduitHelper.AbilityId;
            if (AbilitySystem.AbilityFactory.GetTemplate(arcaneConduitId) != null)
            {
                if (!hero.HasAbility(arcaneConduitId))
                {
                    hero.AddAbility(arcaneConduitId);
                }
                info?.EnsureSelectedAbilityFirst(arcaneConduitId);
                SotorLog.Info($"Arcane Conduit granted + pinned to wheel slot 0 for {hero.GetInfoKey()}.");
            }

            var selected = hero.GetExtendedInfo()?.SelectedAbilities ?? new System.Collections.Generic.List<string>();
            SotorLog.Info(
                $"Starter spell setup. hero={hero.GetInfoKey()} abilityUser={hero.HasAttribute("AbilityUser")} known={hero.GetExtendedInfo()?.AllAbilities.Count ?? 0} winds={hero.GetExtendedInfo()?.WindsOfMagic:0}/{hero.GetExtendedInfo()?.MaxWindsOfMagic:0} selected=[{string.Join(",", selected)}]");
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("_heroInfos", ref _heroInfos);
        }
    }
}
