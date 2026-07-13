using System;

namespace LabelWise.Application.Parsing
{
    public static class ExampleUsage
    {
        public static void Demo()
        {
            var parser = new IngredientAllergenParser();
            var text = @"INGREDIENTES: Farinha de trigo, açúcar, óleo vegetal (soja), leite em pó. CONTÉM: glúten e lactose. PODE CONTER AMENDOIM. Informação nutricional...";
            var res = parser.Parse(text);
            Console.WriteLine("Ingredients: " + string.Join(", ", res.Ingredients));
            Console.WriteLine("Allergens: " + string.Join(", ", res.Allergens));
            Console.WriteLine("Phrases: " + string.Join(" | ", res.ExtractedPhrases));
        }
    }
}
