namespace LabelWise.Application.DTOs.Nutrition
{
    /// <summary>
    /// Detalhamento de confiança por seção da análise nutricional.
    /// </summary>
    public class NutritionConfidenceDetailsDto
    {
        /// <summary>
        /// Confiança na identificação do produto (nome, marca, categoria, peso).
        /// Valor entre 0.0 e 1.0.
        /// </summary>
        public double ProductIdentification { get; set; }

        /// <summary>
        /// Confiança na extração dos claims visíveis da embalagem.
        /// Valor entre 0.0 e 1.0.
        /// </summary>
        public double VisibleClaimsExtraction { get; set; }

        /// <summary>
        /// Confiança no perfil nutricional estimado.
        /// Valor entre 0.0 e 1.0.
        /// Geralmente baixo quando baseado apenas em estimativas de categoria.
        /// </summary>
        public double EstimatedNutritionProfile { get; set; }

        /// <summary>
        /// Confiança na classificação para perfis de saúde.
        /// Valor entre 0.0 e 1.0.
        /// </summary>
        public double Classification { get; set; }
    }
}
