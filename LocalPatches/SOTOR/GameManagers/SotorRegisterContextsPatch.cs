using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.InputSystem;

namespace SOTOR.GameManagers
{

    [HarmonyPatch]
    public static class SotorRegisterContextsPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HotKeyManager), "RegisterInitialContexts")]
        public static bool AddSotorContext(ref IEnumerable<GameKeyContext> contexts)
        {
            var list = contexts.ToList();
            if (!list.Any(x => x is SotorGameKeyContext))
            {
                list.Add(new SotorGameKeyContext());
            }
            contexts = list;
            return true;
        }
    }
}
