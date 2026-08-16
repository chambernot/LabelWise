using System;
using System.Collections.Generic;
using System.Text;

namespace LabelWise.Application.DTOs
{
    public class GeminiLabelExtractionResult
    {
        /// <summary>
        /// Lista de ingredientes extraídos do rótulo.
        /// </summary>
        public List<string> Ingredients { get; set; } = new List<string>();

        /// <summary>
        /// Dados da tabela nutricional extraídos da imagem.
        /// </summary>
        public NutritionFactsData? NutritionFacts { get; set; }

        /// <summary>
        /// Avisos ou problemas que a IA possa ter encontrado (ex: imagem borrada).
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public class NutritionFactsData
    {
        // Usamos nullable (decimal?) porque a IA pode não encontrar algum desses campos no rótulo

        public decimal? Calories { get; set; }
        public decimal? Carbohydrates { get; set; }
        public decimal? Sodium { get; set; }

        // DICA: Você pode expandir essa classe livremente com os campos que o seu motor suporta.
        // O Gemini consegue extrair todos eles se você adicionar no Prompt da controller.
        public decimal? Proteins { get; set; }
        public decimal? TotalFat { get; set; }
        public decimal? SaturatedFat { get; set; }
        public decimal? TransFat { get; set; }
        public decimal? DietaryFiber { get; set; }
    }
}
