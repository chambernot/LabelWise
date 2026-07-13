namespace LabelWise.Application.Configuration;

/// <summary>
/// Configurações do Azure Document Intelligence (Form Recognizer).
/// </summary>
public sealed class AzureDocumentIntelligenceOptions
{
    public const string SectionName = "AzureDocumentIntelligence";

    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey   { get; set; } = string.Empty;

    /// <summary>
    /// Timeout em segundos para chamadas ao Document Intelligence (padrão: 15).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Número de tentativas em caso de falha transitória (padrão: 2).
    /// </summary>
    public int MaxRetries { get; set; } = 2;
}
