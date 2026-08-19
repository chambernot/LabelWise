using LabelWise.Api.Models;

using LabelWise.Application.DTOs;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Services;
using LabelWise.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace LabelWise.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LabelAnalysisController : ControllerBase // Nome mais adequado que ChatController
    {
        private readonly IGeminiService _geminiService;
        private readonly INutritionRulesEngine _rulesEngine;

        public LabelAnalysisController(IGeminiService geminiService, INutritionRulesEngine rulesEngine)
        {
            _geminiService = geminiService;
            _rulesEngine = rulesEngine;
        }

        [HttpPost("analyze")]
        public async Task<IActionResult> AnalyzeProduct([FromForm] AnalyzeLabelRequestDto request)
        {
            if (request.Images == null || !request.Images.Any())
            {
                return BadRequest("Pelo menos uma imagem deve ser fornecida (ingredientes ou tabela).");
            }

            var imageAttachments = new List<ImageAttachmentDto>();
            foreach (var file in request.Images)
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                imageAttachments.Add(new ImageAttachmentDto
                {
                    Bytes = ms.ToArray(),
                    MimeType = file.ContentType
                });
            }

            // 🔥 PROMPT ATUALIZADO: Suporte a 100g/mL e Porção (Padrão ANVISA)
            var extractionPrompt = @"Você é um extrator especialista de dados de rótulos nutricionais.
Analise a(s) imagem(ns) do rótulo e extraia:
1. Nome do produto e marca (se visíveis na foto).
2. Lista de ingredientes.
3. Avisos explícitos de alergênicos e glúten (ex: 'NÃO CONTÉM GLÚTEN', 'ALÉRGICOS: CONTÉM...').
4. Tabela nutricional (colunas de 100g/mL e Porção).

Retorne EXATAMENTE UM JSON, sem marcação markdown (sem ```json), sem textos extras, no seguinte formato:
{
  ""productName"": ""Suco de Uva e Maçã"",
  ""brand"": ""Marca X"",
  ""ingredients"": [""ÁGUA"", ""AÇÚCAR""],
  ""allergenWarnings"": [""NÃO CONTÉM GLÚTEN""],
  ""nutritionFacts"": {
     ""servingSize"": ""200 ml (1 unidade)"",
     ""servingsPerPackage"": null,
     ""per100g"": {
        ""caloriesKcal"": 35,
        ""carbohydrates"": 8.7,
        ""sugars"": 8.7,
        ""addedSugars"": 7.2,
        ""proteins"": 0,
        ""totalFats"": 0,
        ""saturatedFats"": 0,
        ""transFats"": 0,
        ""fiber"": 0,
        ""sodiumMg"": 4.1
     },
     ""perServing"": {
        ""caloriesKcal"": 71,
        ""carbohydrates"": 17,
        ""sugars"": 17,
        ""addedSugars"": 14,
        ""proteins"": 0,
        ""totalFats"": 0,
        ""saturatedFats"": 0,
        ""transFats"": 0,
        ""fiber"": 0,
        ""sodiumMg"": 8.3
     }
  }
}
Se uma informação não existir na imagem, retorne null no respectivo campo.";
            var geminiResponseText = await _geminiService.AnalyzeMultipleImagesAsync(extractionPrompt, imageAttachments);

            if (string.IsNullOrWhiteSpace(geminiResponseText))
            {
                return StatusCode(500, "Falha ao se comunicar com a IA Visual.");
            }

            var cleanJson = CleanJsonString(geminiResponseText);
            GeminiRawExtraction? extractedData;

            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                extractedData = JsonSerializer.Deserialize<GeminiRawExtraction>(cleanJson, options);
            }
            catch (JsonException ex)
            {
                return StatusCode(500, $"Falha ao interpretar JSON da IA. Erro: {ex.Message}. JSON: {cleanJson}");
            }

            if (extractedData == null)
            {
                return BadRequest("Não foi possível extrair dados estruturados das imagens fornecidas.");
            }

            // O Motor de regras recebe o objeto completo com per100g e perServing
            var finalResponse = _rulesEngine.ProcessAndSimplify(extractedData);

            return Ok(finalResponse);
        }

        private static string CleanJsonString(string text)
        {
            var result = text.Trim();
            if (result.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) result = result.Substring(7);
            else if (result.StartsWith("```")) result = result.Substring(3);
            if (result.EndsWith("```")) result = result.Substring(0, result.Length - 3);
            return result.Trim();
        }
    }

    public class AnalyzeLabelRequestDto
    {
        // Esta é a propriedade que vai receber as fotos do App MAUI
        public List<IFormFile> Images { get; set; } = new();
    }
}