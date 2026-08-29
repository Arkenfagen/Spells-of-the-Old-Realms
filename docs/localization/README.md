# Translating SOTOR

Every player-facing string in SOTOR now carries a `{=key}` translation key, so a translation is a
**separate folder you add** — it never replaces a SOTOR file. That means it survives mod updates, and
any string added in a later version simply stays English until someone translates it.

`sotor_strings_TEMPLATE.xml` in this folder lists **all 976 translatable ids** with their English text,
ready to fill in.

| Section in the template | Count | Where it appears in game |
|---|---:|---|
| in-game text | 517 | dialogue, menus, encyclopedia, spellbook, perks |
| item / troop / spell names | 327 | reagents, blueprint books, skeleton troops, spell names |
| MCM labels + hints | 132 | the Mod Options screen |

## How to make a translation

Create this inside the SOTOR module (or ship it as your own small module — either works):

```
SOTOR/ModuleData/Languages/CNs/language_data.xml
SOTOR/ModuleData/Languages/CNs/sotor_strings.xml
```

`language_data.xml` declares the language and points at your strings file:

```xml
<?xml version="1.0" encoding="utf-8"?>
<LanguageData xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
              xsi:noNamespaceSchemaLocation="https://raw.githubusercontent.com/BUTR/Bannerlord.XmlSchemas/master/ModuleLanguageData.xsd"
              id="简体中文" name="简体中文" subtitle_extension="zh-HANS"
              supported_iso="zh-HANS,zh,zho,chi,zh-cn,zh-sg" under_development="false">
  <LanguageFile xml_path="CNs/sotor_strings.xml"/>
</LanguageData>
```

Then copy `sotor_strings_TEMPLATE.xml` to `CNs/sotor_strings.xml`, change the language tag at the top
to match (`<tag language="简体中文"/>`), and replace the English in each `text="..."` with your
translation. Leave every `id` exactly as it is — that is what binds the two together.

Use the same folder and `id` for other languages: `DE` / `Deutsch`, `RU` / `Русский`, and so on. The
folder name is free-form; the `id` must match the game's language name.

## Rules that matter

- **Keep `{VARIABLE}` tokens exactly as they are.** `{HERO} has learned {TRAIT}.` must keep both
  braces and both names; the game substitutes them at runtime. You may move them within the sentence.
- **Do not translate the `id`.** Only the `text`.
- **Partial translations are fine.** Anything you leave out falls back to English — nothing breaks.
- **Escape XML properly**: `&` as `&amp;`, `<` as `&lt;`, `"` inside text as `&quot;`.

## Why English is not translated this way

Native's `LocalizedTextManager.LoadLanguage` skips deserializing strings when the language *is*
English (`bool flag = stringId != "English"`). English text therefore has to come from the fallback
baked in after each `{=key}`, and from `ModuleData/sotor_strings.xml`, which is a GameText file and
loads for every language. Translations use the Languages folder; English does not. See
`SOTOR-Lessons-Learned.md` §77.

**Do not put this template under `ModuleData/Languages/`.** Language discovery is recursive
(`SearchOption.AllDirectories`), so a stray `language_data.xml` there would register a phantom
language in the player's options.
