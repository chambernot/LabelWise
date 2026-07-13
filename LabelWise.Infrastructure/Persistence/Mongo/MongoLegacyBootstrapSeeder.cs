using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using LabelWise.Domain.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace LabelWise.Infrastructure.Persistence.Mongo;

public sealed class MongoLegacyBootstrapSeeder
{
    private static readonly string[] CollectionsToEnsure =
    [
        MongoCollectionNames.Users,
        MongoCollectionNames.AppUsers,
        "user_profiles",
        MongoCollectionNames.Products,
        "product_labels",
        "nutritional_infos",
        "product_ingredients",
        "product_allergens",
        MongoCollectionNames.ValidatedProducts,
        MongoCollectionNames.KnownProducts,
        MongoCollectionNames.NutritionCategories,
        MongoCollectionNames.NutritionCategoryAliases,
        MongoCollectionNames.CategoryNutritionProfiles,
        MongoCollectionNames.CategoryMappings
    ];

    private static readonly string[] HistoryCollectionsToSkip =
    [
        MongoCollectionNames.Analyses,
        "analysis_alerts",
        "analysis_recommendations",
        MongoCollectionNames.AnalysisSessions,
        MongoCollectionNames.ProductCaptures
    ];

    private readonly MongoDbContext _context;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<MongoLegacyBootstrapSeeder> _logger;

    public MongoLegacyBootstrapSeeder(
        MongoDbContext context,
        IHostEnvironment environment,
        ILogger<MongoLegacyBootstrapSeeder> logger)
    {
        _context = context;
        _environment = environment;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var rootPath = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, ".."));
        var nutritionSeedPath = Path.Combine(rootPath, "database-migrations", "v2-seed-nutrition-knowledge-base.sql");
        var knownProductsSeedPath = Path.Combine(rootPath, "seed-known-products.ps1");

        await EnsureCollectionsAsync(cancellationToken);
        var categories = await SeedNutritionKnowledgeBaseAsync(nutritionSeedPath, cancellationToken);
        await SeedKnownProductsAsync(knownProductsSeedPath, cancellationToken);

        _logger.LogInformation(
            "Bootstrap Mongo concluído. Database={DatabaseName}, Categorias={CategoryCount}.",
            _context.Settings.DatabaseName,
            categories.Count);
    }

    private async Task EnsureCollectionsAsync(CancellationToken cancellationToken)
    {
        var existing = await _context.Database.ListCollectionNames(cancellationToken: cancellationToken).ToListAsync(cancellationToken);

        foreach (var collection in HistoryCollectionsToSkip.Intersect(existing, StringComparer.OrdinalIgnoreCase))
        {
            await _context.Database.DropCollectionAsync(collection, cancellationToken);
            _logger.LogInformation("Collection histórica removida do bootstrap: {CollectionName}", collection);
        }

        existing = await _context.Database.ListCollectionNames(cancellationToken: cancellationToken).ToListAsync(cancellationToken);

        foreach (var collection in CollectionsToEnsure.Except(existing, StringComparer.OrdinalIgnoreCase))
        {
            await _context.Database.CreateCollectionAsync(collection, cancellationToken: cancellationToken);
            _logger.LogInformation("Collection criada: {CollectionName}", collection);
        }
    }

    private async Task<Dictionary<string, NutritionCategoryDocument>> SeedNutritionKnowledgeBaseAsync(string sqlPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(sqlPath))
        {
            throw new FileNotFoundException("Arquivo de seed nutricional não encontrado.", sqlPath);
        }

        var statements = SqlInsertParser.Parse(File.ReadAllText(sqlPath, Encoding.UTF8));

        var categoryRows = statements.Where(x => x.TableName.Equals("nutrition_category", StringComparison.OrdinalIgnoreCase)).SelectMany(x => x.Rows).ToList();
        var profileRows = statements.Where(x => x.TableName.Equals("nutrition_category_profile", StringComparison.OrdinalIgnoreCase)).SelectMany(x => x.Rows).ToList();
        var aliasRows = statements.Where(x => x.TableName.Equals("nutrition_category_alias", StringComparison.OrdinalIgnoreCase)).SelectMany(x => x.Rows).ToList();
        var mappingRows = statements.Where(x => x.TableName.Equals("category_mappings", StringComparison.OrdinalIgnoreCase)).SelectMany(x => x.Rows).ToList();

        var categories = categoryRows.Select((row, index) => new NutritionCategoryDocument
        {
            Id = index + 1,
            Code = ReadString(row, "code") ?? string.Empty,
            Name = ReadString(row, "name") ?? string.Empty,
            Description = ReadString(row, "description"),
            ParentCode = ReadString(row, "parent_code"),
            IsActive = ReadBool(row, "is_active", true),
            DisplayOrder = ReadInt(row, "display_order", index + 1)
        }).ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);

        var profiles = profileRows.Select((row, index) => new CategoryNutritionProfileDocument
        {
            Id = index + 1,
            CategoryCode = ReadString(row, "category_code") ?? string.Empty,
            CaloriesPer100g = ReadDecimal(row, "calories_per_100g"),
            CaloriesMin = ReadDecimal(row, "calories_min"),
            CaloriesMax = ReadDecimal(row, "calories_max"),
            ProteinPer100g = ReadDecimal(row, "protein_per_100g"),
            ProteinMin = ReadDecimal(row, "protein_min"),
            ProteinMax = ReadDecimal(row, "protein_max"),
            FatPer100g = ReadDecimal(row, "fat_per_100g"),
            FatMin = ReadDecimal(row, "fat_min"),
            FatMax = ReadDecimal(row, "fat_max"),
            SaturatedFatPer100g = ReadDecimal(row, "saturated_fat_per_100g"),
            SaturatedFatMin = ReadDecimal(row, "saturated_fat_min"),
            SaturatedFatMax = ReadDecimal(row, "saturated_fat_max"),
            TransFatPer100g = ReadDecimal(row, "trans_fat_per_100g"),
            TransFatMin = ReadDecimal(row, "trans_fat_min"),
            TransFatMax = ReadDecimal(row, "trans_fat_max"),
            CarbohydratesPer100g = ReadDecimal(row, "carbohydrates_per_100g"),
            CarbohydratesMin = ReadDecimal(row, "carbohydrates_min"),
            CarbohydratesMax = ReadDecimal(row, "carbohydrates_max"),
            SugarPer100g = ReadDecimal(row, "sugar_per_100g"),
            SugarMin = ReadDecimal(row, "sugar_min"),
            SugarMax = ReadDecimal(row, "sugar_max"),
            FiberPer100g = ReadDecimal(row, "fiber_per_100g"),
            FiberMin = ReadDecimal(row, "fiber_min"),
            FiberMax = ReadDecimal(row, "fiber_max"),
            SodiumPer100g = ReadDecimal(row, "sodium_per_100g"),
            SodiumMin = ReadDecimal(row, "sodium_min"),
            SodiumMax = ReadDecimal(row, "sodium_max"),
            ConfidenceLevel = ReadDecimal(row, "confidence_level") ?? 0.70m,
            DataSource = ReadString(row, "data_source"),
            ReferenceYear = ReadIntNullable(row, "reference_year"),
            SampleSize = ReadIntNullable(row, "sample_size"),
            Notes = ReadString(row, "notes"),
            IsLiquid = ReadBool(row, "is_liquid", false),
            IsActive = ReadBool(row, "is_active", true),
            Version = ReadInt(row, "version", 1)
        }).ToList();

        foreach (var profile in profiles)
        {
            if (categories.TryGetValue(profile.CategoryCode, out var category))
            {
                profile.Category = category;
            }
        }

        var aliases = aliasRows.Select((row, index) => new NutritionCategoryAliasDocument
        {
            Id = index + 1,
            CategoryCode = ReadString(row, "category_code") ?? string.Empty,
            AliasName = ReadString(row, "alias_name") ?? string.Empty,
            AliasNameNormalized = NormalizeAlias(ReadString(row, "alias_name")),
            Confidence = ReadDecimal(row, "confidence") ?? 1.00m,
            MatchType = ReadString(row, "match_type") ?? "exact",
            IsActive = ReadBool(row, "is_active", true),
            UsageCount = ReadInt(row, "usage_count", 0)
        }).ToList();

        var mappingSeed = mappingRows.Select(row => new
        {
            RawCategoryName = ReadString(row, "raw_category_name") ?? string.Empty,
            NormalizedCategoryCode = ReadString(row, "normalized_category_code") ?? string.Empty,
            Confidence = ReadDecimal(row, "confidence") ?? 1.00m,
            IsActive = ReadBool(row, "is_active", true)
        }).ToList();

        mappingSeed.AddRange(aliases.Select(alias => new
        {
            RawCategoryName = alias.AliasName,
            NormalizedCategoryCode = alias.CategoryCode,
            alias.Confidence,
            alias.IsActive
        }));

        mappingSeed.AddRange(aliases
            .Select(alias => new
            {
                OriginalAliasName = alias.AliasName,
                RawCategoryName = RemoveAccents(alias.AliasName),
                NormalizedCategoryCode = alias.CategoryCode,
                alias.Confidence,
                alias.IsActive
            })
            .Where(x => !string.Equals(x.RawCategoryName, x.OriginalAliasName, StringComparison.Ordinal))
            .Select(x => new
            {
                x.RawCategoryName,
                x.NormalizedCategoryCode,
                x.Confidence,
                x.IsActive
            }));

        var mappings = mappingSeed
            .Where(x => !string.IsNullOrWhiteSpace(x.RawCategoryName))
            .GroupBy(x => $"{x.RawCategoryName}::{x.NormalizedCategoryCode}", StringComparer.OrdinalIgnoreCase)
            .Select((group, index) =>
            {
                var first = group.First();
                return new CategoryMappingDocument
                {
                    Id = index + 1,
                    RawCategoryName = first.RawCategoryName,
                    NormalizedCategoryCode = first.NormalizedCategoryCode,
                    Confidence = first.Confidence,
                    IsActive = first.IsActive
                };
            })
            .ToList();

        foreach (var mapping in mappings)
        {
            if (categories.TryGetValue(mapping.NormalizedCategoryCode, out var category))
            {
                mapping.NormalizedCategory = category;
            }
        }

        await ReplaceCollectionAsync(_context.NutritionCategories, categories.Values.ToList(), cancellationToken);
        await ReplaceCollectionAsync(_context.CategoryNutritionProfiles, profiles, cancellationToken);
        await ReplaceCollectionAsync(_context.Database.GetCollection<NutritionCategoryAliasDocument>(MongoCollectionNames.NutritionCategoryAliases), aliases, cancellationToken);
        await ReplaceCollectionAsync(_context.CategoryMappings, mappings, cancellationToken);

        _logger.LogInformation(
            "Seed nutricional aplicado. Categories={CategoryCount}, Profiles={ProfileCount}, Aliases={AliasCount}, Mappings={MappingCount}",
            categories.Count,
            profiles.Count,
            aliases.Count,
            mappings.Count);

        return categories;
    }

    private async Task SeedKnownProductsAsync(string scriptPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException("Arquivo de seed de known products não encontrado.", scriptPath);
        }

        var scriptContent = File.ReadAllText(scriptPath, Encoding.UTF8);
        var sqlMatch = Regex.Match(scriptContent, @"\$seedSql\s*=\s*@""(?<sql>.*?)""@", RegexOptions.Singleline);
        if (!sqlMatch.Success)
        {
            throw new InvalidOperationException("Não foi possível extrair o bloco SQL do seed-known-products.ps1.");
        }

        var statements = SqlInsertParser.Parse(sqlMatch.Groups["sql"].Value);
        var knownProductRows = statements.Where(x => x.TableName.Equals("known_products", StringComparison.OrdinalIgnoreCase)).SelectMany(x => x.Rows).ToList();

        var products = knownProductRows.Select(row =>
        {
            var product = new KnownProduct
            {
                Name = ReadString(row, "name") ?? string.Empty,
                Brand = ReadString(row, "brand") ?? string.Empty,
                Category = ReadString(row, "category") ?? string.Empty,
                Barcode = ReadString(row, "barcode"),
                Keywords = ReadString(row, "keywords") ?? string.Empty,
                KnownFrontText = ReadString(row, "known_front_text"),
                KnownIngredients = ReadString(row, "known_ingredients"),
                KnownAllergens = ReadString(row, "known_allergens"),
                IsValidated = ReadBool(row, "is_validated", true),
                IdentificationCount = ReadInt(row, "identification_count", 0),
                LastIdentifiedAt = ReadDateTimeOffset(row, "last_identified_at")
            };

            product.UpdateSearchText();
            return product;
        }).ToList();

        await ReplaceCollectionAsync(_context.KnownProducts, products, cancellationToken);
        _logger.LogInformation("Seed de known_products aplicado. Records={Count}", products.Count);
    }

    private static async Task ReplaceCollectionAsync<TDocument>(IMongoCollection<TDocument> collection, IReadOnlyCollection<TDocument> documents, CancellationToken cancellationToken)
    {
        await collection.DeleteManyAsync(Builders<TDocument>.Filter.Empty, cancellationToken);
        if (documents.Count > 0)
        {
            await collection.InsertManyAsync(documents, cancellationToken: cancellationToken);
        }
    }

    private static string NormalizeAlias(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var ch in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        return Regex.Replace(builder.ToString(), "\\s+", " ").Trim();
    }

    private static string RemoveAccents(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Concat(
            value.Normalize(NormalizationForm.FormD)
                .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark))
            .Normalize(NormalizationForm.FormC)
            .Trim();
    }

    private static string? ReadString(IReadOnlyDictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var value) ? value?.ToString() : null;

    private static bool ReadBool(IReadOnlyDictionary<string, object?> row, string key, bool defaultValue)
        => row.TryGetValue(key, out var value) && value is not null ? Convert.ToBoolean(value, CultureInfo.InvariantCulture) : defaultValue;

    private static int ReadInt(IReadOnlyDictionary<string, object?> row, string key, int defaultValue)
        => row.TryGetValue(key, out var value) && value is not null ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : defaultValue;

    private static int? ReadIntNullable(IReadOnlyDictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var value) && value is not null ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : null;

    private static decimal? ReadDecimal(IReadOnlyDictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var value) && value is not null ? Convert.ToDecimal(value, CultureInfo.InvariantCulture) : null;

    private static DateTimeOffset? ReadDateTimeOffset(IReadOnlyDictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
            _ => DateTimeOffset.TryParse(value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) ? parsed : null
        };
    }

    private sealed class SqlInsertStatement
    {
        public required string TableName { get; init; }
        public required List<Dictionary<string, object?>> Rows { get; init; }
    }

    private static class SqlInsertParser
    {
        private static readonly Regex InsertRegex = new(
            @"INSERT\s+INTO\s+(?<table>[a-zA-Z0-9_]+)\s*\((?<columns>.*?)\)\s*VALUES\s*(?<values>.*?);",
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static List<SqlInsertStatement> Parse(string sql)
        {
            var sanitized = Regex.Replace(sql, @"--.*?$", string.Empty, RegexOptions.Multiline);
            var statements = new List<SqlInsertStatement>();

            foreach (Match match in InsertRegex.Matches(sanitized))
            {
                var table = match.Groups["table"].Value.Trim();
                var columns = match.Groups["columns"].Value
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(x => x.Trim())
                    .ToArray();

                var rows = ParseRows(match.Groups["values"].Value, columns);
                statements.Add(new SqlInsertStatement
                {
                    TableName = table,
                    Rows = rows
                });
            }

            return statements;
        }

        private static List<Dictionary<string, object?>> ParseRows(string valuesSql, string[] columns)
        {
            var rowTokens = ExtractRowTokens(valuesSql);
            var rows = new List<Dictionary<string, object?>>(rowTokens.Count);

            foreach (var rowToken in rowTokens)
            {
                var values = SplitValues(rowToken);
                if (values.Count != columns.Length)
                {
                    throw new InvalidOperationException($"Quantidade de colunas inválida ao processar seed SQL. Esperado={columns.Length}, Obtido={values.Count}");
                }

                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < columns.Length; i++)
                {
                    row[columns[i]] = ParseValue(values[i]);
                }

                rows.Add(row);
            }

            return rows;
        }

        private static List<string> ExtractRowTokens(string sql)
        {
            var rows = new List<string>();
            var depth = 0;
            var inString = false;
            var start = -1;

            for (var i = 0; i < sql.Length; i++)
            {
                var ch = sql[i];
                if (ch == '\'' && (i == 0 || sql[i - 1] != '\\'))
                {
                    if (inString && i + 1 < sql.Length && sql[i + 1] == '\'')
                    {
                        i++;
                        continue;
                    }

                    inString = !inString;
                    continue;
                }

                if (inString)
                {
                    continue;
                }

                if (ch == '(')
                {
                    if (depth == 0)
                    {
                        start = i + 1;
                    }
                    depth++;
                }
                else if (ch == ')')
                {
                    depth--;
                    if (depth == 0 && start >= 0)
                    {
                        rows.Add(sql[start..i]);
                        start = -1;
                    }
                }
            }

            return rows;
        }

        private static List<string> SplitValues(string rowToken)
        {
            var values = new List<string>();
            var current = new StringBuilder();
            var depth = 0;
            var inString = false;

            for (var i = 0; i < rowToken.Length; i++)
            {
                var ch = rowToken[i];
                if (ch == '\'' && (i == 0 || rowToken[i - 1] != '\\'))
                {
                    if (inString && i + 1 < rowToken.Length && rowToken[i + 1] == '\'')
                    {
                        current.Append("''");
                        i++;
                        continue;
                    }

                    inString = !inString;
                    current.Append(ch);
                    continue;
                }

                if (!inString)
                {
                    if (ch == '(')
                    {
                        depth++;
                    }
                    else if (ch == ')')
                    {
                        depth--;
                    }
                    else if (ch == ',' && depth == 0)
                    {
                        values.Add(current.ToString().Trim());
                        current.Clear();
                        continue;
                    }
                }

                current.Append(ch);
            }

            if (current.Length > 0)
            {
                values.Add(current.ToString().Trim());
            }

            return values;
        }

        private static object? ParseValue(string token)
        {
            var value = token.Trim();
            if (value.Equals("NULL", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (value.Equals("TRUE", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (value.Equals("FALSE", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (value.Equals("NOW()", StringComparison.OrdinalIgnoreCase) || value.Equals("CURRENT_TIMESTAMP", StringComparison.OrdinalIgnoreCase))
            {
                return DateTime.UtcNow;
            }

            if (value.Equals("gen_random_uuid()", StringComparison.OrdinalIgnoreCase))
            {
                return Guid.NewGuid();
            }

            if (value.StartsWith('\'') && value.EndsWith('\''))
            {
                return value[1..^1].Replace("''", "'");
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
            {
                return intValue;
            }

            if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
            {
                return decimalValue;
            }

            return value;
        }
    }
}
