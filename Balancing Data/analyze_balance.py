import pandas as pd
import numpy as np
import os
import glob

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))

# Load tierlist
tierlist = pd.read_csv(os.path.join(SCRIPT_DIR, "Tierlist.csv"))
tierlist.columns = tierlist.columns.str.strip().str.lstrip("\ufeff")
tier_map = dict(zip(tierlist["Name"], tierlist["Tier"]))

slot_cols = ["Slot0", "Slot1", "Slot2", "Slot3", "Slot4", "Slot5"]

OUTPUT_DIR = os.path.join(SCRIPT_DIR, "output")
os.makedirs(OUTPUT_DIR, exist_ok=True)

sim_files = sorted(glob.glob(os.path.join(SCRIPT_DIR, "sim_tier*_teams.csv")))

for sim_file in sim_files:
    teams = pd.read_csv(sim_file)
    tier = teams["Tier"].iloc[0]

    # --- Winrates ---
    pokemon_in_tier = [p for p, t in tier_map.items() if t <= tier]
    winrate_rows = []

    for pokemon in pokemon_in_tier:
        mask = teams[slot_cols].isin([pokemon]).any(axis=1)
        subset = teams[mask]
        if subset.empty:
            continue
        winrates = subset["WinRate%"].values
        winrate_rows.append({
            "Pokemon": pokemon,
            "Tier": tier_map.get(pokemon, tier),
            "Winrate": round(winrates.mean(), 2),
            "Std_Dev": round(winrates.std(), 2),
            "Sample_Size": len(winrates),
        })

    winrate_df = pd.DataFrame(winrate_rows).sort_values("Winrate", ascending=False)
    winrate_path = os.path.join(OUTPUT_DIR, f"output_tier{tier}_winrates.csv")
    winrate_df.to_csv(winrate_path, index=False)
    print(f"Saved {os.path.basename(winrate_path)} ({len(winrate_df)} Pokemon)")

    # --- Synergy ---
    base_winrate = dict(zip(winrate_df["Pokemon"], winrate_df["Winrate"]))
    pokemon_list = winrate_df["Pokemon"].tolist()
    pair_rows = []

    for i, p1 in enumerate(pokemon_list):
        for p2 in pokemon_list[i + 1:]:
            mask = (
                teams[slot_cols].isin([p1]).any(axis=1) &
                teams[slot_cols].isin([p2]).any(axis=1)
            )
            subset = teams[mask]
            if len(subset) < 3:
                continue

            pair_winrate = subset["WinRate%"].mean()
            avg_base = (base_winrate[p1] + base_winrate[p2]) / 2
            synergy = round(pair_winrate - avg_base, 2)

            pair_rows.append({
                "Pokemon1": p1,
                "Pokemon2": p2,
                "Winrate_Together": round(pair_winrate, 2),
                "Avg_Base_Winrate": round(avg_base, 2),
                "Synergy_Delta": synergy,
                "Sample_Size": len(subset),
            })

    synergy_df = pd.DataFrame(pair_rows).sort_values("Synergy_Delta", ascending=False)
    synergy_path = os.path.join(OUTPUT_DIR, f"output_tier{tier}_synergy.csv")
    synergy_df.to_csv(synergy_path, index=False)
    print(f"Saved {os.path.basename(synergy_path)} ({len(synergy_df)} pairs)")
