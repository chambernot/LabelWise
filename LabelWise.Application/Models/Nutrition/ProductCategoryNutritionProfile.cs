namespace LabelWise.Application.Models.Nutrition
{
    /// <summary>
    /// Base de conhecimento nutricional para uma categoria de produto.
    /// </summary>
    public class ProductCategoryNutritionProfile
    {
        /// <summary>
        /// Nome da categoria (ex: "Biscoito Amanteigado").
        /// </summary>
        public string CategoryName { get; set; } = string.Empty;

        /// <summary>
        /// Palavras-chave para identificar esta categoria.
        /// </summary>
        public List<string> Keywords { get; set; } = new();

        /// <summary>
        /// Calorias médias por 100g.
        /// </summary>
        public int CaloriesPer100g { get; set; }

        /// <summary>
        /// Açúcar médio por 100g (em gramas).
        /// </summary>
        public double SugarPer100g { get; set; }

        /// <summary>
        /// Proteína média por 100g (em gramas).
        /// </summary>
        public double ProteinPer100g { get; set; }

        /// <summary>
        /// Sódio médio por 100g (em mg).
        /// </summary>
        public double SodiumPer100g { get; set; }

        /// <summary>
        /// Fibra média por 100g (em gramas).
        /// </summary>
        public double FiberPer100g { get; set; }

        /// <summary>
        /// Gordura total média por 100g (em gramas).
        /// </summary>
        public double FatPer100g { get; set; }

        /// <summary>
        /// Indica se é produto ultraprocessado.
        /// </summary>
        public bool IsUltraProcessed { get; set; }

        /// <summary>
        /// Nível típico de açúcar: "Baixo", "Moderado", "Alto".
        /// </summary>
        public string SugarLevel { get; set; } = "Moderado";

        /// <summary>
        /// Nível típico de sódio: "Baixo", "Moderado", "Alto".
        /// </summary>
        public string SodiumLevel { get; set; } = "Moderado";

        /// <summary>
        /// Densidade calórica: "Baixa", "Moderada", "Alta".
        /// </summary>
        public string CalorieDensity { get; set; } = "Moderada";

        /// <summary>
        /// Nível de proteína: "Baixo", "Moderado", "Alto".
        /// </summary>
        public string ProteinLevel { get; set; } = "Baixo";
    }
}
