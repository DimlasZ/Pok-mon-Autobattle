using System.Collections.Generic;

public static partial class AbilitySystem
{
    private struct EffectContext
    {
        public readonly PokemonInstance       source;
        public readonly List<PokemonInstance> sourceTeam;
        public readonly List<PokemonInstance> enemyTeam;
        public readonly AbilityData           ab;
        public readonly int                   contextDamage;
        public readonly int                   excessDamage;
        public readonly PokemonInstance       contextTarget;
        public readonly float                 v;     // ab.FloatValue
        public readonly int                   count; // ab.count
        // Targets resolved once in ApplyEffect so VFX and effect always hit the same Pokémon.
        public readonly List<PokemonInstance> preResolvedTargets;

        public EffectContext(PokemonInstance source, List<PokemonInstance> sourceTeam,
            List<PokemonInstance> enemyTeam, AbilityData ab,
            int contextDamage, int excessDamage, PokemonInstance contextTarget,
            List<PokemonInstance> preResolvedTargets = null)
        {
            this.source               = source;
            this.sourceTeam           = sourceTeam;
            this.enemyTeam            = enemyTeam;
            this.ab                   = ab;
            this.contextDamage        = contextDamage;
            this.excessDamage         = excessDamage;
            this.contextTarget        = contextTarget;
            this.v                    = ab.FloatValue;
            this.count                = ab.count;
            this.preResolvedTargets   = preResolvedTargets;
        }
    }
}
