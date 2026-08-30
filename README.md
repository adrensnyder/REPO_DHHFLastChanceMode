# DHHF LastChance Mode
Keeps the run alive when every player dies and lets your squad jump with the DeathHead before the dump level triggers. This package pairs with DeathHeadHopper and DeathHeadHopperFix to add a configurable Last Chance layer that controls the timer, UI, monsters, and the Direction ability.

## Update
In the last update i've refactored ALL the code. I tested so many pipeline/monsters/network sync. I could missed something. 
If something isn't working as it should please send a feedback :)

## Functionality
- **Configurable timer curves** – base timer, R.E.P.O. difficulty-floor bonuses, per-player coordination, critical-route distance/room/vertical costs, and monster-weighted adjustments all live inside `BepInEx/config/AdrenSnyder.DHHFLastChanceMode.cfg`.
- **Monsters treat DeathHeads as targets** – beware. Monsters will haunt you (And hear you xD) now when LastChance mode is active.
- **Direction ability with shared energy cost** – activating the Direction indicator subtracts seconds from every player’s timer.
- **JumpBattery behaviour** - If enabled it will automatically disabled during the LastChance phase.

# Requirements
Requires DeathHeadHopperFix and the base DeathHeadHopper mod to be present.

# Multiplayer
Every player (host and clients) must have this mod, and the original, installed.

## Configuration
`BepInEx/config/AdrenSnyder.DHHFLastChanceMode.cfg` lets you tune sections labeled “LastChance: Quick Setup”, “Timer Calculation”, and “Gameplay & UI”. Dynamic timing follows R.E.P.O.'s three vanilla difficulty multipliers: the base timer plus configurable Difficulty 1/2/3 floor bonuses defines the minimum safety time, while the critical Death Head route and monster pressure can add more without a fixed gameplay cap. Direction penalty and monster-search behavior remain configurable as well.

Spectate FOV is now owned by LastChance through `[2. Spectate] LastChanceSpectateDefaultFov` (default `70`, `0` disables enforcement). If you previously customized DeathHeadHopperFix `[8. Camera] DHHSpectateDefaultFov`, copy that value manually; LastChance does not read or migrate another plugin's config file.

## Credits
Thanks to Cronchy for the original DeathHeadHopper mod that inspired this extension.  
Thanks also to Omniscye for the code i used from the Keep_Saves mod

## Feedback and support
You can provide feedback or report bugs on the "[R.E.P.O. Modding Server](https://discord.com/invite/r-e-p-o-modding-server-1344557689979670578)"
