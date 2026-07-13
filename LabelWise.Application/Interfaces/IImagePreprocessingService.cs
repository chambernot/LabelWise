namespace LabelWise.Application.Interfaces;

/// <summary>
/// Pré-processa imagens para melhorar a qualidade de leitura do OCR.
/// Deve ser aplicado antes de qualquer chamada ao Document Intelligence ou IA Vision.
/// </summary>
public interface IImagePreprocessingService
{
    /// <summary>
    /// Aplica apenas correções seguras e não-destrutivas, priorizando a preservação
    /// integral da imagem original para OCR.
    /// Retorna os bytes da imagem processada ou os bytes originais se o
    /// processamento falhar — nunca lança exceção.
    /// </summary>
    byte[] EnhanceForOcr(byte[] imageBytes);
}
