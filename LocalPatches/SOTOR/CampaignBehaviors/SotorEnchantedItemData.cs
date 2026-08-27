using System.Collections.Generic;
using TaleWorlds.SaveSystem;

namespace SOTOR.CampaignBehaviors
{

    public class SotorEnchantedItemData
    {
        [SaveableProperty(1)]
        public string OriginalItemStringId { get; set; }

        [SaveableProperty(2)]
        public string NewItemName { get; set; }

        [SaveableProperty(3)]
        public List<string> ItemTraits { get; set; }

        [SaveableProperty(4)]
        public bool IsPlayerCrafted { get; set; }
    }
}
