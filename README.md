# DHHF LastChance Mode
Keeps the run alive when every player dies and lets your squad jump with the DeathHead before the dump level triggers. This package pairs with DeathHeadHopper and DeathHeadHopperFix to add a configurable Last Chance layer that controls the timer, UI, monsters, and the Direction ability.

## Update
In the last update i've refactored ALL the code. I tested so many pipeline/monsters/network sync. I could missed something. 
If something isn't working as it should please send a feedback :)

## Functionality
- **Configurable timer curves** – base timer, per-player boosts, per-room/per-farthest-meter scaling, and monster-weighted adjustments all live inside `BepInEx/config/AdrenSnyder.DHHFLastChanceMode.cfg`.
- **Monsters treat DeathHeads as targets** – beware. Monsters will haunt you (And hear you xD) now when LastChance mode is active.
- **Direction ability with shared energy cost** – activating the Direction indicator subtracts seconds from every player’s timer.
- **JumpBattery behaviour** - If enabled it will automatically disabled during the LastChance phase.

# Requirements
Requires DeathHeadHopperFix and the base DeathHeadHopper mod to be present.

# Multiplayer
Every player (host and clients) must have this mod, and the original, installed.

## Configuration
`BepInEx/config/AdrenSnyder.DHHFLastChanceMode.cfg` lets you tune sections labeled “LastChance: Quick Setup”, “Timer Calculation”, and “Gameplay & UI”. Use it to adjust the direction penalty, dynamic timer caps, and monster search flags so the Last Chance flow matches your group’s feel.

## Credits
Thanks to Cronchy for the original DeathHeadHopper mod that inspired this extension.  
Thanks also to Omniscye for the code i used from the Keep_Saves mod

## Feedback and support
You can provide feedback or report bugs on the "[R.E.P.O. Modding Server](https://discord.com/invite/r-e-p-o-modding-server-1344557689979670578)"
