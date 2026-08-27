using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SOTOR.Extensions;
using SOTOR.Extensions.ExtendedInfoSystem;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Core.ViewModelCollection.Information;

namespace SOTOR
{

    [HarmonyPatch(typeof(MapInfoVM), "Refresh")]
    public static class SotorMapBarWindsPatch
    {

        private static readonly ConditionalWeakTable<MapInfoVM, MapInfoItemVM> _windsItems =
            new ConditionalWeakTable<MapInfoVM, MapInfoItemVM>();

        private static int _winds;
        private static int _maxWinds;
        private static float _rechargeRate;

        [HarmonyPostfix]
        public static void Postfix(MapInfoVM __instance)
        {
            try
            {
                var hero = Hero.MainHero;
                if (hero == null || __instance?.SecondaryInfoItems == null)
                {
                    return;
                }

                bool isCaster = hero.HasAttribute("SpellCaster");
                bool hasItem = _windsItems.TryGetValue(__instance, out var windsItem);

                if (!isCaster)
                {
                    if (hasItem && __instance.SecondaryInfoItems.Contains(windsItem))
                    {
                        __instance.SecondaryInfoItems.Remove(windsItem);
                    }
                    return;
                }

                if (!hasItem)
                {
                    windsItem = new MapInfoItemVM("winds", (Func<List<TooltipProperty>>)GetWindsHintText);
                    _windsItems.Add(__instance, windsItem);
                }

                if (!__instance.SecondaryInfoItems.Contains(windsItem))
                {
                    __instance.SecondaryInfoItems.Add(windsItem);
                }

                _winds = (int)Math.Round(hero.GetWindsOfMagic());
                _maxWinds = (int)Math.Round(hero.GetMaxWindsOfMagic());
                _rechargeRate = ExtendedInfoManager.GetWindsRechargePerHour(hero);

                windsItem.HasWarning = _winds < 0;
                windsItem.IntValue = _winds;
                windsItem.Value = _winds.ToString();
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"MapBarWinds refresh failed: {ex.Message}");
            }
        }

        private static List<TooltipProperty> GetWindsHintText()
        {
            return new List<TooltipProperty>
            {
                new TooltipProperty(SotorText.Rendered("sotor_mapbar_winds_title"), _winds.ToString(), 0, false, TooltipProperty.TooltipPropertyFlags.Title),
                new TooltipProperty(SotorText.Rendered("sotor_mapbar_winds_max"), _maxWinds.ToString(), 0),
                new TooltipProperty(SotorText.Rendered("sotor_mapbar_winds_recharge"), $"{_rechargeRate:0.00} / hour", 0)
            };
        }
    }
}
