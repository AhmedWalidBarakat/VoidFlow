# VoidFlow

**A CS-style surf game I made for fun, vibe coding on the side. Nothing too serious, just a little game for anyone checking out my portfolio to jump in and try.**

![Unity](https://img.shields.io/badge/Unity-6-000000?style=flat-square&logo=unity&logoColor=white)
![C#](https://img.shields.io/badge/C%23-URP-512BD4?style=flat-square&logo=dotnet&logoColor=white)
![WebGL](https://img.shields.io/badge/WebGL-browser-990000?style=flat-square&logo=webgl&logoColor=white)
![License](https://img.shields.io/badge/license-MIT-D4AF37?style=flat-square)

**[Play it in your browser](https://ahmedwalidbarakat.github.io/VoidFlow/)** (desktop with a keyboard and mouse, it takes a few seconds to load)

![VoidFlow screenshot](docs/screenshot.png)

## What it is

I grew up on CS surf maps, so I made my own take on it. You drop off a start terrace and surf one fixed course of 696 ramps through 116 stages, with a checkpoint at the start of each stage and a finish at the end. Along the way are recreations of real surf maps (utopia, summer and mesa early on, then the hardest maps there are: corruption, deity, anubis, essentia, trofle, spin, before, frags_nightmare and ten more tier 7 maps), laid out ramp by ramp after watching their record runs, and a final stage that is almost impossible. They are my own rebuilds from video, not the original map files. The stages before them are built to be hard in the way the classic hard maps are (surf_sinsane and the maps stitched into it): long gaps with big sideways shifts onto short landings, small windows in the walls to fly through, ramps broken into pieces, and gates of blocks on the ramp face that leave a narrow lane to thread, on ramps of every shape (snipes, drops, stepped dives, kickers, zigzags, bowls, spins, loops and full 360-degree helixes), runs of bhop platforms to hop across, launches that throw you right across a hall, and slot gates: two lit pillars either side of a jump to air-strafe between. Like a real map, each enclosed stage is one big solid hall round all its ramps, joined to the next and split into chambers by dividing walls (after boreas, the white rooms and 666: every ramp waits round the bend in a room of its own, and you only see the next one as you fly through a framed window in the wall), with a floor that steps down in terraces close under the ramps the way a map's floor follows its course, lit like a map too: a strong sun throwing hard shadows from the walls and ramps, soft shade where surfaces meet, and relief on the brick, stone and plate, with walls dressed to fit the zone the way the great maps dress theirs: crystal caves, castle battlements, honeycombs, furnace mouths, warehouse windows, neon shapes, library shelves, arcades of arches and more, plus ivy on the old stone, chandeliers, lava floors in the hot zones, tunnels of light rings over some ramps, pillars, floating platforms and stacked blocks. The view is CS2's (106 degrees across at 16:9). Before the real maps comes LIMINAL, eight stages of my own after the empty, uncanny rooms of murglegurgle's surf_liminal (inspired by it, not its files). Movement is tuned to feel like classic Source surf (air strafing, ramp sliding, the whole thing).

The course's layout, section by section (what each part teaches, its ramps, walls, speeds and landings, with top-down diagrams), is in [COURSE_DESIGN.md](COURSE_DESIGN.md).

Behind the start hall, through the archway in its back wall, there's also a **10 stage bhop challenge**: a bhop trail out to a plaza, then ten rooms in the manner of the classic CS bhop maps, one per stage: walled corridors of pillar blocks a classic hop apart over a floor that sends you back to the stage's pad, with slopes, banks and short surf ramps, each room in its own look (a marble court, a lantern garden, a neon grid, a crystal cave, lava...) and linked to the next by an exit portal, from a warm-up to a final stage that needs near-perfect strafing. Finish it to win every karambit and the Void gloves painted to match each, including the Karambit | Velocity and its gloves, which are only won there. Its design, every jump's numbers and a bot playtest of every stage are in [BHOP_CHALLENGE.md](BHOP_CHALLENGE.md).

Along the way you grab Void Shards, which earn Void Cases, which drop knives, gloves and snipers with their own inspect animations (every karambit has a pair of Void gloves painted to match it); every Void weapon is summoned with a draw of its own and has an inspect made for it (a fencer's lunge, a reaping sweep, a blade that floats above your palm, a sword that falls like a leaf, and more). Every stage is timed like on a surf server: reach the next checkpoint and you see your split against your best (green faster, red slower, gold for a new best), and from then on a glowing ghost of your best run rides each stage with you. The HUD shows the course's progress, the stage, the run and stage clocks, your speed (coloured cool to hot) and the keys you're pressing. At the finish a results card shows your time against your best, your falls, your top speed, the stage bests you set and your sum of best, with a strip of every stage's split. Press Tab for the settings: sensitivity in CS2's units, field of view, 3D resolution (100% by default and never lowered behind your back: turn it down on a slower machine), volume, colour grading, shadows, an FPS counter and the HUD's options. Your items, your best times and ghosts, your settings and your last checkpoint are saved in the browser, so you can come back to them.

## Controls

| Key | Action |
| --- | --- |
| WASD | Move (on ramps, hold A or D toward the ramp and steer with the mouse) |
| Space | Jump (hold to bhop) |
| 1 / 2 / Q | Sniper, knife, last weapon |
| Click / Right click | Fire / scope |
| F | Inspect (hold to show off a Void weapon) |
| I | Inventory |
| E | Use |
| M | Stages you've reached (teleport) and volume |
| C | Continue from your last checkpoint (in the start area) |
| T | Restart the stage (back to its checkpoint) |
| R | Restart the run |
| Tab | Settings |
| Esc | Release the mouse |

## Built with

- Unity 6 (URP), C#, built for WebGL
- Almost everything in the game is generated in code: the course, the zones, the weapons and their skins. The exception is the Void rarity knives and snipers, which are real 3D models from Sketchfab (CC-BY 4.0, each author credited in [CREDITS.md](CREDITS.md) and on the item's card)
- Free CC0 assets: arm model from [Quaternius](https://quaternius.com), leather, fabric and stone textures from [ambientCG](https://ambientcg.com), particle sprites from [Kenney](https://kenney.nl)

## License

MIT for the code. The CC0 assets are public domain; their license files are in the project next to them. The Void weapon models stay under their authors' CC-BY 4.0 licences, see [CREDITS.md](CREDITS.md).
