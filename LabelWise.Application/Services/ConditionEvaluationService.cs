using LabelWise.Application.Interfaces;
using LabelWise.Domain.Entities;

namespace LabelWise.Application.Services;

public class ConditionEvaluationService
{
    private readonly IVisionAnalysisService _visionService;
    private readonly ICardMarketPricingService _pricingService;
    // Em um cenário real, você injetaria seus repositórios aqui (ex: IUserRepository, ICardEvaluationRepository)

    public ConditionEvaluationService(
        IVisionAnalysisService visionService,
        ICardMarketPricingService pricingService)
    {
        _visionService = visionService;
        _pricingService = pricingService;
    }

    public async Task<CardEvaluation> EvaluateConditionAsync(Guid userId, string cardName, Stream frontImage, Stream backImage)
    {
        // 1. Simulação: Buscar carteira do usuário no banco de dados
        var wallet = new UserWallet(userId, 10); // Mockando 10 créditos

        int evaluationCost = 1;
        if (!wallet.HasEnoughCredits(evaluationCost))
            throw new Exception("Saldo insuficiente. Adquira mais créditos na loja.");

        // 2. Chamar a IA de Visão Computacional para analisar as fotos
        var visionResult = await _visionService.AnalyzeCardConditionAsync(frontImage, backImage);

        // 3. Buscar preço de mercado baseado no nome e condição detectada
        decimal estimatedPrice = 0;
        if (visionResult.IsAuthentic)
        {
            estimatedPrice = await _pricingService.GetEstimatedPriceAsync(cardName, visionResult.Condition);
        }

        // 4. Deduzir crédito
        wallet.DeductCredits(evaluationCost);

        // 5. Criar a entidade de domínio com o laudo final
        var evaluation = new CardEvaluation(
            userId: userId,
            cardName: cardName,
            isAuthentic: visionResult.IsAuthentic,
            condition: visionResult.Condition,
            estimatedValue: estimatedPrice,
            defects: visionResult.Defects
        );

        // 6. Aqui você salvaria no banco (ex: _evaluationRepository.SaveAsync(evaluation)) e atualizaria a wallet

        return evaluation;
    }
}