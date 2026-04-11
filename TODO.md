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
- [x] Pokédollar display
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
- [x] Type effectiveness (decide early if included)
- [x] Ability/passive trigger system (on attack, on faint, on round start)
- [ ] Win/loss condition per battle
- [ ] Player HP loss on defeat
- [ ] Health bars in battle UI
- [ ] Enemy team display during battle phase
- [ ] Basic battle animations (attack, hurt, faint)
- [x] Battle log / text feedback
- [ ] Sample teams need to better
- [ ] Flatdamage battle calc, is wrong
- [ ] fix the damage calc sccipt


## Ability System – Known Issues

### Code fixes needed
- [ ] Friend Guard (ID 37, 76): `GetAllyDamageReduction` checks for `boost_defense_allies` but CSV uses `damage_reduction` with `target=ally_all` — ally damage reduction never triggers
- [ ] Flower Gift (ID 82): `GetPassiveAttackMultiplier` only handles `target=self` — passive ally attack boost not applied
- [ ] Levitate (ID 11): `IsImmuneToGround` checks `effect == "immune_to_ground"` but CSV has `effect=immune` — Ground immunity never fires
- [ ] Cloud Nine (ID 4): `negate_weather` effect not handled anywhere in code
- [ ] Emergency Exit (ID 29): `move_to_last` effect not handled anywhere in code
- [ ] Mold Breaker (ID 33): `ignore_abilities` effect not handled anywhere in code
- [ ] Aqua Jet (ID 44): `priority` effect not handled anywhere in code

### Excel/CSV fixes needed
- [ ] Levitate (ID 11): `Ground` is in the Chance column — move to Condition column and set effect to `immune_to_ground`
- [ ] Water Sport (ID 53): Missing `custom=water_sport` — currently reduces ALL damage 50% instead of Fire only
- [ ] Sand Veil (ID 83): Missing `condition=weather_sandstorm` — reduces damage 25% always, not just in sandstorm
- [ ] Technician (ID 21): Effect still `damage_multiplier` — either remove row or replace with correct effect
- [ ] Bone Club (ID 64): Effect is `overflow_damage` but code expects `overflow_damage_next`


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
- [ ] Overlay screen after battle.



## Testing & Balancing

- [ ] Test battle logic with 10 starter Pokemon
- [ ] Balance Pokédollar economy
- [ ] Balance Pokemon stats
- [ ] Test ability triggers
- [x] Add remaining Pokemon once systems are stable


## Things that might change, i need to think about it

- [ ] Start with more HP as a player and lose more life based on surviving enmey pokemon.
- [ ] How to evolve a pokemon, 3x1 = level 2 or buy the level 2 pokemon?
- [ ] After how many rounds there is a draw? prolly non, no draw should be possible. Careful healing outscales damage maybe.
- [x] Make Bench smaller, to 3 Poke? otherwise to many pokeslots
- [x] no max rounds
- [x] fix Tierlist
- [x] fix HP and attack
- [x] better enemy poketeams
- [x] only one type, fix logic and excel
- [ ] 8 wins and then top 4 + champ?
- [ ] weighted tier teams for enemy or database teams
- [ ] Coop Multiplayer? May need a server, or at least a relay. 

## Testing & Balancing

- [ ] Wingull should be flying instead of watter, change excel, add a new water poke for wingull.
- [ ] Bench and battlerown should be switchable as well with drag and drop.
- [ ] Music needs to fade out, after win for exmaple.
- [ ] on hit effects: poke needs to be hit with atleast one damage (no ground vs fyling stacking)
- [ ] Entry Hazard are not stackable.