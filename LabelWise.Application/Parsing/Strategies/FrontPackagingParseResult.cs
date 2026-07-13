using System.Collections.Generic;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Parsing.Strategies
{
    /// <summary>
    /// Resultado do parsing da frente da embalagem.
    /// </summary>
    public class FrontPackagingParseResult
    {
        public string? ProductName { get; set; }
        public string? Brand { get; set; }
        public string? SubBrand { get; set; }
        public string? Flavor { get; set; }

        // Qualidade do parsing
        public ConfidenceLevel Confidence { get; set; } = ConfidenceLevel.High;
        public List<string> ValidationWarnings { get; set; } = new();
        public bool IsProductNameValidated { get; set; }
        public bool IsBrandValidated { get; set; }
        public bool HasProductInfo => !string.IsNullOrWhiteSpace(ProductName) || !string.IsNullOrWhiteSpace(Brand);
    }
}
