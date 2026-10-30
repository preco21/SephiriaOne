# SephiriaOne

[한국어](README.md) | **English**

> A grab-bag mod I made for my own fun (aka. “silver-spoon runs”).

Open the mod menu from the upper-right corner of the ESC menu.

![](media/a.jpg)
![](media/b.jpg)
![](media/c.jpg)
![](media/d.jpg)

## Installation

- Download the ZIP from [Releases](https://github.com/preco21/SephiriaOne/releases) and extract the `SephiriaOne` folder.
- Open the game's installation directory (in Steam, right-click `Sephiria` → Manage → Browse local files). Place the entire extracted folder inside `Sephiria/AddOns`. Create the `AddOns` folder if it does not exist.

## Features

- Adjust base stat multipliers—for example, set Luck to three times a character's native value or triple magic-book cooldown recovery.
- Adjust Wishing Fountain points.
- Adjust reward-choice multipliers.
- Adjust dice, leaves, and inventory-slot multipliers.
- Change nickname colors (visible to users of this mod only).
- Spawn Golden Goblin merchants.
- Add a healer role through Wing-Eared Rabbit.
- Optionally nerf Bat to give the healer role more room.
- Costume options for companion builds, including a starting Collin.
- Adjust Mystic Jar spawn probability.
- [Adjust the probability multiplier for random event rooms](docs/random-events.md), including blood donation, obelisks, and magic fountains.
- PvP features and modes: casual friendly fire, Deathmatch, Tournament, Play Tag, and Zombie Survival.
- PvP balancing for more manageable damage, with participant selection and exclusions.
- Temporary PvP cover with configurable obstacles per level, safe placement, and automatic cleanup when PvP ends (off by default).
- Grant powerful Slates: Requital and Heaven. Heaven exists only as unused game data, but it works, so I added it.
- Allow selling starting items, including the “마음의 짐” item.
- Projectile optimization—a must-have when playing with 12+ players through the raid mod and lag-heavy builds such as Liberation, Ilonica, or Palas' Card.
- And more…

## Development

- All contributors, including AI agents, must follow the game developer's [official modding guide and rules](https://github.com/team-horay/sephiria_modding/tree/main) ([English](https://github.com/team-horay/sephiria_modding/blob/main/README.en.md)). Official API documentation, examples, and templates are listed in the [references](docs/references.md#official-developer-guide-and-materials).
- Built with OpenAI Codex—Astra at `xhigh` reasoning effort—plus [superpowers](https://github.com/obra/superpowers) and [REA](https://github.com/morluto/rea).
- [Visual Studio 2026](https://visualstudio.microsoft.com/downloads/) is required. Select the **.NET desktop development** sdk in the installer to build the project.
- Development resources are available in the [documentation index](docs/README.md). The `docs/` folder contains general development knowledge, game-domain research, feature designs, and references. `AGENTS.md` directs other AI agents to consult these materials. If you have a GitHub account, use **Fork** in the upper-right corner to create your own copy.
- A **Deploy Mod** script is included so you can quickly try your changes in-game. Use `scripts/Deploy-Mod.ps1` or the **Deploy Mod** button at the top of the Visual Studio solution. It deploys to the local `C:\Program Files (x86)\Steam\steamapps\common\Sephiria\AddOns` directory.

## License

GNU Affero General Public License (AGPL)

Anyone may redistribute, share, and modify this project's code and build outputs under the terms of the [license](LICENSE).

## Credits

I used the mods by [Mira090](https://github.com/Mira090) as references.

ありがとう！ Thanks!
