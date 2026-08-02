using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace SOTOR
{
    [PrefabExtension("CharacterDeveloper", "descendant::Standard.TripleDialogCloseButtons")]
    internal class CharacterDeveloperSpellBookButtonExtension : PrefabExtensionInsertPatch
    {
        private readonly XmlDocument _document = new XmlDocument();

        public CharacterDeveloperSpellBookButtonExtension()
        {
            _document.LoadXml(
                @"<ListPanel DoNotAcceptEvents=""true"" WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""CoverChildren"" StackLayout.LayoutMethod=""HorizontalRightToLeft"" HorizontalAlignment=""Right"" VerticalAlignment=""Top"" MarginTop=""30"" MarginRight=""70"">
  <Children>
    <ButtonWidget DoNotPassEventsToChildren=""true"" WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed"" SuggestedWidth=""233"" SuggestedHeight=""75"" HorizontalAlignment=""Right"" Sprite=""SPGeneral\spellbook_button"" Command.Click=""ExecuteOpenSpellBook"" IsVisible=""@IsSpellBookButtonVisible"" />
  </Children>
</ListPanel>");
        }

        public override InsertType Type => InsertType.Append;

        [PrefabExtensionXmlDocument]
        public XmlDocument GetPrefabExtension() => _document;
    }
}
