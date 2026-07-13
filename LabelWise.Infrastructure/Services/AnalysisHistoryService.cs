using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LabelWise.Application.DTOs;
using LabelWise.Application.Interfaces;

namespace LabelWise.Infrastructure.Services
{
    public class AnalysisHistoryService : IAnalysisHistoryService
    {
        private readonly IAnalysisRepository _analysisRepository;

        public AnalysisHistoryService(IAnalysisRepository analysisRepository)
        {
            _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
        }

        public async Task<IEnumerable<AnalysisHistorySummaryDto>> GetUserAnalysisHistoryAsync(Guid userId)
        {
            var analyses = await _analysisRepository.GetByUserIdAsync(userId);
            return analyses.Select(MapSummary).ToList();
        }

        public async Task<IEnumerable<AnalysisHistorySummaryDto>> GetDeviceAnalysisHistoryAsync(string deviceId)
        {
            var analyses = await _analysisRepository.GetByDeviceIdAsync(deviceId);
            return analyses.Select(MapSummary).ToList();
        }

        public async Task<AnalysisHistoryDetailDto?> GetAnalysisDetailAsync(Guid analysisId, Guid userId)
        {
            var analysis = await _analysisRepository.GetByIdAsync(analysisId);

            if (analysis == null || analysis.UserId != userId)
            {
                return null;
            }

            return MapDetail(analysis);
        }

        public async Task<AnalysisHistoryDetailDto?> GetAnalysisDetailByDeviceAsync(Guid analysisId, string deviceId)
        {
            var analysis = await _analysisRepository.GetByIdAsync(analysisId);

            if (analysis == null || !string.Equals(analysis.DeviceId, deviceId, StringComparison.Ordinal))
            {
                return null;
            }

            return MapDetail(analysis);
        }

        private static AnalysisHistorySummaryDto MapSummary(Domain.Entities.ProductAnalysis analysis)
        {
            return new AnalysisHistorySummaryDto
            {
                Id = analysis.Id,
                ProductName = analysis.Product.Name,
                Brand = analysis.Product.Brand,
                AnalyzedAt = analysis.AnalyzedAt,
                Classification = analysis.Classification.ToString(),
                ConfidenceLevel = analysis.Confidence.ToString(),
                AlertsCount = analysis.Alerts.Count,
                RecommendationsCount = analysis.Recommendations.Count
            };
        }

        private static AnalysisHistoryDetailDto MapDetail(Domain.Entities.ProductAnalysis analysis)
        {
            return new AnalysisHistoryDetailDto
            {
                Id = analysis.Id,
                AnalyzedAt = analysis.AnalyzedAt,
                Classification = analysis.Classification.ToString(),
                ConfidenceLevel = analysis.Confidence.ToString(),
                Summary = analysis.Summary,
                Product = new ProductDetailsDto
                {
                    Id = analysis.Product.Id,
                    Name = analysis.Product.Name,
                    Brand = analysis.Product.Brand,
                    Barcode = analysis.Product.Barcode
                },
                Alerts = analysis.Alerts.Select(alert => new AlertDetailsDto
                {
                    Id = alert.Id,
                    Message = alert.Message,
                    Severity = alert.Severity.ToString(),
                    Confidence = alert.Confidence.ToString()
                }).ToList(),
                Recommendations = analysis.Recommendations.Select(rec => new RecommendationDetailsDto
                {
                    Id = rec.Id,
                    Recommendation = rec.Recommendation,
                    Reason = rec.Reason,
                    ExplanationLevel = rec.ExplanationLevel.ToString()
                }).ToList()
            };
        }
    }
}
