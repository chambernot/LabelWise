using LabelWise.Api.Dtos;
using LabelWise.Api.Models;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace LabelWise.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EvaluationController : ControllerBase
{
    private readonly ConditionEvaluationService _evaluationService;
    private readonly PreGradingService _preGradingService;
    private readonly IPokemonPriceService _priceService;

    public EvaluationController(
        ConditionEvaluationService evaluationService,
        PreGradingService preGradingService,
        IPokemonPriceService priceService)
    {
        _evaluationService = evaluationService;
        _preGradingService = preGradingService;
        _priceService = priceService;
    }

    [HttpPost("pre-grade")]
    public async Task<IActionResult> PreGradeCard([FromForm] PreGradeRequestDto request)
    {
        if (request.FrontStraight == null || request.FrontAngled == null ||
            request.BackStraight == null || request.BackAngled == null)
        {
            return BadRequest("Todas as 4 fotos (Retas e Inclinadas com luz) são obrigatórias.");
        }

        try
        {
            using var frontStraightStream = request.FrontStraight.OpenReadStream();
            using var frontAngledStream = request.FrontAngled.OpenReadStream();
            using var backStraightStream = request.BackStraight.OpenReadStream();
            using var backAngledStream = request.BackAngled.OpenReadStream();

            var result = await _preGradingService.SimulateGradingAsync(
                request.Id,
                request.CurrentRawValue,
                frontStraightStream,
                frontAngledStream,
                backStraightStream,
                backAngledStream);

            // Se o valor não veio preenchido no DTO (ou veio 0), busca automaticamente na API de preços
            if (result != null && (result.CurrentRawValue == 0 || request.CurrentRawValue == 0))
            {
                var cardNumber = ExtractCardNumber(result.CardName);
                var marketPrice = await _priceService.GetCardMarketPriceAsync(result.CardName, cardNumber);

                if (marketPrice > 0)
                {
                    result.UpdateCurrentRawValue(marketPrice);
                }
            }

            return Ok(new
            {
                Message = "Simulação de Gradação concluída com sucesso.",
                Data = result
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    [HttpPost("condition")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> EvaluateCondition(
        [FromForm] string cardName,
        IFormFile frontImage,
        IFormFile backImage)
    {
        if (frontImage == null || backImage == null)
            return BadRequest("As fotos da frente e do verso são obrigatórias.");

        try
        {
            var userId = Guid.NewGuid();

            using var frontStream = frontImage.OpenReadStream();
            using var backStream = backImage.OpenReadStream();

            var result = await _evaluationService.EvaluateConditionAsync(
                userId,
                cardName,
                frontStream,
                backStream);

            return Ok(new
            {
                Message = "Avaliação concluída com sucesso.",
                Data = result
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    private static string ExtractCardNumber(string cardName)
    {
        if (string.IsNullOrWhiteSpace(cardName) || !cardName.Contains('-'))
            return string.Empty;

        var parts = cardName.Split('-');
        return parts.Length > 1 ? parts[1].Trim() : string.Empty;
    }
}