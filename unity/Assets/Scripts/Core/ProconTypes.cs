using System;
using UnityEngine;

namespace Procon
{
    public enum LevelKey { Fundamental = 0, Medio = 1, Tecnico = 2 }

    public enum ScenarioKey { Supermercado = 0, Eletronicos = 1, Banco = 2, Online = 3, Telefonia = 4 }

    /// <summary>
    /// Dossie opcional de um caso: as tres pistas que o fiscal precisa reunir
    /// antes de poder emitir o parecer.
    /// </summary>
    [Serializable]
    public class Investigation
    {
        [TextArea(2, 4)] public string briefing;
        [TextArea(2, 4)] public string consumerStatement;
        public string documentTitle;
        [TextArea(2, 4)] public string documentBody;
        public string inspectionTitle;
        [TextArea(2, 4)] public string inspectionBody;

        public bool HasContent => !string.IsNullOrWhiteSpace(briefing);
    }

    /// <summary>Um caso: a fala do responsavel e o gabarito de infracao.</summary>
    [Serializable]
    public class ProconCase
    {
        public string id;
        public LevelKey level;
        public ScenarioKey scenario;
        public string owner;
        [TextArea(2, 5)] public string text;
        [Tooltip("Marcado = a situacao descrita E uma infracao ao CDC.")]
        public bool isViolation;
        [TextArea(1, 3)] public string hint;
        [TextArea(2, 6)] public string explanation;
        [TextArea(1, 3)] public string legalReference;
        public Investigation investigation = new Investigation();

        public bool HasInvestigation => investigation != null && investigation.HasContent;
    }

    [Serializable]
    public class LevelDef
    {
        public LevelKey key;
        public string label = "";
        [Min(0)] public int basePoints = 100;
        [Tooltip("Mostra a pista curta antes da resposta.")]
        public bool showHints;
        [TextArea(1, 3)] public string description = "";
    }

    [Serializable]
    public class ScenarioDef
    {
        public ScenarioKey key;
        public string label = "";
        public string subtitle = "";
        public string decor = "";
        public Color tint = Color.gray;
    }
}
