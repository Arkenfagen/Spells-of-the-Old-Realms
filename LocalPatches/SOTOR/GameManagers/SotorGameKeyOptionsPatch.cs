using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.MountAndBlade.Options;

namespace SOTOR.GameManagers
{

    [HarmonyPatch]
    public static class SotorGameKeyOptionsPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(OptionsProvider), "GetGameKeyCategoriesList")]
        public static IEnumerable<string> Postfix(IEnumerable<string> __result)
        {
            var list = __result?.ToList() ?? new List<string>();
            if (!list.Contains("SotorGameKeyContext"))
            {
                list.Add("SotorGameKeyContext");
            }
            return list;
        }
    }
}
