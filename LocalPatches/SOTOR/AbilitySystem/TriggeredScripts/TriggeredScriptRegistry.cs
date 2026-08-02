using System;
using System.Collections.Generic;

namespace SOTOR.AbilitySystem.TriggeredScripts
{

    public static class TriggeredScriptRegistry
    {
        private static readonly Dictionary<string, ITriggeredScript> _scripts =
            new Dictionary<string, ITriggeredScript>(StringComparer.OrdinalIgnoreCase)
            {
                ["SpiritLeech"] = new SpiritLeech(),

                ["SummonScript"] = new Summon(),
                ["Summon"] = new Summon(),
            };

        public static ITriggeredScript Resolve(string scriptName)
        {
            if (string.IsNullOrWhiteSpace(scriptName) || scriptName.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            int dot = scriptName.LastIndexOf('.');
            string shortName = dot >= 0 ? scriptName.Substring(dot + 1) : scriptName;

            return _scripts.TryGetValue(shortName, out var script) ? script : null;
        }
    }
}
