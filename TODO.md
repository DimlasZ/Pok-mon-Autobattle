# Pokemon Autobattler – Todo List

## Setup

- [x] Create private GitHub repo with Unity .gitignore
- [x] Set up Unity project
- [x] Create folder structure (Scripts, Sprites, Data, Scenes, Audio)
- [ ] Create the 4 Manager GameObjects in scene


## Data Layer

- [x] Finalize Excel sheet structure (name, hp, attack, speed, ability, type, etc.)
- [x] Create PokemonData ScriptableObject script
- [x] Create ItemData ScriptableObject script
- [ ] Import first 4 Pokemon as ScriptableObject assets
- [ ] Import Pokemon sprites and link them to assets


## Main Menu

- [ ] Main Menu scene (Play, Quit button)
- [ ] Basic UI layout


## Buy Phase

- [ ] Team slots UI (6 slots)
- [ ] Shop slots UI (5-6 Pokemon offered)
- [ ] Shop tier upgrade button and logic (unlocks stronger Pokemon per run)
- [ ] Pokédollar display
- [ ] Pokédollar interest and Pokédollar cap system
- [ ] Buy button logic
- [ ] Sell button logic
- [ ] Reroll button logic (costs Pokédollar)
- [ ] Freeze/lock shop slot
- [ ] Drag & drop Pokemon from shop to team slots
- [ ] Team reordering (drag & drop within team slots)
- [ ] Pokemon combining (3x same = level up)
- [ ] Pokemon star/level indicator on cards
- [ ] Item shop and held item logic
- [ ] ShopManager script
- [ ] Round start Pokédollar income


## Battle Phase

- [ ] BattleManager script
- [ ] Attack order logic (based on Speed)
- [ ] Damage calculation
- [ ] Type effectiveness (decide early if included)
- [ ] Ability/passive trigger system (on attack, on faint, on round start)
- [ ] Win/loss condition per battle
- [ ] Player HP loss on defeat
- [ ] Health bars in battle UI
- [ ] Enemy team display during battle phase
- [ ] Basic battle animations (attack, hurt, faint)
- [ ] Battle log / text feedback


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

- [ ] UIManager script
- [ ] Screen transitions (fade in/out)
- [ ] Round indicator (Round 3 of 8)
- [ ] Player HP display
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
- [ ] Add remaining Pokemon once systems are stable


## Things that might change, i need to think about it

- [ ] Start with more HP as a player and lose more life based on surviving enmey pokemon.
- [ ] How to evolve a pokemon, 3x1 = level 2 or buy the level 2 pokemon?
- [ ] After how many rounds there is a draw?
- [ ] Make Bench smaller, to 3 Poke? otherwise to many pokeslots
- [ ] no max rounds