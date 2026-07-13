using LabelWise.Domain.Enums;

namespace LabelWise.Application.SummaryGeneration
{
    /// <summary>
    /// Resultado da geração de resumo com informações de confiança.
    /// </summary>
    public class SummaryGenerationResult
    {
        /// <summary>
        /// Resumo completo formatado.
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// Resumo curto (uma frase).
        /// </summary>
        public string ShortSummary { get; set; } = string.Empty;

        /// <summary>
        /// Classificação determinada.
        /// </summary>
        public AnalysisClassification Classification { get; set; }

        /// <summary>
        /// Classificação como string para compatibilidade.
        /// </summary>
        public string ClassificationString { get; set; } = string.Empty;

        /// <summary>
        /// Indica se a classificação foi ajustada devido à confiança.
        /// </summary>
        public bool ClassificationAdjusted { get; set; }

        /// <summary>
        /// Classificação original antes do ajuste.
        /// </summary>
        public AnalysisClassification? OriginalClassification { get; set; }

        /// <summary>
        /// Motivo do ajuste da classificação.
        /// </summary>
        public string AdjustmentReason { get; set; } = string.Empty;

        /// <summary>
        /// Lista de disclaimers ou avisos sobre a análise.
        /// </summary>
        public List<string> Disclaimers { get; set; } = [];

        /// <summary>
        /// Indica se a análise é parcial.
        /// </summary>
        public bool IsPartialAnalysis { get; set; }

        /// <summary>
        /// Nível de confiança na análise.
        /// </summary>
        public string ConfidenceLevel { get; set; } = string.Empty;
    }

    /// <summary>
    /// Regras para ajuste de resumo baseado em completude e confiança.
    /// Implementa regras explícitas para mensagens seguras em cenários parciais.
    /// </summary>
    public static class SummaryAdjustmentRules
    {
        // ═══════════════════════════════════════════════════════════════════
        // FRASES PROIBIDAS EM ANÁLISES PARCIAIS
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Frases que NÃO devem ser usadas em análises parciais.
        /// </summary>
        public static readonly HashSet<string> ProhibitedPhrasesPartialAnalysis = new(StringComparer.OrdinalIgnoreCase)
        {
            "Boa Escolha",
            "Excelente Escolha",
            "Pode consumir regularmente",
            "Produto adequado para consumo regular",
            "Produto saudável",
            "Recomendado",
            "Seguro para consumo",
            "Sem preocupações",
            "Totalmente seguro"
        };

        /// <summary>
        /// Classificações otimistas que NÃO devem ser usadas em análises parciais.
        /// </summary>
        public static readonly HashSet<AnalysisClassification> ProhibitedClassificationsPartialAnalysis =
        [
            AnalysisClassification.Safe,
            AnalysisClassification.Excellent
        ];

        // ═══════════════════════════════════════════════════════════════════
        // FRASES RECOMENDADAS PARA ANÁLISES PARCIAIS
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Frases para usar quando o OCR está incompleto.
        /// </summary>
        public static readonly string[] IncompleteOcrPhrases =
        [
            "Leitura parcial do rótulo",
            "Texto não foi totalmente extraído",
            "Envie outra imagem para maior precisão",
            "Qualidade da imagem pode ter afetado a leitura"
        ];

        /// <summary>
        /// Frases para usar quando a análise está parcial.
        /// </summary>
        public static readonly string[] PartialAnalysisPhrases =
        [
            "Análise parcial do rótulo",
            "Informações incompletas",
            "Não foi possível analisar todos os dados",
            "Alguns dados podem estar faltando"
        ];

        /// <summary>
        /// Frases para usar quando o produto não foi identificado.
        /// </summary>
        public static readonly string[] UnidentifiedProductPhrases =
        [
            "Produto não identificado com segurança",
            "Não foi possível confirmar o produto",
            "Identificação do produto incerta"
        ];

        /// <summary>
        /// Frases para usar quando há alérgenos declarados.
        /// </summary>
        public static readonly string[] AllergenWarningPhrases =
        [
            "Contém alérgenos declarados",
            "Verifique os alérgenos antes de consumir",
            "Atenção: produto contém substâncias alergênicas"
        ];

        // ═══════════════════════════════════════════════════════════════════
        // MÉTODOS DE AJUSTE
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Ajusta a classificação baseada no contexto de análise.
        /// </summary>
        public static AnalysisClassification AdjustClassification(
            AnalysisClassification originalClassification,
            AnalysisContext context,
            out string adjustmentReason)
        {
            adjustmentReason = string.Empty;

            // Regra 1: Produto não identificado → Caution ou Incomplete
            if (!context.ProductIdentified)
            {
                adjustmentReason = "Produto não foi identificado com segurança";
                return AnalysisClassification.Incomplete;
            }

            // Regra 2: Análise parcial → não permitir Safe ou Excellent
            if (context.IsPartialAnalysis)
            {
                if (ProhibitedClassificationsPartialAnalysis.Contains(originalClassification))
                {
                    adjustmentReason = "Análise parcial não permite classificação otimista";
                    return originalClassification == AnalysisClassification.Excellent
                        ? AnalysisClassification.Caution
                        : AnalysisClassification.Caution;
                }
            }

            // Regra 3: Alérgenos declarados → evitar Safe por padrão
            if (context.HasDeclaredAllergens && originalClassification == AnalysisClassification.Safe)
            {
                adjustmentReason = "Presença de alérgenos declarados requer atenção";
                return AnalysisClassification.Caution;
            }

            // Regra 4: Confiança baixa → não permitir classificação Safe
            if (context.OverallConfidenceLevel < Confidence.ConfidenceLevel.Medium &&
                originalClassification == AnalysisClassification.Safe)
            {
                adjustmentReason = "Confiança insuficiente para classificação segura";
                return AnalysisClassification.Caution;
            }

            return originalClassification;
        }

        /// <summary>
        /// Valida se um resumo contém frases proibidas para análises parciais.
        /// </summary>
        public static bool ContainsProhibitedPhrases(string summary)
        {
            if (string.IsNullOrEmpty(summary)) return false;

            foreach (var phrase in ProhibitedPhrasesPartialAnalysis)
            {
                if (summary.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Obtém uma frase apropriada para o contexto de análise.
        /// </summary>
        public static string GetAppropriatePhrase(AnalysisContext context)
        {
            if (!context.ProductIdentified)
                return UnidentifiedProductPhrases[0];

            if (!context.OcrComplete)
                return IncompleteOcrPhrases[0];

            if (!context.AnalysisComplete)
                return PartialAnalysisPhrases[0];

            if (context.HasDeclaredAllergens)
                return AllergenWarningPhrases[0];

            return string.Empty;
        }

        /// <summary>
        /// Obtém disclaimers apropriados para o contexto.
        /// </summary>
        public static List<string> GetDisclaimers(AnalysisContext context)
        {
            var disclaimers = new List<string>();

            if (!context.ProductIdentified)
                disclaimers.Add("⚠️ " + UnidentifiedProductPhrases[0]);

            if (!context.OcrComplete)
                disclaimers.Add("📷 " + IncompleteOcrPhrases[2]); // "Envie outra imagem..."

            if (context.IsPartialAnalysis)
                disclaimers.Add("📋 " + PartialAnalysisPhrases[0]);

            if (context.HasDeclaredAllergens)
                disclaimers.Add("🔴 " + AllergenWarningPhrases[1]); // "Verifique os alérgenos..."

            return disclaimers;
        }

        /// <summary>
        /// Determina o nível de confiança como string para exibição.
        /// </summary>
        public static string GetConfidenceLevelDisplay(AnalysisContext context)
        {
            if (!context.ProductIdentified || !context.OcrComplete)
                return "Baixo";

            return context.OverallConfidenceLevel switch
            {
                Confidence.ConfidenceLevel.High => "Alto",
                Confidence.ConfidenceLevel.Medium => "Médio",
                Confidence.ConfidenceLevel.Low => "Baixo",
                Confidence.ConfidenceLevel.VeryLow => "Muito Baixo",
                _ => "Desconhecido"
            };
        }
    }
}
