using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using LabelWise.Application.DTOs;
using LabelWise.Application.Interfaces;

namespace LabelWise.Infrastructure.Ocr
{
    /// <summary>
    /// Implementação mock do OCR para testes e desenvolvimento.
    /// Retorna dados simulados representativos de um rótulo nutricional real.
    /// IMPORTANTE: Este provider simula OCR real, mas deve ser substituído por um provider
    /// real (TesseractOcrProvider ou AzureComputerVisionOcrProvider) em produção.
    /// </summary>
    public class MockOcrProvider : IOcrProvider
    {
        private readonly Random _random = new Random();

        public string ProviderName => "Mock OCR Provider (Development Only)";

        public Task<OcrResultDto> ExtractTextAsync(OcrRequestDto request)
        {
            // Simula leitura do arquivo para validação básica
            if (!File.Exists(request.ImagePath))
            {
                return Task.FromResult(new OcrResultDto
                {
                    Success = false,
                    ErrorMessage = "Arquivo de imagem não encontrado.",
                    RawText = string.Empty,
                    Confidence = 0,
                    TextBlocks = new List<OcrTextBlock>(),
                    ProviderMetadata = GetMetadata()
                });
            }

            // Simula variabilidade no OCR (como seria no mundo real)
            var confidence = 0.85 + (_random.NextDouble() * 0.10); // 85-95%

            // Simula um texto OCR típico de um rótulo nutricional brasileiro
            var mockText = GenerateRealisticLabelText();

            var result = new OcrResultDto
            {
                RawText = mockText.Trim(),
                Confidence = confidence,
                Success = true,
                TextBlocks = GenerateTextBlocks(mockText),
                ProviderMetadata = GetMetadata()
            };

            return Task.FromResult(result);
        }

        public Task<bool> IsAvailableAsync()
        {
            return Task.FromResult(true);
        }

        private string GenerateRealisticLabelText()
        {
            // Varia entre diferentes tipos de produtos simulados
            var variant = _random.Next(0, 3);

            return variant switch
            {
                0 => GenerateCerealLabel(),
                1 => GenerateCookieLabel(),
                2 => GenerateYogurtLabel(),
                _ => GenerateCerealLabel()
            };
        }

        private string GenerateCerealLabel()
        {
            return @"
CEREAL MATINAL INTEGRAL

INFORMAÇÃO NUTRICIONAL
Porção 30g (1/2 xícara)

Quantidade por porção    %VD(*)
Valor energético    130kcal    7%
Carboidratos        23g        8%
Proteínas           3g         4%
Gorduras totais     2,5g       5%
Gorduras saturadas  0,5g       2%
Gorduras trans      0g         -
Fibra alimentar     2g         8%
Sódio              150mg       6%

(*) % Valores Diários de referência com base em uma dieta de 2.000 kcal ou 8400 kJ.

INGREDIENTES: Farinha de trigo enriquecida com ferro e ácido fólico, açúcar, gordura vegetal, 
sal, fermentos químicos (bicarbonato de sódio, bicarbonato de amônio e pirofosfato ácido de sódio), 
emulsificante lecitina de soja e aromatizante.

ALÉRGICOS: CONTÉM GLÚTEN. CONTÉM DERIVADOS DE TRIGO E SOJA. 
PODE CONTER LEITE, CENTEIO, CEVADA, AVEIA E SEUS DERIVADOS.
";
        }

        private string GenerateCookieLabel()
        {
            return @"
BISCOITO RECHEADO

INFORMAÇÃO NUTRICIONAL
Porção 30g (3 unidades)

Quantidade por porção    %VD(*)
Valor energético    150kcal    8%
Carboidratos        20g        7%
Proteínas           2g         3%
Gorduras totais     7g         13%
Gorduras saturadas  3,5g       16%
Gorduras trans      0g         -
Fibra alimentar     0,5g       2%
Sódio              90mg        4%

INGREDIENTES: Farinha de trigo enriquecida, açúcar, gordura vegetal hidrogenada, 
cacau em pó, açúcar invertido, amido, sal, fermentos químicos bicarbonato de amônio 
e bicarbonato de sódio, emulsificante lecitina de soja, aromatizantes.

ALÉRGICOS: CONTÉM GLÚTEN. CONTÉM DERIVADOS DE TRIGO, SOJA E LEITE.
PODE CONTER AMENDOIM, CASTANHAS E OVO.
";
        }

        private string GenerateYogurtLabel()
        {
            return @"
IOGURTE NATURAL INTEGRAL

INFORMAÇÃO NUTRICIONAL
Porção 170g (1 unidade)

Quantidade por porção    %VD(*)
Valor energético    115kcal    6%
Carboidratos        11g        4%
Proteínas           6g         8%
Gorduras totais     5g         9%
Gorduras saturadas  3g         14%
Gorduras trans      0g         -
Fibra alimentar     0g         0%
Sódio              70mg        3%
Cálcio             200mg      20%

INGREDIENTES: Leite integral, leite em pó desnatado, açúcar, preparado de morango 
(polpa de morango, açúcar, amido modificado, corante natural carmim), 
fermentos lácteos (Streptococcus thermophilus e Lactobacillus bulgaricus).

ALÉRGICOS: CONTÉM LEITE E DERIVADOS DE LEITE.
NÃO CONTÉM GLÚTEN.
";
        }

        private List<OcrTextBlock> GenerateTextBlocks(string fullText)
        {
            var blocks = new List<OcrTextBlock>();
            var lines = fullText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            var y = 10;
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var trimmedLine = line.Trim();
                var blockType = DetermineBlockType(trimmedLine);

                blocks.Add(new OcrTextBlock
                {
                    Text = trimmedLine,
                    Confidence = 0.88 + (_random.NextDouble() * 0.10),
                    BlockType = blockType,
                    BoundingBox = new BoundingBox 
                    { 
                        Left = 10, 
                        Top = y, 
                        Width = 300, 
                        Height = 20 
                    }
                });

                y += 25;
            }

            return blocks;
        }

        private string DetermineBlockType(string line)
        {
            var upperLine = line.ToUpperInvariant();

            if (upperLine.Contains("INFORMAÇÃO NUTRICIONAL") || 
                upperLine.Contains("INGREDIENTES") || 
                upperLine.Contains("ALÉRGICOS"))
                return "HEADING";

            if (upperLine.Contains("PORÇÃO") || upperLine.Contains("QUANTIDADE"))
                return "SUBHEADING";

            return "TEXT";
        }

        public Dictionary<string, string> GetMetadata()
        {
            return new Dictionary<string, string>
            {
                ["ProviderName"] = ProviderName,
                ["ProviderType"] = GetType().FullName ?? "MockOcrProvider",
                ["IsMock"] = "true",
                ["Warning"] = "This is a MOCK provider returning SIMULATED data. Not for production use."
            };
        }
    }
}
