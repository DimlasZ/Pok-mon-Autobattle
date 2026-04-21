import pandas as pd
import glob
import os

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
OUTPUT = os.path.join(SCRIPT_DIR, "enemy_teams.csv")

SIM_DIR = SCRIPT_DIR

# ── Configuration ────────────────────────────────────────────────────────────
ROWS_PER_ROUND = 5000   # top N teams to take from each round file
TIER6_LIMIT    = 10000  # override for Tier 6 rounds
# ─────────────────────────────────────────────────────────────────────────────

sim_files = sorted(glob.glob(os.path.join(SIM_DIR, "sim_round*_tier*_teams.csv")))

if not sim_files:
    print("No sim_round*_tier*_teams.csv files found in", SCRIPT_DIR)
    exit(1)

all_dfs = []

for sim_file in sim_files:
    df = pd.read_csv(sim_file)
    tier  = df["Tier"].iloc[0]
    round_ = df["Round"].iloc[0]
    limit = TIER6_LIMIT if tier == 6 else ROWS_PER_ROUND
    all_dfs.append(df.head(limit))
    print(f"Round {round_:>2}  Tier {tier}: {min(limit, len(df))} rows  ({os.path.basename(sim_file)})")

result = pd.concat(all_dfs, ignore_index=True).sort_values("Round").reset_index(drop=True)
result.to_csv(OUTPUT, index=False)
print(f"\nSaved {len(result)} total rows -> {os.path.normpath(OUTPUT)}")
