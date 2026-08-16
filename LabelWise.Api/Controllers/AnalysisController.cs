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

            // 1. Converter IFormFile para DTOs de transporte em memória
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

            // 2. O Prompt Estruturado (Força a saída no formato exato da classe GeminiRawExtraction)
            var extractionPrompt = @"Você é um extrator de dados de rótulos de alimentos. Receba estas fotos do mesmo produto.
Extraia a lista de ingredientes e a tabela nutricional completa (porção, calorias, carboidratos, açúcares, proteínas, gorduras totais, gorduras saturadas, gorduras trans, fibras e sódio).
Retorne EXATAMENTE UM JSON, sem markdown (```json), sem textos extras, no seguinte formato:
{
  ""ingredients"": [""ingrediente 1"", ""ingrediente 2""],
  ""nutritionFacts"": {
     ""servingSize"": ""30g"",
     ""caloriesKcal"": 140,
     ""carbohydrates"": 19,
     ""sugars"": 5.7,
     ""proteins"": 1.3,
     ""totalFats"": 6,
     ""saturatedFats"": 1.4,
     ""transFats"": 0.2,
     ""fiber"": 2.2,
     ""sodiumMg"": 72
  }
}
Se uma informação não existir nas fotos, retorne null no campo específico.";

            // 3. Chamada para o Gemini
            var geminiResponseText = await _geminiService.AnalyzeMultipleImagesAsync(extractionPrompt, imageAttachments);

            if (string.IsNullOrWhiteSpace(geminiResponseText))
            {
                return StatusCode(500, "Falha ao se comunicar com a IA Visual.");
            }

            // 4. Limpeza e Desserialização de Dados Brutos
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

            // 5. O Motor (Cérebro) entra em ação
            // Ele pega os dados "burros" do Gemini e gera os dados "inteligentes" para o App
            var finalResponse = _rulesEngine.ProcessAndSimplify(extractedData);

            // 6. Retorna o DTO ultra-leve e processado para o aplicativo MAUI pintar a tela
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