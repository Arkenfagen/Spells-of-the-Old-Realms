using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem
{

    public static class SotorCastAnimation
    {
        public static void PlayRelease(Agent caster, string actionName)
        {
            if (caster == null || !caster.IsActive() || string.IsNullOrWhiteSpace(actionName)
                || actionName.Equals("none", System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                var action = ActionIndexCache.Create(actionName);
                if (action.Index == ActionIndexCache.act_none.Index)
                {

                    SotorLog.Debug($"CastRelease: action '{actionName}' unresolved (act_none); skipping.");
                    return;
                }

                if (caster.GetCurrentAction(1) == action)
                {
                    return;
                }

                caster.SetActionChannel(1, action, false, (AnimFlags)0, 0f, 1f, -0.2f, 0.4f, 0f, false, -0.2f, 0, true);
                SotorLog.Debug($"CastRelease: played '{actionName}' on '{caster.Name}' "
                               + $"(ai={caster.IsAIControlled}).");
            }
            catch (System.Exception ex)
            {
                SotorLog.Warn($"SotorCastAnimation.PlayRelease('{actionName}') failed: {ex.Message}");
            }
        }
    }
}
