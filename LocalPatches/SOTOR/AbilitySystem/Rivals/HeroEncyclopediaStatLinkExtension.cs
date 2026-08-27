using System.Collections.Generic;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace SOTOR.AbilitySystem.Rivals
{

    [PrefabExtension("EncyclopediaHeroPage",
        "descendant::GridWidget[@Id='StatsGrid']/ItemTemplate/ListPanel/Children/AutoHideRichTextWidget[2]")]
    internal class HeroEncyclopediaStatLinkExtension : PrefabExtensionSetAttributePatch
    {
        public override List<Attribute> Attributes => new List<Attribute>
        {
            new Attribute("Command.LinkClick", @"..\..\ExecuteLink"),
        };
    }
}
