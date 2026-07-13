using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LabelWise.Application.Configuration;
using LabelWise.Application.DTOs.Nutrition;
using LabelWise.Application.Interfaces;
using LabelWise.Application.Models.Nutrition;
using LabelWise.Infrastructure.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LabelWise.Infrastructure.AI;

/// <summary>
/// Implementação de extração nutricional via OpenAI Chat Completions API.
/// 
/// Fluxo:
///   1. Converte imagem para base64
///   2. Envia para OpenAI /v1/chat/completions com vision
///   3. Recebe JSON estruturado
///   4. Mapeia para EstimatedNutritionProfileDto
/// 
/// NÃO realiza validação, cálculo ou inferência.
/// Apenas extrai dados VISÍVEIS na imagem.
/// </summary>
public sealed class OpenAiNutritionImageAnalyzer : INutritionImageAnalyzer
{
    private readonly HttpClient _httpClient;
    private readonly AzureOpenAiVisionOptions _options;
    private readonly ILogger<OpenAiNutritionImageAnalyzer> _logger;
    private readonly INutritionConfidenceEngine _nutritionConfidenceEngine;
    private readonly IImageAnalysisCacheService _imageAnalysisCacheService;
    private readonly INutritionFingerprintService _nutritionFingerprintService;
    private readonly IDocumentIntelligenceService _documentIntelligenceService;

    private const string AnalyzerCacheVersion = "openai-nutrition-v10-micro-serving-sodium-guard";

    private const string SystemPrompt = "Você é um sistema de OCR/extração de dados nutricionais ESTRITO. Você APENAS lê números VISÍVEIS E LEGÍVEIS na imagem. Você NUNCA calcula, estima, infere ou deduz valores. Se um número não está COMPLETAMENTE VISÍVEL e NÍTIDO, você retorna null para aquele campo. Você prefere retornar um JSON com muitos campos null do que inventar um único número. Sempre retorne APENAS JSON válido, sem explicações.";

    private const string UserPrompt = @"TAREFA: Ler a tabela nutricional da imagem e devolver os valores como JSON.

════════════════════════════════════════════════════════════════
REGRA CRÍTICA — EXISTÊNCIA DA TABELA NUTRICIONAL
════════════════════════════════════════════════════════════════

Antes de qualquer extração, verifique:

A imagem contém uma TABELA NUTRICIONAL VISÍVEL, com estrutura de linhas e colunas?

Se a resposta for NÃO:

→ TODOS os campos nutricionais devem ser null
→ NÃO retorne nenhum número

É PROIBIDO:
  • usar conhecimento do produto
  • usar valores típicos
  • usar memória de alimentos similares
  • preencher qualquer campo sem tabela visível

Exemplo:
Imagem mostra apenas frente da embalagem sem tabela →
→ nutritionPer100g = null
→ nutritionPerServing = null
→ nutritionPer100ml = null


════════════════════════════════════════════════════════════════
PRINCÍPIO ABSOLUTO — LEIA ISTO PRIMEIRO
════════════════════════════════════════════════════════════════
Você é um leitor OCR LITERAL. Cada campo do JSON deve ser o resultado
de uma LEITURA VISUAL DIRETA de um número IMPRESSO e LEGÍVEL na imagem.
Você NUNCA calcula, estima, infere ou deduz valores.

Se você não consegue ver o número COMPLETO e NÍTIDO → null.

❌ EXEMPLOS DO QUE NÃO FAZER (PROIBIDO):
  • Calcular per100g multiplicando perServing por (100 / porção)
  • Calcular perServing dividindo per100g por (100 / porção)
  • Ver carbs=10g e assumir que sugar deve ser ~8g
  • Ver gordura=5g e assumir saturada=3g, trans=0
  • Ver parte da coluna coberta e 'completar' com valores típicos

✅ O QUE VOCÊ DEVE FAZER:
  • Ler APENAS números que estão COMPLETAMENTE VISÍVEIS
  • Retornar null se houver QUALQUER dúvida
  • Preferir 90% de campos null do que 1 valor inventado

REGRA ZERO — PROIBIDO INVENTAR NÚMEROS
Se um número estiver ilegível, desfocado, cortado, parcialmente visível,
coberto por dedo/objeto, com reflexo, ou houver QUALQUER dúvida sobre
o dígito exato, retorne null.

É PROIBIDO 'completar' valores por:
  • consistência matemática entre colunas (porção vs 100g)
  • padrões típicos de rótulos
  • valores esperados/'prováveis'
  • contexto do produto
  • conhecimento prévio sobre o tipo de alimento

════════════════════════════════════════════════════════════════
REGRA 1 — VALOR ENERGÉTICO (CALORIAS) ← CRÍTICO
════════════════════════════════════════════════════════════════
PROIBIDO calcular calorias a partir de gorduras, proteínas e
carboidratos, mesmo que você consiga ler todos eles.
Calorias só podem ser extraídas se estiverem VISIVELMENTE
impressas na linha 'Valor energético / Valor calórico / kcal'.

Se a linha de calorias estiver:
  • bloqueada por dedo, mão ou objeto → null
  • com reflexo ou brilho cobrindo o número → null
  • cortada ou fora do enquadramento → null
  • ilegível por qualquer motivo → null

════════════════════════════════════════════════════════════════
REGRA 2 — INDEPENDÊNCIA ABSOLUTA DOS CAMPOS
════════════════════════════════════════════════════════════════
🚫 PROIBIDO FAZER QUALQUER CÁLCULO ENTRE CAMPOS:
  • Calcular per100g a partir de porção × fator
  • Calcular porção a partir de per100g ÷ fator
  • Deduzir açúcar a partir de carboidratos
  • Deduzir gordura saturada a partir de gordura total
  • Completar qualquer campo por contexto ou proximidade

📋 REGRA DE VERIFICAÇÃO OBRIGATÓRIA:
Antes de preencher um campo, pergunte a si mesmo:
  'Eu estou VENDO este número IMPRESSO nesta célula específica
   da tabela, OU estou calculando/inferindo?'

Se a resposta for 'calculando/inferindo' → null.

🔍 TESTE PRÁTICO — Coluna por 100g:
Se a coluna por 100g estiver PARCIALMENTE COBERTA (ex: dedo na frente),
você deve retornar:
  'nutritionPer100g': null  ← CORRETO

Você NÃO deve:
  • Pegar os valores de perServing
  • Multiplicar por (100 / porção)
  • Preencher per100g com o resultado ← ERRADO, É CÁLCULO

Se o valor de uma coluna não está visivelmente impresso na linha
correta daquela coluna → null para esse campo/coluna.

════════════════════════════════════════════════════════════════
REGRA 3 — OBSTRUÇÃO E QUALIDADE DE LEITURA
════════════════════════════════════════════════════════════════
Retorne null quando o campo:
  a) Estiver fisicamente obstruído (dedo, mão, objeto)
  b) Tiver reflexo, brilho ou sombra cobrindo o número
  c) Estiver borrado, cortado ou distorcido
  d) Não estiver claramente alinhado na linha do nutriente
  e) Deixar qualquer dúvida sobre o valor correto

Precisão > Completude. Retornar menos campos corretos é
preferível a retornar mais campos com valores duvidosos.

🖐️ CASO ESPECIAL: OBSTRUÇÃO POR DEDO/MÃO - LEITURA PARCIAL PERMITIDA

Se há um dedo ou mão cobrindo PARTE de uma coluna:
  → Retorne null APENAS para os CAMPOS ESPECÍFICOS que estão cobertos
  → Leia NORMALMENTE os campos que estão VISÍVEIS na mesma coluna
  → NUNCA calcule valores para campos cobertos

IMPORTANTE: Obstrução PARCIAL é diferente de Coluna inteira invisível

Exemplo: Dedo cobrindo APENAS a linha de calorias na coluna por 100g.
Se você consegue ver na coluna por 100g:
  - Proteínas: 8g (visível) → preencha proteins: 8
  - Carboidratos: 12g (visível) → preencha carbohydrates: 12
  - Gorduras: 60g (visível) → preencha totalFats: 60
  - Calorias: coberta pelo dedo → preencha caloriesKcal: null

Resposta CORRETA:
  nutritionPer100g com alguns campos preenchidos e outros null

Resposta ERRADA:
  nutritionPer100g: null (anular coluna inteira quando parte está visível)

IMPORTANTE (SÓDIO): o campo sodiumMg (mg) frequentemente fica ilegível/desfocado.
Se você NÃO conseguir ler o número com certeza na linha do sódio, retorne sodiumMg = null.
Para sodiumMg = 0 em coluna por 100 g/100 ml, só retorne 0 se o número 0 estiver VISÍVEL nessa mesma coluna.
Se a porção for muito pequena (ex.: gotas) e apenas a coluna por porção mostrar 0 mg, isso NÃO prova que a coluna por 100 g/100 ml seja 0; nesse caso, retorne null para sodiumMg da coluna por 100 g/100 ml se ela não estiver nítida.
Em tabelas com colunas como '100 ml', '0,16 ml' e '%VD', mantenha cada número na coluna correta. Exemplo: na linha 'Sódio (mg)', se a coluna '100 ml' mostra 777 e a coluna '0,16 ml' mostra 0, então nutritionPer100ml.sodiumMg = 777 e nutritionPerServing.sodiumMg = 0. NUNCA copie o 0 da porção para a coluna 100 ml.

════════════════════════════════════════════════════════════════
REGRA 4 — ZERO vs AUSENTE
════════════════════════════════════════════════════════════════
  • '0', '0g', '0,0g', '0,00g', '0mg' → retorne 0 (numérico)
  • Frases como 'Não contém quantidades significativas de X',
    'SIN AZÚCARES', 'SIN GRASAS', 'SIN SODIO' → retorne 0
    para os nutrientes mencionados
  • Campo inexistente ou ilegível → null

════════════════════════════════════════════════════════════════
REGRA 5 — MÚLTIPLAS TABELAS (Mercosul: PT-BR e ES)
════════════════════════════════════════════════════════════════
  a) Inspecione TODAS as tabelas, inclusive rotacionadas
  b) Conte campos preenchidos em cada tabela (caloriesKcal,
     carbohydrates, sugar, addedSugar, polyols, proteins, totalFats,
     saturatedFats, transFats, fiber, sodiumMg)
  c) Use a tabela com MAIS campos preenchidos
  d) Em empate: prefira a que tem sugar e/ou addedSugar
  e) NUNCA misture valores de tabelas diferentes

════════════════════════════════════════════════════════════════
REGRA 6 — COLUNAS
════════════════════════════════════════════════════════════════
  • Coluna 'por porção' → preencha nutritionPerServing
  • Coluna 'por 100 g' → preencha nutritionPer100g
  • Coluna 'por 100 ml' → preencha nutritionPer100ml
  • Preencha todos os blocos simultaneamente se visíveis
  • Nunca misture valores entre colunas diferentes
  • Ignore SEMPRE colunas de percentual: '%VD', '%V.D.', '%', 'VD'.
    Valores percentuais NUNCA são nutrientes e NUNCA devem preencher campos.
  • Se NÃO existir uma coluna explícita 'por porção', 'por unidad',
    'por unidade' ou equivalente, retorne nutritionPerServing = null.
  • NUNCA copie os mesmos valores de nutritionPer100g para
    nutritionPerServing. Se só há coluna 'por 100g', apenas
    nutritionPer100g deve ser preenchido.

ATENÇÃO PARA TABELAS COM DUAS SEÇÕES LADO A LADO:

Alguns rótulos dividem a tabela em duas metades no mesmo quadro:
nutrientes à esquerda e nutrientes à direita, compartilhando cabeçalhos
como '100 g', '30 g' e '%VD'.

REGRA OBRIGATÓRIA:
  • Associe cada número ao nutriente da MESMA LINHA horizontal.
  • NÃO use números da linha acima ou abaixo.
  • NÃO copie 'gorduras totais' para 'proteínas'.
  • NÃO copie '%VD' para nenhum nutriente.
  • Em rótulos com colunas '100 ml' e 'porção' muito pequena, a coluna '100 ml' tem prioridade para nutritionPer100ml. O valor da porção não deve substituir o valor de 100 ml.
  • Se a linha estiver pequena, inclinada ou ambígua → null.

ATENÇÃO PARA RÓTULOS EM ESPANHOL:

• 'Valor energético (kJ/kcal)' pode mostrar dois números, ex.: 419/99.

REGRA OBRIGATÓRIA:
  • O PRIMEIRO número corresponde a kJ
  • O SEGUNDO número corresponde a kcal

Portanto:
  caloriesKcal = SEGUNDO número

⚠️ IMPORTANTE:
  • NÃO inverter a ordem
  • NÃO escolher o maior número
  • NÃO usar heurística

Se NÃO for possível identificar claramente os dois números separados:
  → caloriesKcal = null
  • 'Grasas' → totalFats
  • 'de las cuales saturadas' → saturatedFats
  • 'Hidratos de carbono' → carbohydrates
  • 'de los cuales azúcares' → sugar
  • 'Proteínas' → proteins
  • 'Sal (g)' não é sódio. Para sodiumMg, só é permitido converter se o
    número de sal estiver VISÍVEL: Se a tabela mostrar apenas ""Sal (g)"" e NÃO mostrar ""Sódio (mg)"":

→ sodiumMg = null

É PROIBIDO converter sal para sódio.. Se houver dúvida,
    retorne sodiumMg = null.

════════════════════════════════════════════════════════════════
REGRA 7 — NORMALIZAÇÃO OCR (sem alterar valores)
════════════════════════════════════════════════════════════════
  • 'acucares', 'azucares' → sugar
  • 'poliois', 'polióis', 'polioles' → polyols
  • 'proteinas', 'prote1nas' → proteins
  • 'sodio', 'sod1o' → sodiumMg
  • 'gorduras totais', 'grasas totales' → totalFats
  • 'carboidrato' ≠ 'açúcar' — NÃO copie carbs para sugar

════════════════════════════════════════════════════════════════
REGRA 8 — MÚLTIPLAS IMAGENS
════════════════════════════════════════════════════════════════
Quando receber mais de uma imagem, todas são versões da MESMA foto:
imagem inteira, rotações e recortes ampliados. Use o recorte em que a
tabela nutricional estiver MAIS NÍTIDA. NUNCA combine valores de produtos
diferentes. Se houver conflito entre imagem inteira e recorte ampliado,
prefira o valor VISÍVEL e LEGÍVEL no recorte ampliado.

════════════════════════════════════════════════════════════════
REGRA 9 — METADADOS DO PRODUTO (productName / brand / serving)
════════════════════════════════════════════════════════════════
É PROIBIDO inferir, deduzir ou adivinhar qualquer um destes
campos a partir de conhecimento prévio, formato da embalagem,
cor, contexto ou similaridade com produtos conhecidos.

  • productName → null se o nome do produto não estiver
    impresso e legível DENTRO da imagem recebida
  • brand → null se a marca não estiver impressa e legível
    DENTRO da imagem recebida
  • serving.amount / unit / description → null se a linha
    'Porção: X g/ml (Y medida caseira)' NÃO estiver visível
    e legível na imagem. NUNCA use um número que apareça
    DENTRO da grade de valores nutricionais como porção.

Se a imagem mostra APENAS o recorte da tabela nutricional
(sem cabeçalho do produto e sem a frase de porção),
TODOS os campos acima devem ser null.

REGRA CRÍTICA — IDENTIFICAÇÃO DE COLUNA

Se a tabela contém apenas UMA coluna de valores (ex: apenas ""por 100g""):

• Preencha SOMENTE nutritionPer100g
• Retorne nutritionPerServing = null
• Retorne nutritionPer100ml = null

É PROIBIDO assumir que qualquer outra coluna representa porção,
mesmo que contenha texto como ""%"", ""%VD"", ""% por unidade"".

REGRA CRÍTICA — ISOLAMENTO TOTAL

Cada célula da tabela deve ser tratada como independente.

Se um valor não está visível EXATAMENTE na célula:
→ null

É PROIBIDO usar:
• outra coluna
• outro nutriente
• proporções
• qualquer relação matemática


Se NÃO houver linha explícita contendo ""polióis"", ""poliois"" ou ""polioles"":

→ polyols = null

É PROIBIDO inferir polióis a partir de ""sem açúcar"" ou outros dados.

════════════════════════════════════════════════════════════════
REGRA CRÍTICA — NÃO AJUSTAR POR COERÊNCIA
════════════════════════════════════════════════════════════════

Você NÃO deve verificar consistência entre colunas.

Exemplo:
Se 100g mostra 33g de proteína e porção mostra 8g:
→ você deve retornar EXATAMENTE o número VISÍVEL na porção (8g)

É PROIBIDO:
  • ajustar valores para manter proporção
  • corrigir números “aparentemente errados”
  • recalcular baseado em outra coluna

Cada célula é independente.

Se houver linha separada de ""açúcares adicionados"":
→ preencher addedSugar
→ NÃO deixar null

════════════════════════════════════════════════════════════════
ESTRUTURA DE SAÍDA (JSON puro, sem texto fora do JSON)
════════════════════════════════════════════════════════════════
{
  ""productName"": string | null,
  ""brand"": string | null,
  ""serving"": {
    ""amount"": number | null,
    ""unit"": ""g"" | ""ml"" | null,
    ""description"": string | null
  },
  ""nutritionPerServing"": {
    ""caloriesKcal"": number | null,
    ""carbohydrates"": number | null,
    ""sugar"": number | null,
    ""addedSugar"": number | null,
    ""polyols"": number | null,
    ""proteins"": number | null,
    ""totalFats"": number | null,
    ""saturatedFats"": number | null,
    ""transFats"": number | null,
    ""fiber"": number | null,
    ""sodiumMg"": number | null
  },
  ""nutritionPer100g"": {
    ""caloriesKcal"": number | null,
    ""carbohydrates"": number | null,
    ""sugar"": number | null,
    ""addedSugar"": number | null,
    ""polyols"": number | null,
    ""proteins"": number | null,
    ""totalFats"": number | null,
    ""saturatedFats"": number | null,
    ""transFats"": number | null,
    ""fiber"": number | null,
    ""sodiumMg"": number | null
  },
  ""nutritionPer100ml"": {
    ""caloriesKcal"": number | null,
    ""carbohydrates"": number | null,
    ""sugar"": number | null,
    ""addedSugar"": number | null,
    ""polyols"": number | null,
    ""proteins"": number | null,
    ""totalFats"": number | null,
    ""saturatedFats"": number | null,
    ""transFats"": number | null,
    ""fiber"": number | null,
    ""sodiumMg"": number | null
  }
}

Bloco inteiro ausente na tabela → retorne o bloco como null.
Não retorne nada além do JSON.
";

    public OpenAiNutritionImageAnalyzer(
        IHttpClientFactory httpClientFactory,
        IOptions<AzureOpenAiVisionOptions> options,
        ILogger<OpenAiNutritionImageAnalyzer> logger,
        INutritionConfidenceEngine nutritionConfidenceEngine,
        IImageAnalysisCacheService imageAnalysisCacheService,
        INutritionFingerprintService nutritionFingerprintService,
        IDocumentIntelligenceService documentIntelligenceService)
    {
        _httpClient = httpClientFactory.CreateClient("OpenAI");
        _options = options.Value;
        _logger = logger;
        _nutritionConfidenceEngine = nutritionConfidenceEngine;
        _imageAnalysisCacheService = imageAnalysisCacheService;
        _nutritionFingerprintService = nutritionFingerprintService;
        _documentIntelligenceService = documentIntelligenceService;

        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri(_options.Endpoint);
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<EstimatedNutritionProfileDto?> AnalyzeAsync(
        byte[] imageBytes,
        string? mimeType,
        string? precomputedExactHash = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("[OpenAI] ═══ Iniciando análise via Chat Completions API ═══");

            // ── Validação CRÍTICA da imagem ────────────────────────────────
            if (imageBytes == null || imageBytes.Length == 0)
            {
                _logger.LogError("[OpenAI] ❌ Imagem vazia ou null");
                return null;
            }

            // ✅ Validar tamanho mínimo para OCR confiável
            if (imageBytes.Length < 20_000)
            {
                _logger.LogError("[OpenAI] ❌ Imagem muito pequena ({Size} bytes). Mínimo: 20000 bytes para OCR",
                    imageBytes.Length);
                return null;
            }

            if (IsLikelyGrayscale(imageBytes))
            {
                _logger.LogWarning("[OpenAI] ⚠️ Imagem aparenta estar monocromática. Prosseguindo com a análise por ser uma tabela nutricional.");
            }

            _logger.LogInformation("[OpenAI] ✅ Imagem válida: {Size} bytes ({SizeKB} KB)", 
                imageBytes.Length, imageBytes.Length / 1024);

            // ── Verificar Cache de Imagem ──────────────────────────────────
            var exactHash = !string.IsNullOrWhiteSpace(precomputedExactHash)
                ? precomputedExactHash
                : _imageAnalysisCacheService.ComputeExactHash(imageBytes);
            exactHash = BuildVersionedExactHash(exactHash);
            _logger.LogInformation("[OpenAI] 🔑 Exact hash: {Hash} (precomputed={Precomputed})",
                exactHash, !string.IsNullOrWhiteSpace(precomputedExactHash));

            var exactMatch = await _imageAnalysisCacheService.GetByExactHashAsync(exactHash, cancellationToken);
            if (exactMatch != null)
            {
                _logger.LogInformation("[OpenAI] 🎯 Cache HIT (hash exato). Retornando análise do cache.");
                var resolvedExactMatch = await ResolveMissingFieldsFromCompatibleCacheAsync(exactMatch, cancellationToken);
                if (!ReferenceEquals(resolvedExactMatch, exactMatch))
                {
                    var exactPerceptualHashes = _imageAnalysisCacheService.ComputePerceptualHashes(imageBytes);
                    await _imageAnalysisCacheService.SaveCacheAsync(
                        exactHash,
                        exactPerceptualHashes,
                        AnalyzerCacheVersion,
                        resolvedExactMatch,
                        cancellationToken);
                }

                return resolvedExactMatch;
            }

            var perceptualHashes = _imageAnalysisCacheService.ComputePerceptualHashes(imageBytes);
            _logger.LogInformation("[OpenAI] 🔑 Perceptual hashes: {Count}", perceptualHashes.Count);

            var visualMatch = await _imageAnalysisCacheService.FindSimilarAsync(
                perceptualHashes,
                AnalyzerCacheVersion,
                cancellationToken);

            if (visualMatch != null)
            {
                _logger.LogInformation("[OpenAI] 🎯 Cache HIT visual por pHash/rotação. Retornando análise do cache.");
                return visualMatch;
            }

            _logger.LogInformation("[OpenAI] ❄️ Cache MISS. Prosseguindo para chamada OpenAI Vision.");

            // ✅ Salvar imagem para debug (comparação com Playground)
            await SaveDebugImageAsync(imageBytes, mimeType);

            var base64Image = Convert.ToBase64String(imageBytes);
            var resolvedMimeType = ResolveMimeType(mimeType, base64Image);

            _logger.LogInformation("[OpenAI] Image size: {Size}", imageBytes.Length);
            _logger.LogInformation("[OpenAI] Base64 length: {Length}", base64Image.Length);
            _logger.LogInformation("[OpenAI] ✅ Base64 gerado: {Length} chars",
                base64Image.Length);
            _logger.LogInformation("[OpenAI] MIME type preservado: {MimeType}", resolvedMimeType);

            // ── Construir request com imagem inteira + recortes ampliados ──
            // A tabela nutricional costuma ocupar uma área pequena da embalagem.
            // Recortes verticais sobrepostos aumentam a legibilidade sem depender
            // de heurística de produto ou posição fixa do rótulo.
            var imageParts = BuildOcrImageParts(imageBytes, resolvedMimeType, base64Image);
            var requestBody = BuildRequestBody(imageParts);

            _logger.LogInformation("[OpenAI] ═══ Request Configuration ═══");
            _logger.LogInformation("[OpenAI]   → Endpoint: {Endpoint}", _options.Endpoint);
            _logger.LogInformation("[OpenAI]   → Model: {Model}", _options.Model);
            _logger.LogInformation("[OpenAI]   → Max Tokens: 900");
            _logger.LogInformation("[OpenAI]   → Temperature: 0 (deterministic)");
            _logger.LogInformation("[OpenAI]   → Prompt length: {Length} chars", UserPrompt.Length);

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            // ── Enviar para OpenAI ─────────────────────────────────────────
            _logger.LogInformation("[OpenAI] 🚀 Enviando requisição...");
            var response = await _httpClient.PostAsync("", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "[OpenAI] ❌ Falha na requisição. Status={Status}, Body={Body}",
                    response.StatusCode, errorContent);
                return null;
            }

            // ── Processar resposta ─────────────────────────────────────────
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogInformation("[OpenAI] ✅ Resposta recebida: {Length} chars",
                responseBody.Length);

            // Durante a fase de calibração, logamos o JSON cru em Information
            // para auditoria rápida (escolha de tabela, campos vazios etc.).
            _logger.LogInformation("[OpenAI] 📦 Raw response body: {Response}", responseBody);

            var result = ParseOpenAiResponse(responseBody);

            if (result == null)
            {
                _logger.LogWarning("[OpenAI] ⚠️ Falha ao parsear resposta");
                return null;
            }

            // ── Validar resultado ──────────────────────────────────────────
            var validationResult = ValidateAiResult(result);

            // 🚨 REJEITAR COMPLETAMENTE se detectou alucinação
            if (!validationResult.IsValid)
            {
                _logger.LogError(
                    "[OpenAI] ❌ Resposta REJEITADA por falhar na validação: {Errors}",
                    string.Join("; ", validationResult.Errors));
                return null;
            }

            // ── Validação anti-alucinação: rejeita respostas sem dados reais ──
            if (!HasMinimumNutritionData(result))
            {
                _logger.LogWarning(
                    "[OpenAI] ❌ Resposta rejeitada: imagem não contém tabela nutricional legível. " +
                    "Per100g={HasPer100g}, Per100ml={HasPer100ml}, PerServing={HasPerServing}",
                    result.NutritionPer100g != null,
                    result.NutritionPer100ml != null,
                    result.NutritionPerServing != null);
                return null;
            }

            var profile = MapToNutritionProfile(result);
            var servingDiscardWarning = validationResult.Warnings.FirstOrDefault(w =>
                w.StartsWith("Coluna por porção descartada", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(servingDiscardWarning))
            {
                profile.DataSource["ServingColumnDiscarded"] = "true";
                profile.DataSource["ServingColumnDiscardReason"] = servingDiscardWarning;
            }

            var consensus = await ApplyDocumentIntelligenceConsensusAsync(profile, imageBytes, cancellationToken);
            if (!consensus.Accepted)
            {
                _logger.LogWarning(
                    "[OpenAI] ⚠️ Consenso OCR não aceitou a extração, mas a leitura OpenAI será mantida em baixa confiança. Reason={Reason}",
                    consensus.Reason);
                profile.ParserConfidence = "low";
                profile.DataSource["ConsensusDecision"] = "OpenAIRetainedAfterConsensusReject";
                profile.DataSource["ConsensusReason"] = consensus.Reason;
            }

            profile.NutritionConfidence = _nutritionConfidenceEngine.Evaluate(ToConfidenceInput(result));
            if (validationResult.Warnings.Count > 0)
            {
                profile.DataSource["ValidationWarnings"] = string.Join(" | ", validationResult.Warnings);
                if (profile.NutritionConfidence?.GlobalScore is double current)
                    profile.NutritionConfidence.GlobalScore = Math.Min(current, validationResult.Warnings.Count >= 3 ? 0.55 : 0.70);
                profile.ParserConfidence = validationResult.Warnings.Count >= 3 ? "low" : "medium";
            }
            if (profile.IsPer100Derived)
            {
                profile.DataSource["DerivedPer100"] = "true";
                profile.DataSource["DerivedPer100Reason"] = "Base 100g/100ml calculada a partir de porção visível; tratada como leitura parcial.";
                if (profile.NutritionConfidence?.GlobalScore is double derivedConfidence)
                    profile.NutritionConfidence.GlobalScore = Math.Min(derivedConfidence, 0.65);
                profile.ParserConfidence = "medium";
            }
            profile = await ResolveMissingFieldsFromCompatibleCacheAsync(profile, cancellationToken);

            _logger.LogInformation(
                "[OpenAI] ✅ Análise concluída — Calorias={Cal}, Proteínas={Prot}, Carbs={Carbs}, Açúcar={Sugar}, Fibra={Fiber}, Confidence={Confidence}",
                profile.CaloriesPer100g, profile.EstimatedProteinPer100g, 
                profile.EstimatedCarbsPer100g, profile.EstimatedSugarPer100g, 
                profile.EstimatedFiberPer100g,
                profile.NutritionConfidence?.GlobalScore ?? 0);

            // ── Cache de Fingerprint Nutricional ───────────────────────────
            var fingerprint = _nutritionFingerprintService.GenerateFingerprint(profile);
            var fingerprintCached = await _nutritionFingerprintService.FindByFingerprintAsync(fingerprint, cancellationToken);

            if (fingerprintCached != null)
            {
                if (profile.NutritionConfidence?.GlobalScore < 0.85 || profile.IsPer100Derived)
                {
                    profile.DataSource["FingerprintCacheDecision"] = "SkippedForPartialRead";
                    _logger.LogInformation(
                        "[OpenAI] Cache por fingerprint ignorado: leitura atual parcial/baixa confiança. Confidence={Confidence}, Derived={Derived}",
                        profile.NutritionConfidence?.GlobalScore, profile.IsPer100Derived);
                    return profile;
                }

                _logger.LogInformation("[OpenAI] 🎯 Cache HIT (conteúdo nutricional via Fingerprint).");
                var exactCacheSeeded = await _imageAnalysisCacheService.SaveCacheAsync(
                    exactHash,
                    perceptualHashes,
                    AnalyzerCacheVersion,
                    fingerprintCached,
                    cancellationToken);
                _logger.LogInformation(
                    "[OpenAI] 💾 Cache exato atualizado a partir do fingerprint. ImageCacheSaved={ImageCacheSaved}",
                    exactCacheSeeded);
                return fingerprintCached;
            }

            // ── Salvar no Cache ────────────────────────────────────────────
            if (profile.NutritionConfidence?.GlobalScore >= 0.7)
            {
                var imageCacheSaved = await _imageAnalysisCacheService.SaveCacheAsync(
                    exactHash,
                    perceptualHashes,
                    AnalyzerCacheVersion,
                    profile,
                    cancellationToken);
                await _nutritionFingerprintService.SaveAsync(fingerprint, profile, profile.NutritionConfidence.GlobalScore, cancellationToken);
                _logger.LogInformation(
                    "[OpenAI] 💾 Persistência de cache concluída. ImageCacheSaved={ImageCacheSaved}, NutritionFingerprintSaved=True",
                    imageCacheSaved);
            }

            return profile;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("[OpenAI] ⏱️ Timeout na requisição (30s)");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OpenAI] ❌ Erro inesperado na análise");
            return null;
        }
    }

    private async Task<EstimatedNutritionProfileDto> ResolveMissingFieldsFromCompatibleCacheAsync(
        EstimatedNutritionProfileDto profile,
        CancellationToken cancellationToken)
    {
        var compatible = await _nutritionFingerprintService.FindCompatibleMoreCompleteAsync(profile, cancellationToken);
        if (compatible is null)
            return profile;

        _logger.LogInformation(
            "[OpenAI] Cache nutricional compatível encontrado, mas não usado para completar campos ausentes. Sodium={Sodium}, Protein={Protein}, Sugar={Sugar}, Fat={Fat}",
            compatible.EstimatedSodiumPer100g,
            compatible.EstimatedProteinPer100g,
            compatible.EstimatedSugarPer100g,
            compatible.EstimatedFatPer100g);

        profile.DataSource["CompatibleCacheCompletion"] = "Skipped_NoAggressiveInference";
        return profile;
    }

    /// <summary>
    /// Salva imagem para debug (comparação com Playground).
    /// </summary>
    private async Task SaveDebugImageAsync(byte[] imageBytes, string? mimeType)
    {
        if (string.IsNullOrWhiteSpace(_options.DebugImagePath))
            return;

        try
        {
            if (!Directory.Exists(_options.DebugImagePath))
            {
                Directory.CreateDirectory(_options.DebugImagePath);
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var filename = $"debug_{timestamp}{GetFileExtension(mimeType)}";
            var fullPath = Path.Combine(_options.DebugImagePath, filename);

            await File.WriteAllBytesAsync(fullPath, imageBytes);

            _logger.LogInformation("[OpenAI] 💾 Imagem salva para debug: {Path}", fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[OpenAI] ⚠️ Falha ao salvar imagem de debug");
        }
    }

    private object BuildRequestBody(IReadOnlyList<(string Base64, string MimeType)> images)
    {
        var userContent = new List<object> { new { type = "text", text = UserPrompt } };

        foreach (var (base64, mime) in images)
        {
            userContent.Add(new
            {
                type = "image_url",
                image_url = new { url = $"data:{mime};base64,{base64}", detail = "high" }
            });
        }

        return new
        {
            model = _options.Model,
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = userContent.ToArray() }
            },
            max_tokens = 900,
            temperature = 0  // ✅ Determinístico (igual ao Playground)
        };
    }

    private IReadOnlyList<(string Base64, string MimeType)> BuildOcrImageParts(
        byte[] imageBytes,
        string resolvedMimeType,
        string originalBase64)
    {
        var parts = new List<(string Base64, string MimeType)>
        {
            (originalBase64, resolvedMimeType)
        };

        try
        {
            var rotatedBytes = RotateClockwise90(imageBytes);
            parts.Add((Convert.ToBase64String(rotatedBytes), "image/jpeg"));
            _logger.LogInformation("[OpenAI] 🔄 Variante rotacionada 90°: {Size} bytes", rotatedBytes.Length);

            var crops = CreateVerticalZoomCrops(imageBytes);
            foreach (var crop in crops)
            {
                parts.Add((Convert.ToBase64String(crop.Bytes), "image/jpeg"));
                _logger.LogInformation(
                    "[OpenAI] 🔎 Recorte OCR {Name}: {Width}x{Height}, {Size} bytes",
                    crop.Name, crop.Width, crop.Height, crop.Bytes.Length);
            }

            var horizontalCrops = CreateHorizontalZoomCrops(imageBytes);
            foreach (var crop in horizontalCrops)
            {
                parts.Add((Convert.ToBase64String(crop.Bytes), "image/jpeg"));
                _logger.LogInformation(
                    "[OpenAI] 🔎 Recorte OCR {Name}: {Width}x{Height}, {Size} bytes",
                    crop.Name, crop.Width, crop.Height, crop.Bytes.Length);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[OpenAI] ⚠️ Falha ao gerar recortes ampliados. Usando apenas imagem original.");
        }

        return parts;
    }

    private static IReadOnlyList<OcrImageCrop> CreateVerticalZoomCrops(byte[] imageBytes)
    {
        using var source = Image.Load<Rgba32>(imageBytes);

        var bands = new (string Name, double StartRatio, double WidthRatio)[]
        {
            ("left", 0.00, 0.65),
            ("center", 0.15, 0.70),
            ("right", 0.35, 0.65)
        };

        var crops = new List<OcrImageCrop>(bands.Length);

        foreach (var (name, startRatio, widthRatio) in bands)
        {
            var x = Math.Clamp((int)Math.Round(source.Width * startRatio), 0, source.Width - 1);
            var width = Math.Clamp((int)Math.Round(source.Width * widthRatio), 1, source.Width - x);
            var rect = new Rectangle(x, 0, width, source.Height);

            using var crop = source.Clone(ctx => ctx.Crop(rect));
            ResizeForOcr(crop);

            using var output = new MemoryStream();
            crop.Save(output, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 95 });

            crops.Add(new OcrImageCrop(name, output.ToArray(), crop.Width, crop.Height));
        }

        return crops;
    }

    private static IReadOnlyList<OcrImageCrop> CreateHorizontalZoomCrops(byte[] imageBytes)
    {
        using var source = Image.Load<Rgba32>(imageBytes);

        var bands = new (string Name, double StartRatio, double HeightRatio)[]
        {
            ("top", 0.00, 0.55),
            ("middle", 0.22, 0.56),
            ("bottom", 0.45, 0.55)
        };

        var crops = new List<OcrImageCrop>(bands.Length);

        foreach (var (name, startRatio, heightRatio) in bands)
        {
            var y = Math.Clamp((int)Math.Round(source.Height * startRatio), 0, source.Height - 1);
            var height = Math.Clamp((int)Math.Round(source.Height * heightRatio), 1, source.Height - y);
            var rect = new Rectangle(0, y, source.Width, height);

            using var crop = source.Clone(ctx => ctx.Crop(rect));
            ResizeForOcr(crop);

            using var output = new MemoryStream();
            crop.Save(output, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 95 });

            crops.Add(new OcrImageCrop($"horizontal-{name}", output.ToArray(), crop.Width, crop.Height));
        }

        return crops;
    }

    private static void ResizeForOcr(Image<Rgba32> image)
    {
        const int targetLongSide = 2048;

        var longSide = Math.Max(image.Width, image.Height);
        if (longSide >= targetLongSide)
            return;

        var scale = targetLongSide / (double)longSide;
        image.Mutate(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(
                Math.Max(1, (int)Math.Round(image.Width * scale)),
                Math.Max(1, (int)Math.Round(image.Height * scale))),
            Mode = ResizeMode.Stretch,
            Sampler = KnownResamplers.Lanczos3
        }));
    }

    private static string BuildVersionedExactHash(string exactHash)
    {
        var input = $"{AnalyzerCacheVersion}:{exactHash}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Rotaciona os bytes da imagem 90° no sentido horário e devolve como JPEG.
    /// Usado para enviar uma versão adicional à Vision API quando o rótulo
    /// contém tabelas em orientações diferentes (caso comum no Mercosul).
    /// </summary>
    private static byte[] RotateClockwise90(byte[] imageBytes)
    {
        using var image = Image.Load<Rgba32>(imageBytes);
        image.Mutate(ctx => ctx.Rotate(SixLabors.ImageSharp.Processing.RotateMode.Rotate90));

        using var output = new MemoryStream();
        image.Save(output, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 92 });
        return output.ToArray();
    }

    private static string ResolveMimeType(string? mimeType, string base64Image)
    {
        if (!string.IsNullOrWhiteSpace(mimeType) &&
            mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return mimeType;
        }

        return ImageFormatHelper.DetectMimeTypeFromBase64(base64Image);
    }

    private static string GetFileExtension(string? mimeType)
    {
        return mimeType?.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => ".jpg"
        };
    }

    private static bool IsLikelyGrayscale(byte[] imageBytes)
    {
        using var image = Image.Load<Rgba32>(imageBytes);

        var stepX = Math.Max(1, image.Width / 64);
        var stepY = Math.Max(1, image.Height / 64);
        var colorfulSamples = 0;
        var sampledPixels = 0;

        for (var y = 0; y < image.Height; y += stepY)
        {
            for (var x = 0; x < image.Width; x += stepX)
            {
                var pixel = image[x, y];
                sampledPixels++;

                if (Math.Abs(pixel.R - pixel.G) > 8 ||
                    Math.Abs(pixel.R - pixel.B) > 8 ||
                    Math.Abs(pixel.G - pixel.B) > 8)
                {
                    colorfulSamples++;
                    if (colorfulSamples >= 3)
                    {
                        return false;
                    }
                }
            }
        }

        return sampledPixels > 0;
    }

    private OpenAiVisionResult? ParseOpenAiResponse(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            var content = root
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(content))
                return null;

            var clean = ExtractCleanJson(content);

            // 🔥 Validação mínima de JSON esperado
            if (!clean.Contains("nutritionPer"))
            {
                _logger.LogWarning("[OpenAI] JSON não contém estrutura esperada");
                return null;
            }

            return JsonSerializer.Deserialize<OpenAiVisionResult>(
                clean,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OpenAI] Falha crítica no parse");
            return null;
        }
    }

    /// <summary>
    /// Remove markdown code blocks e whitespace desnecessário.
    /// </summary>
    private string ExtractCleanJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "{}";

        var cleaned = raw.Trim();

        // Remover ```json ... ```
        if (cleaned.StartsWith("```json"))
        {
            cleaned = cleaned["```json".Length..];
        }
        else if (cleaned.StartsWith("```"))
        {
            cleaned = cleaned[3..];
        }

        if (cleaned.EndsWith("```"))
        {
            cleaned = cleaned[..^3];
        }

        return cleaned.Trim();
    }

    /// <summary>
    /// Valida consistência dos valores nutricionais retornados pela IA.
    /// Valores fisicamente impossíveis são anulados aqui para evitar
    /// que propagam distorções no motor de score e de confiança.
    /// </summary>
    private NutritionValidationResult ValidateAiResult(OpenAiVisionResult result)
    {
        var validation = new NutritionValidationResult();

        var per100    = result.NutritionPer100g;
        var perServing = result.NutritionPerServing;
        var hasExplicitServing = result.Serving?.Amount > 0;

        _logger.LogInformation(
            "[OpenAI] 🔬 ValidateAiResult iniciado. " +
            "Per100g={HasPer100g}, PerServing={HasPerServing}, Serving={Amount}{Unit}",
            per100 != null, perServing != null,
            result.Serving?.Amount, result.Serving?.Unit);

        // ── Log detalhado dos valores recebidos ───────────────────────────
        if (perServing != null)
        {
            _logger.LogInformation(
                "[OpenAI] 📊 PerServing recebido: " +
                "Calorias={Cal}, Carbs={Carbs}, Proteína={Prot}, " +
                "Gordura={Fat}, Açúcar={Sugar}, Fibra={Fiber}, Sódio={Sodium}mg",
                perServing.CaloriesKcal, perServing.Carbohydrates, perServing.Proteins,
                perServing.TotalFats, perServing.Sugar, perServing.Fiber, perServing.SodiumMg);
        }

        if (per100 != null)
        {
            _logger.LogInformation(
                "[OpenAI] 📊 Per100g recebido: " +
                "Calorias={Cal}, Carbs={Carbs}, Proteína={Prot}, " +
                "Gordura={Fat}, Açúcar={Sugar}, Fibra={Fiber}, Sódio={Sodium}mg",
                per100.CaloriesKcal, per100.Carbohydrates, per100.Proteins,
                per100.TotalFats, per100.Sugar, per100.Fiber, per100.SodiumMg);
        }

        if (!hasExplicitServing && perServing != null)
        {
            validation.Warnings.Add(
                "Bloco por porção retornado sem porção explícita visível — nutritionPerServing ignorado para evitar cópia/inferência indevida.");
            result.NutritionPerServing = null;
            perServing = null;
        }

        if (per100 != null && perServing != null && AreNutritionBlocksEffectivelyEqual(per100, perServing))
        {
            validation.Warnings.Add(
                "nutritionPerServing idêntico a nutritionPer100g — provável cópia entre colunas. Bloco por porção ignorado.");
            result.NutritionPerServing = null;
            perServing = null;
        }

        ApplyServingRowConsistencyChecks(result, validation);
        ApplyMixedUnitServingSanityChecks(result, validation);
        ApplyMicroServingSodiumAmbiguityCheck(result, validation);
        per100 = result.NutritionPer100g;
        perServing = result.NutritionPerServing;

        var servingRowInconsistencies = validation.Warnings.Count(w =>
            w.Contains("Campo por porção ignorado", StringComparison.OrdinalIgnoreCase) ||
            w.Contains("Valor por porção", StringComparison.OrdinalIgnoreCase));

        if (servingRowInconsistencies >= 2)
        {
            validation.Warnings.Add(
                "Coluna por porção descartada: múltiplas inconsistências entre coluna por porção e coluna 100g indicam provável desalinhamento de linhas, mas a coluna 100g será preservada como evidência visível independente.");
            result.NutritionPerServing = null;
            perServing = null;
            servingRowInconsistencies = 0;
        }

        // Quando só há coluna por porção, deriva um per100 virtual para validação.
        NutritionInfo? derived = null;
        if (per100 == null && perServing != null && result.Serving?.Amount > 0)
        {
            var f = 100.0 / result.Serving.Amount.Value;
            derived = new NutritionInfo
            {
                CaloriesKcal   = perServing.CaloriesKcal   * f,
                Carbohydrates  = perServing.Carbohydrates  * f,
                TotalFats      = perServing.TotalFats      * f,
                Proteins       = perServing.Proteins       * f,
                Fiber          = perServing.Fiber          * f,
                Sugar          = perServing.Sugar          * f,
                Polyols        = perServing.Polyols        * f,
                SodiumMg       = perServing.SodiumMg       * f,
                SaturatedFats  = perServing.SaturatedFats  * f,
                TransFats      = perServing.TransFats      * f,
                AddedSugar     = perServing.AddedSugar     * f
            };

            _logger.LogInformation(
                "[OpenAI] 🧮 Per100g DERIVADO para validação: " +
                "Calorias={Cal:F0}, Carbs={Carbs:F1}g, Prot={Prot:F1}g, " +
                "Fat={Fat:F1}g (serving={Serving}g, fator={Factor:F2})",
                derived.CaloriesKcal, derived.Carbohydrates, derived.Proteins,
                derived.TotalFats, result.Serving.Amount, f);
        }

        var ref100 = per100 ?? derived;

        if (ref100 == null)
        {
            validation.Warnings.Add("Sem coluna 100g");
            return validation;
        }

        // ── Limites físicos absolutos ─────────────────────────────────────
        if (ref100.Carbohydrates > 100) { validation.IsValid = false; validation.Errors.Add("Carbs > 100"); }
        if (ref100.TotalFats     > 100) { validation.IsValid = false; validation.Errors.Add("Fat > 100"); }

        // ── Validação: Proteína zerada em produto rico em gordura (suspeito) ──
        // Produtos com gordura > 50g/100g geralmente têm proteína > 0
        // (ex: coco, amendoim, nozes, queijos). Proteína=0 indica erro de leitura.
        if (ref100.Proteins == 0 && ref100.TotalFats > 50)
        {
            validation.Warnings.Add(
                $"Proteína=0g em produto com {ref100.TotalFats:F1}g de gordura é suspeito. " +
                "Verifique se o campo foi lido corretamente.");

            _logger.LogWarning(
                "[OpenAI] ⚠️ Proteína=0 em produto rico em gordura ({Fat}g). Possível erro de leitura.",
                ref100.TotalFats);
        }

        // ── Fibra não pode superar carboidratos totais ────────────────────
        // Fibra é um subcampo de carboidratos. Se fibra > carbs o OCR leu
        // um dos valores incorretamente. Anula a fibra (mais provável de
        // estar errada, pois a linha de fibra costuma ser menor no rótulo).
        if (ref100.Fiber.HasValue && ref100.Carbohydrates.HasValue
            && ref100.Fiber.Value > ref100.Carbohydrates.Value * 1.05)
        {
            validation.Warnings.Add(
                $"Fibra ({ref100.Fiber.Value:F1}g) maior que carboidratos ({ref100.Carbohydrates.Value:F1}g) — impossível. Valor de fibra ignorado.");

            // Anula fibra nas duas colunas para não propagar o erro.
            if (per100 != null)      per100.Fiber      = null;
            if (perServing != null)  perServing.Fiber  = null;
        }

        // ── Açúcar não pode superar carboidratos totais ───────────────────
        if (ref100.Sugar.HasValue && ref100.Carbohydrates.HasValue
            && ref100.Sugar.Value > ref100.Carbohydrates.Value * 1.05)
        {
            validation.Warnings.Add(
                $"Açúcar ({ref100.Sugar.Value:F1}g) maior que carboidratos ({ref100.Carbohydrates.Value:F1}g) — impossível. Valor de açúcar ignorado.");

            if (per100 != null)      per100.Sugar      = null;
            if (perServing != null)  perServing.Sugar  = null;
        }

        // ── Polióis não podem superar carboidratos totais ─────────────────
        if (ref100.Polyols.HasValue && ref100.Carbohydrates.HasValue
            && ref100.Polyols.Value > ref100.Carbohydrates.Value * 1.05)
        {
            validation.Warnings.Add(
                $"Polióis ({ref100.Polyols.Value:F1}g) maiores que carboidratos ({ref100.Carbohydrates.Value:F1}g) — impossível. Valor de polióis ignorado.");

            if (per100 != null)      per100.Polyols      = null;
            if (perServing != null)  perServing.Polyols  = null;
        }

        // ── Açúcares adicionados: se perServing = 0 e per100 = null, propaga 0 ──
        // Açúcares adicionados são uma característica do produto (não escalam
        // com o peso). Zero por porção → zero por 100g também.
        if (per100 != null && perServing != null
            && per100.AddedSugar is null && perServing.AddedSugar == 0)
        {
            per100.AddedSugar = 0;
        }

        // ── Validação calórica ────────────────────────────────────────────
        if (ref100.CaloriesKcal.HasValue)
        {
            // Fórmula utilizada por fabricantes brasileiros (ANVISA RDC 360/2003,
            // Anexo A): carboidratos totais (incluindo fibra) × 4 + proteína × 4 +
            // gordura × 9. É a mesma fórmula que o rótulo usa, então leituras
            // legítimas batem com folga.
            //
            // Nota: NÃO usamos Atwater modificado (USDA/EU, com fibra a 2 kcal/g)
            // porque a maioria dos rótulos nacionais não desconta fibra — usar
            // Atwater aqui geraria falso positivo em produtos integrais legítimos.
            var calc =
                (ref100.Carbohydrates ?? 0) * 4 +
                (ref100.Proteins      ?? 0) * 4 +
                (ref100.TotalFats     ?? 0) * 9;

            var diff = Math.Abs(calc - ref100.CaloriesKcal.Value);
            // Tolerância relativa ao maior dos dois (declarado vs calculado),
            // evitando que produtos hipocalóricos derrubem o threshold absoluto.
            var relDiff = calc > 0
                ? diff / Math.Max(calc, ref100.CaloriesKcal.Value)
                : 0;

            // Para evitar apagar valores legítimos de kcal lidos diretamente do rótulo,
            // quando há divergência com a fórmula dos macros, preferimos apenas registrar
            // warning (o prompt já proíbe inventar kcal; se veio, costuma estar impresso).
            // A anulação agressiva pode degradar a resposta da API (ex.: kcal vira null).
            if (calc > 0 && relDiff > 0.18)
            {
                validation.Warnings.Add(
                    $"Calorias ({ref100.CaloriesKcal.Value:F0} kcal) divergem em " +
                    $"{relDiff * 100:F0}% do cálculo dos macros ({calc:F0} kcal). Mantendo o valor, mas marcando como potencialmente inconsistente.");
            }

            // Só invalidamos quando a divergência das kcal é realmente massiva
            // (>30% e >100 kcal) E há indícios de desalinhamento entre colunas.
            // Divergências menores são tratadas como warning (já registrado acima)
            // para evitar descartar tabelas legítimas onde o fabricante arredondou
            // os macros de forma diferente do impresso em kcal.
            if (calc > 0 && relDiff > 0.30 && diff > 100 && servingRowInconsistencies > 0)
            {
                validation.IsValid = false;
                validation.Errors.Add(
                    $"Calorias divergem dos macros e há inconsistências entre colunas — provável leitura desalinhada da tabela (kcal={ref100.CaloriesKcal.Value:F0}, macros≈{calc:F0}).");
            }
            // Observação: não tentamos detectar "kcal fabricado pela IA" pela
            // proximidade com a fórmula. Rótulos legítimos sempre batem com ela
            // (é assim que o fabricante calcula). A defesa contra obstrução fica
            // a cargo da REGRA 1 do prompt (linha de kcal coberta → null).
        }

        // ── Consistência entre colunas ────────────────────────────────────
        // Rótulos reais frequentemente exibem colunas por porção e por 100g
        // matematicamente coerentes. Portanto, correspondência entre colunas
        // NÃO é motivo para rejeição. Só registramos divergências fortes.
        if (per100 != null && perServing != null && result.Serving?.Amount > 0)
        {
            var factor = 100.0 / result.Serving.Amount.Value;

            var expected = (perServing.Carbohydrates ?? 0) * factor;
            var actual   = per100.Carbohydrates ?? 0;

            if (perServing.Carbohydrates.HasValue && per100.Carbohydrates.HasValue
                && Math.Abs(expected - actual) > 10)
                validation.Warnings.Add("Inconsistência porção vs 100g em carboidratos");

            // ── Sódio ilegível/com ruído: consistência porção vs 100g ────────
            // Em fotos com glare/blur, a linha de sódio costuma ser a mais fina e
            // é frequentemente alucinada. Se há porção declarada e ambas as colunas
            // existem, comparamos o sódio declarado em 100g com o que seria derivado
            // da porção. Divergência grande indica leitura corrompida; anulamos para
            // respeitar o princípio do prompt (não chutar quando não está nítido).
            if (perServing.SodiumMg.HasValue && per100.SodiumMg.HasValue)
            {
                var expectedSodium = perServing.SodiumMg.Value * factor;
                var actualSodium   = per100.SodiumMg.Value;

                // Threshold relativo: 35% cobre arredondamento e porções fracionadas,
                // mas captura casos em que o OCR "inventou" um número.
                var max = Math.Max(1.0, Math.Max(Math.Abs(expectedSodium), Math.Abs(actualSodium)));
                var rel = Math.Abs(expectedSodium - actualSodium) / max;

                if (rel > 0.35)
                {
                    validation.Warnings.Add(
                        $"Sódio porção vs 100g diverge em {rel * 100:F0}% (porção≈{expectedSodium:F0}mg/100g, 100g={actualSodium:F0}mg/100g) — provável leitura corrompida. Valor de sódio anulado.");

                    per100.SodiumMg = null;
                    perServing.SodiumMg = null;
                }
            }
        }

        // ── Porção suspeita de ter sido lida da grade nutricional ─────────
        // Quando serving.amount coincide exatamente com algum dos valores
        // numéricos da coluna por 100g (carbs, fat, sat, sugar, fiber), é
        // praticamente certo que a IA confundiu uma célula da tabela com
        // o tamanho da porção. Anulamos serving para evitar derivar
        // per100g a partir de uma porção fictícia.
        if (result.Serving?.Amount is double serv && serv > 0 && per100 != null)
        {
            var grid = new double?[]
            {
                per100.Carbohydrates, per100.TotalFats, per100.SaturatedFats,
                per100.Sugar, per100.Polyols, per100.Fiber, per100.Proteins
            };

            if (grid.Any(v => v.HasValue && Math.Abs(v.Value - serv) < 0.01))
            {
                validation.Warnings.Add(
                    $"Porção ({serv:F0}{result.Serving.Unit}) coincide com um valor " +
                    $"da coluna por 100g — provável confusão de célula. Porção anulada.");
                result.Serving.Amount = null;
                result.Serving.Description = null;
            }
        }

        if (validation.Warnings.Count > 0)
            _logger.LogWarning("[OpenAI] Validação nutricional: {Warnings}",
                string.Join("; ", validation.Warnings));

        return validation;
    }

    /// <summary>
    /// Valida se a resposta da IA contém dados nutricionais reais.
    /// Rejeita respostas sem tabela nutricional, mas aceita tabelas legítimas
    /// quando apenas uma das bases retornadas está completa.
    /// </summary>
    private bool HasMinimumNutritionData(OpenAiVisionResult result)
    {
        var per100g = result.NutritionPer100g;
        var per100ml = result.NutritionPer100ml;
        var perServing = result.NutritionPerServing;

        if (per100g == null && per100ml == null && perServing == null)
        {
            _logger.LogWarning("[OpenAI] Nenhuma coluna nutricional encontrada na resposta");
            return false;
        }

        var per100gFields = CountNutritionFields(per100g);
        var per100mlFields = CountNutritionFields(per100ml);
        var perServingFields = CountNutritionFields(perServing);
        var bestFilledFields = Math.Max(per100gFields, Math.Max(per100mlFields, perServingFields));

        var bestHasCoreNutrition =
            HasCoreNutritionData(per100g) ||
            HasCoreNutritionData(per100ml) ||
            HasCoreNutritionData(perServing);

        // Uma tabela nutricional real normalmente traz vários campos, mas alguns
        // recortes podem expor só parte dela. Aceitamos quando qualquer bloco tem
        // pelo menos 3 campos preenchidos e contém pelo menos um sinal central
        // de nutrição: kcal, carboidratos, proteínas ou gordura total.
        const int MinimumRequiredFields = 3;
        var isValid = bestFilledFields >= MinimumRequiredFields && bestHasCoreNutrition;

        if (!isValid)
        {
            _logger.LogWarning(
                "[OpenAI] Dados insuficientes: melhor bloco tem {FilledFields} campos preenchidos " +
                "(mínimo: {MinRequired}, core={HasCore}). Per100g={Per100gFields}, Per100ml={Per100mlFields}, PerServing={PerServingFields}",
                bestFilledFields, MinimumRequiredFields, bestHasCoreNutrition,
                per100gFields, per100mlFields, perServingFields);
        }

        return isValid;
    }

    private static int CountNutritionFields(NutritionInfo? nutrition)
    {
        if (nutrition == null)
            return 0;

        var filledFields = 0;

        if (nutrition.CaloriesKcal.HasValue) filledFields++;
        if (nutrition.Carbohydrates.HasValue) filledFields++;
        if (nutrition.Sugar.HasValue) filledFields++;
        if (nutrition.AddedSugar.HasValue) filledFields++;
        if (nutrition.Polyols.HasValue) filledFields++;
        if (nutrition.Proteins.HasValue) filledFields++;
        if (nutrition.TotalFats.HasValue) filledFields++;
        if (nutrition.SaturatedFats.HasValue) filledFields++;
        if (nutrition.TransFats.HasValue) filledFields++;
        if (nutrition.Fiber.HasValue) filledFields++;
        if (nutrition.SodiumMg.HasValue) filledFields++;

        return filledFields;
    }

    private static bool HasCoreNutritionData(NutritionInfo? nutrition) =>
        nutrition?.CaloriesKcal.HasValue == true ||
        nutrition?.Carbohydrates.HasValue == true ||
        nutrition?.Proteins.HasValue == true ||
        nutrition?.TotalFats.HasValue == true;

    private static bool AreNutritionBlocksEffectivelyEqual(NutritionInfo left, NutritionInfo right)
    {
        var pairs = new (double? Left, double? Right)[]
        {
            (left.CaloriesKcal, right.CaloriesKcal),
            (left.Carbohydrates, right.Carbohydrates),
            (left.Sugar, right.Sugar),
            (left.AddedSugar, right.AddedSugar),
            (left.Polyols, right.Polyols),
            (left.Proteins, right.Proteins),
            (left.TotalFats, right.TotalFats),
            (left.SaturatedFats, right.SaturatedFats),
            (left.TransFats, right.TransFats),
            (left.Fiber, right.Fiber),
            (left.SodiumMg, right.SodiumMg)
        };

        var comparable = 0;
        var equal = 0;

        foreach (var (leftValue, rightValue) in pairs)
        {
            if (!leftValue.HasValue || !rightValue.HasValue)
                continue;

            comparable++;
            if (Math.Abs(leftValue.Value - rightValue.Value) <= 0.01)
                equal++;
        }

        return comparable >= 4 && comparable == equal;
    }

    private static void ApplyServingRowConsistencyChecks(
        OpenAiVisionResult result,
        NutritionValidationResult validation)
    {
        var per100 = result.NutritionPer100g;
        var perServing = result.NutritionPerServing;

        if (per100 is null || perServing is null || result.Serving?.Amount is not double servingAmount || servingAmount <= 0)
            return;

        if (!string.Equals(result.Serving.Unit, "g", StringComparison.OrdinalIgnoreCase))
            return;

        var servingFactor = servingAmount / 100.0;

        ClearInconsistentServingValue(
            "calorias",
            per100.CaloriesKcal,
            perServing.CaloriesKcal,
            servingFactor,
            absoluteTolerance: 8,
            relativeTolerance: 0.18,
            v => perServing.CaloriesKcal = v,
            validation);

        ClearInconsistentServingValue("carboidratos", per100.Carbohydrates, perServing.Carbohydrates, servingFactor, 0.8, 0.25, v => perServing.Carbohydrates = v, validation);
        ClearInconsistentServingValue("açúcar", per100.Sugar, perServing.Sugar, servingFactor, 0.6, 0.35, v => perServing.Sugar = v, validation);
        ClearInconsistentServingValue("açúcar adicionado", per100.AddedSugar, perServing.AddedSugar, servingFactor, 0.5, 0.40, v => perServing.AddedSugar = v, validation);
        ClearInconsistentServingValue("proteína", per100.Proteins, perServing.Proteins, servingFactor, 0.8, 0.25, v => perServing.Proteins = v, validation);
        ClearInconsistentServingValue("gordura total", per100.TotalFats, perServing.TotalFats, servingFactor, 0.8, 0.25, v => perServing.TotalFats = v, validation);
        ClearInconsistentServingValue("gordura saturada", per100.SaturatedFats, perServing.SaturatedFats, servingFactor, 0.5, 0.25, v => perServing.SaturatedFats = v, validation);
        ClearInconsistentServingValue("gordura trans", per100.TransFats, perServing.TransFats, servingFactor, 0.2, 0.50, v => perServing.TransFats = v, validation);
        ClearInconsistentServingValue("fibra", per100.Fiber, perServing.Fiber, servingFactor, 0.6, 0.30, v => perServing.Fiber = v, validation);
        ClearInconsistentServingValue("sódio", per100.SodiumMg, perServing.SodiumMg, servingFactor, 8, 0.35, v => perServing.SodiumMg = v, validation);

        ClearServingMacroOutliersWithoutPer100Reference(per100, perServing, servingAmount, validation);
    }

    private static void ApplyMixedUnitServingSanityChecks(
        OpenAiVisionResult result,
        NutritionValidationResult validation)
    {
        var per100ml = result.NutritionPer100ml;
        var perServing = result.NutritionPerServing;

        if (per100ml is null || perServing is null || result.Serving?.Amount is not double servingAmount || servingAmount <= 0)
            return;

        if (!string.Equals(result.Serving.Unit, "g", StringComparison.OrdinalIgnoreCase))
            return;

        if (servingAmount > 100)
            return;

        var suspiciousFields = new List<string>();

        AddIfServingExceedsPer100("carboidratos", perServing.Carbohydrates, per100ml.Carbohydrates, 0.8, suspiciousFields);
        AddIfServingExceedsPer100("açúcar", perServing.Sugar, per100ml.Sugar, 0.6, suspiciousFields);
        AddIfServingExceedsPer100("açúcar adicionado", perServing.AddedSugar, per100ml.AddedSugar, 0.5, suspiciousFields);
        AddIfServingExceedsPer100("proteína", perServing.Proteins, per100ml.Proteins, 0.8, suspiciousFields);
        AddIfServingExceedsPer100("gordura total", perServing.TotalFats, per100ml.TotalFats, 0.5, suspiciousFields);
        AddIfServingExceedsPer100("gordura saturada", perServing.SaturatedFats, per100ml.SaturatedFats, 0.3, suspiciousFields);
        AddIfServingExceedsPer100("fibra", perServing.Fiber, per100ml.Fiber, 0.6, suspiciousFields);
        AddIfServingExceedsPer100("sódio", perServing.SodiumMg, per100ml.SodiumMg, 8, suspiciousFields);

        if (suspiciousFields.Count < 2)
            return;

        validation.Warnings.Add(
            "Coluna por porção descartada: porção em gramas com base 100ml apresentou valores maiores que a base 100ml em múltiplos campos " +
            $"({string.Join(", ", suspiciousFields)}), indicando provável desalinhamento de coluna/linha.");
        result.NutritionPerServing = null;
    }

    private static void ApplyMicroServingSodiumAmbiguityCheck(
        OpenAiVisionResult result,
        NutritionValidationResult validation)
    {
        if (result.Serving?.Amount is not double servingAmount || servingAmount <= 0 || servingAmount > 1)
            return;

        if (ParseUnit(result.Serving.Unit) is not (NutritionUnit.Gram or NutritionUnit.Milliliter))
            return;

        if (result.NutritionPerServing?.SodiumMg != 0)
            return;

        ClearAmbiguousMicroServingSodium(result.NutritionPer100g, "100g", validation);
        ClearAmbiguousMicroServingSodium(result.NutritionPer100ml, "100ml", validation);
    }

    private static void ClearAmbiguousMicroServingSodium(
        NutritionInfo? nutrition,
        string basis,
        NutritionValidationResult validation)
    {
        if (nutrition?.SodiumMg != 0)
            return;

        var hasPositiveNonSodiumValue =
            nutrition.CaloriesKcal > 0 ||
            nutrition.Carbohydrates > 0 ||
            nutrition.Sugar > 0 ||
            nutrition.AddedSugar > 0 ||
            nutrition.Polyols > 0 ||
            nutrition.Proteins > 0 ||
            nutrition.TotalFats > 0 ||
            nutrition.SaturatedFats > 0 ||
            nutrition.TransFats > 0 ||
            nutrition.Fiber > 0;

        if (!hasPositiveNonSodiumValue)
            return;

        nutrition.SodiumMg = null;
        validation.Warnings.Add(
            $"Sódio {basis} ignorado: porção muito pequena com sódio por porção = 0 mg não confirma sódio {basis} = 0 mg. Campo tratado como ilegível para evitar classificação enganosa.");
    }

    private static void AddIfServingExceedsPer100(
        string fieldName,
        double? perServingValue,
        double? per100Value,
        double absoluteTolerance,
        List<string> suspiciousFields)
    {
        if (!perServingValue.HasValue || !per100Value.HasValue)
            return;

        if (perServingValue.Value > per100Value.Value * 1.10 + absoluteTolerance)
            suspiciousFields.Add(fieldName);
    }

    private static void ClearInconsistentServingValue(
        string fieldName,
        double? per100Value,
        double? perServingValue,
        double servingFactor,
        double absoluteTolerance,
        double relativeTolerance,
        Action<double?> setServingValue,
        NutritionValidationResult validation)
    {
        if (!per100Value.HasValue || !perServingValue.HasValue)
            return;

        var expectedServing = per100Value.Value * servingFactor;
        var diff = Math.Abs(perServingValue.Value - expectedServing);
        var max = Math.Max(1.0, Math.Max(Math.Abs(perServingValue.Value), Math.Abs(expectedServing)));
        var relative = diff / max;

        if (diff <= absoluteTolerance || relative <= relativeTolerance)
            return;

        validation.Warnings.Add(
            $"Valor por porção de {fieldName} inconsistente com a coluna 100g " +
            $"(porção={perServingValue.Value:F1}, esperado≈{expectedServing:F1}). Campo por porção ignorado para evitar desalinhamento de linha.");
        setServingValue(null);
    }

    private static void ClearServingMacroOutliersWithoutPer100Reference(
        NutritionInfo per100,
        NutritionInfo perServing,
        double servingAmount,
        NutritionValidationResult validation)
    {
        var candidates = new List<(string Name, double? ServingValue, bool HasPer100, Action Clear)>
        {
            ("carboidratos", perServing.Carbohydrates, per100.Carbohydrates.HasValue, () => perServing.Carbohydrates = null),
            ("proteína", perServing.Proteins, per100.Proteins.HasValue, () => perServing.Proteins = null),
            ("gordura total", perServing.TotalFats, per100.TotalFats.HasValue, () => perServing.TotalFats = null),
            ("fibra", perServing.Fiber, per100.Fiber.HasValue, () => perServing.Fiber = null)
        };

        var macroMass = candidates.Sum(c => c.ServingValue ?? 0);
        var maxExpectedMass = servingAmount * 1.10;

        if (macroMass <= maxExpectedMass)
            return;

        foreach (var candidate in candidates
            .Where(c => c.ServingValue.HasValue && !c.HasPer100)
            .OrderByDescending(c => c.ServingValue!.Value))
        {
            candidate.Clear();
            macroMass -= candidate.ServingValue!.Value;

            validation.Warnings.Add(
                $"Valor por porção de {candidate.Name} removido: soma de macros/fibra ({macroMass + candidate.ServingValue.Value:F1}g) " +
                $"ultrapassava a porção declarada ({servingAmount:F1}g) e não havia valor correspondente na coluna 100g para validar a linha.");

            if (macroMass <= maxExpectedMass)
                return;
        }
    }

    private async Task<ConsensusResult> ApplyDocumentIntelligenceConsensusAsync(
        EstimatedNutritionProfileDto profile,
        byte[] imageBytes,
        CancellationToken cancellationToken)
    {
        DocumentIntelligenceNutritionResult? documentResult;

        try
        {
            documentResult = await _documentIntelligenceService.AnalyzeAsync(imageBytes, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Consensus] Document Intelligence falhou. Mantendo OpenAI como fonte única.");
            profile.DataSource["Consensus"] = "OpenAIOnly_DocumentIntelligenceException";
            return ConsensusResult.Accept("document_intelligence_exception");
        }

        if (documentResult is null || !documentResult.HasAnyData())
        {
            _logger.LogWarning("[Consensus] Document Intelligence sem dados úteis. Mantendo OpenAI como fonte única.");
            profile.DataSource["Consensus"] = "OpenAIOnly_DocumentIntelligenceEmpty";
            return ConsensusResult.Accept("document_intelligence_empty");
        }

        var fields = BuildConsensusFields(profile, documentResult);
        var comparable = fields.Where(f => f.OpenAiValue.HasValue && f.DocumentValue.HasValue).ToList();
        var conflicts = comparable.Where(IsConflict).ToList();
        var agreements = comparable.Count - conflicts.Count;

        profile.DataSource["Consensus"] = "OpenAI_DocumentIntelligence";
        profile.DataSource["ConsensusComparableFields"] = comparable.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        profile.DataSource["ConsensusConflicts"] = conflicts.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);

        _logger.LogInformation(
            "[Consensus] DI: Table={HasTable}, Mode={Mode}, Comparable={Comparable}, Agreements={Agreements}, Conflicts={Conflicts}",
            documentResult.HasNutritionTable, documentResult.ExtractionMode, comparable.Count, agreements, conflicts.Count);

        foreach (var field in conflicts)
        {
            _logger.LogWarning(
                "[Consensus] Divergência em {Field}: OpenAI={OpenAi}, DocumentIntelligence={Document}",
                field.Name, field.OpenAiValue, field.DocumentValue);
        }

        var unresolvedConflicts = conflicts
            .Where(conflict => !HasStrongOpenAiServingConsistency(conflict.Name, profile))
            .ToList();

        if (unresolvedConflicts.Count >= 3 || (comparable.Count >= 4 && unresolvedConflicts.Count >= 2 && agreements <= unresolvedConflicts.Count))
        {
            profile.ParserConfidence = "low";
            profile.DataSource["ConsensusDecision"] = "Rejected";
            profile.DataSource["ConsensusReason"] = "CriticalFieldConflicts";
            return ConsensusResult.Reject("critical_field_conflicts");
        }

        foreach (var conflict in conflicts)
        {
            if (HasStrongOpenAiServingConsistency(conflict.Name, profile))
            {
                profile.DataSource[$"{conflict.Name}Source"] = "OpenAIKept_InternalServingConsistency";
                continue;
            }

            ClearProfileField(conflict.Name, profile);
            profile.DataSource[$"{conflict.Name}Source"] = "null_consensus_conflict";
        }

        foreach (var field in fields.Where(f => !f.OpenAiValue.HasValue && f.DocumentValue.HasValue))
        {
            // Aceita valor de fonte única apenas quando o Document Intelligence detectou tabela estruturada.
            if (!documentResult.HasNutritionTable)
                continue;

            SetProfileField(field.Name, profile, field.DocumentValue);
            profile.DataSource[$"{field.Name}Source"] = "DocumentIntelligenceOnly";
        }

        profile.ParserConfidence = unresolvedConflicts.Count > 0 ? "medium" : "high";
        profile.DataSource["ConsensusDecision"] = conflicts.Count > 0
            ? unresolvedConflicts.Count > 0 ? "AcceptedWithNullConflicts" : "AcceptedWithOpenAIInternalConsistency"
            : "Accepted";

        return ConsensusResult.Accept("accepted");
    }

    private static bool HasStrongOpenAiServingConsistency(string fieldName, EstimatedNutritionProfileDto profile)
    {
        if (profile.RawPerServing is null || profile.ServingAmount is not double servingAmount || servingAmount <= 0)
            return false;

        var factor = 100.0 / servingAmount;
        var (per100, perServing, absoluteTolerance, relativeTolerance) = fieldName switch
        {
            "Calories" => (profile.CaloriesPer100g ?? profile.CaloriesPer100ml, profile.RawPerServing.CaloriesKcal, 8.0, 0.18),
            "Carbs" => (profile.EstimatedCarbsPer100g, profile.RawPerServing.Carbohydrates, 0.8, 0.25),
            "Sugar" => (profile.EstimatedSugarPer100g, profile.RawPerServing.Sugar, 0.6, 0.35),
            "AddedSugar" => (profile.EstimatedAddedSugarPer100g, profile.RawPerServing.AddedSugar, 0.5, 0.40),
            "Protein" => (profile.EstimatedProteinPer100g, profile.RawPerServing.Proteins, 0.8, 0.25),
            "Fat" => (profile.EstimatedFatPer100g, profile.RawPerServing.TotalFats, 0.8, 0.25),
            "SaturatedFat" => (profile.EstimatedSaturatedFatPer100g, profile.RawPerServing.SaturatedFats, 0.5, 0.25),
            "Fiber" => (profile.EstimatedFiberPer100g, profile.RawPerServing.Fiber, 0.6, 0.30),
            "Sodium" => (profile.EstimatedSodiumPer100g, profile.RawPerServing.SodiumMg, 8.0, 0.35),
            _ => (null, null, 0.0, 0.0)
        };

        if (!per100.HasValue || !perServing.HasValue)
            return false;

        var expectedPer100 = perServing.Value * factor;
        var diff = Math.Abs(per100.Value - expectedPer100);
        var max = Math.Max(1.0, Math.Max(Math.Abs(per100.Value), Math.Abs(expectedPer100)));
        var relative = diff / max;

        return diff <= absoluteTolerance || relative <= relativeTolerance;
    }

    private static List<ConsensusField> BuildConsensusFields(
        EstimatedNutritionProfileDto profile,
        DocumentIntelligenceNutritionResult documentResult) =>
        [
            new("Calories", profile.CaloriesPer100g ?? profile.CaloriesPer100ml, documentResult.Calories?.Value),
            new("Carbs", profile.EstimatedCarbsPer100g, documentResult.Carbs?.Value),
            new("Sugar", profile.EstimatedSugarPer100g, documentResult.Sugar?.Value),
            new("AddedSugar", profile.EstimatedAddedSugarPer100g, documentResult.AddedSugar?.Value),
            new("Protein", profile.EstimatedProteinPer100g, documentResult.Protein?.Value),
            new("Fat", profile.EstimatedFatPer100g, documentResult.Fat?.Value),
            new("SaturatedFat", profile.EstimatedSaturatedFatPer100g, documentResult.SaturatedFat?.Value),
            new("Fiber", profile.EstimatedFiberPer100g, documentResult.Fiber?.Value),
            new("Sodium", profile.EstimatedSodiumPer100g, documentResult.Sodium?.Value)
        ];

    private static bool IsConflict(ConsensusField field)
    {
        if (!field.OpenAiValue.HasValue || !field.DocumentValue.HasValue)
            return false;

        var openAi = field.OpenAiValue.Value;
        var document = field.DocumentValue.Value;
        var diff = Math.Abs(openAi - document);
        var max = Math.Max(Math.Abs(openAi), Math.Abs(document));

        if (max <= 0.01)
            return false;

        var relative = diff / max;

        return field.Name switch
        {
            "Calories" => diff > 20 && relative > 0.15,
            "Sodium" => diff > 20 && relative > 0.25,
            "SaturatedFat" => diff > 1.0 && relative > 0.25,
            _ => diff > 1.5 && relative > 0.25
        };
    }

    private static void ClearProfileField(string fieldName, EstimatedNutritionProfileDto profile) =>
        SetProfileField(fieldName, profile, null);

    private static void SetProfileField(string fieldName, EstimatedNutritionProfileDto profile, double? value)
    {
        switch (fieldName)
        {
            case "Calories":
                if (string.Equals(profile.NutritionUnit, "ml", StringComparison.OrdinalIgnoreCase))
                {
                    profile.CaloriesPer100ml = value;
                    profile.CaloriesPer100g = value;
                }
                else
                {
                    profile.CaloriesPer100g = value;
                    profile.CaloriesPer100ml = null;
                }
                break;
            case "Carbs":
                profile.EstimatedCarbsPer100g = value;
                break;
            case "Sugar":
                profile.EstimatedSugarPer100g = value;
                break;
            case "AddedSugar":
                profile.EstimatedAddedSugarPer100g = value;
                break;
            case "Protein":
                profile.EstimatedProteinPer100g = value;
                break;
            case "Fat":
                profile.EstimatedFatPer100g = value;
                break;
            case "SaturatedFat":
                profile.EstimatedSaturatedFatPer100g = value;
                break;
            case "Fiber":
                profile.EstimatedFiberPer100g = value;
                break;
            case "Sodium":
                profile.EstimatedSodiumPer100g = value;
                break;
        }
    }

    private EstimatedNutritionProfileDto MapToNutritionProfile(OpenAiVisionResult result)
    {
        _logger.LogInformation(
            "[OpenAI] 📊 MapToNutritionProfile iniciado. " +
            "Per100g={HasPer100g}, Per100ml={HasPer100ml}, PerServing={HasPerServing}, " +
            "ServingAmount={Amount}, ServingUnit={Unit}",
            result.NutritionPer100g != null,
            result.NutritionPer100ml != null,
            result.NutritionPerServing != null,
            result.Serving?.Amount,
            result.Serving?.Unit);

        // ── Correção automática: porção em ml mas IA retornou per100g ──────
        // Bebidas às vezes vêm rotuladas como "g" na resposta da IA quando
        // deveriam ser "ml". Detecta pela unidade da porção e renomeia.
        if (result.Serving?.Unit?.Equals("ml", StringComparison.OrdinalIgnoreCase) == true
            && result.NutritionPer100g != null
            && result.NutritionPer100ml == null)
        {
            result.NutritionPer100ml = result.NutritionPer100g;
            result.NutritionPer100g = null;
        }

        var profile = new EstimatedNutritionProfileDto
        {
            Basis = "OpenAI Vision - extração estruturada",
            IsFromOpenAI = true,
            ProductName = result.ProductName,
            Brand = result.Brand,
            ServingAmount = result.Serving?.Amount,
            ServingUnit = result.Serving?.Unit,
            ServingDescription = result.Serving?.Description,
            RawPerServing = result.NutritionPerServing is null
                ? null
                : new RawServingNutrition
                {
                    CaloriesKcal = result.NutritionPerServing.CaloriesKcal,
                    Carbohydrates = result.NutritionPerServing.Carbohydrates,
                    Sugar = result.NutritionPerServing.Sugar,
                    AddedSugar = result.NutritionPerServing.AddedSugar,
                    Polyols = result.NutritionPerServing.Polyols,
                    Proteins = result.NutritionPerServing.Proteins,
                    TotalFats = result.NutritionPerServing.TotalFats,
                    SaturatedFats = result.NutritionPerServing.SaturatedFats,
                    TransFats = result.NutritionPerServing.TransFats,
                    Fiber = result.NutritionPerServing.Fiber,
                    SodiumMg = result.NutritionPerServing.SodiumMg
                }
        };

        var unit = DetectUnit(result);

        _logger.LogInformation(
            "[OpenAI] 🔍 DetectUnit retornou: {Unit}. " +
            "Iniciando decisão de mapeamento...",
            unit);

        // 🔥 REGRA DE OURO — base sólida em 100g
        if (unit == NutritionUnit.Gram && result.NutritionPer100g != null)
        {
            _logger.LogInformation("[OpenAI] ✅ Branch 1: Usando per100g DIRETO da IA");

            var n = result.NutritionPer100g;

            profile.NutritionUnit = "g";
            profile.CaloriesPer100g = n.CaloriesKcal;
            profile.EstimatedCarbsPer100g = n.Carbohydrates;
            profile.EstimatedSugarPer100g = n.Sugar;
            profile.EstimatedAddedSugarPer100g = n.AddedSugar;
            profile.EstimatedPolyolsPer100g = n.Polyols;
            profile.EstimatedProteinPer100g = n.Proteins;
            profile.EstimatedFatPer100g = n.TotalFats;
            profile.EstimatedSaturatedFatPer100g = n.SaturatedFats;
            profile.EstimatedTransFatPer100g = n.TransFats;
            profile.EstimatedSodiumPer100g = n.SodiumMg;
            profile.EstimatedFiberPer100g = n.Fiber;

            _logger.LogInformation(
                "[OpenAI] ✅ per100g mapeado: Calorias={Cal}, Carbs={Carbs}",
                profile.CaloriesPer100g, profile.EstimatedCarbsPer100g);

            return profile;
        }

        // 🔥 Tabela em ml
        // motor de score nos campos per100g. Os limiares ANVISA / front-of-pack
        // usam a MESMA base numérica para sólidos (g/100 g) e líquidos (g/100 ml),
        // e a densidade da quase totalidade de bebidas comerciais é ≈ 1,0 g/ml.
        // Tratar 100 ml como 100 g aqui é uma regra genérica, não específica de produto.
        if (unit == NutritionUnit.Milliliter && result.NutritionPer100ml != null)
        {
            _logger.LogInformation("[OpenAI] ✅ Branch 2: Usando per100ml DIRETO da IA");

            var n = result.NutritionPer100ml;

            profile.NutritionUnit = "ml";
            profile.CaloriesPer100ml = n.CaloriesKcal;

            // Espelha os macros para os campos per100g consumidos pelo scoring/perfis.
            profile.CaloriesPer100g = n.CaloriesKcal;
            profile.EstimatedCarbsPer100g = n.Carbohydrates;
            profile.EstimatedSugarPer100g = n.Sugar;
            profile.EstimatedAddedSugarPer100g = n.AddedSugar;
            profile.EstimatedPolyolsPer100g = n.Polyols;
            profile.EstimatedProteinPer100g = n.Proteins;
            profile.EstimatedFatPer100g = n.TotalFats;
            profile.EstimatedSaturatedFatPer100g = n.SaturatedFats;
            profile.EstimatedTransFatPer100g = n.TransFats;
            profile.EstimatedSodiumPer100g = n.SodiumMg;
            profile.EstimatedFiberPer100g = n.Fiber;

            _logger.LogInformation(
                "[OpenAI] ✅ per100ml→per100g mapeado: Calorias={Cal}, Carbs={Carbs}",
                profile.CaloriesPer100g, profile.EstimatedCarbsPer100g);

            return profile;
        }

        // 🔥 Fallback controlado por porção — só quando a unidade da porção é grama.
        // Se a porção for em ml (ou desconhecida), NÃO derivamos valores per100g
        // para evitar que ml seja tratado como g.
        if (result.NutritionPerServing != null && result.Serving?.Amount > 0)
        {
            var servingUnit = ParseUnit(result.Serving.Unit);

            if (servingUnit == NutritionUnit.Gram)
            {
                var factor = 100.0 / result.Serving.Amount.Value;
                var n = result.NutritionPerServing;

                profile.NutritionUnit   = "g";
                profile.IsPer100Derived = true;
                profile.CaloriesPer100g             = Round1(n.CaloriesKcal   * factor);
                profile.EstimatedCarbsPer100g       = Round1(n.Carbohydrates  * factor);
                profile.EstimatedSugarPer100g       = Round1(n.Sugar          * factor);
                profile.EstimatedAddedSugarPer100g  = Round1(n.AddedSugar     * factor);
                profile.EstimatedPolyolsPer100g     = Round1(n.Polyols        * factor);
                profile.EstimatedProteinPer100g     = Round1(n.Proteins       * factor);
                profile.EstimatedFatPer100g         = Round1(n.TotalFats      * factor);
                profile.EstimatedSaturatedFatPer100g= Round1(n.SaturatedFats  * factor);
                profile.EstimatedTransFatPer100g    = Round1(n.TransFats      * factor);
                profile.EstimatedSodiumPer100g      = Round1(n.SodiumMg       * factor);
                profile.EstimatedFiberPer100g       = Round1(n.Fiber          * factor);
            }
            else if (servingUnit == NutritionUnit.Milliliter)
            {
                var factor = 100.0 / result.Serving.Amount.Value;
                var n = result.NutritionPerServing;

                profile.NutritionUnit   = "ml";
                profile.IsPer100Derived = true;
                profile.CaloriesPer100ml            = Round1(n.CaloriesKcal   * factor);

                // Mesma regra do branch per100ml: alimenta os campos per100g do
                // scoring usando 100 ml ≈ 100 g (densidade ~1 para bebidas).
                profile.CaloriesPer100g             = Round1(n.CaloriesKcal   * factor);
                profile.EstimatedCarbsPer100g       = Round1(n.Carbohydrates  * factor);
                profile.EstimatedSugarPer100g       = Round1(n.Sugar          * factor);
                profile.EstimatedAddedSugarPer100g  = Round1(n.AddedSugar     * factor);
                profile.EstimatedPolyolsPer100g     = Round1(n.Polyols        * factor);
                profile.EstimatedProteinPer100g     = Round1(n.Proteins       * factor);
                profile.EstimatedFatPer100g         = Round1(n.TotalFats      * factor);
                profile.EstimatedSaturatedFatPer100g= Round1(n.SaturatedFats  * factor);
                profile.EstimatedTransFatPer100g    = Round1(n.TransFats      * factor);
                profile.EstimatedSodiumPer100g      = Round1(n.SodiumMg       * factor);
                profile.EstimatedFiberPer100g       = Round1(n.Fiber          * factor);
            }
        }

        return profile;
    }

    /// <summary>
    /// Arredonda para 1 casa decimal, preservando null.
    /// Evita artefatos como 616.6666... em valores calculados por fator de porção.
    /// </summary>
    private static double? Round1(double? value) =>
        value.HasValue ? Math.Round(value.Value, 1, MidpointRounding.AwayFromZero) : null;

    private static NutritionUnit DetectUnit(OpenAiVisionResult result)
    {
        if (result.NutritionPer100g != null) return NutritionUnit.Gram;
        if (result.NutritionPer100ml != null) return NutritionUnit.Milliliter;
        return ParseUnit(result.Serving?.Unit);
    }

    private static NutritionUnit ParseUnit(string? unit)
    {
        if (string.IsNullOrWhiteSpace(unit)) return NutritionUnit.Unknown;
        return unit.Trim().ToLowerInvariant() switch
        {
            "g"  => NutritionUnit.Gram,
            "ml" => NutritionUnit.Milliliter,
            _    => NutritionUnit.Unknown
        };
    }

    private static OpenAiNutritionExtractionResult ToConfidenceInput(OpenAiVisionResult result)
    {
        // Quando a tabela é em ml, alimentamos o confidence engine usando os
        // valores per 100ml na slot de "per100", já que as regras avaliadas
        // (limites físicos, cruzamentos açúcar/carbs, calorias = 4*c + 4*p + 9*f)
        // são igualmente válidas em base 100g e 100ml. Isso evita duplicar o engine.
        var per100 = result.NutritionPer100g ?? result.NutritionPer100ml;

        return new OpenAiNutritionExtractionResult
        {
            ProductName = result.ProductName,
            Brand = result.Brand,
            Serving = result.Serving is null
                ? null
                : new OpenAiNutritionServingInfo
                {
                    Amount = result.Serving.Amount,
                    Unit = result.Serving.Unit,
                    Description = result.Serving.Description
                },
            NutritionPerServing = result.NutritionPerServing is null
                ? null
                : new OpenAiNutritionInfo
                {
                    CaloriesKcal = result.NutritionPerServing.CaloriesKcal,
                    Carbohydrates = result.NutritionPerServing.Carbohydrates,
                    Proteins = result.NutritionPerServing.Proteins,
                    TotalFats = result.NutritionPerServing.TotalFats,
                    SaturatedFats = result.NutritionPerServing.SaturatedFats,
                    TransFats = result.NutritionPerServing.TransFats,
                    Fiber = result.NutritionPerServing.Fiber,
                    Sugar = result.NutritionPerServing.Sugar,
                    AddedSugar = result.NutritionPerServing.AddedSugar,
                    SodiumMg = result.NutritionPerServing.SodiumMg
                },
            NutritionPer100g = per100 is null
                ? null
                : new OpenAiNutritionInfo
                {
                    CaloriesKcal = per100.CaloriesKcal,
                    Carbohydrates = per100.Carbohydrates,
                    Proteins = per100.Proteins,
                    TotalFats = per100.TotalFats,
                    SaturatedFats = per100.SaturatedFats,
                    TransFats = per100.TransFats,
                    Fiber = per100.Fiber,
                    Sugar = per100.Sugar,
                    AddedSugar = per100.AddedSugar,
                    SodiumMg = per100.SodiumMg
                }
        };
    }

    private sealed class OpenAiVisionResult
    {
        public string? ProductName { get; set; }
        public string? Brand { get; set; }
        public ServingInfo? Serving { get; set; }
        public NutritionInfo? NutritionPerServing { get; set; }
        public NutritionInfo? NutritionPer100g { get; set; }
        public NutritionInfo? NutritionPer100ml { get; set; }
    }

    private sealed class ServingInfo
    {
        public double? Amount { get; set; }
        public string? Unit { get; set; }
        public string? Description { get; set; }
    }

    private sealed record OcrImageCrop(string Name, byte[] Bytes, int Width, int Height);

    private sealed record ConsensusField(string Name, double? OpenAiValue, double? DocumentValue);

    private sealed record ConsensusResult(bool Accepted, string Reason)
    {
        public static ConsensusResult Accept(string reason) => new(true, reason);
        public static ConsensusResult Reject(string reason) => new(false, reason);
    }

    private sealed class NutritionInfo
    {
        public double? CaloriesKcal { get; set; }
        public double? Carbohydrates { get; set; }
        public double? Proteins { get; set; }
        public double? TotalFats { get; set; }
        public double? SaturatedFats { get; set; }
        public double? TransFats { get; set; }
        public double? Fiber { get; set; }
        public double? Sugar { get; set; }
        public double? AddedSugar { get; set; }
        public double? Polyols { get; set; }
        public double? SodiumMg { get; set; }
    }

}
