namespace LabelWise.Application.DTOs.Nutrition;

public record PatientDto(
    string Id, // Geralmente o número do WhatsApp (ex: 5511988887777)
    string ProfessionalId,
    string Name,
    string WhatsAppNumber,
    // 🚀 CAMPOS CLÍNICOS E GUARDRAILS
    string MainGoal = "Manutenção Saudável",
    string MedicalRestrictions = "Nenhuma",
    string FoodAversions = "Nenhuma",
    string ClinicalProtocol = "" // 🛡️ Adicionado aqui para o MongoDB conseguir mapear o documento!
);