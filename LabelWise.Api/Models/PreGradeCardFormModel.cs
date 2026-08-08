using Microsoft.AspNetCore.Http;

namespace LabelWise.Api.Models
{
    /// <summary>
    /// Model usado para endpoints que recebem arquivos via multipart/form-data
    /// e outros campos relacionados.
    /// </summary>
    public class PreGradeCardFormModel
    {
        public decimal CurrentRawValue { get; set; }
        public IFormFile? FrontImage { get; set; }
        public IFormFile? BackImage { get; set; }
    }
}
