using System.ComponentModel.DataAnnotations;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.DTOs.Analysis
{
    /// <summary>
    /// Request para análise de imagem capturada com suporte a diferentes tipos de captura.
    /// </summary>
    public class CapturedImageAnalysisRequest : IValidatableObject
    {
        /// <summary>
        /// Tipo de captura da imagem.
        /// Define como a imagem será processada (código de barras, tabela nutricional, ingredientes, etc.).
        /// </summary>
        /// <example>NutritionTable</example>
        [Required(ErrorMessage = "CaptureType é obrigatório.")]
        public CaptureType CaptureType { get; set; }

        /// <summary>
        /// Código de barras do produto.
        /// Obrigatório quando CaptureType = Barcode.
        /// Opcional para outros tipos de captura (pode ajudar na identificação do produto).
        /// </summary>
        /// <example>7891234567890</example>
        public string? Barcode { get; set; }

        /// <summary>
        /// Idioma preferido para OCR e análise (ISO 639-1).
        /// </summary>
        /// <example>pt</example>
        public string LanguageCode { get; set; } = "pt";

        /// <summary>
        /// Indica se deve buscar informações em bases externas (Open Food Facts, etc.).
        /// </summary>
        public bool EnableExternalDatabaseLookup { get; set; } = true;

        /// <summary>
        /// Indica se deve usar múltiplos provedores de OCR (estratégia de fallback).
        /// </summary>
        public bool EnableMultiProviderOcr { get; set; } = true;

        /// <summary>
        /// Threshold mínimo de confiança do OCR (0.0 a 1.0).
        /// </summary>
        [Range(0.0, 1.0, ErrorMessage = "OcrConfidenceThreshold deve estar entre 0.0 e 1.0.")]
        public double OcrConfidenceThreshold { get; set; } = 0.85;

        /// <summary>
        /// Indica se deve executar análise nutricional completa após leitura bem-sucedida.
        /// </summary>
        public bool ExecuteNutritionalAnalysis { get; set; } = true;

        /// <summary>
        /// Indica se deve executar análise de qualidade do processo (quality gate).
        /// </summary>
        public bool EnableQualityGate { get; set; } = true;

        /// <summary>
        /// Threshold mínimo para considerar o pipeline bem-sucedido (0.0 a 1.0).
        /// </summary>
        [Range(0.0, 1.0, ErrorMessage = "MinimumConfidenceThreshold deve estar entre 0.0 e 1.0.")]
        public double MinimumConfidenceThreshold { get; set; } = 0.70;

        /// <summary>
        /// Indica se deve salvar o histórico de análise.
        /// </summary>
        public bool SaveAnalysisHistory { get; set; } = true;

        /// <summary>
        /// Validações customizadas do request.
        /// </summary>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // Barcode obrigatório quando CaptureType = Barcode
            if (CaptureType == CaptureType.Barcode && string.IsNullOrWhiteSpace(Barcode))
            {
                yield return new ValidationResult(
                    "Barcode é obrigatório quando CaptureType = Barcode.",
                    new[] { nameof(Barcode) });
            }

            // Validar formato do barcode se fornecido
            if (!string.IsNullOrWhiteSpace(Barcode))
            {
                var cleanBarcode = Barcode.Trim().Replace(" ", "").Replace("-", "");
                
                // Aceita EAN-8, EAN-13, UPC-A (12 dígitos) ou UPC-E (8 dígitos)
                if (!System.Text.RegularExpressions.Regex.IsMatch(cleanBarcode, @"^\d{8}$|^\d{12}$|^\d{13}$"))
                {
                    yield return new ValidationResult(
                        "Barcode inválido. Formatos aceitos: EAN-8 (8 dígitos), EAN-13 (13 dígitos), UPC-A (12 dígitos).",
                        new[] { nameof(Barcode) });
                }
            }

            // Validar CaptureType é um valor válido do enum
            if (!Enum.IsDefined(typeof(CaptureType), CaptureType))
            {
                yield return new ValidationResult(
                    $"CaptureType inválido. Valores aceitos: {string.Join(", ", Enum.GetNames(typeof(CaptureType)))}",
                    new[] { nameof(CaptureType) });
            }

            // Validar LanguageCode
            var validLanguages = new[] { "pt", "en", "es", "fr", "de", "it" };
            if (!validLanguages.Contains(LanguageCode.ToLowerInvariant()))
            {
                yield return new ValidationResult(
                    $"LanguageCode inválido. Valores aceitos: {string.Join(", ", validLanguages)}",
                    new[] { nameof(LanguageCode) });
            }
        }
    }
}
