using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade.ViewModelCollection.GameOptions.GameKeys;

namespace SOTOR.GameManagers
{

    [HarmonyPatch(typeof(GameKeyGroupVM), "PopulateGameKeys")]
    public static class SotorUnboundKeybindRowsPatch
    {
        public static void Postfix(GameKeyGroupVM __instance)
        {
            try
            {
                var keys = AccessTools.Field(typeof(GameKeyGroupVM), "_keys")?.GetValue(__instance) as IEnumerable<GameKey>;
                if (keys == null)
                {
                    return;
                }

                var ours = keys.Where(k => k != null && k.GroupId == "SotorGameKeyContext").ToList();
                if (ours.Count == 0)
                {
                    return;
                }

                var list = __instance.GameKeys;
                if (list == null)
                {
                    return;
                }
                var present = new HashSet<int>(list.Where(o => o?.CurrentGameKey != null).Select(o => o.CurrentGameKey.Id));

                var onKeybindRequest = AccessTools.Field(typeof(GameKeyGroupVM), "_onKeybindRequest")?.GetValue(__instance);
                var setGameKeyMethod = AccessTools.Method(typeof(GameKeyGroupVM), "SetGameKey");
                if (onKeybindRequest == null || setGameKeyMethod == null)
                {
                    return;
                }
                var onKeySet = Delegate.CreateDelegate(typeof(Action<GameKeyOptionVM, InputKey>), __instance, setGameKeyMethod);

                object getExtra = AccessTools.Field(typeof(GameKeyGroupVM), "_getExtraInformation")?.GetValue(__instance);

                var ctor = typeof(GameKeyOptionVM).GetConstructors().FirstOrDefault();
                if (ctor == null)
                {
                    return;
                }
                int paramCount = ctor.GetParameters().Length;

                foreach (var key in ours)
                {
                    if (present.Contains(key.Id))
                    {
                        continue;
                    }
                    object[] args = paramCount >= 4
                        ? new object[] { key, onKeybindRequest, onKeySet, getExtra }
                        : new object[] { key, onKeybindRequest, onKeySet };
                    list.Add((GameKeyOptionVM)ctor.Invoke(args));
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorUnboundKeybindRowsPatch failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
