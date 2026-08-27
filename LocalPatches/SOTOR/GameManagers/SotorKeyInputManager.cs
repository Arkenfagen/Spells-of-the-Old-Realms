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

                RegisterKeyStrings(tm, context, SotorGameKeyContext.QuickCastSelectionMenu,
                    new TextObject("{=sotor_quickcast_key_name}Spellcasting Mode"),
                    new TextObject("{=sotor_quickcast_key_desc}Opens the spell selection menu in battle. Hold to pick a spell, release to enter aiming mode; left-click casts, right-click cancels."));

                RegisterKeyStrings(tm, context, SotorGameKeyContext.OpenSpellbook,
                    new TextObject("{=sotor_spellbook_key_name}Open Spellbook"),
                    new TextObject("{=sotor_spellbook_key_desc}Opens the spellbook from the campaign map. Unbound by default; useful when another mod covers the character-panel button. While rebinding, press Delete to clear the key."));

                for (int slot = 0; slot < SotorGameKeyContext.CastSlotCount; slot++)
                {
                    var name = new TextObject("{=sotor_castslot_key_name}Cast Spell {SLOT}");
                    name.SetTextVariable("SLOT", slot + 1);
                    var desc = new TextObject("{=sotor_castslot_key_desc}Readies spell {SLOT} from your casting wheel as if picked with the spell menu: aimed spells enter aiming mode, instant spells cast at once. Unbound by default. While rebinding, press Delete to clear the key.");
                    desc.SetTextVariable("SLOT", slot + 1);
                    RegisterKeyStrings(tm, context, SotorGameKeyContext.CastSpellSlot1 + slot, name, desc);
                }
            }
            catch (System.Exception ex)
            {
                SotorLog.Warn($"SotorKeyInputManager.RegisterKeybindStrings failed: {ex.Message}");
            }
        }

        private static void RegisterKeyStrings(GameTextManager tm, string context, int keyId, TextObject name, TextObject description)
        {
            string id = context + "_" + ((GameKeyDefinition)keyId).ToString();
            tm.GetGameText("str_key_name").AddVariationWithId(id, name, new List<GameTextManager.ChoiceTag>());
            tm.GetGameText("str_key_description").AddVariationWithId(id, description, new List<GameTextManager.ChoiceTag>());
        }
    }
}
