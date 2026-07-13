using System;
using Xunit;
using LabelWise.Application.Parsing;

namespace LabelWise.Application.Tests.Parsing
{
    public class IngredientAllergenParserTests
    {
        [Fact]
        public void Parse_BasicIngredientsAndAllergens_ReturnsExpected()
        {
            var parser = new IngredientAllergenParser();
            var text = "INGREDIENTES: Farinha de trigo, açúcar, óleo vegetal (soja), leite em pó. CONTÉM: glúten e lactose. PODE CONTER AMENDOIM.";

            var res = parser.Parse(text);

            Assert.Contains("farinha de trigo", res.Ingredients);
            Assert.Contains("açúcar", res.Ingredients);
            Assert.Contains("leite em pó", res.Ingredients);

            Assert.Contains("gluten", res.Allergens); // normalized
            Assert.Contains("lactose", res.Allergens);
            Assert.Contains("amendoim", res.Allergens);

            Assert.Contains("contém", res.CriticalTerms);
            Assert.Contains("pode conter", res.CriticalTerms);
            Assert.NotEmpty(res.ExtractedPhrases);
        }

        [Fact]
        public void Parse_EmptyText_ReturnsEmptyResult()
        {
            var parser = new IngredientAllergenParser();
            var res = parser.Parse("");
            Assert.False(res.HasIngredients);
            Assert.False(res.HasAllergens);
        }
    }
}
