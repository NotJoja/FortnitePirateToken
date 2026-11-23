# Fortnite Pirate Token

A [R.E.P.O.](https://store.steampowered.com/app/3241660/REPO/) mod that adds the **Pirate Shop Token**, a very rare valuable that is worth nothing, can't be broken, and makes the next shop **completely free** when you extract it.

## The token

- A big, flat, spiked pirate token with a glowing orange gem and the Fortnite "F". It is about as wide as the Wizard Cube of Knowledge.
- It is **worth $0** and **indestructible**: drop it, throw it, let monsters hit it, nothing breaks it.
- It is a valuable, so you have to haul it to the extraction point like anything else.

## The reward

Extract the token and **everything in the next shop costs nothing**: every item, upgrade and health pack, and the **upgrade stand's reroll** too. Free stuff for the whole team, once per token. The free shop only lasts for that shop; the next normal level goes back to regular prices.

## The found animation

The first time someone grabs the token, a "YOU FOUND PIRATE SHOP ACCESS" banner with sound plays on that player's screen only. Everyone else is told it has been found, so it never plays twice for the same token.

## How rare is it?

Every valuable the game spawns in a level rolls its own chance of turning into the token. On a level with 81 valuables, that is 81 separate rolls.

| Setting | Chance per valuable |
| --- | --- |
| `OneIn30000` (default) | 1 in 30,000 |
| `OneIn137500` | 1 in 137,500 (the original Fortnite drop chance) |

Even at 1 in 30,000 you will go a long time between sightings, so don't expect to find it every run.

## Config

Change the chance in the config file (`Spawning/Chance`) or live in-game with [REPOConfig](https://thunderstore.io/c/repo/p/nickklmao/REPOConfig/), where it shows up as a dropdown. The change applies from the next level.

## Multiplayer

The host decides which valuables become the token and whether the shop is free. Everyone needs the mod installed.

## Spawn mods

The token is registered with REPOLib, so spawn mods that list REPOLib valuables (for example ItemSpawner) show it as **Pirate Shop Token**. It never turns up on its own other than through the roll above.

## Requirements

- [BepInExPack](https://thunderstore.io/c/repo/p/BepInEx/BepInExPack/)
- [REPOLib](https://thunderstore.io/c/repo/p/Zehs/REPOLib/)

## Source

https://github.com/NotJoja/FortnitePirateToken
