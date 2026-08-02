using TaleWorlds.InputSystem;

namespace SOTOR.GameManagers
{
    public class SotorGameKeyContext : GameKeyContext
    {

        public const int QuickCastSelectionMenu = 111;

        public SotorGameKeyContext()
            : base("SotorGameKeyContext", 120, GameKeyContextType.Default)
        {
            RegisterGameKey(
                new GameKey(QuickCastSelectionMenu, "QuickCastSelectionMenu", "SotorGameKeyContext", InputKey.Q, "SotorGameKeyContext"),
                true);
        }
    }
}
