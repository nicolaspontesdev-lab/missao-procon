using System.Collections.Generic;
using UnityEngine;

namespace Procon
{
    /// <summary>
    /// Caderno de configuracoes visuais dos casos. E um asset separado do banco
    /// de casos de proposito: reimportar o procon-cases.json recria a lista de
    /// casos, e o trabalho de arte nao pode morrer junto.
    /// </summary>
    [CreateAssetMenu(fileName = "CaseVisualBook", menuName = "Missao PROCON/Caderno visual dos casos")]
    public class CaseVisualBook : ScriptableObject
    {
        public List<CaseVisual> entries = new List<CaseVisual>();

        public CaseVisual For(string caseId)
        {
            if (string.IsNullOrEmpty(caseId)) return null;
            foreach (var entry in entries)
                if (entry != null && entry.caseId == caseId) return entry;
            return null;
        }

        public bool Has(string caseId) => For(caseId) != null;
    }
}
