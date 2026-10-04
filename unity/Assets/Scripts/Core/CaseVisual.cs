using System;
using System.Collections.Generic;
using UnityEngine;

namespace Procon
{
    /// <summary>
    /// O que um caso liga em um encaixe do palco. O texto vem sempre da fonte
    /// (fala, dossie ou explicacao do proprio caso); nada aqui inventa fato.
    /// </summary>
    [Serializable]
    public class SlotFill
    {
        [Tooltip("Identificador do encaixe no palco, por exemplo produto-principal.")]
        public string slotId = "";

        [Tooltip("Texto aplicado sobre a superficie: preco, peso, capacidade, prazo.")]
        public string caption = "";

        [Tooltip("Desenho do objeto neste caso. Vazio = o desenho padrao do encaixe.")]
        public Sprite sprite;

        [Tooltip("Cor do objeto, quando o encaixe aceita variacao.")]
        public Color tint = Color.white;

        public bool overrideTint;
    }

    /// <summary>
    /// A configuracao visual de um caso. Fica fora do CaseDatabase de proposito:
    /// reimportar os casos do JSON nao pode apagar o trabalho de arte.
    /// </summary>
    [Serializable]
    public class CaseVisual
    {
        [Tooltip("Id do caso no CaseDatabase, por exemplo f-sup-1.")]
        public string caseId = "";

        [Tooltip("Aparencia do responsavel, 0 a 3, como as variantes do jogo antigo. " +
                 "O fiscal nao varia: ele e sempre o mesmo, de azul.")]
        [Range(0, 3)] public int ownerVariant;

        [TextArea(1, 2)]
        [Tooltip("Anotacao de producao. Nao aparece no jogo.")]
        public string note = "";

        public List<SlotFill> fills = new List<SlotFill>();
    }
}
