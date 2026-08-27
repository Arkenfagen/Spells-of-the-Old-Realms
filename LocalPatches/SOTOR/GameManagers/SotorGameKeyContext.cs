using HarmonyLib;
using TaleWorlds.InputSystem;

namespace SOTOR.GameManagers
{
    public class SotorGameKeyContext : GameKeyContext
    {

        public const int QuickCastSelectionMenu = 111;

        public const int OpenSpellbook = 112;
        public const int CastSpellSlot1 = 113;
        public const int CastSpellSlot2 = 114;
        public const int CastSpellSlot3 = 115;
        public const int CastSpellSlot4 = 116;
        public const int CastSpellSlot5 = 117;

        public const int CastSlotCount = 5;

        public SotorGameKeyContext()
            : base("SotorGameKeyContext", 120, GameKeyContextType.Default)
        {
            RegisterGameKey(
                new GameKey(QuickCastSelectionMenu, "QuickCastSelectionMenu", "SotorGameKeyContext", InputKey.Q, "SotorGameKeyContext"),
                true);
            RegisterGameKey(MakeUnboundKey(OpenSpellbook, "OpenSpellbook"), true);
            for (int slot = 0; slot < CastSlotCount; slot++)
            {
                RegisterGameKey(MakeUnboundKey(CastSpellSlot1 + slot, "CastSpellSlot" + (slot + 1)), true);
            }
        }

        private static GameKey MakeUnboundKey(int id, string stringId)
        {
            var key = new GameKey(id, stringId, "SotorGameKeyContext", InputKey.Invalid, "SotorGameKeyContext");
            if (key.KeyboardKey == null)
            {
                AccessTools.PropertySetter(typeof(GameKey), "KeyboardKey")
                    ?.Invoke(key, new object[] { new Key(InputKey.Invalid) });
            }
            return key;
        }
    }
}
