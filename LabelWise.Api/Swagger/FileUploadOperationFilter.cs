using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Collections.Generic;
using System.Linq;

namespace LabelWise.Api.Swagger
{
    /// <summary>
    /// Filtro de operação do Swagger para suportar upload de arquivos (IFormFile).
    /// Configura corretamente a documentação para endpoints que usam multipart/form-data.
    /// </summary>
    public class FileUploadOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            // Detectar parâmetros que são IFormFile ou IFormFile[]
            var formFileParams = context.ApiDescription.ParameterDescriptions
                .Where(p => p.ModelMetadata != null && 
                           (p.ModelMetadata.ModelType == typeof(IFormFile) ||
                            p.ModelMetadata.ModelType == typeof(IFormFile[])))
                .ToList();

            if (!formFileParams.Any())
                return;

            // Obter todos os parâmetros (incluindo não-IFormFile como strings, etc.)
            var allParams = context.ApiDescription.ParameterDescriptions.ToList();

            // Limpar parâmetros existentes
            operation.Parameters?.Clear();

            // Criar schema de properties
            var properties = new Dictionary<string, OpenApiSchema>();
            var requiredFields = new HashSet<string>();

            // Processar cada parâmetro
            foreach (var param in allParams)
            {
                var paramName = param.Name;
                var isRequired = param.IsRequired;

                if (param.ModelMetadata?.ModelType == typeof(IFormFile))
                {
                    // Parâmetro IFormFile
                    properties[paramName] = new OpenApiSchema
                    {
                        Type = "string",
                        Format = "binary",
                        Description = GetFileDescription(paramName)
                    };

                    if (isRequired)
                    {
                        requiredFields.Add(paramName);
                    }
                }
                else if (param.ModelMetadata?.ModelType == typeof(IFormFile[]))
                {
                    // Array de IFormFile
                    properties[paramName] = new OpenApiSchema
                    {
                        Type = "array",
                        Items = new OpenApiSchema
                        {
                            Type = "string",
                            Format = "binary"
                        },
                        Description = GetFileDescription(paramName)
                    };

                    if (isRequired)
                    {
                        requiredFields.Add(paramName);
                    }
                }
                else if (param.ModelMetadata?.ModelType == typeof(string))
                {
                    // Parâmetro string
                    properties[paramName] = new OpenApiSchema
                    {
                        Type = "string",
                        Description = param.ModelMetadata.Description ?? $"Parameter: {paramName}"
                    };

                    if (isRequired)
                    {
                        requiredFields.Add(paramName);
                    }
                }
                else
                {
                    // Outros tipos (int, bool, etc.)
                    properties[paramName] = new OpenApiSchema
                    {
                        Type = GetSchemaType(param.ModelMetadata?.ModelType),
                        Description = param.ModelMetadata?.Description ?? $"Parameter: {paramName}"
                    };

                    if (isRequired)
                    {
                        requiredFields.Add(paramName);
                    }
                }
            }

            // Configurar RequestBody para multipart/form-data
            operation.RequestBody = new OpenApiRequestBody
            {
                Required = requiredFields.Any(),
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["multipart/form-data"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties = properties,
                            Required = requiredFields
                        }
                    }
                }
            };
        }

        private string GetFileDescription(string paramName)
        {
            return paramName.ToLowerInvariant() switch
            {
                "file" => "Arquivo de imagem do rótulo do produto (.jpg, .jpeg, .png, .webp)",
                "frontimage" => "Imagem frontal da embalagem (opcional)",
                "ingredientsimage" => "Imagem da lista de ingredientes (recomendado)",
                "nutritionimage" => "Imagem da tabela nutricional (recomendado)",
                "allergenimage" => "Imagem da declaração de alérgenos (opcional)",
                _ => $"Arquivo de imagem (.jpg, .jpeg, .png, .webp)"
            };
        }

        private string GetSchemaType(System.Type? type)
        {
            if (type == null) return "string";

            if (type == typeof(int) || type == typeof(long) || type == typeof(short))
                return "integer";

            if (type == typeof(bool))
                return "boolean";

            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
                return "number";

            return "string";
        }
    }
}
