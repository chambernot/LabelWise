using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using LabelWise.Application.Parsing;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services.LabelReading
{
    /// <summary>
    /// Estratégia para ler e estruturar informações da embalagem frontal.
    /// 
    /// OBJETIVO:
    /// Extrair claims nutricionais e informações úteis da frente da embalagem.
    /// 
    /// PROCESSO:
    /// 1. Identificar claims nutricionais comuns
    /// 2. Extrair textos promocionais e selos
    /// 3. Validar qualidade da extração
    /// 4. Retornar JSON com lista de claims
    /// 
    /// EXEMPLOS DE CLAIMS:
    /// - "Sem glúten"
    /// - "Rico em fibras"
    /// - "Fonte de proteínas"
    /// - "Zero açúcar"
    /// - "Light"
    /// - "Diet"
    /// </summary>
    public class FrontPackagingReadingStrategy : ICaptureReadingStrategy
    {
        private readonly IIngredientAllergenParser _parser;
        private readonly ILogger _logger;

        // Claims nutricionais comuns
        private static readonly string[] KnownClaims = new[]
        {
            "sem glúten", "sem gluten", "gluten free",
            "sem lactose", "lactose free",
            "sem açúcar", "sem acucar", "zero açúcar", "zero acucar", "sugar free",
            "diet", "light",
            "rico em fibras", "high fiber", "fonte de fibras",
            "fonte de proteínas", "proteína", "proteina", "protein",
            "integral", "whole grain",
            "orgânico", "organico", "organic",
            "natural",
            "sem conservantes", "sem corantes",
            "zero trans", "zero gordura trans",
            "baixo sódio", "baixo sodio", "low sodium",
            "reduzido em calorias",
            "sem adição de açúcar", "sem adicao de acucar"
        };

        public FrontPackagingReadingStrategy(
            IIngredientAllergenParser parser,
            ILogger logger)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public CaptureReadingStrategyResult Parse(string rawOcrText, double ocrConfidence)
        {
            _logger.LogDebug("📦 Iniciando parsing de embalagem frontal...");

            var result = new CaptureReadingStrategyResult
            {
                Success = false,
                Confidence = ocrConfidence
            };

            if (string.IsNullOrWhiteSpace(rawOcrText))
            {
                result.ErrorMessage = "Texto OCR vazio";
                return result;
            }

            try
            {
                var claims = ExtractClaims(rawOcrText);

                if (!claims.Any())
                {
                    _logger.LogDebug("   → Nenhum claim nutricional encontrado");

                    // Isso não é um erro - a embalagem pode não ter claims
                    claims.Add("Nenhum claim nutricional identificado");
                    result.Confidence = Math.Min(ocrConfidence, 0.5);
                }

                // Serializar para JSON
                result.StructuredData = JsonSerializer.Serialize(claims);
                result.Success = true;

                result.Metadata["TotalClaims"] = claims.Count.ToString();

                _logger.LogDebug("   ✅ Parsing concluído: {Count} claims identificados",
                    claims.Count);

                if (claims.Count > 1) // Mais de 1 porque 1 pode ser "Nenhum claim..."
                {
                    _logger.LogDebug("   📋 Claims: {Claims}",
                        string.Join(", ", claims.Where(c => !c.StartsWith("Nenhum"))));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao fazer parsing da embalagem frontal");
                result.ErrorMessage = $"Erro no parsing: {ex.Message}";
                result.Success = false;
            }

            return result;
        }

        private List<string> ExtractClaims(string text)
        {
            var claims = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Normalizar texto
            var normalizedText = text.ToLowerInvariant()
                .Replace('\n', ' ')
                .Replace('\r', ' ');

            // Procurar por claims conhecidos
            foreach (var knownClaim in KnownClaims)
            {
                if (normalizedText.Contains(knownClaim, StringComparison.OrdinalIgnoreCase))
                {
                    // Normalizar o claim para formato consistente
                    var normalizedClaim = NormalizeClaim(knownClaim);
                    claims.Add(normalizedClaim);

                    _logger.LogDebug("   → Claim encontrado: {Claim}", normalizedClaim);
                }
            }

            // Procurar por padrões de percentagem (ex: "30% menos gordura")
            var percentagePattern = new Regex(@"(\d+%\s*(?:menos|mais|a mais|a menos)\s+\w+)", RegexOptions.IgnoreCase);
            var percentageMatches = percentagePattern.Matches(normalizedText);

            foreach (Match match in percentageMatches)
            {
                claims.Add(CapitalizeFirstLetter(match.Groups[1].Value.Trim()));
                _logger.LogDebug("   → Claim percentual encontrado: {Claim}", match.Groups[1].Value);
            }

            // Procurar por "fonte de" ou "rico em"
            var sourcePattern = new Regex(@"(?:fonte de|rico em)\s+(\w+)", RegexOptions.IgnoreCase);
            var sourceMatches = sourcePattern.Matches(normalizedText);

            foreach (Match match in sourceMatches)
            {
                claims.Add(CapitalizeFirstLetter(match.Value.Trim()));
                _logger.LogDebug("   → Claim 'fonte/rico' encontrado: {Claim}", match.Value);
            }

            return claims.ToList();
        }

        private string NormalizeClaim(string claim)
        {
            // Capitalizar primeira letra de cada palavra importante
            var words = claim.Split(' ');
            var normalized = string.Join(" ", words.Select(w => CapitalizeFirstLetter(w)));

            return normalized;
        }

        private string CapitalizeFirstLetter(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            return char.ToUpperInvariant(text[0]) + text.Substring(1).ToLowerInvariant();
        }
    }
}
