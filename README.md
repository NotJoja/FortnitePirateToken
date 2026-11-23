# Fortnite Pirate Token

A [R.E.P.O.](https://store.steampowered.com/app/3241660/REPO/) mod that adds the **Pirate Shop Token**: a rare, indestructible valuable worth $0 that glows gold and throws sparks. Extract one and you bank a **free shop**: everything in it, even the upgrade stand's reroll, costs nothing. Free shops **stack**, so five tokens means five free shops.

## The token

- A big, flat, spiked pirate token with a glowing orange gem and the Fortnite "F". It is about as wide as the Wizard Cube of Knowledge.
- It is **worth $0** and **indestructible**: drop it, throw it, let monsters hit it, nothing breaks it.
- It has a **golden glow** and gold **sparks** rising off it, so it is easy to spot.
- It is a valuable, so you have to haul it to the extraction point like anything else.

## The reward

Extract the token and your next shop is **completely free**: every item, upgrade and health pack, and the **upgrade stand's reroll** too. Free stuff for the whole team.

**It stacks.** Every token you extract banks one free shop, and each shop you visit uses one up. Find five and the next five shops cost nothing; find another one later and it is added on top. The count is saved with your run and starts over when you start a new run.

## The found animation

The first time anyone grabs the token, a "YOU FOUND PIRATE SHOP ACCESS" banner with sound plays on **everyone's** screen. After that the token counts as found: if someone else touches the same token again, nothing plays. A second token that turns up later gets its own animation.

## How rare is it?

Every valuable the game spawns in a level rolls its own chance of turning into the token. On a level with 81 valuables, that is 81 separate rolls.

| Setting | Chance per valuable |
| --- | --- |
| `OneIn1375` (default) | 1 in 1,375 |
| `OneIn137500` | 1 in 137,500 (the original Fortnite drop chance) |
| `Custom` | Your own number: type it into `CustomOneIn`, like `5000` for 1 in 5,000. |

At 1 in 1,375 on a level with 81 valuables, you will see it roughly once every 17 levels.

## Config

Change the chance in the config file (`Spawning/Chance`, and `Spawning/CustomOneIn` for your own number) or live in-game with [REPOConfig](https://thunderstore.io/c/repo/p/nickklmao/REPOConfig/), where `Chance` shows up as a dropdown and `CustomOneIn` as a box you can type any number into. `CustomOneIn` is only used when `Chance` is set to `Custom`. The change applies from the next level.

## Multiplayer

The host decides which valuables become the token and whether the shop is free. Everyone needs the mod installed.

## Spawn mods

The token is registered with REPOLib, so spawn mods that list REPOLib valuables (for example ItemSpawner) show it as **Pirate Shop Token**. It never turns up on its own other than through the roll above.

## Requirements

- [BepInExPack](https://thunderstore.io/c/repo/p/BepInEx/BepInExPack/)
- [REPOLib](https://thunderstore.io/c/repo/p/Zehs/REPOLib/)

## Source

https://github.com/NotJoja/FortnitePirateToken
