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

- [ ] Main Menu scene (Play, Quit button)
- [ ] Basic UI layout


## Buy Phase

- [x] Team slots UI (6 slots)
- [x] Shop slots UI (5-6 Pokemon offered)
- [ ] Shop tier upgrade button and logic (unlocks stronger Pokemon per run)
- [x] Pokédollar display
- [ ] Pokédollar interest and Pokédollar cap system
- [x] Buy button logic
- [x] Sell button logic
- [x] Reroll button logic (costs Pokédollar)
- [ ] Freeze/lock shop slot dont think i want that
- [ ] Drag & drop Pokemon from shop to team slots
- [ ] Team reordering (drag & drop within team slots)
- [ ] Pokemon evolving
- [ ] Pokemon evolution and preevolution on hover
- [ ] Ability on hover
- [ ] Item shop and held item logic
- [x] ShopManager script
- [ ] Round start Pokédollar income


## Battle Phase

- [x] BattleManager script
- [x] Attack order logic (based on Speed)
- [x] Damage calculation
- [ ] Type effectiveness (decide early if included)
- [ ] Ability/passive trigger system (on attack, on faint, on round start)
- [ ] Win/loss condition per battle
- [ ] Player HP loss on defeat
- [ ] Health bars in battle UI
- [ ] Enemy team display during battle phase
- [ ] Basic battle animations (attack, hurt, faint)
- [x] Battle log / text feedback
- [ ] Sample teams need to better


## Results Screen

- [ ] Show win/loss/draw
- [ ] Show damage taken
- [ ] Show remaining team
- [ ] Continue button back to Buy Phase


## Game Loop

- [ ] GameManager script (phase transitions)
- [ ] Round counter system
- [ ] Enemy team generation (scales with round)
- [ ] Game Over screen (when player HP = 0)
- [ ] Victory screen (survived all rounds)


## UI & Polish

- [x] UIManager script
- [ ] Screen transitions (fade in/out)
- [x] Round indicator (Round 3 of 8)
- [x] Player HP display
- [ ] Pause/settings menu
- [ ] Sound effects (buy, sell, attack, faint)
- [ ] Background music per phase
- [ ] Main Menu screen
- [ ] pokedollar icon



## Testing & Balancing

- [ ] Test battle logic with 10 starter Pokemon
- [ ] Balance Pokédollar economy
- [ ] Balance Pokemon stats
- [ ] Test ability triggers
- [x] Add remaining Pokemon once systems are stable


## Things that might change, i need to think about it

- [ ] Start with more HP as a player and lose more life based on surviving enmey pokemon.
- [ ] How to evolve a pokemon, 3x1 = level 2 or buy the level 2 pokemon?
- [ ] After how many rounds there is a draw?
- [ ] Make Bench smaller, to 3 Poke? otherwise to many pokeslots
- [ ] no max rounds
- [ ] fix Tierlist
- [ ] fix HP and attack
- [ ] better enemy poketeams
- [ ] only one type, fix logic and excel