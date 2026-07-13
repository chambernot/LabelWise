using System.Threading.Tasks;
using LabelWise.Application.DTOs.Orchestration;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Orchestration
{
    /// <summary>
    /// Estratégia abstrata para processar cada tipo de captura.
    /// Cada CaptureType tem um comportamento específico.
    /// </summary>
    public abstract class CaptureTypeStrategy
    {
        /// <summary>
        /// Tipo de captura que esta estratégia processa.
        /// </summary>
        public abstract CaptureType CaptureType { get; }

        /// <summary>
        /// Nome descritivo da estratégia para logs.
        /// </summary>
        public abstract string StrategyName { get; }

        /// <summary>
        /// Determina se esta captura requer OCR.
        /// Barcode geralmente não precisa de OCR completo.
        /// </summary>
        public virtual bool RequiresOcr => true;

        /// <summary>
        /// Determina se esta captura pode identificar o produto.
        /// </summary>
        public virtual bool CanIdentifyProduct => false;

        /// <summary>
        /// Determina se esta captura contém dados nutricionais.
        /// </summary>
        public virtual bool HasNutritionalData => false;

        /// <summary>
        /// Determina se esta captura contém ingredientes.
        /// </summary>
        public virtual bool HasIngredients => false;

        /// <summary>
        /// Determina se esta captura contém alergênicos.
        /// </summary>
        public virtual bool HasAllergens => false;

        /// <summary>
        /// Executa o processamento específico para este tipo de captura.
        /// </summary>
        public abstract Task<CaptureProcessingResult> ProcessAsync(
            byte[] imageData,
            string? fileName,
            CaptureProcessingContext context);
    }
}
