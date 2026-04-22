# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A Unity 2024.2 (URP 2D) auto-battler game. Players build a team during a shop phase, then watch a fully automated battle resolve. The game runs for up to 13 rounds (8 badge wins + 4 Elite 4 + Champion).

## Unity Editor Commands

There are no CLI build scripts. All commands run from the Unity Editor menu bar under **Pokemon/**:

| Menu Item | What it does |
|---|---|
| `Pokemon/Import Pokemon from CSV` | Parses `Assets/Data/Pokémon_data.csv` → creates/updates PokemonData ScriptableObjects in `Assets/Resources/Data/Pokemon/` |
| `Pokemon/Import Abilities from CSV` | Parses ability CSV → creates AbilityData ScriptableObjects in `Assets/Data/Abilities/` |
| `Pokemon/Auto Import CSVs` | One-click import of all CSV assets |
| `Pokemon/Team Simulator` | EditorWindow to simulate battles, export winning teams to CSV |

**Balancing workflow:**
1. Edit stats in `Balancing Data/Tierlist.xlsm`, export as `Assets/Data/Pokémon_data.csv`
2. Run `Pokemon/Import Pokemon from CSV`
3. Run `Pokemon/Team Simulator` (All Rounds mode, ~1000 teams × 50 opponents)
4. Copy output CSVs into `Assets/Resources/Data/Teams/enemy_teams.csv`

## Architecture

### Core Separation: Logic vs. Visual

The battle system is split into two parallel implementations:

- **`BattleManager.cs`** — synchronous, headless, no UI. Used by the editor Team Simulator.
- **`BattleSceneManager.cs`** — coroutine-based visual version with animations, VFX, and playback modes (Step / Auto / SpeedUp). Subscribes to `AbilitySystem` events.
- **`BattleSimulator.cs`** — static utility wrapping BattleManager for offline tournament runs.

When modifying battle logic, changes usually need to be applied to **both BattleManager and BattleSceneManager**.

### Game Flow

```
MainMenuScene → GameManager.StartGame()
  → ShopScene (buy phase)
      ShopManager: 3 rows — Shop (6 slots), BattleRow (up to 6), Bench (6)
      Player buys, places, evolves Pokémon
  → GameManager.StartBattle()
      Captures BattleRow, reverses order (rightmost = front = index 0)
      Loads BattleScene
  → BattleSceneManager runs coroutine battle
  → GameManager.OnBattleComplete(result)
      HP/win tracking, tier upgrade checks, ReturnToShop()
```

### AbilitySystem

Fully data-driven — every ability is defined in CSV/ScriptableObjects, no hardcoded effects.

```
AbilitySystem.TryFire(trigger, source, sourceTeam, enemyTeam)
  → CheckCondition()   — hp_below_N, full_hp, weather_*, super_effective, first_hit, ...
  → ShouldTrigger()    — chance roll (0 = always)
  → ResolveTargets()   — self, enemy_front, enemy_all, enemy_random, ally_random, ...
  → _handlers[effect]  — dispatched to partial class handlers
```

Trigger points: `on_battle_start`, `on_round_start`, `before_attack`, `on_attack`, `after_attack`, `before_hit`, `on_hit`, `on_faint`, `on_ally_faint`, `on_debuff`, `on_heal`, `on_heal_trigger`, `on_ally_heal`

Effect handlers are split across partial classes by category: `AbilitySystem.Buffs.cs`, `Damage.cs`, `Debuffs.cs`, `Heal.cs`, `Weather.cs`, `Special.cs`, `Passives.cs`, `Targets.cs`, `Conditions.cs`, `Context.cs`

To add a new ability effect: register a handler in `AbilitySystem.cs`'s `_handlers` dictionary, implement it in the appropriate partial class.

### Data Model

- **`PokemonData`** (ScriptableObject) — static template from CSV. Never mutate at runtime.
- **`PokemonInstance`** — runtime copy with stats rolled at 80–100% of base (`Spread()`), current HP, active buffs. Call `ResetForBattle()` before each battle.
- Buffs/debuffs are direct stat mutations (`attack *= 1.5`), not separate objects.

### Randomness

`UnityEngine.Random` is used for speed ties and `enemy_random`/`ally_random` targeting. This is **not seeded**, so two independent simulations of the same battle can diverge. Relevant sites: `BattleSimulator.cs:42`, `BattleManager.cs:107`, `AbilitySystem.cs:162`, `AbilitySystem.Targets.cs:56,67,163`, `AbilitySystem.Special.cs:23`.

### Scene Generators — Golden Rule

**Never add UI elements manually in the Unity Editor.** All scenes are fully generated from code via the editor generators. Any UI change (new button, panel, label) must be implemented in the corresponding generator script first, then the scene regenerated:

| Scene | Generator | Menu Item |
|---|---|---|
| MainMenuScene | `Assets/Scripts/Editor/MainMenuSceneGenerator.cs` | `Pokemon/Generate Main Menu Scene` |
| ShopScene (SampleScene) | `Assets/Scripts/Editor/UILayoutGenerator.cs` | (check menu) |
| BattleScene | `Assets/Scripts/Editor/BattleSceneGenerator.cs` | (check menu) |

If a scene is regenerated without the code change, manually placed elements will be lost.

### Scene Structure

- **MainMenuScene** — entry point, Play Now / Continue
- **SampleScene** (Shop) — buy phase UI, 3 rows, Pokédollars, Reroll/Start Battle
- **BattleScene** — 2×3 grid each side, battle log, playback controls, 8 badge + 4 elite + champ + 6 hearts progress bar

### Persistence

- **AutoSaveManager** — JSON to `Application.persistentDataPath/autosave.json`. Only saves during buy phase.
- **PlayerTeamSaver** — records team per round for analysis.
- **HallOfFameManager** — stores champion team on victory.
- **PokedexUnlockManager** — tracks Elite 4 (8 wins) and Champion (13 wins) milestones.

### Enemy Generation

`EnemyGenerator` loads pre-simulated teams from `Resources/Data/Teams/enemy_teams.csv` (keyed by round). Falls back to random generation only if the CSV is missing. Always regenerate the CSV after balance changes.

## Key Patterns

- **Scene generators** (`BattleSceneGenerator`, `MainMenuSceneGenerator`) auto-wire Inspector references — re-run them after structural scene changes.
- **Event-driven VFX**: `AbilitySystem.OnAbilityFired` → `BattleSceneManager` spawns VFX (loose coupling, visual side only).
- **DontDestroyOnLoad**: `GameManager` and `ShopManager` persist across scene loads.
- **Tier progression**: rounds 1–2 = Tier 1, scaling up to Tier 6 at round 15+. Tier upgrades at tiers 2 and 4 restore 1 HP.
