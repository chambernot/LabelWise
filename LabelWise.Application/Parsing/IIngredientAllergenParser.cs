namespace LabelWise.Application.Parsing
{
    public interface IIngredientAllergenParser
    {
        IngredientAllergenParseResult Parse(string rawOcrText);
    }
}
