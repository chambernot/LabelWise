namespace LabelWise.Application.DTOs.Nutrition
{
    /// <summary>
    /// Status de adequação para um perfil de saúde específico.
    /// </summary>
    public class ProfileStatusDto
    {
        /// <summary>
        /// Status de adequação do produto para este perfil.
        /// Valores: "nao_recomendado", "consumo_moderado", "mais_adequado", "bom", "moderado", "fraco", "nao_indicado"
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Explicação do status.
        /// </summary>
        public string Reason { get; set; } = string.Empty;
    }
}
