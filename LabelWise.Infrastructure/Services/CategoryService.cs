using LabelWise.Application.Interfaces;

namespace LabelWise.Infrastructure.Services;

/// <summary>
/// Determina a categoria do produto a partir de sinais textuais no nome.
/// Regras genéricas orientadas por nutrição — sem hardcode de marcas ou SKUs.
/// </summary>
public sealed class CategoryService : ICategoryService
{
    private static readonly (string Signal, string Category)[] Rules =
    [
        ("coco",        "Derivados de coco"),
        ("leite",       "Laticínios"),
        ("iogurte",     "Laticínios"),
        ("queijo",      "Laticínios"),
        ("requeijão",   "Laticínios"),
        ("manteiga",    "Laticínios"),
        ("carne",       "Proteínas animais"),
        ("frango",      "Proteínas animais"),
        ("peixe",       "Proteínas animais"),
        ("atum",        "Proteínas animais"),
        ("ovo",         "Proteínas animais"),
        ("feijão",      "Leguminosas"),
        ("lentilha",    "Leguminosas"),
        ("grão",        "Leguminosas"),
        ("arroz",       "Cereais e grãos"),
        ("aveia",       "Cereais e grãos"),
        ("macarrão",    "Massas"),
        ("massa",       "Massas"),
        ("pão",         "Panificados"),
        ("biscoito",    "Biscoitos e snacks"),
        ("bolacha",     "Biscoitos e snacks"),
        ("salgadinho",  "Biscoitos e snacks"),
        ("chocolate",   "Doces e guloseimas"),
        ("açúcar",      "Açúcares e adoçantes"),
        ("mel",         "Açúcares e adoçantes"),
        ("óleo",        "Óleos e gorduras"),
        ("azeite",      "Óleos e gorduras"),
        ("margarina",   "Óleos e gorduras"),
        ("suco",        "Bebidas"),
        ("refrigerante","Bebidas"),
        ("água",        "Bebidas"),
        ("cereal",      "Cereais matinais"),
        ("granola",     "Cereais matinais"),
        ("proteína",    "Suplementos"),
        ("whey",        "Suplementos"),
        ("farinha",     "Farinhas e amidos"),
        ("amido",       "Farinhas e amidos"),
    ];

    public string Fix(string? productName)
    {
        if (string.IsNullOrWhiteSpace(productName))
            return "Outros";

        var lower = productName.ToLowerInvariant();

        foreach (var (signal, category) in Rules)
        {
            if (lower.Contains(signal))
                return category;
        }

        return "Outros";
    }
}
