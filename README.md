# Empire Refactored

Now under new management!

[Get Empire Refactored now on the Steam workshop!](https://steamcommunity.com/sharedfiles/filedetails/?id=3701480464) (Note: the Steam Workshop is on the 1.5 branch, while the development branch here on Github is the 1.6 branch. For the 1.5 branch, look for dev-v1-5)

This branch of Empire is under active development. Adding features and stamping out bugs is the name of the game.

If you do run into a bug, please report it in the issues section with logs and, if applicable, screenshots. The more information I have, the faster I can fix issues.

**NOT SAVE COMPATIBLE WITH OLD VERSIONS OF EMPIRE!!** Adding this verson of Empire to a save that did not have an old version of Empire should be fine, but if you replace old Empire with this Empire in an active save, then **expect bugs and bad behavior. This is unsupported!**

**Rimworld v1.6 only!**

# Empire
Spread your rule across the Rimworld with self governing settlements that are loyal to you and you alone. Command them to fight in your name, and destroy your enemies.

Events will periodically influence your settlements, or your faction as a whole. You must decide how best to respond to these situations.

Your colonists will pay taxes, be it in silver, or goods. You can tell them where you want your taxes, and they will dutifully pay them. A fully customizable faction name and title allows you to put your own spin on your subjects. They can be feudal vassals, a megacorporation’s branch offices, or even a shining beacon of truth and liberty. It is all part of the greater story.

## Refactored
This version of Empire has been heavily refactored. Old classes have been merged, reworked, replaced, or removed. Any mod whose compatibility with Empire used code, will need to be reworked.

Any mod whose compatibility with Empire depends entirely on referencing the PColony factionDef may be fine, as the def has *not* been renamed.

### New features
- Replaced the old webby road builder with a Minimum Spanning Tree-based algorithm, resulting in much more natural-looking road networks
- UI overhaul -- the vast majority of the UI has been reworked and updated. Previously hidden information has been brought to the fore, and tooltips reveal even more!
- Edicts -- old, unused policies were reworked as three new categories of edicts: Social, Tax, and Military. Each edict provices faction-wide bonuses and maluses. Each category unlocks as your faction levels up.
- Reworked Events -- Most of the existing events were reworked to give the player a choice over how they respond
- New Squad mechanics -- squads cost money to hire, but are now individually customizable. You can even add implants, psycasts, and mechs to your units
- Empire Codex -- a helpful in-game resource to explain the mod's mechanics, similar to the Civilopedia from the Civilization games

## Manual Battles!
Yes, you heard that right. Manual battles are back!

Currently, only defense battles can be run manually.

## Submods and Extensibility
Along with the refactor comes far greater extensibility. Settlement and resource types are defined by XML defs now; basic resources and settlements can be created in XML alone. And if you want to get fancy, there is a handy set of extensible abstract classes to use.

Buildings have been changed as well; they can have upgrade paths, or depend on the presence of other buildings.

Adding new events or policies is possible through XML, too!

On the whole, it should be far easier to write submods without having to harmony patch everything!

For more info on the mod's extensibility, refer to the documentation in the Docs directory.

## Contributing
Contributions welcome! Just fork the project (top right), make your changes, and then open a pull request.

**Make sure that you fork the development branch, and open PRs into the development branch.**

Translations are especially welcome. There are a *lot* of new translation keys, and many of the old ones are likely outdated.

Depending on what you want to add, though, consider making a submod (especially for new resource types or settlement types). It's way easier than ever before!

# Credit
This mod was initially developed by Saakra, a lone Mod Dev. He has since moved on due to IRL issues and handed over the reins of development to the community.

The current active maintainer is yours truly, Matathias.

Additional contributors over the mod's lifetime:
- Epistatic - Brought Empire to Rimworld 1.6
- Pecha - Updated the biome patches
- Midnight - Updated building icons
- Erok031 - Made the faction trait icons and some of the flags
- Helixien - Made the mod banner
- MaoDetector - Has made a lot of faction flags
- Miriouki62 - Updated French translation
- CreeeesP - Updated Russian translation
- carmoran0 - Updated Spanish translation
- PhenylPropion - Updated Japanese translation