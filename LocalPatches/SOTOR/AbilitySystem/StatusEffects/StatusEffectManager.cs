using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem.StatusEffects
{

    public static class StatusEffectManager
    {
        private const string TemplateFileName = "tor_statuseffects.xml";

        private static readonly Dictionary<string, StatusEffectTemplate> Templates =
            new Dictionary<string, StatusEffectTemplate>();

        public static StatusEffectTemplate GetTemplate(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return Templates.TryGetValue(id, out var t) ? t : null;
        }

        public static List<StatusEffectTemplate> GetStatusEffectTemplatesWithIds(IEnumerable<string> ids)
        {
            var list = new List<StatusEffectTemplate>();
            if (ids == null) return list;
            foreach (var id in ids)
            {
                var t = GetTemplate(id);
                if (t != null) list.Add(t);
            }
            return list;
        }

        public static StatusEffect CreateNewStatusEffect(string effectId, Agent applierAgent)
        {
            var template = GetTemplate(effectId);
            return template == null ? null : new StatusEffect(template, applierAgent);
        }

        public static void LoadTemplates()
        {
            Templates.Clear();

            var modulePath = ModuleHelper.GetModuleFullPath("SOTOR");
            var path = Path.Combine(modulePath, "ModuleData", "tor_custom_xmls", TemplateFileName);
            if (!File.Exists(path))
            {
                SotorLog.Warn($"Status-effect templates not found at {path}");
                return;
            }

            var serializer = new XmlSerializer(
                typeof(List<StatusEffectTemplate>), new XmlRootAttribute("StatusEffects"));
            using (var stream = File.OpenRead(path))
            {
                if (serializer.Deserialize(stream) is List<StatusEffectTemplate> list)
                {
                    foreach (var t in list.Where(t => t != null && !string.IsNullOrEmpty(t.StringID)))
                    {
                        Templates[t.StringID] = t;
                    }
                }
            }

            SotorLog.Info($"Loaded {Templates.Count} status-effect template(s) from {path}");
        }
    }
}
