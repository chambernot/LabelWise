using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.DTOs.AI
{
    /// <summary>
    /// Represents the result of a visual interpretation of an image.
    /// </summary>
    public class VisualInterpretationResult
    {
        /// <summary>
        /// The most likely type of capture based on the visual content.
        /// </summary>
        public CaptureType ProbableCaptureType { get; set; } = CaptureType.FrontPackaging;

        /// <summary>
        /// The identified probable product name.
        /// </summary>
        public string? ProbableProductName { get; set; }

        /// <summary>
        /// The identified probable brand name.
        /// </summary>
        public string? ProbableBrand { get; set; }

        /// <summary>
        /// A suggested category for the product.
        /// </summary>
        public string? ProbableCategory { get; set; }

        /// <summary>
        /// The probable package weight or volume (e.g., "560 g", "200 ml").
        /// </summary>
        public string? ProbablePackageWeight { get; set; }

        /// <summary>
        /// Claims or statements visible on the package (e.g., "Não contém glúten", "Fonte de vitaminas").
        /// </summary>
        public List<string> VisibleClaims { get; set; } = new();

        public string? ProductName { get; set; }

        public string? Brand { get; set; }

        public string? Category { get; set; }

        public string? PackageWeight { get; set; }

        public EstimatedNutritionProfileDto? EstimatedNutritionProfile { get; set; }

        public ProductClassificationDto? Classification { get; set; }

        public ConfidenceDetailsDto? ConfidenceDetails { get; set; }

        public string? Summary { get; set; }

        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Linhas brutas da tabela nutricional exatamente como extraídas da imagem.
        /// Campo opcional para reconstrução de tabela no pipeline.
        /// </summary>
        public List<string>? RawExtractedText { get; set; }

        public string? ErrorMessage { get; set; }

        /// <summary>
        /// A summary of the interpretation of the image content.
        /// </summary>
        public string? InterpretationSummary { get; set; }

        /// <summary>
        /// The confidence level of the interpretation.
        /// </summary>
        public ConfidenceLevel InterpretationConfidence { get; set; } = ConfidenceLevel.Low;
    }
}
