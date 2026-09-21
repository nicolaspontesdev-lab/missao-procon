using System.Collections.Generic;
using UnityEngine;

namespace Procon
{
    /// <summary>
    /// Banco de casos do jogo. E um asset: da para editar tudo pelo Inspector,
    /// sem tocar em codigo.
    /// </summary>
    [CreateAssetMenu(fileName = "CaseDatabase", menuName = "Missao PROCON/Banco de casos")]
    public class CaseDatabase : ScriptableObject
    {
        [Header("Rodada")]
        [Tooltip("Quantos casos por local. O jogo nunca pede mais do que existe no banco.")]
        [Min(1)] public int casesPerScenario = 3;

        [Header("Tabelas")]
        public List<LevelDef> levels = new List<LevelDef>();
        public List<ScenarioDef> scenarios = new List<ScenarioDef>();

        [Header("Casos")]
        public List<ProconCase> cases = new List<ProconCase>();

        public LevelDef Level(LevelKey key)
        {
            foreach (var level in levels)
                if (level.key == key) return level;
            return new LevelDef { key = key, label = key.ToString().ToUpperInvariant(), basePoints = 100 };
        }

        public ScenarioDef Scenario(ScenarioKey key)
        {
            foreach (var scenario in scenarios)
                if (scenario.key == key) return scenario;
            return new ScenarioDef { key = key, label = key.ToString().ToUpperInvariant() };
        }

        public List<ProconCase> CasesFor(LevelKey level, ScenarioKey scenario)
        {
            var found = new List<ProconCase>();
            foreach (var item in cases)
                if (item.level == level && item.scenario == scenario) found.Add(item);
            return found;
        }

        /// <summary>Quantos casos uma rodada deste nivel realmente tera.</summary>
        public int RoundSize(LevelKey level)
        {
            var total = 0;
            foreach (var scenario in scenarios)
            {
                var pool = CasesFor(level, scenario.key).Count;
                total += Mathf.Min(casesPerScenario, pool);
            }
            return total;
        }
    }
}
