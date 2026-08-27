using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using SOTOR;
using SOTOR.AbilitySystem.Crosshairs;
using TaleWorlds.ModuleManager;
using SOTOR.Extensions;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;

namespace SOTOR.AbilitySystem
{
    public static class AbilityFactory
    {
        private static readonly Dictionary<string, AbilityTemplate> Templates = new Dictionary<string, AbilityTemplate>();

        private const string TemplateFileName = "tor_abilitytemplates.xml";

        public static AbilityTemplate GetTemplate(string id)
        {
            return Templates.TryGetValue(id, out var template) ? template : null;
        }

        public static IReadOnlyList<AbilityTemplate> GetTemplatesByLore(string loreId)
        {

            return Templates.Values
                .Where(t => t.BelongsToLoreID == loreId)
                .OrderBy(t => t.SpellTier)
                .ToList();
        }

        public static void LoadTemplates()
        {
            Templates.Clear();

            var modulePath = ModuleHelper.GetModuleFullPath("SOTOR");
            var path = Path.Combine(modulePath, "ModuleData", "tor_custom_xmls", TemplateFileName);
            if (!File.Exists(path))
            {
                SotorLog.Warn($"Ability templates not found at {path}");
                return;
            }

            var serializer = new XmlSerializer(typeof(List<AbilityTemplate>), new XmlRootAttribute("AbilityTemplates"));
            using (var stream = File.OpenRead(path))
            {
                if (serializer.Deserialize(stream) is List<AbilityTemplate> list)
                {
                    foreach (var template in list.Where(t => t != null && !string.IsNullOrEmpty(t.StringID)))
                    {
                        Templates[template.StringID] = template;
                    }
                }
            }

            SotorLog.Info($"Loaded {Templates.Count} ability template(s) from {path}");
        }
        public static Ability CreateNew(string id, Agent caster)
        {

            bool isPlayerCaster = caster != null
                && (caster.IsMainAgent
                    || (TaleWorlds.CampaignSystem.Hero.MainHero != null
                        && caster.GetHero() == TaleWorlds.CampaignSystem.Hero.MainHero));

            if (id == "AmberSpear" && SotorSettings.UseThrownAmberSpear && isPlayerCaster)
            {
                id = "AmberSpearThrown";
                SotorLog.Info("AbilityFactory: AmberSpear -> AmberSpearThrown for the player.");
            }

            if (!Templates.TryGetValue(id, out var template))
            {
                return null;
            }

            if (id == "AmberSpearThrown")
            {
                return new ThrownWeaponAbility(template);
            }

            if (template.AbilityType == AbilityType.Spell)
            {
                return new Spell(template);
            }

            return null;
        }

        public static AbilityCrosshair InitializeCrosshair(AbilityTemplate template, Mission mission, MissionScreen missionScreen, Agent caster)
        {
            switch (template.CrosshairType)
            {
                case CrosshairType.Missile:
                    return new MissileCrosshair(template, mission, missionScreen, caster);
                case CrosshairType.SingleTarget:
                    return new SingleTargetCrosshair(template, mission, missionScreen, caster);
                case CrosshairType.Self:
                    return new SelfCrosshair(template, mission, missionScreen, caster);
                case CrosshairType.Wind:
                    return new WindCrosshair(template, mission, missionScreen, caster);
                case CrosshairType.TargetedAOE:
                    return new TargetedAOECrosshair(template, mission, missionScreen, caster);

                default:
                    SotorLog.Debug($"InitializeCrosshair: no crosshair impl for {template.CrosshairType} yet.");
                    return null;
            }
        }
    }
}
