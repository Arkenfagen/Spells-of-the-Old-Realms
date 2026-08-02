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
        }

        protected override void DefineContainerDefinitions()
        {
            ConstructContainerDefinition(typeof(System.Collections.Generic.Dictionary<string, HeroExtendedInfo>));
        }
    }
}
