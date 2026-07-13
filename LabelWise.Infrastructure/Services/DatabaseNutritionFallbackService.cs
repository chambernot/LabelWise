using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Domain.Entities;
using LabelWise.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LabelWise.Infrastructure.Services;

/// <summary>
/// Serviço de fallback nutricional baseado em perfis de categoria do banco de dados.
/// Usa diretamente o código de categoria já normalizado — sem re-resolução.
/// </summary>
public class DatabaseNutritionFallbackService : IDatabaseNutritionFallbackService
{
    private readonly ICategoryNutritionProfileRepository _profileRepository;
    private readonly ILogger<DatabaseNutritionFallbackService> _logger;

    public DatabaseNutritionFallbackService(
        ICategoryNutritionProfileRepository profileRepository,
        ILogger<DatabaseNutritionFallbackService> logger)
    {
        _profileRepository = profileRepository;
        _logger = logger;
    }

    public async Task<DatabaseFallbackResult> ApplyFallbackAsync(
        EstimatedNutritionProfileDto? partialNutrition,
        string? normalizedCategoryCode,
        string analysisMode)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(normalizedCategoryCode))
            {
                _logger.LogWarning("[NutritionFallback] Fallback requested with null/empty category code. No profile will be applied.");
                return CreateUnknownFallback(partialNutrition, "Categoria não informada para o fallback nutricional.", requestedCode: null);
            }

            _logger.LogInformation(
                "[NutritionFallback] Starting fallback lookup. RequestedCategoryCode={RequestedCategoryCode}",
                normalizedCategoryCode);

            // === BUSCA DIRETA pelo código normalizado — sem re-resolução ===
            var profile = await _profileRepository.GetByCategoryCodeAsync(normalizedCategoryCode);
            var usedParentFallback = false;

            if (profile == null)
            {
                _logger.LogWarning(
                    "[NutritionFallback] No profile found for exact category code '{CategoryCode}'. Attempting parent category fallback.",
                    normalizedCategoryCode);

                // Tentar perfil da categoria pai (hierarquia), se existir
                profile = await TryLoadParentCategoryProfileAsync(normalizedCategoryCode);

                if (profile != null)
                {
                    usedParentFallback = true;
                    _logger.LogInformation(
                        "[NutritionFallback] Parent category fallback applied. RequestedCode={RequestedCode}, ParentProfileCode={ParentCode}, ParentProfileName={ParentName}",
                        normalizedCategoryCode,
                        profile.CategoryCode,
                        profile.Category?.Name);
                }
                else
                {
                    _logger.LogWarning(
                        "[NutritionFallback] No parent category profile found either. RequestedCode={RequestedCode}. Returning unknown fallback.",
                        normalizedCategoryCode);

                    return CreateUnknownFallback(
                        partialNutrition,
                        $"Perfil nutricional não encontrado para a categoria '{normalizedCategoryCode}' nem para sua categoria pai.",
                        requestedCode: normalizedCategoryCode);
                }
            }

            // === VALIDAÇÃO: confirmar que o perfil carregado é coerente ===
            _logger.LogInformation(
                "[NutritionFallback] Profile loaded. RequestedCode={RequestedCode}, LoadedProfileCode={LoadedCode}, LoadedProfileName={LoadedName}, UsedParentFallback={UsedParent}",
                normalizedCategoryCode,
                profile.CategoryCode,
                profile.Category?.Name,
                usedParentFallback);

            var validation = ValidateProfileCoherence(partialNutrition, profile);
            if (validation.RejectProfile && CountRealFields(partialNutrition) > 0)
            {
                _logger.LogWarning(
                    "[NutritionFallback] Profile REJECTED for category '{CategoryCode}'. Inconsistencies={Inconsistencies}",
                    profile.CategoryCode,
                    string.Join(" | ", validation.Issues));

                return CreateRejectedFallback(partialNutrition, profile, validation.Issues, normalizedCategoryCode, usedParentFallback);
            }

            var result = ApplyIntelligentFallback(partialNutrition, profile, validation.Issues, analysisMode, normalizedCategoryCode, usedParentFallback);

            _logger.LogInformation(
                "[NutritionFallback] Fallback APPLIED successfully. " +
                "RequestedCode={RequestedCode}, AppliedProfileCode={AppliedCode}, AppliedProfileName={AppliedName}, " +
                "Confidence={Confidence}, FullyEstimated={IsFullyEstimated}, UsedParent={UsedParent}, " +
                "FieldSources={FieldSources}, Inconsistencies={Inconsistencies}",
                normalizedCategoryCode,
                result.NormalizedCategoryCode,
                result.NormalizedCategoryName,
                result.Confidence,
                result.IsFullyEstimated,
                usedParentFallback,
                string.Join(", ", result.FallbackSources.Select(kvp => $"{kvp.Key}={kvp.Value}")),
                string.Join(" | ", result.Inconsistencies));

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NutritionFallback] Error applying fallback for category: {Category}", normalizedCategoryCode);
            return CreateUnknownFallback(partialNutrition, "Erro interno ao aplicar fallback nutricional.", requestedCode: normalizedCategoryCode);
        }
    }

    /// <summary>
    /// Tenta carregar o perfil da categoria pai quando o perfil da categoria exata não existe.
    /// Busca a categoria no repositório de perfis usando a hierarquia de categorias.
    /// </summary>
    private async Task<CategoryNutritionProfile?> TryLoadParentCategoryProfileAsync(string categoryCode)
    {
        try
        {
            // Carregar todos os perfis ativos para verificar hierarquia
            var allProfiles = await _profileRepository.GetAllActiveAsync();
            var targetProfile = allProfiles.FirstOrDefault(p =>
                string.Equals(p.CategoryCode, categoryCode, StringComparison.OrdinalIgnoreCase));

            if (targetProfile != null)
            {
                // O perfil existe mas GetByCategoryCodeAsync não o encontrou (possível casing issue)
                _logger.LogWarning(
                    "[NutritionFallback] Profile found via case-insensitive search. StoredCode='{StoredCode}', RequestedCode='{RequestedCode}'",
                    targetProfile.CategoryCode,
                    categoryCode);
                return targetProfile;
            }

            // Buscar categoria pai na tabela de categorias via perfis existentes
            var parentCode = allProfiles
                .Where(p => p.Category?.ParentCode != null)
                .SelectMany(p => new[] { new { p.CategoryCode, p.Category?.ParentCode } })
                .Where(x => string.Equals(x.CategoryCode, categoryCode, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.ParentCode)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(parentCode))
            {
                var parentProfile = allProfiles.FirstOrDefault(p =>
                    string.Equals(p.CategoryCode, parentCode, StringComparison.OrdinalIgnoreCase));

                if (parentProfile != null)
                {
                    return parentProfile;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NutritionFallback] Error trying parent category fallback for: {CategoryCode}", categoryCode);
            return null;
        }
    }

    private DatabaseFallbackResult ApplyIntelligentFallback(
        EstimatedNutritionProfileDto? partial,
        CategoryNutritionProfile profile,
        IReadOnlyCollection<string> issues,
        string analysisMode,
        string requestedCategoryCode,
        bool usedParentFallback)
    {
        var result = CloneProfile(partial);
        var fieldSources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        ApplyField(result.CaloriesPer100g, profile.CaloriesPer100g, value => result.CaloriesPer100g = value, "calories", fieldSources);
        ApplyField(result.EstimatedProteinPer100g, profile.ProteinPer100g, value => result.EstimatedProteinPer100g = value, "protein", fieldSources);
        ApplyField(result.EstimatedFatPer100g, profile.FatPer100g, value => result.EstimatedFatPer100g = value, "fat", fieldSources);
        ApplyField(result.EstimatedSugarPer100g, profile.SugarPer100g, value => result.EstimatedSugarPer100g = value, "sugar", fieldSources);
        ApplyField(result.EstimatedSodiumPer100g, profile.SodiumPer100g, value => result.EstimatedSodiumPer100g = value, "sodium", fieldSources);
        ApplyField(result.EstimatedFiberPer100g, profile.FiberPer100g, value => result.EstimatedFiberPer100g = value, "fiber", fieldSources);

        result.Basis = BuildBasis(fieldSources, profile, analysisMode, issues, requestedCategoryCode, usedParentFallback);

        var realFields = fieldSources.Count(kvp => kvp.Value == "Real");
        var estimatedFields = fieldSources.Count(kvp => kvp.Value == "EstimatedByCategory");
        var confidence = CalculateConfidence(realFields, estimatedFields, (double)profile.ConfidenceLevel, issues.Count, usedParentFallback);

        return new DatabaseFallbackResult
        {
            Profile = result,
            RequestedCategoryCode = requestedCategoryCode,
            NormalizedCategoryCode = profile.CategoryCode,
            NormalizedCategoryName = profile.Category?.Name,
            Confidence = confidence,
            IsFullyEstimated = realFields == 0 && estimatedFields > 0,
            IsPartiallyEstimated = realFields > 0 && estimatedFields > 0,
            UsedParentCategoryFallback = usedParentFallback,
            FallbackSources = fieldSources,
            Inconsistencies = issues.ToList(),
            ProfileRejected = false
        };
    }

    private static void ApplyField(
        double? currentValue,
        decimal? profileValue,
        Action<double> assign,
        string fieldName,
        IDictionary<string, string> sources)
    {
        if (currentValue.HasValue && currentValue.Value >= 0)
        {
            sources[fieldName] = "Real";
            return;
        }

        if (!profileValue.HasValue)
        {
            return;
        }

        assign((double)profileValue.Value);
        sources[fieldName] = "EstimatedByCategory";
    }

    private static EstimatedNutritionProfileDto CloneProfile(EstimatedNutritionProfileDto? profile)
    {
        if (profile == null)
        {
            return new EstimatedNutritionProfileDto();
        }

        return new EstimatedNutritionProfileDto
        {
            CaloriesPer100g = profile.CaloriesPer100g,
            EstimatedPackageCalories = profile.EstimatedPackageCalories,
            EstimatedSugarPer100g = profile.EstimatedSugarPer100g,
            EstimatedProteinPer100g = profile.EstimatedProteinPer100g,
            EstimatedSodiumPer100g = profile.EstimatedSodiumPer100g,
            EstimatedFiberPer100g = profile.EstimatedFiberPer100g,
            EstimatedFatPer100g = profile.EstimatedFatPer100g,
            Basis = profile.Basis
        };
    }

    private ProfileValidationResult ValidateProfileCoherence(EstimatedNutritionProfileDto? partial, CategoryNutritionProfile profile)
    {
        var issues = new List<string>();
        var categoryCode = profile.CategoryCode.ToLowerInvariant();
        var family = profile.Category?.ParentCode?.ToLowerInvariant() ?? string.Empty;

        ValidateRealVsProfileRange(partial?.EstimatedProteinPer100g, profile.ProteinMin, profile.ProteinMax, "protein", issues);
        ValidateRealVsProfileRange(partial?.EstimatedFatPer100g, profile.FatMin, profile.FatMax, "fat", issues);
        ValidateRealVsProfileRange(partial?.EstimatedSugarPer100g, profile.SugarMin, profile.SugarMax, "sugar", issues);
        ValidateRealVsProfileRange(partial?.EstimatedSodiumPer100g, profile.SodiumMin, profile.SodiumMax, "sodium", issues);
        ValidateRealVsProfileRange(partial?.CaloriesPer100g, profile.CaloriesMin, profile.CaloriesMax, "calories", issues);

        var protein = GetEffective(partial?.EstimatedProteinPer100g, profile.ProteinPer100g);
        var fat = GetEffective(partial?.EstimatedFatPer100g, profile.FatPer100g);
        var sugar = GetEffective(partial?.EstimatedSugarPer100g, profile.SugarPer100g);
        var sodium = GetEffective(partial?.EstimatedSodiumPer100g, profile.SodiumPer100g);

        if (IsBeverage(categoryCode, family) && protein > 8)
        {
            issues.Add("Protein seems unusually high for a beverage category.");
        }

        if (IsHardCheese(categoryCode, family) && protein > 0 && protein < 10)
        {
            issues.Add("Protein seems unusually low for a hard-cheese category.");
        }

        if (IsSweetDairyDessert(categoryCode) && sugar >= 0 && sugar < 6)
        {
            issues.Add("Sugar seems unusually low for a sweet dairy dessert category.");
        }

        if (IsCarbBase(categoryCode, family) && fat > 18)
        {
            issues.Add("Fat seems unusually high for a carbohydrate-base category.");
        }

        if (issues.Count == 0)
        {
            return ProfileValidationResult.Accepted();
        }

        var severeIssues = issues.Count(issue => issue.Contains("outside expected range", StringComparison.OrdinalIgnoreCase)) >= 2;
        return new ProfileValidationResult(severeIssues, issues);
    }

    private static void ValidateRealVsProfileRange(double? realValue, decimal? min, decimal? max, string field, ICollection<string> issues)
    {
        if (!realValue.HasValue || !min.HasValue || !max.HasValue)
        {
            return;
        }

        var tolerance = Math.Max(1.0, ((double)(max.Value - min.Value)) * 0.20);
        if (realValue.Value < (double)min.Value - tolerance || realValue.Value > (double)max.Value + tolerance)
        {
            issues.Add($"{field} is outside expected range for the resolved category.");
        }
    }

    private static double GetEffective(double? realValue, decimal? profileValue)
    {
        if (realValue.HasValue)
        {
            return realValue.Value;
        }

        return profileValue.HasValue ? (double)profileValue.Value : -1;
    }

    private static bool IsBeverage(string categoryCode, string family)
        => family == "bebida" || categoryCode.Contains("refrigerante") || categoryCode.Contains("suco") || categoryCode.Contains("cha") || categoryCode.Contains("bebida");

    private static bool IsHardCheese(string categoryCode, string family)
        => family == "laticinio" && (categoryCode.Contains("queijo_duro") || categoryCode.Contains("queijo_ralado"));

    private static bool IsSweetDairyDessert(string categoryCode)
        => categoryCode.Contains("sobremesa") || categoryCode.Contains("adocicado");

    private static bool IsCarbBase(string categoryCode, string family)
        => family == "carboidrato" || categoryCode.Contains("arroz") || categoryCode.Contains("macarrao") || categoryCode.Contains("pao") || categoryCode.Contains("cereal");

    private static string BuildBasis(
        IReadOnlyDictionary<string, string> fieldSources,
        CategoryNutritionProfile profile,
        string analysisMode,
        IReadOnlyCollection<string> issues,
        string requestedCategoryCode,
        bool usedParentFallback)
    {
        var realFields = fieldSources.Where(x => x.Value == "Real").Select(x => x.Key).ToArray();
        var estimatedFields = fieldSources.Where(x => x.Value == "EstimatedByCategory").Select(x => x.Key).ToArray();

        var categoryLabel = profile.Category?.Name ?? profile.CategoryCode;
        var categoryDetail = usedParentFallback
            ? $"(categoria pai de '{requestedCategoryCode}': {categoryLabel} [{profile.CategoryCode}])"
            : $"({categoryLabel} [{profile.CategoryCode}])";

        var basis = analysisMode switch
        {
            nameof(AnalysisMode.FullNutritionLabel) when estimatedFields.Length == 0 =>
                "Dados extraídos integralmente da tabela nutricional.",
            nameof(AnalysisMode.FullNutritionLabel) =>
                $"Leitura parcial da tabela nutricional. Campos reais: {string.Join(", ", realFields)}. Campos estimados por categoria {categoryDetail}: {string.Join(", ", estimatedFields)}.",
            _ when estimatedFields.Length > 0 =>
                $"Perfil nutricional estimado pela categoria {categoryDetail}.",
            _ => "Análise baseada em dados disponíveis."
        };

        if (usedParentFallback)
        {
            basis += " [Nota: perfil de categoria pai utilizado porque não há perfil específico.]";
        }

        if (issues.Count > 0)
        {
            basis += $" Verificações de coerência: {string.Join(" ", issues)}";
        }

        return basis;
    }

    private static double CalculateConfidence(int realFields, int estimatedFields, double profileConfidence, int issueCount, bool usedParentFallback)
    {
        var totalFields = realFields + estimatedFields;
        if (totalFields == 0)
        {
            return 0.10;
        }

        var realShare = realFields / (double)totalFields;
        var estimatedShare = estimatedFields / (double)totalFields;
        var confidence = (realShare * 0.75) + (estimatedShare * profileConfidence * 0.65);
        confidence -= issueCount * 0.10;

        if (usedParentFallback)
        {
            confidence *= 0.80;
        }

        return Math.Round(Math.Clamp(confidence, 0.10, 0.98), 2);
    }

    private static int CountRealFields(EstimatedNutritionProfileDto? partial)
    {
        if (partial == null)
        {
            return 0;
        }

        var count = 0;
        if (partial.CaloriesPer100g.HasValue) count++;
        if (partial.EstimatedProteinPer100g.HasValue) count++;
        if (partial.EstimatedFatPer100g.HasValue) count++;
        if (partial.EstimatedSugarPer100g.HasValue) count++;
        if (partial.EstimatedSodiumPer100g.HasValue) count++;
        if (partial.EstimatedFiberPer100g.HasValue) count++;
        return count;
    }

    private static DatabaseFallbackResult CreateRejectedFallback(
        EstimatedNutritionProfileDto? partial,
        CategoryNutritionProfile profile,
        IReadOnlyCollection<string> issues,
        string requestedCategoryCode,
        bool usedParentFallback)
    {
        var result = CloneProfile(partial);
        result.Basis = $"Fallback por categoria descartado para {profile.Category?.Name ?? profile.CategoryCode} [{profile.CategoryCode}] devido a inconsistências detectadas.";

        return new DatabaseFallbackResult
        {
            Profile = result,
            RequestedCategoryCode = requestedCategoryCode,
            NormalizedCategoryCode = profile.CategoryCode,
            NormalizedCategoryName = profile.Category?.Name,
            Confidence = 0.35,
            IsFullyEstimated = false,
            IsPartiallyEstimated = false,
            UsedParentCategoryFallback = usedParentFallback,
            FallbackSources = BuildRealFieldSources(result),
            Inconsistencies = issues.ToList(),
            ProfileRejected = true
        };
    }

    private static DatabaseFallbackResult CreateUnknownFallback(EstimatedNutritionProfileDto? partial, string reason, string? requestedCode)
    {
        var result = CloneProfile(partial);
        result.Basis = reason;

        return new DatabaseFallbackResult
        {
            Profile = result,
            RequestedCategoryCode = requestedCode,
            NormalizedCategoryCode = null,
            NormalizedCategoryName = null,
            Confidence = CountRealFields(partial) > 0 ? 0.45 : 0.10,
            IsFullyEstimated = false,
            IsPartiallyEstimated = false,
            UsedParentCategoryFallback = false,
            FallbackSources = BuildRealFieldSources(result),
            Inconsistencies = [],
            ProfileRejected = true
        };
    }

    private static Dictionary<string, string> BuildRealFieldSources(EstimatedNutritionProfileDto profile)
    {
        var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (profile.CaloriesPer100g.HasValue) sources["calories"] = "Real";
        if (profile.EstimatedProteinPer100g.HasValue) sources["protein"] = "Real";
        if (profile.EstimatedFatPer100g.HasValue) sources["fat"] = "Real";
        if (profile.EstimatedSugarPer100g.HasValue) sources["sugar"] = "Real";
        if (profile.EstimatedSodiumPer100g.HasValue) sources["sodium"] = "Real";
        if (profile.EstimatedFiberPer100g.HasValue) sources["fiber"] = "Real";
        return sources;
    }

    private sealed record ProfileValidationResult(bool RejectProfile, IReadOnlyCollection<string> Issues)
    {
        public static ProfileValidationResult Accepted() => new(false, Array.Empty<string>());
    }
}
