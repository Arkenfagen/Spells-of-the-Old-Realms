using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace SOTOR.GameManagers
{
    public static class SotorKeyInputManager
    {
        public static void Initialize()
        {
            var contexts = HotKeyManager.GetAllCategories().Cast<GameKeyContext>().ToList();
            if (!contexts.Any(x => x is SotorGameKeyContext))
            {
                contexts.Add(new SotorGameKeyContext());
            }

#if BL13
            HotKeyManager.RegisterInitialContexts(contexts, true);
#else
            HotKeyManager.RegisterInitialContexts(contexts);
#endif

            RegisterKeybindStrings();
        }

        private static void RegisterKeybindStrings()
        {
            try
            {
                const string context = "SotorGameKeyContext";
                var tm = Module.CurrentModule?.GlobalTextManager;
                if (tm == null)
                {
                    return;
                }

                tm.GetGameText("str_key_category_name").AddVariationWithId(
                    context,
                    new TextObject("{=sotor_key_category_name}Spells of the Old Realms"),
                    new List<GameTextManager.ChoiceTag>());

                string keyId = context + "_" + ((GameKeyDefinition)SotorGameKeyContext.QuickCastSelectionMenu).ToString();

                tm.GetGameText("str_key_name").AddVariationWithId(
                    keyId,
                    new TextObject("{=sotor_quickcast_key_name}Spellcasting Mode"),
                    new List<GameTextManager.ChoiceTag>());

                tm.GetGameText("str_key_description").AddVariationWithId(
                    keyId,
                    new TextObject("{=sotor_quickcast_key_desc}Opens the spell selection menu in battle. Hold to pick a spell, release to enter aiming mode; left-click casts, right-click cancels."),
                    new List<GameTextManager.ChoiceTag>());
            }
            catch (System.Exception ex)
            {
                SotorLog.Warn($"SotorKeyInputManager.RegisterKeybindStrings failed: {ex.Message}");
            }
        }
    }
}
