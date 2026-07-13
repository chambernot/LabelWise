using System;

namespace LabelWise.Application.DTOs.Nutrition
{
    /// <summary>
    /// Represents the calculated nutritional score for a product.
    /// </summary>
    public class NutritionScoreDto
    {
        /// <summary>
        /// The overall nutritional score, from 0 to 100.
        /// </summary>
        public int Score { get; set; }

        /// <summary>
        /// The qualitative status of the score (e.g., "excelente", "bom", "atencao", "ruim").
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// A color code associated with the score (e.g., "green", "yellow", "orange", "red").
        /// </summary>
        public string Color { get; set; } = string.Empty;

        /// <summary>
        /// A user-friendly label for the score (e.g., "Muito saudável", "Boa escolha").
        /// </summary>
        public string Label { get; set; } = string.Empty;
    }
}
