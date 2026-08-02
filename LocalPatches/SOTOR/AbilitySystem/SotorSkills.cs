using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace SOTOR.AbilitySystem
{

    public class SotorSkills
    {
        public const string SpellcraftId = "SotorSpellcraft";

        private static SotorSkills _instance;

        private SkillObject _spellcraft;

        public static SkillObject Spellcraft => _instance?._spellcraft;

        public SotorSkills()
        {
            _instance = this;

            var attribute = ResolveGoverningAttribute(out string resolvedId);

            _spellcraft = Game.Current.ObjectManager.RegisterPresumedObject(new SkillObject(SpellcraftId));
            _spellcraft.Initialize(
                new TextObject("Spellcraft"),
                new TextObject("Your mastery of the Winds of Magic. Higher Spellcraft increases spell damage and unlocks higher spell tiers."),
                new[] { attribute });

            SotorLog.Info($"SotorSkills: registered '{SpellcraftId}' (attr={resolvedId}).");
        }

        private static CharacterAttribute ResolveGoverningAttribute(out string resolvedId)
        {
            string id = SotorSettings.SpellcraftAttributeId;
            if (!string.IsNullOrWhiteSpace(id))
            {
                var found = MBObjectManager.Instance?.GetObject<CharacterAttribute>(id);
                if (found != null)
                {
                    resolvedId = id;
                    return found;
                }
                SotorLog.Warn($"SotorSkills: Spellcraft attribute '{id}' not found; falling back to Intelligence.");
            }

            resolvedId = "intelligence";
            return DefaultCharacterAttributes.Intelligence;
        }
    }
}
