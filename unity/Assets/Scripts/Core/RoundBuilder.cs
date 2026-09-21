using System.Collections.Generic;
using UnityEngine;

namespace Procon
{
    /// <summary>Uma parada da rota: o local e os casos daquele local.</summary>
    public class RoundStop
    {
        public ScenarioKey scenario;
        public List<ProconCase> cases = new List<ProconCase>();
    }

    /// <summary>A rodada inteira, ja achatada na ordem em que sera jogada.</summary>
    public class Round
    {
        public List<RoundStop> stops = new List<RoundStop>();
        public List<ProconCase> cases = new List<ProconCase>();
        public List<int> stopOfCase = new List<int>();
        public List<int> positionInStop = new List<int>();

        public int Total => cases.Count;
        public int StopCount => stops.Count;
    }

    public static class RoundBuilder
    {
        /// <summary>
        /// Sorteia a ordem dos locais e, em cada local, abre pelo caso que tem
        /// dossie de investigacao. Nunca pede mais casos do que o banco tem.
        /// </summary>
        public static Round Build(CaseDatabase database, LevelKey level)
        {
            var round = new Round();
            if (database == null) return round;

            var order = new List<ScenarioKey>();
            foreach (var scenario in database.scenarios) order.Add(scenario.key);
            Shuffle(order);

            foreach (var scenarioKey in order)
            {
                var pool = database.CasesFor(level, scenarioKey);
                if (pool.Count == 0) continue;

                ProconCase opening = null;
                foreach (var item in pool)
                {
                    if (!item.HasInvestigation) continue;
                    opening = item;
                    break;
                }

                var rest = new List<ProconCase>();
                foreach (var item in pool)
                    if (item != opening) rest.Add(item);
                Shuffle(rest);

                var wanted = Mathf.Clamp(database.casesPerScenario, 1, pool.Count);
                var stop = new RoundStop { scenario = scenarioKey };
                if (opening != null) stop.cases.Add(opening);
                for (var i = 0; i < rest.Count && stop.cases.Count < wanted; i++)
                    stop.cases.Add(rest[i]);

                var stopIndex = round.stops.Count;
                round.stops.Add(stop);
                for (var i = 0; i < stop.cases.Count; i++)
                {
                    round.cases.Add(stop.cases[i]);
                    round.stopOfCase.Add(stopIndex);
                    round.positionInStop.Add(i);
                }
            }

            return round;
        }

        static void Shuffle<T>(IList<T> items)
        {
            for (var i = items.Count - 1; i > 0; i--)
            {
                var j = Random.Range(0, i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }
    }

    public static class Scoring
    {
        /// <summary>Bonus de sequencia: +25 por acerto encadeado, teto de 100.</summary>
        public static int ComboBonus(int streak)
        {
            return streak < 2 ? 0 : Mathf.Min((streak - 1) * 25, 100);
        }

        /// <summary>
        /// Patente final. As faixas sao as mesmas do jogo original, so que em
        /// proporcao, para continuarem valendo se a rodada mudar de tamanho.
        /// </summary>
        public static string Rank(int correct, int total)
        {
            var ratio = total <= 0 ? 0f : (float)correct / total;
            if (ratio <= 0.36f) return "ESTAGIÁRIO EM TREINAMENTO";
            if (ratio <= 0.60f) return "FISCAL INICIANTE";
            if (ratio <= 0.80f) return "DEFENSOR DO CONSUMIDOR";
            if (ratio <= 0.92f) return "ESPECIALISTA DO PROCON";
            return "LENDÁRIO FISCAL DO CONSUMIDOR";
        }
    }
}
