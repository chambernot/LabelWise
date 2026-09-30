namespace LabelWise.Application.DTOs.Nutrition;

public record PatientDto(
    string Id, // Geralmente o número do WhatsApp (ex: 5511988887777)
    string ProfessionalId,
    string Name,
    string WhatsAppNumber,
    // 🚀 NOVOS CAMPOS CLÍNICOS (Com valores padrão para não quebrar instâncias existentes)
    string MainGoal = "Manutenção Saudável",
    string MedicalRestrictions = "Nenhuma",
    string FoodAversions = "Nenhuma"
);