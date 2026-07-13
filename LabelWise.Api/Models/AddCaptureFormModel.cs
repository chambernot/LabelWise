using System.ComponentModel.DataAnnotations;
using LabelWise.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace LabelWise.Api.Models
{
    /// <summary>
    /// Modelo para upload de captura via form-data.
    /// </summary>
    public class AddCaptureFormModel
    {
        /// <summary>
        /// Arquivo de imagem (obrigatório exceto para Barcode).
        /// Formatos aceitos: .jpg, .jpeg, .png, .webp
        /// Tamanho máximo: 10MB
        /// </summary>
        public IFormFile? File { get; set; }

        /// <summary>
        /// Tipo de captura (obrigatório).
        /// 1=Barcode, 2=FrontPackaging, 3=NutritionTable, 4=IngredientsList, 5=AllergenStatement
        /// </summary>
        [Required(ErrorMessage = "CaptureType é obrigatório")]
        public CaptureType CaptureType { get; set; }

        /// <summary>
        /// Código de barras (obrigatório apenas para CaptureType=1).
        /// Formatos: EAN-8, EAN-13, UPC-A.
        /// </summary>
        public string? Barcode { get; set; }

        /// <summary>
        /// Idioma para OCR. Padrão: pt
        /// </summary>
        public string LanguageCode { get; set; } = "pt";
    }
}
