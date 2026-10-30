namespace LabelWise.Application.DTOs.Nutrition
{
    public record ParseMealRequestDto(
        string UserId,
        string? TextInput,
        string? Base64Image,
        string? AudioUrl,
        DateTime LocalTime,
        string? ClinicalProtocol = null,
        int? TargetCalories = 0,
        string? DietaryRestrictions = null,

        // Modo GLP-1 ativo: contexto do tratamento para a IA (medicamento, dia, metas, consumo e sintomas; nunca a dose).
        // Preenchido = a IA também recebe as regras de segurança do GLP-1. Nulo para todos os outros usuários.
        string? Glp1Context = null




    );
}