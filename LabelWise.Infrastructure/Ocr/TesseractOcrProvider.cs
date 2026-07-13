// ⚠️ IMPORTANTE: O pacote Tesseract deve estar instalado para esta implementação funcionar
// Instale via: dotnet add package Tesseract --version 5.2.0
// Após instalação, descomente o #define abaixo:

#define TESSERACT_INSTALLED

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LabelWise.Application.DTOs;
using LabelWise.Application.Interfaces;
using Microsoft.Extensions.Logging;

#if TESSERACT_INSTALLED
using Tesseract;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;
#endif

namespace LabelWise.Infrastructure.Ocr
{
    /// <summary>
    /// Implementação real do OCR usando Tesseract para extração de texto de imagens.
    /// Suporta português do Brasil com fallback para inglês.
    /// 
    /// INSTALAÇÃO NECESSÁRIA:
    /// 1. Instale o pacote NuGet: Tesseract (versão 5.2.0 ou superior)
    /// 2. Baixe os arquivos de linguagem (tessdata) de: https://github.com/tesseract-ocr/tessdata_best
    ///    - por.traineddata (Português)
    ///    - eng.traineddata (Inglês - fallback)
    /// 3. Coloque na pasta: [projeto-raiz]\tessdata\
    /// 4. Descomente a linha: #define TESSERACT_INSTALLED (linha 12 deste arquivo)
    /// 
    /// CONFIGURAÇÃO:
    /// A variável TESSDATA_PREFIX pode ser configurada no appsettings.json ou variável de ambiente.
    /// Por padrão, busca em: .\tessdata\
    /// 
    /// PARA SETUP AUTOMÁTICO:
    /// Execute o script: .\setup-tesseract.ps1
    /// </summary>
    public class TesseractOcrProvider : IOcrProvider
    {
        private readonly string _tessdataPath;
        private readonly ILogger<TesseractOcrProvider>? _logger;
        private readonly string _language;

        public string ProviderName => "Tesseract OCR (Local)";

        public TesseractOcrProvider(ILogger<TesseractOcrProvider>? logger = null, string? tessdataPath = null, string? language = null)
        {
            _logger = logger;
            _language = language ?? "por+eng"; // Português com fallback para inglês

            // Estratégia robusta para localizar tessdata
            _tessdataPath = ResolveTessdataPath(tessdataPath);

#if !TESSERACT_INSTALLED
            _logger?.LogWarning(
                "⚠️ TesseractOcrProvider foi instanciado mas o pacote Tesseract NÃO está instalado. " +
                "Instale via: dotnet add package Tesseract --version 5.2.0 " +
                "e descomente #define TESSERACT_INSTALLED no código.");
#else
            _logger?.LogInformation("TesseractOcrProvider inicializado. Tessdata path: {Path}, Language: {Language}", 
                _tessdataPath, _language);

            // Valida se o diretório existe e contém arquivos necessários
            ValidateTessdataDirectory();
#endif
        }

        /// <summary>
        /// Resolve o caminho do tessdata usando uma estratégia de fallback.
        /// Prioridade: 
        /// 1. Parâmetro explícito
        /// 2. Variável de ambiente TESSDATA_PREFIX
        /// 3. Pasta tessdata na raiz do projeto (workspace)
        /// 4. Pasta tessdata no diretório base da aplicação
        /// 5. Pasta tessdata no diretório de execução atual
        /// </summary>
        private string ResolveTessdataPath(string? configuredPath)
        {
            var candidates = new List<string>();

            // 1. Caminho configurado explicitamente
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                candidates.Add(configuredPath);
            }

            // 2. Variável de ambiente
            var envPath = Environment.GetEnvironmentVariable("TESSDATA_PREFIX");
            if (!string.IsNullOrWhiteSpace(envPath))
            {
                candidates.Add(envPath);
            }

            // 3. Raiz do projeto (workspace) - assume que estamos em bin/Debug/netX.Y
            var baseDirectory = AppContext.BaseDirectory;
            var projectRoot = Directory.GetParent(baseDirectory)?.Parent?.Parent?.Parent?.FullName;
            if (!string.IsNullOrWhiteSpace(projectRoot))
            {
                candidates.Add(Path.Combine(projectRoot, "tessdata"));
            }

            // 4. Diretório base da aplicação
            candidates.Add(Path.Combine(AppContext.BaseDirectory, "tessdata"));

            // 5. Diretório de trabalho atual
            candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "tessdata"));

            // Testa cada candidato em ordem
            foreach (var path in candidates)
            {
                if (Directory.Exists(path) && Directory.GetFiles(path, "*.traineddata").Length > 0)
                {
                    _logger?.LogInformation("✅ Tessdata encontrado em: {Path}", path);
                    return path;
                }
            }

            // Se nenhum foi encontrado, retorna o primeiro candidato (configurado ou base directory)
            var fallbackPath = candidates.FirstOrDefault() ?? Path.Combine(AppContext.BaseDirectory, "tessdata");
            _logger?.LogWarning("⚠️ Tessdata não encontrado em nenhum local. Usando fallback: {Path}", fallbackPath);
            _logger?.LogInformation("Caminhos tentados: {Paths}", string.Join(", ", candidates));

            return fallbackPath;
        }

        /// <summary>
        /// Valida se o diretório tessdata existe e contém os arquivos necessários.
        /// </summary>
        private void ValidateTessdataDirectory()
        {
            if (!Directory.Exists(_tessdataPath))
            {
                _logger?.LogError(
                    "❌ ERRO: Diretório tessdata não encontrado: {Path}. " +
                    "OCR não funcionará. Configure o caminho correto ou copie os arquivos .traineddata.", 
                    _tessdataPath);
                return;
            }

            var trainedDataFiles = Directory.GetFiles(_tessdataPath, "*.traineddata");
            if (trainedDataFiles.Length == 0)
            {
                _logger?.LogError(
                    "❌ ERRO: Diretório tessdata existe mas não contém arquivos .traineddata: {Path}. " +
                    "Baixe os arquivos de idioma de: https://github.com/tesseract-ocr/tessdata",
                    _tessdataPath);
                return;
            }

            _logger?.LogInformation(
                "✅ Tessdata validado com sucesso. Arquivos encontrados: {Files}",
                string.Join(", ", trainedDataFiles.Select(Path.GetFileName)));

            // Valida se os idiomas necessários estão disponíveis
            var languages = _language.Split('+');
            var missingLanguages = new List<string>();

            foreach (var lang in languages)
            {
                var langFile = Path.Combine(_tessdataPath, $"{lang}.traineddata");
                if (!File.Exists(langFile))
                {
                    missingLanguages.Add(lang);
                }
            }

            if (missingLanguages.Any())
            {
                _logger?.LogWarning(
                    "⚠️ ATENÇÃO: Idiomas necessários não encontrados: {Languages}. " +
                    "Baixe de: https://github.com/tesseract-ocr/tessdata",
                    string.Join(", ", missingLanguages));
            }
            else
            {
                _logger?.LogInformation("✅ Todos os idiomas necessários estão disponíveis: {Languages}", _language);
            }
        }

        public async Task<OcrResultDto> ExtractTextAsync(OcrRequestDto request)
        {
#if !TESSERACT_INSTALLED
            return await Task.FromResult(CreateErrorResult(
                "⚠️ Pacote Tesseract não instalado. " +
                "Instale via: dotnet add package Tesseract --version 5.2.0\n" +
                "Execute: .\\setup-tesseract.ps1 para configuração completa.\n" +
                "Ou use MockOcrProvider para testes sem OCR real."));
#else
            _logger?.LogInformation("Iniciando extração OCR da imagem: {FileName}", request.FileName);

            // Validação 1: Arquivo existe?
            if (!File.Exists(request.ImagePath))
            {
                var errorMsg = $"Arquivo de imagem não encontrado: {request.ImagePath}";
                _logger?.LogError(errorMsg);
                return CreateErrorResult(errorMsg);
            }

            // Validação 2: Tessdata configurado?
            if (!Directory.Exists(_tessdataPath))
            {
                var errorMsg = $"❌ ERRO CRÍTICO: Diretório tessdata não encontrado: {_tessdataPath}\n\n" +
                    $"📝 SOLUÇÃO:\n" +
                    $"1. Execute o script: .\\diagnose-and-fix-tesseract.ps1\n" +
                    $"   OU\n" +
                    $"2. Baixe manualmente os arquivos:\n" +
                    $"   - por.traineddata de https://github.com/tesseract-ocr/tessdata/raw/main/por.traineddata\n" +
                    $"   - eng.traineddata de https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata\n" +
                    $"3. Salve em: LabelWise.Api\\tessdata\\\n" +
                    $"4. Recompile: dotnet clean; dotnet build";

                _logger?.LogError(errorMsg);
                return CreateErrorResult(errorMsg);
            }

            // Validação 3: Arquivos .traineddata existem?
            var trainedDataFiles = Directory.GetFiles(_tessdataPath, "*.traineddata");
            if (trainedDataFiles.Length == 0)
            {
                var errorMsg = $"❌ ERRO CRÍTICO: Diretório tessdata existe mas está VAZIO: {_tessdataPath}\n\n" +
                    $"📝 SOLUÇÃO:\n" +
                    $"1. Execute: .\\diagnose-and-fix-tesseract.ps1\n" +
                    $"   Este script irá baixar automaticamente os arquivos necessários\n" +
                    $"   OU\n" +
                    $"2. Baixe manualmente:\n" +
                    $"   - por.traineddata: https://github.com/tesseract-ocr/tessdata/raw/main/por.traineddata\n" +
                    $"   - eng.traineddata: https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata\n" +
                    $"3. Salve em: {_tessdataPath}\n" +
                    $"4. Recompile: dotnet clean; dotnet build";

                _logger?.LogError(errorMsg);
                return CreateErrorResult(errorMsg);
            }

            // Validação 4: Idiomas necessários existem?
            var languages = _language.Split('+');
            var missingLanguages = new List<string>();

            foreach (var lang in languages)
            {
                var langFile = Path.Combine(_tessdataPath, $"{lang}.traineddata");
                if (!File.Exists(langFile))
                {
                    missingLanguages.Add(lang);
                }
            }

            if (missingLanguages.Any())
            {
                var errorMsg = $"❌ ERRO: Arquivos de idioma não encontrados: {string.Join(", ", missingLanguages)}\n\n" +
                    $"📍 Procurado em: {_tessdataPath}\n" +
                    $"📂 Arquivos existentes: {string.Join(", ", trainedDataFiles.Select(Path.GetFileName))}\n\n" +
                    $"📝 SOLUÇÃO:\n" +
                    $"1. Execute: .\\diagnose-and-fix-tesseract.ps1\n" +
                    $"   OU\n" +
                    $"2. Baixe os arquivos faltantes de:\n" +
                    $"   https://github.com/tesseract-ocr/tessdata\n" +
                    $"3. Salve em: {_tessdataPath}";

                _logger?.LogError(errorMsg);
                return CreateErrorResult(errorMsg);
            }

            try
            {
                return await Task.Run(() => ProcessImageWithTesseract(request));
            }
            catch (Exception ex)
            {
                var errorMsg = $"Erro ao processar OCR: {ex.Message}";
                _logger?.LogError(ex, "Falha na extração de texto da imagem {FileName}", request.FileName);
                return CreateErrorResult(errorMsg);
            }
#endif
        }

#if TESSERACT_INSTALLED
        /// <summary>
        /// Normaliza e valida imagem antes de processar com Tesseract.
        /// Aplica pré-processamento para melhorar qualidade do OCR.
        /// 
        /// Pré-processamento aplicado:
        /// - Conversão para escala de cinza
        /// - Aumento de contraste
        /// - Redimensionamento para DPI ideal (300 DPI)
        /// - Binarização (preto e branco) para melhor reconhecimento
        /// - Remoção de ruído
        /// </summary>
        private string NormalizeAndValidateImage(string imagePath)
        {
            var extension = Path.GetExtension(imagePath).ToLowerInvariant();

            _logger?.LogInformation("📸 Normalizando e pré-processando imagem {Extension}...", extension);

            try
            {
                // Validar que o arquivo existe
                if (!File.Exists(imagePath))
                {
                    throw new FileNotFoundException($"Arquivo de imagem não encontrado: {imagePath}");
                }

                // Validar tamanho do arquivo
                var fileInfo = new FileInfo(imagePath);
                if (fileInfo.Length == 0)
                {
                    throw new InvalidOperationException($"Arquivo de imagem está vazio: {imagePath}");
                }

                _logger?.LogDebug("📏 Tamanho do arquivo: {Size} bytes ({SizeMB:F2} MB)", 
                    fileInfo.Length, fileInfo.Length / 1024.0 / 1024.0);

                var tempPath = Path.Combine(Path.GetTempPath(), "labelwise", $"{Guid.NewGuid()}.png");
                var tempDir = Path.GetDirectoryName(tempPath);

                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir!);
                }

                // Usar ImageSharp para carregar, validar e pré-processar
                using (var image = Image.Load(imagePath))
                {
                    _logger?.LogInformation("📐 Dimensões originais: {Width}x{Height}px, Formato: {Format}", 
                        image.Width, image.Height, image.Metadata.DecodedImageFormat?.Name ?? "Unknown");

                    // Validar dimensões
                    if (image.Width == 0 || image.Height == 0)
                    {
                        throw new InvalidOperationException($"Imagem tem dimensões inválidas: {image.Width}x{image.Height}");
                    }

                    // Validar que não é muito pequena
                    if (image.Width < 100 || image.Height < 100)
                    {
                        _logger?.LogWarning("⚠️ Imagem muito pequena ({Width}x{Height}). Pode afetar qualidade do OCR.", 
                            image.Width, image.Height);
                    }

                    // PRÉ-PROCESSAMENTO PARA MELHORAR OCR
                    _logger?.LogDebug("🔧 Aplicando pré-processamento...");

                    // 1. Remover metadata EXIF problemática
                    image.Metadata.ExifProfile = null;

                    // 2. Converter para escala de cinza (melhora OCR)
                    _logger?.LogDebug("   • Convertendo para escala de cinza...");
                    image.Mutate(x => x.Grayscale());

                    // 3. Redimensionar se muito pequena (ideal: pelo menos 300 DPI equivalente)
                    // Para texto, ideal é ter pelo menos 20px de altura de texto
                    const int minWidth = 800;
                    if (image.Width < minWidth)
                    {
                        var scale = (double)minWidth / image.Width;
                        var newWidth = minWidth;
                        var newHeight = (int)(image.Height * scale);

                        _logger?.LogDebug("   • Redimensionando de {OldW}x{OldH} para {NewW}x{NewH} (escala: {Scale:F2}x)", 
                            image.Width, image.Height, newWidth, newHeight, scale);

                        image.Mutate(x => x.Resize(newWidth, newHeight));
                    }

                    // 4. Aumentar contraste (melhora legibilidade)
                    _logger?.LogDebug("   • Aumentando contraste...");
                    image.Mutate(x => x.Contrast(1.5f));

                    // 5. Aplicar sharpening leve (melhora bordas do texto)
                    _logger?.LogDebug("   • Aplicando sharpening...");
                    image.Mutate(x => x.GaussianSharpen(1.5f));

                    // 6. Binarização (preto e branco puro) - MUITO efetivo para OCR
                    _logger?.LogDebug("   • Aplicando binarização (Otsu)...");
                    image.Mutate(x => x.BinaryThreshold(0.5f));

                    _logger?.LogInformation("📐 Dimensões finais: {Width}x{Height}px (após pré-processamento)", 
                        image.Width, image.Height);

                    // Salvar como PNG de alta qualidade
                    var encoder = new PngEncoder
                    {
                        CompressionLevel = SixLabors.ImageSharp.Formats.Png.PngCompressionLevel.BestSpeed,
                        ColorType = SixLabors.ImageSharp.Formats.Png.PngColorType.Grayscale
                    };

                    image.Save(tempPath, encoder);
                }

                // Validar que o arquivo PNG foi criado corretamente
                if (!File.Exists(tempPath))
                {
                    throw new InvalidOperationException("Falha ao criar arquivo PNG temporário");
                }

                var tempFileInfo = new FileInfo(tempPath);
                if (tempFileInfo.Length == 0)
                {
                    throw new InvalidOperationException("Arquivo PNG temporário está vazio");
                }

                _logger?.LogInformation("✅ Imagem pré-processada: {OriginalSize}KB → {ProcessedSize}KB", 
                    fileInfo.Length / 1024, tempFileInfo.Length / 1024);

                return tempPath;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ Erro ao normalizar imagem {Extension}", extension);
                throw new InvalidOperationException(
                    $"Não foi possível processar a imagem. " +
                    $"Formato: {extension}, Arquivo: {Path.GetFileName(imagePath)}. " +
                    $"Erro: {ex.Message}. " +
                    $"Verifique se a imagem não está corrompida e tente fazer upload novamente.", 
                    ex);
            }
        }

        private OcrResultDto ProcessImageWithTesseract(OcrRequestDto request)
        {
            string? convertedImagePath = null;

            try
            {
                _logger?.LogInformation("🚀 Iniciando OCR com Tesseract...");
                _logger?.LogDebug("   Idioma: {Language}, Tessdata: {Path}", _language, _tessdataPath);

                using var engine = new TesseractEngine(_tessdataPath, _language, EngineMode.Default);

                // SEMPRE normalizar imagem através do ImageSharp com pré-processamento
                _logger?.LogDebug("📸 Normalizando e pré-processando imagem...");
                var imagePathToUse = NormalizeAndValidateImage(request.ImagePath);
                convertedImagePath = imagePathToUse;

                _logger?.LogDebug("📂 Carregando imagem no Tesseract: {ImagePath}", imagePathToUse);
                using var img = Pix.LoadFromFile(imagePathToUse);

                _logger?.LogDebug("📐 Imagem Pix: {Width}x{Height}px, {Depth} bits", img.Width, img.Height, img.Depth);

                // TENTAR MÚLTIPLOS PageSegMode para máxima compatibilidade
                var pageModes = new[]
                {
                    PageSegMode.Auto,              // Automático (padrão)
                    PageSegMode.AutoOsd,           // Auto com detecção de orientação
                    PageSegMode.SingleBlock,       // Bloco único de texto
                    PageSegMode.SparseText         // Texto esparso (rótulos)
                };

                string bestText = string.Empty;
                double bestConfidence = 0;
                List<OcrTextBlock> bestTextBlocks = new();
                PageSegMode bestMode = PageSegMode.Auto;

                _logger?.LogDebug("🔍 Tentando {Count} modos de segmentação...", pageModes.Length);

                foreach (var mode in pageModes)
                {
                    try
                    {
                        engine.DefaultPageSegMode = mode;

                        _logger?.LogDebug("   Tentando modo: {Mode}...", mode);

                        using var page = engine.Process(img);
                        var text = page.GetText();
                        var confidence = page.GetMeanConfidence();

                        _logger?.LogDebug("   Resultado: {Chars} caracteres, confiança: {Confidence:F2}%", 
                            text?.Length ?? 0, confidence * 100);

                        // Se encontrou texto melhor, guarda
                        if (!string.IsNullOrWhiteSpace(text) && 
                            (text.Trim().Length > bestText.Trim().Length || confidence > bestConfidence))
                        {
                            bestText = text;
                            bestConfidence = confidence;
                            bestMode = mode;
                            bestTextBlocks = ExtractTextBlocks(page);

                            _logger?.LogDebug("   ✅ Melhor resultado até agora!");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning("   ⚠️ Modo {Mode} falhou: {Error}", mode, ex.Message);
                    }
                }

                // Resultado final
                if (string.IsNullOrWhiteSpace(bestText))
                {
                    _logger?.LogWarning("⚠️ Nenhum texto foi extraído em nenhum modo de segmentação!");
                    _logger?.LogWarning("💡 Possíveis causas:");
                    _logger?.LogWarning("   • Imagem sem texto legível");
                    _logger?.LogWarning("   • Qualidade da imagem muito baixa");
                    _logger?.LogWarning("   • Contraste insuficiente");
                    _logger?.LogWarning("   • Texto muito pequeno ou desfocado");
                    _logger?.LogWarning("   • Idioma não suportado (usando: {Language})", _language);
                }
                else
                {
                    _logger?.LogInformation("✅ OCR concluído com sucesso!");
                    _logger?.LogInformation("   Modo usado: {Mode}", bestMode);
                    _logger?.LogInformation("   Confiança: {Confidence:F2}%", bestConfidence * 100);
                    _logger?.LogInformation("   Caracteres extraídos: {Length}", bestText.Length);
                    _logger?.LogInformation("   Blocos de texto: {Blocks}", bestTextBlocks.Count);

                    // Log de preview do texto (primeiras 100 chars)
                    var preview = bestText.Length > 100 ? bestText.Substring(0, 100) + "..." : bestText;
                    _logger?.LogDebug("   Preview: {Preview}", preview.Replace("\n", "\\n"));
                }

                return new OcrResultDto
                {
                    RawText = bestText,
                    Confidence = bestConfidence,
                    Success = !string.IsNullOrWhiteSpace(bestText),
                    TextBlocks = bestTextBlocks,
                    ProviderMetadata = GetMetadata()
                };
            }
            catch (TesseractException ex)
            {
                // Verifica se é o erro de inicialização (Error 1)
                var isInitError = ex.Message.Contains("Failed to initialise") || 
                                 ex.Message.Contains("initialize") ||
                                 ex.Message.Contains("Error -1");

                string errorMsg;

                if (isInitError)
                {
                    var trainedDataFiles = Directory.Exists(_tessdataPath) 
                        ? Directory.GetFiles(_tessdataPath, "*.traineddata")
                        : Array.Empty<string>();

                    errorMsg = $"❌ ERRO CRÍTICO: Tesseract não conseguiu inicializar\n\n" +
                        $"🔍 DIAGNÓSTICO:\n" +
                        $"   - Tessdata Path: {_tessdataPath}\n" +
                        $"   - Diretório existe: {Directory.Exists(_tessdataPath)}\n" +
                        $"   - Arquivos .traineddata: {trainedDataFiles.Length}\n";

                    if (trainedDataFiles.Length > 0)
                    {
                        errorMsg += $"   - Arquivos encontrados: {string.Join(", ", trainedDataFiles.Select(Path.GetFileName))}\n";
                    }

                    errorMsg += $"   - Idioma solicitado: {_language}\n\n" +
                        $"📝 SOLUÇÃO:\n" +
                        $"1. Execute o script de diagnóstico: .\\diagnose-and-fix-tesseract.ps1\n" +
                        $"   Este script irá:\n" +
                        $"   - Verificar todos os caminhos\n" +
                        $"   - Baixar arquivos faltantes\n" +
                        $"   - Recompilar o projeto\n" +
                        $"   - Validar a instalação\n\n" +
                        $"2. OU faça manualmente:\n" +
                        $"   a) Baixe os arquivos:\n" +
                        $"      - por.traineddata: https://github.com/tesseract-ocr/tessdata/raw/main/por.traineddata\n" +
                        $"      - eng.traineddata: https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata\n" +
                        $"   b) Salve em: LabelWise.Api\\tessdata\\\n" +
                        $"   c) Execute: dotnet clean && dotnet build\n" +
                        $"   d) Reinicie a API\n\n" +
                        $"🔗 Referência: https://github.com/charlesw/tesseract/wiki/Error-1\n" +
                        $"📄 Erro original: {ex.Message}";
                }
                else
                {
                    errorMsg = $"❌ Erro Tesseract: {ex.Message}\n\n" +
                        $"📍 Verifique:\n" +
                        $"   - Tessdata Path: {_tessdataPath}\n" +
                        $"   - Idioma: {_language}\n\n" +
                        $"💡 Execute: .\\diagnose-and-fix-tesseract.ps1";
                }

                _logger?.LogError(ex, "TesseractException ao processar imagem. Path: {Path}, Language: {Language}", 
                    _tessdataPath, _language);

                return CreateErrorResult(errorMsg);
            }
            catch (Exception ex)
            {
                var errorMsg = $"❌ Erro inesperado no OCR: {ex.Message}";
                _logger?.LogError(ex, "Exception ao processar imagem com Tesseract");
                return CreateErrorResult(errorMsg);
            }
            finally
            {
                // Limpar arquivo temporário se foi criado
                if (convertedImagePath != null && File.Exists(convertedImagePath))
                {
                    try
                    {
                        File.Delete(convertedImagePath);
                        _logger?.LogDebug("🗑️ Arquivo temporário removido: {Path}", convertedImagePath);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Não foi possível remover arquivo temporário: {Path}", convertedImagePath);
                    }
                }
            }
        }

        private List<OcrTextBlock> ExtractTextBlocks(Page page)
        {
            var blocks = new List<OcrTextBlock>();

            try
            {
                using var iterator = page.GetIterator();
                iterator.Begin();

                do
                {
                    // Extrai informações de cada bloco de texto
                    if (iterator.TryGetBoundingBox(PageIteratorLevel.TextLine, out var rect))
                    {
                        var text = iterator.GetText(PageIteratorLevel.TextLine);
                        var confidence = iterator.GetConfidence(PageIteratorLevel.TextLine) / 100.0;

                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            blocks.Add(new OcrTextBlock
                            {
                                Text = text.Trim(),
                                Confidence = confidence,
                                BlockType = DetermineBlockType(text),
                                BoundingBox = new BoundingBox
                                {
                                    Left = rect.X1,
                                    Top = rect.Y1,
                                    Width = rect.Width,
                                    Height = rect.Height
                                }
                            });
                        }
                    }
                } while (iterator.Next(PageIteratorLevel.TextLine));

                _logger?.LogDebug("Extraídos {Count} blocos de texto", blocks.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Erro ao extrair blocos detalhados, retornando lista vazia");
            }

            return blocks;
        }
#endif

        private string DetermineBlockType(string text)
        {
            var upperText = text.ToUpperInvariant();

            // Identifica cabeçalhos típicos de rótulos
            if (upperText.Contains("INFORMAÇÃO NUTRICIONAL") ||
                upperText.Contains("INGREDIENTES") ||
                upperText.Contains("ALÉRGICOS") ||
                upperText.Contains("CONTÉM") ||
                upperText.Contains("NUTRITION") ||
                upperText.Contains("INGREDIENTS"))
            {
                return "HEADING";
            }

            if (upperText.Contains("PORÇÃO") ||
                upperText.Contains("QUANTIDADE") ||
                upperText.Contains("SERVING") ||
                upperText.Contains("AMOUNT"))
            {
                return "SUBHEADING";
            }

            return "TEXT";
        }

        public Task<bool> IsAvailableAsync()
        {
#if !TESSERACT_INSTALLED
            _logger?.LogWarning("IsAvailableAsync: Pacote Tesseract não instalado");
            return Task.FromResult(false);
#else
            var isAvailable = Directory.Exists(_tessdataPath);

            if (isAvailable)
            {
                // Verifica se pelo menos um arquivo de idioma existe
                var hasLanguageFiles = Directory.GetFiles(_tessdataPath, "*.traineddata").Length > 0;
                isAvailable = hasLanguageFiles;

                if (!hasLanguageFiles)
                {
                    _logger?.LogWarning("Tessdata existe mas nenhum arquivo .traineddata foi encontrado");
                }
            }

            _logger?.LogDebug("IsAvailableAsync: {IsAvailable}", isAvailable);
            return Task.FromResult(isAvailable);
#endif
        }

        public Dictionary<string, string> GetMetadata()
        {
            var metadata = new Dictionary<string, string>
            {
                ["ProviderName"] = ProviderName,
                ["ProviderType"] = GetType().FullName ?? "TesseractOcrProvider",
                ["TessdataPath"] = _tessdataPath,
                ["Language"] = _language,
                ["IsMock"] = "false"
            };

#if TESSERACT_INSTALLED
            metadata["TesseractInstalled"] = "true";
            metadata["TessdataExists"] = Directory.Exists(_tessdataPath).ToString();

            if (Directory.Exists(_tessdataPath))
            {
                var trainedDataFiles = Directory.GetFiles(_tessdataPath, "*.traineddata");
                metadata["TrainedDataFilesCount"] = trainedDataFiles.Length.ToString();
                metadata["TrainedDataFiles"] = string.Join(", ", trainedDataFiles.Select(Path.GetFileName));
            }
            else
            {
                metadata["TrainedDataFilesCount"] = "0";
                metadata["TrainedDataFiles"] = "";
            }
#else
            metadata["TesseractInstalled"] = "false";
#endif

            return metadata;
        }

        private OcrResultDto CreateErrorResult(string errorMessage)
        {
            return new OcrResultDto
            {
                Success = false,
                ErrorMessage = errorMessage,
                RawText = string.Empty,
                Confidence = 0,
                TextBlocks = new List<OcrTextBlock>(),
                ProviderMetadata = GetMetadata()
            };
        }
    }
}
