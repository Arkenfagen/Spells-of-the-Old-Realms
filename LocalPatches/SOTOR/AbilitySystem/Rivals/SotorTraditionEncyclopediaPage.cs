using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace SOTOR.AbilitySystem.Rivals
{

    [OverrideEncyclopediaModel(new[] { typeof(SotorTraditionObject) })]
    public class SotorTraditionEncyclopediaPage : EncyclopediaPage
    {

        public const string PageIdentifier = "SotorTradition";

        public const int HomeOrder = 590;

        public class NameComparer : EncyclopediaListItemComparerBase
        {
            public override int Compare(EncyclopediaListItem x, EncyclopediaListItem y) => ResolveEquality(x, y);

            public override string GetComparedValueText(EncyclopediaListItem item) => "";
        }

        public SotorTraditionEncyclopediaPage()
        {
            HomePageOrderIndex = HomeOrder;
            SotorTraditionObject.EnsureCreated();
        }

        public override string GetViewFullyQualifiedName() => "EncyclopediaTraditionPage";

        public override string GetStringID() => "SotorEncyclopediaTradition";

        public override TextObject GetName() =>
            SotorText.GetObject("sotor_enc_traditions_title");

        public override TextObject GetDescriptionText() =>
            SotorText.GetObject("sotor_enc_traditions_desc");

        public override MBObjectBase GetObject(string typeName, string stringID) => SotorTraditionObject.Find(stringID);

        public override bool IsValidEncyclopediaItem(object o) => o is SotorTraditionObject;

        public override bool IsRelevant() => SotorSettings.EnableRivalCasters;

        protected override IEnumerable<EncyclopediaListItem> InitializeListItems()
        {
            SotorTraditionObject.EnsureCreated();
            foreach (var tradition in SotorTraditionObject.All)
            {

                yield return new EncyclopediaListItem(
                    tradition,
                    tradition.Name.ToString(),
                    tradition.Description.ToString(),
                    tradition.StringId,
                    PageIdentifier,
                    true,
                    null);
            }
        }

        protected override IEnumerable<EncyclopediaFilterGroup> InitializeFilterItems() =>
            new List<EncyclopediaFilterGroup>();

        protected override IEnumerable<EncyclopediaSortController> InitializeSortControllers() =>
            new List<EncyclopediaSortController>();

        internal IEnumerable<EncyclopediaFilterGroup> BuildFilters() => InitializeFilterItems();

        internal IEnumerable<EncyclopediaListItem> BuildItems() => InitializeListItems();

        internal IEnumerable<EncyclopediaSortController> BuildSortControllers() => InitializeSortControllers();

        internal static Type[] IdentifierTypes => new[] { typeof(SotorTraditionObject) };
    }
}
