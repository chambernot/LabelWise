using System.ComponentModel.DataAnnotations;

namespace LabelWise.Application.Configuration
{
    /// <summary>
    /// Configuration options for OpenAI Vision API (supports both Azure and regular OpenAI).
    /// </summary>
    public class AzureOpenAiVisionOptions
    {
        public const string SectionName = "OpenAiVision";

        [Required]
        public string Endpoint { get; set; } = string.Empty;

        [Required]
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Model name for regular OpenAI (e.g., gpt-4o)
        /// </summary>
        public string Model { get; set; } = "gpt-4o";

        /// <summary>
        /// Deployment name for Azure OpenAI (backwards compatibility)
        /// </summary>
        public string VisionDeployment 
        { 
            get => Model; 
            set => Model = value; 
        }

        /// <summary>
        /// Path para salvar imagens para debug (comparação com Playground)
        /// </summary>
        public string? DebugImagePath { get; set; }
    }
}
