namespace LabelWise.Application.DTOs.Nutrition
{
    /// <summary>
    /// Dados nutricionais estimados do produto.
    /// </summary>
    public class EstimatedNutritionDto
    {
        /// <summary>
        /// Calorias estimadas por 100g do produto.
        /// </summary>
        public int CaloriesPer100g { get; set; }

        /// <summary>
        /// Calorias totais estimadas da embalagem (se peso identificado).
        /// </summary>
        public int? EstimatedPackageCalories { get; set; }

        /// <summary>
        /// Açúcar estimado por 100g (em gramas).
        /// </summary>
        public double? EstimatedSugarPer100g { get; set; }

        /// <summary>
        /// Proteína estimada por 100g (em gramas).
        /// </summary>
        public double? EstimatedProteinPer100g { get; set; }

        /// <summary>
        /// Sódio estimado por 100g (em mg).
        /// </summary>
        public double? EstimatedSodiumPer100g { get; set; }

        /// <summary>
        /// Fibra estimada por 100g (em gramas).
        /// </summary>
        public double? EstimatedFiberPer100g { get; set; }

        /// <summary>
        /// Gordura total estimada por 100g (em gramas).
        /// </summary>
        public double? EstimatedFatPer100g { get; set; }
    }
}
