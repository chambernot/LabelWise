using System;

namespace LabelWise.Application.DTOs.Nutrition // Ajuste o namespace conforme o seu projeto
{
    public record PatientDto(
        string Id, // Geralmente o número do WhatsApp (ex: 5511988887777)
        string ProfessionalId,
        string Name,
        string WhatsAppNumber,

        // 🚀 CAMPOS CLÍNICOS E GUARDRAILS
        string MainGoal = "Manutenção Saudável",
        string MedicalRestrictions = "Nenhuma",
        string FoodAversions = "Nenhuma",
        string ClinicalProtocol = "", // 🛡️ Protocolo estrito do nutricionista

        // 📊 NOVOS CAMPOS PARA O RADAR CLÍNICO E DASHBOARD
        string Status = "Em dia",              // "Em dia", "Atenção", "Inativo"
        int AdhesionPercentage = 100,          // Percentagem de adesão calculada
        DateTime? LastInteractionDate = null,  // Data da última refeição ou interação
        bool IsArchived = false                // Suporte para arquivar paciente (SaaS)
    );
}