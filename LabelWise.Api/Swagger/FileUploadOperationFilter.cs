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
            // Detectar parâmetros que são IFormFile ou IFormFile[] (diretos)
            var directFormFileParams = context.ApiDescription.ParameterDescriptions
                .Where(p => p.ModelMetadata != null && 
                           (p.ModelMetadata.ModelType == typeof(IFormFile) ||
                            p.ModelMetadata.ModelType == typeof(IFormFile[])))
                .ToList();

            // Detectar propriedades IFormFile em modelos complexos
            var complexWithFiles = context.ApiDescription.ParameterDescriptions
                .Where(p => p.ModelMetadata != null && p.ModelMetadata.Properties != null && p.ModelMetadata.Properties.Any(prop => prop.ModelType == typeof(IFormFile) || prop.ModelType == typeof(IFormFile[])))
                .ToList();

            if (!directFormFileParams.Any() && !complexWithFiles.Any())
                return;

            // Obter todos os parâmetros (incluindo não-IFormFile como strings, etc.)
            var allParams = context.ApiDescription.ParameterDescriptions.ToList();

            // Limpar parâmetros existentes
            operation.Parameters?.Clear();

            // Criar schema de properties
            var properties = new Dictionary<string, OpenApiSchema>();
            var requiredFields = new HashSet<string>();

            // Processar parâmetros diretos (ex.: method(IFormFile file))
            foreach (var param in directFormFileParams)
            {
                var paramName = param.Name;
                var isRequired = param.IsRequired;

                if (param.ModelMetadata.ModelType == typeof(IFormFile))
                {
                    properties[paramName] = new OpenApiSchema
                    {
                        Type = "string",
                        Format = "binary",
                        Description = GetFileDescription(paramName)
                    };

                    if (isRequired)
                        requiredFields.Add(paramName);
                }
                else if (param.ModelMetadata.ModelType == typeof(IFormFile[]))
                {
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
                        requiredFields.Add(paramName);
                }
            }

            // Processar parâmetros complexos: expor propriedades do modelo
            foreach (var param in complexWithFiles)
            {
                // Para cada propriedade do modelo
                foreach (var prop in param.ModelMetadata.Properties)
                {
                    var propName = prop.BinderModelName ?? prop.PropertyName ?? prop.Name ?? prop.UnderlyingOrModelType?.Name ?? "unknown";

                    var isRequired = prop.IsRequired;

                    if (prop.ModelType == typeof(IFormFile))
                    {
                        properties[propName] = new OpenApiSchema
                        {
                            Type = "string",
                            Format = "binary",
                            Description = GetFileDescription(propName)
                        };

                        if (isRequired)
                            requiredFields.Add(propName);
                    }
                    else if (prop.ModelType == typeof(IFormFile[]))
                    {
                        properties[propName] = new OpenApiSchema
                        {
                            Type = "array",
                            Items = new OpenApiSchema
                            {
                                Type = "string",
                                Format = "binary"
                            },
                            Description = GetFileDescription(propName)
                        };

                        if (isRequired)
                            requiredFields.Add(propName);
                    }
                    else
                    {
                        // Adicionar também outras propriedades simples do modelo para documentação
                        properties[propName] = new OpenApiSchema
                        {
                            Type = GetSchemaType(prop.ModelType),
                            Description = prop.Description ?? $"Parameter: {propName}"
                        };

                        if (isRequired)
                            requiredFields.Add(propName);
                    }
                }
            }

            // Para suportar também parâmetros simples que não fazem parte dos complexos
            // (ex.: string cardName via [FromForm])
            var simpleParams = allParams
                .Where(p => p.ModelMetadata != null && (p.ModelMetadata.ModelType != typeof(IFormFile) && p.ModelMetadata.ModelType != typeof(IFormFile[]) && (p.ModelMetadata.Properties == null || !p.ModelMetadata.Properties.Any())))
                .ToList();

            foreach (var param in simpleParams)
            {
                // Se a propriedade já foi adicionada acima (por modelo), pular
                if (properties.ContainsKey(param.Name))
                    continue;

                var paramName = param.Name;
                var isRequired = param.IsRequired;

                properties[paramName] = new OpenApiSchema
                {
                    Type = GetSchemaType(param.ModelMetadata?.ModelType),
                    Description = param.ModelMetadata?.Description ?? $"Parameter: {paramName}"
                };

                if (isRequired)
                    requiredFields.Add(paramName);
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
