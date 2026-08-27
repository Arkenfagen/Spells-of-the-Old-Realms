using SOTOR.CampaignBehaviors;
using SOTOR.Extensions.ExtendedInfoSystem;
using TaleWorlds.SaveSystem;

namespace SOTOR.SaveGameSystem
{
    public class SotorSaveableTypeDefiner : SaveableTypeDefiner
    {
        public SotorSaveableTypeDefiner()
            : base(881500)
        {
        }

        protected override void DefineClassTypes()
        {
            AddClassDefinition(typeof(HeroExtendedInfo), 1);
            AddClassDefinition(typeof(SotorGraveyardNightWatchPartyComponent), 2);

            AddClassDefinition(typeof(SOTOR.Quests.SotorApprenticeDuelQuest), 3);

            AddClassDefinition(typeof(SOTOR.Quests.SotorPracticeQuest), 4);

            AddClassDefinition(typeof(SotorEnchantedItemData), 5);

            AddClassDefinition(typeof(SOTOR.Quests.SotorCommissionQuest), 6);
        }

        protected override void DefineContainerDefinitions()
        {
            ConstructContainerDefinition(typeof(System.Collections.Generic.Dictionary<string, HeroExtendedInfo>));

            ConstructContainerDefinition(typeof(System.Collections.Generic.Dictionary<string, int>));
            ConstructContainerDefinition(typeof(System.Collections.Generic.Dictionary<string, float>));
            ConstructContainerDefinition(typeof(System.Collections.Generic.Dictionary<int, int>));
            ConstructContainerDefinition(typeof(System.Collections.Generic.Dictionary<int, float>));

            ConstructContainerDefinition(typeof(System.Collections.Generic.List<string>));

            ConstructContainerDefinition(typeof(System.Collections.Generic.Dictionary<TaleWorlds.Core.ItemObject, SotorEnchantedItemData>));
        }
    }
}
