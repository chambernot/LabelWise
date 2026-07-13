using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace LabelWise.Application.QualityGate
{
    /// <summary>
    /// Avalia a qualidade do texto extraído pelo OCR.
    /// Detecta ruído, caracteres estranhos, e padrões que indicam baixa confiabilidade.
    /// </summary>
    public class OcrQualityAssessor
    {
        private static readonly char[] NoiseChars = { '~', '`', '§', '¬', '¢', '£', '¤', '¥', '¦', '©', '«', '®', '°', '±', '²', '³', 
                                                       'µ', '¶', '·', '¸', '¹', '»', '¼', '½', '¾', '×', '÷', '∂', '∆', '∏', '∑' };

        public OcrQualityMetrics AssessQuality(string extractedText, double ocrConfidence)
        {
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                return new OcrQualityMetrics
                {
                    OverallQuality = OcrQualityLevel.VeryLow,
                    HasSignificantNoise = true,
                    NoiseRatio = 1.0,
                    HasValidWords = false,
                    RecommendedConfidenceLevel = "Baixo",
                    RecommendedMessage = "Texto não extraído. Tire outra foto com melhor iluminação."
                };
            }

            var metrics = new OcrQualityMetrics
            {
                TextLength = extractedText.Length,
                OcrReportedConfidence = ocrConfidence
            };

            // 1. Análise de ruído e caracteres estranhos
            metrics.NoiseCharCount = extractedText.Count(c => NoiseChars.Contains(c));
            metrics.SpecialCharCount = extractedText.Count(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c) && c != ',' && c != '.' && c != '-' && c != '%' && c != '(' && c != ')');
            metrics.NoiseRatio = metrics.TextLength > 0 ? (double)metrics.NoiseCharCount / metrics.TextLength : 0;
            metrics.HasSignificantNoise = metrics.NoiseRatio > 0.15 || metrics.SpecialCharCount > metrics.TextLength * 0.25;

            // 2. Análise de palavras válidas
            var words = Regex.Split(extractedText, @"\s+").Where(w => w.Length > 0).ToList();
            metrics.WordCount = words.Count;
            
            // Palavra válida: pelo menos 2 caracteres, maioria letras
            var validWords = words.Where(w => w.Length >= 2 && w.Count(char.IsLetter) >= w.Length * 0.6).ToList();
            metrics.ValidWordCount = validWords.Count;
            metrics.HasValidWords = metrics.ValidWordCount > 0;
            metrics.ValidWordRatio = metrics.WordCount > 0 ? (double)metrics.ValidWordCount / metrics.WordCount : 0;

            // 3. Análise de fragmentação (muitas palavras de 1 caractere indica OCR ruim)
            var singleCharWords = words.Count(w => w.Length == 1);
            metrics.IsHighlyFragmented = metrics.WordCount > 0 && (double)singleCharWords / metrics.WordCount > 0.3;

            // 4. Análise de repetição suspeita (mesmo caractere repetido muitas vezes)
            metrics.HasSuspiciousRepetition = Regex.IsMatch(extractedText, @"(.)\1{5,}");

            // 5. Determinar qualidade geral
            metrics.OverallQuality = DetermineOverallQuality(metrics, ocrConfidence);

            // 6. Determinar recomendações
            (metrics.RecommendedConfidenceLevel, metrics.RecommendedMessage) = GetRecommendations(metrics);

            return metrics;
        }

        private OcrQualityLevel DetermineOverallQuality(OcrQualityMetrics metrics, double ocrConfidence)
        {
            // Score baseado em múltiplos fatores
            int qualityScore = 0;

            // Confiança do OCR (0-30 pontos)
            if (ocrConfidence >= 0.90) qualityScore += 30;
            else if (ocrConfidence >= 0.80) qualityScore += 25;
            else if (ocrConfidence >= 0.70) qualityScore += 20;
            else if (ocrConfidence >= 0.60) qualityScore += 15;
            else if (ocrConfidence >= 0.50) qualityScore += 10;
            else qualityScore += 5;

            // Proporção de palavras válidas (0-30 pontos)
            if (metrics.ValidWordRatio >= 0.80) qualityScore += 30;
            else if (metrics.ValidWordRatio >= 0.65) qualityScore += 25;
            else if (metrics.ValidWordRatio >= 0.50) qualityScore += 20;
            else if (metrics.ValidWordRatio >= 0.35) qualityScore += 15;
            else if (metrics.ValidWordRatio >= 0.20) qualityScore += 10;
            else qualityScore += 5;

            // Ruído (0-20 pontos)
            if (metrics.NoiseRatio <= 0.05) qualityScore += 20;
            else if (metrics.NoiseRatio <= 0.10) qualityScore += 15;
            else if (metrics.NoiseRatio <= 0.15) qualityScore += 10;
            else if (metrics.NoiseRatio <= 0.25) qualityScore += 5;

            // Penalidades
            if (metrics.IsHighlyFragmented) qualityScore -= 15;
            if (metrics.HasSuspiciousRepetition) qualityScore -= 10;
            if (!metrics.HasValidWords) qualityScore -= 20;
            if (metrics.TextLength < 20) qualityScore -= 10;

            // Classificação final (0-100)
            qualityScore = Math.Max(0, Math.Min(100, qualityScore));

            return qualityScore switch
            {
                >= 80 => OcrQualityLevel.High,
                >= 60 => OcrQualityLevel.Medium,
                >= 40 => OcrQualityLevel.Low,
                _ => OcrQualityLevel.VeryLow
            };
        }

        private (string confidenceLevel, string message) GetRecommendations(OcrQualityMetrics metrics)
        {
            return metrics.OverallQuality switch
            {
                OcrQualityLevel.High => 
                    ("Alto", "Leitura clara do rótulo."),

                OcrQualityLevel.Medium => 
                    ("Médio", "Leitura parcial do rótulo. Análise pode estar incompleta."),

                OcrQualityLevel.Low => 
                    ("Baixo", "Leitura com dificuldades. Tire outra foto com melhor iluminação e foco."),

                OcrQualityLevel.VeryLow => 
                    ("Baixo", "Não foi possível ler o rótulo adequadamente. Tire uma nova foto mais próxima, com boa iluminação e sem reflexo."),

                _ => 
                    ("Baixo", "Qualidade de leitura desconhecida.")
            };
        }
    }

    /// <summary>
    /// Métricas de qualidade do OCR
    /// </summary>
    public class OcrQualityMetrics
    {
        public int TextLength { get; set; }
        public int WordCount { get; set; }
        public int ValidWordCount { get; set; }
        public double ValidWordRatio { get; set; }
        public int NoiseCharCount { get; set; }
        public int SpecialCharCount { get; set; }
        public double NoiseRatio { get; set; }
        public bool HasSignificantNoise { get; set; }
        public bool HasValidWords { get; set; }
        public bool IsHighlyFragmented { get; set; }
        public bool HasSuspiciousRepetition { get; set; }
        public double OcrReportedConfidence { get; set; }
        public OcrQualityLevel OverallQuality { get; set; }
        public string RecommendedConfidenceLevel { get; set; } = "Baixo";
        public string RecommendedMessage { get; set; } = string.Empty;
    }

    public enum OcrQualityLevel
    {
        VeryLow,
        Low,
        Medium,
        High
    }
}
