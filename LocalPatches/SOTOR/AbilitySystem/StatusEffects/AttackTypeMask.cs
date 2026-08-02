using System;

namespace SOTOR.AbilitySystem.StatusEffects
{

    [Flags]
    public enum AttackTypeMask
    {
        Ranged = 1,
        Melee = 2,
        Spell = 4,
        All = 7
    }
}
