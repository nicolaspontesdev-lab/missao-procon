using UnityEditor;
using UnityEngine;

namespace Procon.EditorTools
{
    /// <summary>
    /// Configuracao visual dos 45 casos: quais objetos cada caso liga no palco e
    /// que texto aparece neles.
    ///
    /// Regra: todo texto aqui vem da fala ou do dossie do proprio caso. Nenhum
    /// preco, prazo, valor ou nome foi inventado; quando a fala nao da o dado, o
    /// objeto aparece sem texto. E a cena nunca adianta a resposta: mostra o
    /// fato, quem julga e o jogador.
    ///
    /// Rodar de novo so preenche casos vazios, entao nao apaga ajuste feito a mao.
    /// </summary>
    public static class CaseVisuals
    {
        const string VisualBookPath = "Assets/Data/CaseVisualBook.asset";

        [MenuItem("Missao PROCON/3 - Semear visual dos casos", false, 22)]
        public static void SeedMenu() => Seed(false);

        /// <summary>force = true reescreve os 45. So use se nada foi ajustado a mao.</summary>
        public static void Seed(bool force)
        {
            var book = AssetDatabase.LoadAssetAtPath<CaseVisualBook>(VisualBookPath);
            if (book == null)
            {
                Debug.LogError("Caderno visual nao encontrado. Rode \"Gerar tudo\" antes.");
                return;
            }

            if (force)
                foreach (var entry in book.entries)
                    entry.fills.Clear();

            var filled = 0;
            var kept = 0;

            // ------------------------------------------------------- supermercado
            filled += Fill(book, "f-sup-1", 0, ref kept, "etiqueta", "ARROZ\nR$ 8,90", "produto-principal", "ARROZ", "terminal", "CAIXA\nR$ 11,90");
            filled += Fill(book, "f-sup-2", 1, ref kept, "produto-principal", "", "caixa", "RETIRADOS");
            filled += Fill(book, "f-sup-3", 2, ref kept, "produto-principal", "ARROZ", "produto-secundario", "ÓLEO");
            filled += Fill(book, "m-sup-1", 0, ref kept, "etiqueta", "R$ 5,99", "produto-principal", "", "terminal", "SISTEMA\nR$ 7,49");
            filled += Fill(book, "m-sup-2", 3, ref kept, "produto-principal", "1 kg", "balanca", "850 g");
            filled += Fill(book, "m-sup-3", 1, ref kept, "cartaz", "PROMOÇÃO\nPERÍODO\nPRODUTOS\nLIMITE POR PESSOA\nQUANTIDADE");
            filled += Fill(book, "t-sup-1", 2, ref kept, "produto-principal", "", "produto-secundario", "", "etiqueta", "PREÇO TOTAL");
            filled += Fill(book, "t-sup-2", 3, ref kept, "produto-principal", "", "balanca", "");
            filled += Fill(book, "t-sup-3", 0, ref kept, "etiqueta", "PREÇO ANUNCIADO", "produto-principal", "", "terminal", "CORRIGIDO");

            // ------------------------------------------------------- eletronicos
            filled += Fill(book, "f-ele-1", 1, ref kept, "celular", "DEFEITO APÓS 5 DIAS");
            filled += Fill(book, "f-ele-2", 2, ref kept, "fone", "", "documento", "COMPRA NA\nLOJA FÍSICA");
            filled += Fill(book, "f-ele-3", 0, ref kept, "notebook", "", "etiqueta", "À VISTA E\nPARCELADO");
            filled += Fill(book, "m-ele-1", 3, ref kept, "notebook", "", "documento", "35 DIAS NA\nASSISTÊNCIA");
            filled += Fill(book, "m-ele-2", 1, ref kept, "documento", "CONTRATO", "documento-secundario", "GARANTIA\nESTENDIDA PAGA");
            filled += Fill(book, "m-ele-3", 2, ref kept, "documento", "GARANTIA ESTENDIDA\nOPCIONAL");
            filled += Fill(book, "t-ele-1", 0, ref kept, "produto-principal", "", "documento", "DEFEITO\nMESES DEPOIS");
            filled += Fill(book, "t-ele-2", 3, ref kept, "documento", "TERMO SEPARADO\n45 DIAS");
            filled += Fill(book, "t-ele-3", 1, ref kept, "produto-principal", "", "documento", "ESPERA DE\n30 DIAS");

            // ------------------------------------------------------- banco
            filled += Fill(book, "f-ban-1", 0, ref kept, "documento", "PROPOSTA\nDE CRÉDITO", "documento-secundario", "CÓPIA DO\nCLIENTE");
            filled += Fill(book, "f-ban-2", 2, ref kept, "documento", "PROPOSTA\nDE CRÉDITO");
            filled += Fill(book, "f-ban-3", 3, ref kept, "terminal", "COBRANÇA");
            filled += Fill(book, "m-ban-1", 1, ref kept, "cartaz", "PROPAGANDA\nPARCELA EM\nDESTAQUE", "documento", "CONTRATO");
            filled += Fill(book, "m-ban-2", 0, ref kept, "documento", "ANTECIPAÇÃO DAS\nÚLTIMAS PARCELAS");
            filled += Fill(book, "m-ban-3", 2, ref kept, "documento", "EMPRÉSTIMO", "documento-secundario", "SEGURO\nOPCIONAL");
            filled += Fill(book, "t-ban-1", 3, ref kept, "documento", "MULTA DE\nMORA 2%");
            filled += Fill(book, "t-ban-2", 1, ref kept, "documento", "CÁLCULO DA\nQUITAÇÃO");
            filled += Fill(book, "t-ban-3", 0, ref kept, "documento", "AVALIAÇÃO\nDE CRÉDITO");

            // ------------------------------------------------------- compras online
            filled += Fill(book, "f-onl-1", 1, ref kept, "caixa", "RECEBIDO\nHÁ 4 DIAS", "terminal", "PEDIDO DE\nDESISTÊNCIA");
            filled += Fill(book, "f-onl-2", 0, ref kept, "caixa", "DEVOLVIDO", "terminal", "PRODUTO\n+ FRETE");
            filled += Fill(book, "f-onl-3", 2, ref kept, "celular", "64 GB", "terminal", "ANÚNCIO\n128 GB");
            filled += Fill(book, "m-onl-1", 3, ref kept, "terminal", "OFERTA\nCONFIRMADA", "documento", "CANCELAMENTO");
            filled += Fill(book, "m-onl-2", 1, ref kept, "caixa", "DEVOLVIDO", "cartao", "VALE-COMPRAS");
            filled += Fill(book, "m-onl-3", 0, ref kept, "caixa", "6º DIA", "terminal", "ESTORNO\nINTEGRAL");
            filled += Fill(book, "t-onl-1", 2, ref kept, "caixa", "DEVOLVIDO", "terminal", "PRODUTO\nFRETE DE IDA");
            filled += Fill(book, "t-onl-2", 3, ref kept, "documento", "OFERTA\nACEITA");
            filled += Fill(book, "t-onl-3", 1, ref kept, "documento", "PEDIDO", "documento-secundario", "CRÉDITO\nCONEXO");

            // ------------------------------------------------------- telefonia
            filled += Fill(book, "f-tel-1", 0, ref kept, "celular", "", "documento", "FATURA\nPACOTE DE JOGOS");
            filled += Fill(book, "f-tel-2", 1, ref kept, "celular", "", "documento", "MUDANÇA\nDE PLANO");
            filled += Fill(book, "f-tel-3", 2, ref kept, "terminal", "CANCELAMENTO\nPROTOCOLO");
            filled += Fill(book, "m-tel-1", 3, ref kept, "celular", "", "documento", "CRÉDITO DO\nPERÍODO SEM USO");
            filled += Fill(book, "m-tel-2", 0, ref kept, "documento", "FATURA\nPLANO MAIS CARO");
            filled += Fill(book, "m-tel-3", 1, ref kept, "cartaz", "PLANO\nPREÇO • FRANQUIA\nVELOCIDADE\nFIDELIZAÇÃO • MULTA");
            filled += Fill(book, "t-tel-1", 2, ref kept, "documento", "CONTRATO");
            filled += Fill(book, "t-tel-2", 3, ref kept, "documento", "COBRANÇA\nRECALCULADA", "terminal", "PROTOCOLO");
            filled += Fill(book, "t-tel-3", 0, ref kept, "documento", "CONTRATO", "documento-secundario", "CÓPIA DO\nCLIENTE");

            // desenho do produto, quando a fala diz qual e
            SetSprite(book, "f-sup-1", "produto-principal", "arroz");
            SetSprite(book, "f-sup-3", "produto-principal", "arroz");
            SetSprite(book, "f-sup-3", "produto-secundario", "oleo");

            EditorUtility.SetDirty(book);
            AssetDatabase.SaveAssets();
            Debug.Log("Missao PROCON: " + filled + " caso(s) configurados; " + kept + " ja tinham ajuste e foram preservados.");
        }

        static void SetSprite(CaseVisualBook book, string caseId, string slotId, string artName)
        {
            var visual = book.For(caseId);
            if (visual == null) return;
            foreach (var fill in visual.fills)
                if (fill.slotId == slotId && fill.sprite == null)
                    fill.sprite = StageArt.LoadSprite(artName);
        }

        /// <summary>Preenche um caso. Pares de encaixe e texto. Nao mexe no que ja tem conteudo.</summary>
        static int Fill(CaseVisualBook book, string caseId, int ownerVariant, ref int kept,
                        params string[] slotAndCaption)
        {
            var visual = book.For(caseId);
            if (visual == null)
            {
                Debug.LogWarning("Caso " + caseId + " nao esta no caderno visual.");
                return 0;
            }

            if (visual.fills.Count > 0)
            {
                kept++;
                return 0;
            }

            visual.ownerVariant = ownerVariant;
            for (var i = 0; i + 1 < slotAndCaption.Length; i += 2)
                visual.fills.Add(new SlotFill { slotId = slotAndCaption[i], caption = slotAndCaption[i + 1] });

            return 1;
        }
    }
}
