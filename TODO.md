# Pokemon Autobattler – Todo List

## Setup

- [x] Create private GitHub repo with Unity .gitignore
- [x] Set up Unity project
- [x] Create folder structure (Scripts, Sprites, Data, Scenes, Audio)
- [x] Create the 4 Manager GameObjects in scene


## Data Layer

- [x] Finalize Excel sheet structure (name, hp, attack, speed, ability, type, etc.)
- [x] Create PokemonData ScriptableObject script
- [x] Create ItemData ScriptableObject script
- [x] Import all 386 Pokemon as ScriptableObject assets
- [x] Import Pokemon sprites and link them to assets


## Main Menu

- [x] Main Menu scene (Play, Quit button)
- [x] Basic UI layout
- [ ] finalize resolution tests
- [x] quitbutton


## Buy Phase

- [x] Team slots UI (6 slots)
- [x] Shop slots UI (5-6 Pokemon offered)
- [x] Pokédollar display
- [x] Buy button logic
- [x] Sell button logic
- [x] Reroll button logic (costs Pokédollar)
- [ ] Freeze/lock shop slot dont think i want that
- [x] Drag & drop Pokemon from shop to team slots
- [x] Team reordering (drag & drop within team slots)
- [x] Pokemon evolving
- [x] Pokemon evolution and preevolution on hover
- [x] Ability on hover
- [ ] Item shop and held item logic
- [x] ShopManager script



## Battle Phase

- [x] BattleManager script
- [x] Attack order logic (based on Speed)
- [x] Damage calculation
- [x] Type effectiveness (decide early if included)
- [x] Ability/passive trigger system (on attack, on faint, on round start)
- [x] Win/loss condition per battle
- [x] Player HP loss on defeat
- [x] Health bars in battle UI
- [x] Enemy team display during battle phase
- [x] Basic battle animations (attack, hurt, faint)
- [x] Battle log / text feedback
- [x] Sample teams need to better
- [x] Flatdamage battle calc, is wrong
- [x] fix the damage calc sccipt

- [ ] Entry Hazard are not stackable.
- [x] weather effects visual and with buffs / debuffs
- [x] sound effects


## Ability Issues
- [x] on hit effects: poke needs to be hit with atleast one damage (no ground vs fyling stacking)

## Results Screen

- [x] Show win/loss/draw
- [x] Show damage taken
- [x] Show remaining team
- [x] Continue button back to Buy Phase


## Game Loop

- [x] GameManager script (phase transitions)
- [x] Round counter system
- [x] Enemy team generation (scales with round)
- [ ] Game Over screen (when player HP = 0)
- [ ] Victory screen (survived all rounds)


## UI & Polish

- [x] UIManager script
- [x] Screen transitions (fade in/out)
- [x] Round indicator (Round 3 of 8)
- [x] Player HP display
- [x] Pause/settings menu
- [x] Sound effects (buy, sell, attack, faint)
- [x] Background music per phase
- [x] Main Menu screen
- [x] type icon
- [x] pokedollar icon
- [x] Overlay screen after battle.
- [ ] Music needs to fade out, after win for exmaple.
- [x] stat green and red , need to be a bit darker
- [x] Polish UI - Tooltip, bigger and on the right side
- [x] eve all evolutions shown in the tooltip of the pokedex same for bloom
- [ ] tool tip for Wearther effects
- [ ] better loss sound
- [ ] all poke looking to the right
- [ ] sandstorm damage another sound more like swoosh
- [ ] shop background
- [ ] Battle background
- [ ] back to mainmenu button. to left, with confirmation
- [ ] warning if continue with money in the bank.
- [ ] show which Tier we are
- [ ] show which unlocked this round
- [ ] Desktop Icon




## Testing & Balancing

- [x] Test battle logic with 10 starter Pokemon
- [x] Balance Pokédollar economy
- [x] Balance Pokemon stats
- [x] Test ability triggers
- [x] Add remaining Pokemon once systems are stable


## Things that might change, i need to think about it

- [ ] Start with more HP as a player and lose more life based on surviving enmey pokemon.
- [ ] max 1 legendary / legendary extremly rare in the shop
- [ ] 6 bench slots as well.
- [x] How to evolve a pokemon, 3x1 = level 2 or buy the level 2 pokemon?
- [x] After how many rounds there is a draw? prolly non, no draw should be possible. Careful healing outscales damage maybe.
- [x] Make Bench smaller, to 3 Poke? otherwise to many pokeslots
- [x] no max rounds
- [x] fix Tierlist
- [x] fix HP and attack
- [x] better enemy poketeams
- [x] only one type, fix logic and excel
- [x] 8 wins and then top 4 + champ?
- [x] weighted tier teams for enemy or database teams / LLM teams / harder enemy teams
- [x] always have a team size of 6.
- [ ] Coop Multiplayer? May need a server, or at least a relay. 

## Continue/ bugs 

- [ ] leech doesnt heal after attack. check if true for leech seed, should be fixed
- [ ] weather after each other viusal and stats bug to high attack stays, not sure need checking
- [ ] tool tip for Wearther effects
- [ ] new ability for maunzi
- [ ] batonpass fix
- [ ] run sim again take top 5000 as input or top 3000. or weighted.

investigate late teams, loading strange pokemon, tier 1 etc. 

on hit effects dont trigger on death



 
HP remove bvattlescreen top bar
cubone ability bugged, trigger on hit not on conditon
glurak worng dexcption , word missing
draw shoudnt cost a life



claude.md file learning








