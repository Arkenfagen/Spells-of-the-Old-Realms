# SOTOR: Spells of the Old Realms

A standalone Mount & Blade II: Bannerlord mod that brings a magic system to the
base game: spell lores, casting, Winds of Magic resource management, a spellbook
UI, a Spellcraft skill with perks, and summoned undead.

## Credit

SOTOR is a port of the magic system from **The Old Realms (TOR)**, and is derived
from its source code:

- The Old Realms: https://github.com/TheOldRealms
- TOR_Core: https://github.com/TheOldRealms/TOR_Core

All credit for the original magic system design and implementation goes to the
TOR team. SOTOR reworks it to run without TOR installed.

## Building

```
dotnet build LocalPatches/SOTOR/SOTOR.csproj -c Release
```

Override the game path with `-p:BannerlordDir="..."`. For Bannerlord 1.3.15, add
`-p:GameVersion=1315`.

## License

GPL-3.0, see [LICENSE](LICENSE).
