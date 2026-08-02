using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using TaleWorlds.ModuleManager;

namespace SOTOR.AbilitySystem
{

    public static class TriggeredEffectManager
    {
        private static readonly Dictionary<string, TriggeredEffectTemplate> Templates =
            new Dictionary<string, TriggeredEffectTemplate>();

        private const string TemplateFileName = "tor_triggeredeffects.xml";

        public static TriggeredEffectTemplate GetTemplate(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return Templates.TryGetValue(id, out var template) ? template : null;
        }

        public static void LoadTemplates()
        {
            Templates.Clear();

            var modulePath = ModuleHelper.GetModuleFullPath("SOTOR");
            var path = Path.Combine(modulePath, "ModuleData", "tor_custom_xmls", TemplateFileName);
            if (!File.Exists(path))
            {
                SotorLog.Warn($"Triggered-effect templates not found at {path}");
                return;
            }

            var serializer = new XmlSerializer(
                typeof(List<TriggeredEffectTemplate>), new XmlRootAttribute("TriggeredEffects"));
            using (var stream = File.OpenRead(path))
            {
                if (serializer.Deserialize(stream) is List<TriggeredEffectTemplate> list)
                {
                    foreach (var template in list.Where(t => t != null && !string.IsNullOrEmpty(t.StringID)))
                    {
                        Templates[template.StringID] = template;
                    }
                }
            }

            SotorLog.Info($"Loaded {Templates.Count} triggered-effect template(s) from {path}");
        }
    }
}
