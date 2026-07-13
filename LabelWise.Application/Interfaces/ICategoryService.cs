namespace LabelWise.Application.Interfaces;

/// <summary>
/// Determina ou corrige a categoria do produto com base em sinais textuais do nome.
/// Regras genéricas — não depende de produto ou marca específica.
/// </summary>
public interface ICategoryService
{
    /// <summary>
    /// Retorna a categoria normalizada. Nunca retorna null.
    /// </summary>
    string Fix(string? productName);
}
