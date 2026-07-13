using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using LabelWise.Domain.Enums;

namespace LabelWise.Application.Parsing.Strategies
{
    /// <summary>
    /// Implementação do parser de declarações de alérgenos.
    /// Separa containsAllergens e mayContainAllergens.
    /// Reconhece frases: "contém", "contém derivados de", "pode conter", "não contém".
    /// </summary>
    public class AllergenParser : IAllergenParser
    {
        private static readonly string[] CommonAllergens =
        {
            "glúten", "gluten",
            "leite", "lactose",
            "soja",
            "amendoim",
            "amêndoa", "amendoa",
            "castanha",
            "noz", "nozes",
            "peixe",
            "crustáceo", "crustaceo", "crustáceos", "crustaceos",
            "camarão", "camarao",
            "ovo", "ovos",
            "trigo",
            "cevada",
            "centeio",
            "aveia",
            "gergelim",
            "mostarda",
            "sulfito", "sulfitos"
        };

        private static readonly Dictionary<string, AllergenDeclarationType> DeclarationPhrases = new()
        {
            // Confirmados (contém)
            { "contém", AllergenDeclarationType.Contains },
            { "contem", AllergenDeclarationType.Contains },
            { "contém derivados de", AllergenDeclarationType.ContainsDerivatives },
            { "contem derivados de", AllergenDeclarationType.ContainsDerivatives },
            { "contém traços de", AllergenDeclarationType.MayContain },
            { "contem tracos de", AllergenDeclarationType.MayContain },

            // Potenciais (pode conter)
            { "pode conter", AllergenDeclarationType.MayContain },
            { "pode conter traços de", AllergenDeclarationType.MayContain },
            { "pode conter tracos de", AllergenDeclarationType.MayContain },
            { "produzido em linha que processa", AllergenDeclarationType.MayContain },

            // Negativos (não contém)
            { "não contém", AllergenDeclarationType.DoesNotContain },
            { "nao contem", AllergenDeclarationType.DoesNotContain },
            { "isento de", AllergenDeclarationType.DoesNotContain },
            { "sem", AllergenDeclarationType.DoesNotContain },
            { "livre de", AllergenDeclarationType.DoesNotContain },
            { "zero", AllergenDeclarationType.DoesNotContain }
        };

        private enum AllergenDeclarationType
        {
            Contains,
            ContainsDerivatives,
            MayContain,
            DoesNotContain
        }

        public AllergenParseResult Parse(string ocrText)
        {
            var result = new AllergenParseResult
            {
                Confidence = ConfidenceLevel.High
            };

            if (string.IsNullOrWhiteSpace(ocrText))
            {
                result.Confidence = ConfidenceLevel.Low;
                result.ValidationWarnings.Add("Texto OCR vazio ou nulo");
                return result;
            }

            var normalizedText = NormalizeText(ocrText);

            // Extrair frases com declarações de alérgenos
            ExtractAllergenPhrases(normalizedText, result);

            // Validação e ajuste de confiança
            ValidateAndAdjustConfidence(result);

            // Remover duplicatas
            result.ConfirmedAllergens = result.ConfirmedAllergens.Distinct().ToList();
            result.MayContainAllergens = result.MayContainAllergens.Distinct().ToList();
            result.DoesNotContainAllergens = result.DoesNotContainAllergens.Distinct().ToList();

            return result;
        }

        private static string NormalizeText(string text)
        {
            var normalized = text ?? string.Empty;
            normalized = normalized.Replace('\r', ' ').Replace('\n', ' ');
            normalized = Regex.Replace(normalized, @"[ \u00A0\t]+", " ");
            normalized = normalized.ToLowerInvariant();
            return normalized.Trim();
        }

        private static void ExtractAllergenPhrases(string text, AllergenParseResult result)
        {
            // IMPORTANTE: Processar frases negativas PRIMEIRO para evitar falsos positivos
            // Ex: "NÃO CONTÉM GLÚTEN" deve ser detectado antes de "CONTÉM GLÚTEN"
            var sortedPhrases = DeclarationPhrases
                .OrderByDescending(kvp => kvp.Value == AllergenDeclarationType.DoesNotContain) // negativas primeiro
                .ThenByDescending(kvp => kvp.Key.Length); // depois por tamanho

            // Rastrear contextos já processados para evitar sobreposição
            var processedContexts = new HashSet<string>();

            foreach (var phraseEntry in sortedPhrases)
            {
                var phrase = phraseEntry.Key;
                var declarationType = phraseEntry.Value;

                var index = text.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);

                while (index >= 0)
                {
                    // Extrair a frase completa (até o próximo ponto ou fim do texto)
                    var phraseText = ExtractFullPhrase(text, index);

                    // Verificar se este contexto já foi processado com uma declaração negativa
                    var contextKey = $"{index}-{phraseText.Length}";
                    if (processedContexts.Contains(contextKey))
                    {
                        index = text.IndexOf(phrase, index + phrase.Length, StringComparison.OrdinalIgnoreCase);
                        continue;
                    }

                    // Para declarações negativas, marcar contexto mais amplo
                    if (declarationType == AllergenDeclarationType.DoesNotContain)
                    {
                        // Marcar todos os contextos que se sobrepõem com esta frase
                        for (int i = Math.Max(0, index - 10); i < index + phraseText.Length + 10; i++)
                        {
                            processedContexts.Add($"{i}-{phraseText.Length}");
                        }
                    }

                    result.ExtractedPhrases.Add(phraseText);

                    // Identificar alérgenos na frase
                    var allergensInPhrase = ExtractAllergensFromPhrase(phraseText);

                    // Classificar alérgenos de acordo com o tipo de declaração
                    foreach (var allergen in allergensInPhrase)
                    {
                        switch (declarationType)
                        {
                            case AllergenDeclarationType.Contains:
                            case AllergenDeclarationType.ContainsDerivatives:
                                // NÃO adicionar se já está em DoesNotContain
                                if (!result.DoesNotContainAllergens.Contains(allergen) && 
                                    !result.ConfirmedAllergens.Contains(allergen))
                                {
                                    result.ConfirmedAllergens.Add(allergen);
                                }
                                break;

                            case AllergenDeclarationType.MayContain:
                                // NÃO adicionar se já está em DoesNotContain
                                if (!result.DoesNotContainAllergens.Contains(allergen) && 
                                    !result.MayContainAllergens.Contains(allergen))
                                {
                                    result.MayContainAllergens.Add(allergen);
                                }
                                break;

                            case AllergenDeclarationType.DoesNotContain:
                                if (!result.DoesNotContainAllergens.Contains(allergen))
                                {
                                    result.DoesNotContainAllergens.Add(allergen);
                                }
                                break;
                        }
                    }

                    // Procurar próxima ocorrência
                    index = text.IndexOf(phrase, index + phrase.Length, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        private static string ExtractFullPhrase(string text, int startIndex)
        {
            // Encontrar o fim da frase (ponto, ponto e vírgula, ou fim do texto)
            var endIndex = text.IndexOfAny(new[] { '.', ';', '\n' }, startIndex);
            if (endIndex == -1)
            {
                endIndex = Math.Min(text.Length, startIndex + 200);
            }

            var phrase = text.Substring(startIndex, endIndex - startIndex);
            return phrase.Trim();
        }

        private static List<string> ExtractAllergensFromPhrase(string phrase)
        {
            var foundAllergens = new List<string>();

            foreach (var allergen in CommonAllergens)
            {
                // Usar boundary para evitar falsos positivos
                var pattern = @"\b" + Regex.Escape(allergen) + @"\b";
                if (Regex.IsMatch(phrase, pattern, RegexOptions.IgnoreCase))
                {
                    // Normalizar o nome do alérgeno (remover acentos se houver variante sem acento)
                    var normalizedAllergen = NormalizeAllergenName(allergen);
                    if (!foundAllergens.Contains(normalizedAllergen))
                    {
                        foundAllergens.Add(normalizedAllergen);
                    }
                }
            }

            return foundAllergens;
        }

        private static string NormalizeAllergenName(string allergen)
        {
            // Preferir a forma com acento quando aplicável
            var mappings = new Dictionary<string, string>
            {
                { "gluten", "glúten" },
                { "amendoa", "amêndoa" },
                { "crustaceo", "crustáceo" },
                { "crustaceos", "crustáceos" },
                { "camarao", "camarão" }
            };

            if (mappings.ContainsKey(allergen.ToLowerInvariant()))
            {
                return mappings[allergen.ToLowerInvariant()];
            }

            return allergen;
        }

        private static void ValidateAndAdjustConfidence(AllergenParseResult result)
        {
            if (!result.HasAllergenInfo)
            {
                result.Confidence = ConfidenceLevel.Low;
                result.ValidationWarnings.Add("Nenhuma informação de alérgeno encontrada");
                return;
            }

            // Se encontrou frases mas nenhum alérgeno foi identificado
            if (result.ExtractedPhrases.Count > 0 && 
                !result.HasConfirmedAllergens && 
                !result.HasPotentialAllergens && 
                result.DoesNotContainAllergens.Count == 0)
            {
                result.Confidence = ConfidenceLevel.Medium;
                result.ValidationWarnings.Add("Frases de alérgenos encontradas, mas nenhum alérgeno específico identificado");
            }

            // Se encontrou apenas declarações negativas
            if (!result.HasConfirmedAllergens && 
                !result.HasPotentialAllergens && 
                result.DoesNotContainAllergens.Count > 0)
            {
                result.Confidence = ConfidenceLevel.High;
                // Não é um warning, é uma informação válida
            }
        }
    }
}
