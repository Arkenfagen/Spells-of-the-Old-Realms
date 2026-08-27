using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace SOTOR.Items
{

    public class SotorEnchantingState : GameState
    {
        public override bool IsMenuState => true;

        public Hero Enchanter { get; set; }
    }
}
