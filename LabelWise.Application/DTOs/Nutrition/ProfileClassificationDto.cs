namespace LabelWise.Application.DTOs.Nutrition
{
    /// <summary>
    /// Classificação do produto para diferentes perfis de saúde.
    /// </summary>
    public class ProfileClassificationDto
    {
        /// <summary>
        /// Adequação para perfil diabético.
        /// </summary>
        public ProfileStatusDto Diabetic { get; set; } = new();

        /// <summary>
        /// Adequação para perfil com problema de pressão alta.
        /// </summary>
        public ProfileStatusDto BloodPressure { get; set; } = new();

        /// <summary>
        /// Adequação para perfil de perda de peso.
        /// </summary>
        public ProfileStatusDto WeightLoss { get; set; } = new();

        /// <summary>
        /// Adequação para perfil de ganho de massa muscular.
        /// </summary>
        public ProfileStatusDto MuscleGain { get; set; } = new();
    }
}
