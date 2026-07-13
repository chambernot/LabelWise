using LabelWise.Application.Models.IngredientAnalysis;

namespace LabelWise.Infrastructure.Services.IngredientAnalysis;

internal static class IngredientDictionary
{
    public static IReadOnlyList<IngredientDictionaryEntry> Allergens { get; } =
    [
        new() { CanonicalName = "leite", Category = "milk_derivative", Synonyms = ["leite", "leite pasteurizado", "lactose", "derivados de leite", "derivado de leite", "derivados do leite", "derivado do leite", "fermento lácteo", "fermento lacteo", "whey", "whey protein", "caseína", "caseina", "caseinato", "proteína láctea", "proteina lactea", "proteína do leite", "proteina do leite", "soro de leite", "leite em pó", "leite em po", "manteiga", "creme de leite"] },
        new() { CanonicalName = "glúten", Category = "gluten_source", Synonyms = ["glúten", "gluten", "trigo", "cevada", "centeio", "malte", "extrato de malte", "farinha de trigo"] },
        new() { CanonicalName = "ovo", Category = "egg_derivative", Synonyms = ["ovo", "ovos", "albumina", "clara de ovo", "gema de ovo"] },
        new() { CanonicalName = "soja", Category = "soy_derivative", Synonyms = ["soja", "lecitina de soja", "proteína de soja", "proteina de soja"] },
        new() { CanonicalName = "amendoim", Category = "peanut", Synonyms = ["amendoim"] },
        new() { CanonicalName = "castanhas", Category = "tree_nut", Synonyms = ["castanha", "castanhas", "amêndoa", "amendoa", "avelã", "avela", "avelãs", "avelas", "nozes", "pistache", "caju", "macadâmia", "macadamia", "macadâmias", "macadamias"] },
        new() { CanonicalName = "aveia", Category = "gluten_risk_cereal", Synonyms = ["aveia", "aveias", "oat", "oats"] },
        new() { CanonicalName = "peixe", Category = "fish", Synonyms = ["peixe", "atum", "sardinha", "anchova", "salmão", "salmao"] },
        new() { CanonicalName = "crustáceos", Category = "crustacean", Synonyms = ["crustáceo", "crustaceo", "camarão", "camarao", "lagosta", "caranguejo"] }
    ];

    public static IReadOnlyList<string> HighRegulatoryAllergens { get; } =
    [
        "leite", "soja", "ovo", "ovos", "trigo", "glúten", "gluten", "amendoim", "castanhas", "peixe", "crustáceos", "crustaceos"
    ];

    public static IReadOnlyList<string> MediumRegulatoryAllergens { get; } =
    [
        "aveia", "milho", "gergelim"
    ];

    public static IReadOnlyList<string> LowRegulatoryAllergens { get; } =
    [
        "coco", "fruta", "frutas", "maçã", "maca", "banana", "uva", "morango", "vegetal", "vegetais"
    ];

    public static IReadOnlyList<string> NaturalPredominantTerms { get; } =
    [
        "coco", "coco ralado", "castanha", "castanhas", "amendoim", "abacate", "azeite", "nozes", "amêndoa", "amendoa", "fruta", "aveia", "milho"
    ];

    public static IReadOnlyList<IngredientDictionaryEntry> IngredientNormalization { get; } =
    [
        new() { CanonicalName = "Vitamina A", Category = "vitamin", Synonyms = ["palmitato de retinil", "palmitato de petinil", "vitamina a"] },
        new() { CanonicalName = "Vitamina C", Category = "vitamin", Synonyms = ["ácido ascórbico", "acido ascorbico", "ascorbico"] },
        new() { CanonicalName = "Ácido cítrico", Category = "acidulant", Synonyms = ["ácido cítrico", "acido citrico", "acidulante acido citrico"] },
        new() { CanonicalName = "Gordura vegetal interesterificada", Category = "processed_fat", Synonyms = ["gordura vegetal interesterificada", "gordura interesterificada"] },
        new() { CanonicalName = "Açúcar", Category = "sugar", Synonyms = ["açúcar", "acucar", "sacarose"] },
        new() { CanonicalName = "Maltodextrina", Category = "processed_carbohydrate", Synonyms = ["maltodextrina"] },
        new() { CanonicalName = "Aveia", Category = "whole_grain", Synonyms = ["aveia", "aveias", "farinha de aveia", "flocos de aveia"] },
        new() { CanonicalName = "Macadâmia", Category = "tree_nut", Synonyms = ["macadâmia", "macadamia", "macadâmias", "macadamias"] },
        new() { CanonicalName = "Avelã", Category = "tree_nut", Synonyms = ["avelã", "avela", "avelãs", "avelas"] },
        new() { CanonicalName = "Castanha-de-caju", Category = "tree_nut", Synonyms = ["castanha-de-caju", "castanha de caju", "caju"] },
        new() { CanonicalName = "Proteína isolada", Category = "protein", Synonyms = ["proteína isolada", "proteina isolada", "isolado proteico", "isolada de soja", "whey protein isolate"] },
        new() { CanonicalName = "Gordura hidrogenada", Category = "hydrogenated_fat", Synonyms = ["gordura hidrogenada", "gordura vegetal hidrogenada", "óleo hidrogenado", "oleo hidrogenado"] },
        new() { CanonicalName = "Goma xantana", Category = "stabilizer", Synonyms = ["goma xantana", "estabilizante goma xantana"] },
        new() { CanonicalName = "Goma guar", Category = "stabilizer", Synonyms = ["goma guar", "estabilizante goma guar"] },
        new() { CanonicalName = "Aromatizante", Category = "flavoring", Synonyms = ["aromatizante", "aroma"] },
        new() { CanonicalName = "Conservante", Category = "preservative", Synonyms = ["conservante", "conservador", "ins 223", "metabissulfito de sódio", "metabissulfito de sodio", "sorbato de potássio", "sorbato de potassio", "benzoato de sódio", "benzoato de sodio", "ácido benzoico", "acido benzoico", "metilparabeno"] },
        new() { CanonicalName = "Emulsificante", Category = "emulsifier", Synonyms = ["emulsificante", "lecitina", "mono e diglicerídeos", "mono e diglicerideos"] },
        new() { CanonicalName = "Adoçante artificial", Category = "artificial_sweetener", Synonyms = ["adoçante", "adocante", "edulcorante", "sucralose", "aspartame", "acesulfame", "ciclamato", "ciclamato de sódio", "ciclamato de sodio", "sacarina", "sacarina sódica", "sacarina sodica"] },
        new() { CanonicalName = "Poliol", Category = "polyol_sweetener", Synonyms = ["sorbitol", "manitol", "xilitol", "maltitol", "eritritol", "isomalte"] },
        new() { CanonicalName = "Corante de casca de uva", Category = "colorant", Synonyms = ["corante extrato de casca de uva", "extrato de casca de uva"] },
        new() { CanonicalName = "Sulfato de zinco", Category = "mineral", Synonyms = ["sulfato de zinco", "zinco"] },
        new() { CanonicalName = "Água", Category = "base", Synonyms = ["água", "agua"] },
        new() { CanonicalName = "Mel", Category = "animal_derivative", Synonyms = ["mel"] },
        new() { CanonicalName = "Gelatina", Category = "animal_derivative", Synonyms = ["gelatina"] },
        new() { CanonicalName = "Colágeno", Category = "animal_derivative", Synonyms = ["colágeno", "colageno"] },
        // Condimentos, especiarias e temperos comuns
        new() { CanonicalName = "Sal", Category = "seasoning", Synonyms = ["sal", "cloreto de sódio", "cloreto de sodio"] },
        new() { CanonicalName = "Alho", Category = "vegetable", Synonyms = ["alho", "alho granulado", "alho em pó", "alho em po", "alho desidratado"] },
        new() { CanonicalName = "Cebola", Category = "vegetable", Synonyms = ["cebola", "cebola em flocos", "cebola desidratada", "cebola em pó", "cebola em po"] },
        new() { CanonicalName = "Tomate seco", Category = "vegetable", Synonyms = ["tomate seco", "tomate seco em flocos", "tomate desidratado"] },
        new() { CanonicalName = "Tomate", Category = "vegetable", Synonyms = ["tomate"] },
        new() { CanonicalName = "Pimentão vermelho", Category = "vegetable", Synonyms = ["pimentão vermelho", "pimentao vermelho", "pimentão vermelho seco", "pimentao vermelho seco"] },
        new() { CanonicalName = "Pimentão", Category = "vegetable", Synonyms = ["pimentão", "pimentao", "pimentão amarelo", "pimentão verde"] },
        new() { CanonicalName = "Pimenta preta", Category = "seasoning", Synonyms = ["pimenta preta", "pimenta-preta", "pimenta do reino", "pimenta negra"] },
        new() { CanonicalName = "Pimenta", Category = "seasoning", Synonyms = ["pimenta"] },
        new() { CanonicalName = "Salsa", Category = "herb", Synonyms = ["salsa", "salsinha", "salsa desidratada"] },
        new() { CanonicalName = "Glutamato monossódico", Category = "flavor_enhancer", Synonyms = ["glutamato monossódico", "glutamato monossodico", "glutamato", "msg", "ins 621"] },
        new() { CanonicalName = "Orégano", Category = "herb", Synonyms = ["orégano", "oregano"] },
        new() { CanonicalName = "Coentro", Category = "herb", Synonyms = ["coentro"] },
        new() { CanonicalName = "Cominho", Category = "seasoning", Synonyms = ["cominho"] },
        new() { CanonicalName = "Cúcuma", Category = "seasoning", Synonyms = ["cúcuma", "cucuma", "açafrão da terra", "acafrao da terra"] },
        new() { CanonicalName = "Páprica", Category = "seasoning", Synonyms = ["páprica", "paprica", "paprika"] },
        new() { CanonicalName = "Vinagre", Category = "condiment", Synonyms = ["vinagre", "vinagre de vinho", "vinagre de maçã"] },
        new() { CanonicalName = "Azeite", Category = "fat", Synonyms = ["azeite", "azeite de oliva", "azeite extra virgem"] },
        new() { CanonicalName = "Canela", Category = "seasoning", Synonyms = ["canela", "canela em pó", "canela em po"] },
        new() { CanonicalName = "Gengibre", Category = "seasoning", Synonyms = ["gengibre"] }
    ];

    public static IReadOnlyList<string> VeganBlockedTerms { get; } =
    [
        "leite", "lactose", "whey", "caseína", "caseina", "caseinato", "mel", "ovo", "ovos", "albumina",
        "gelatina", "colágeno", "colageno", "carne", "frango", "peixe", "sardinha", "atum", "bacon", "banha"
    ];

    public static IReadOnlyList<string> VegetarianBlockedTerms { get; } =
    [
        "gelatina", "colágeno", "colageno", "carne", "frango", "peixe", "sardinha", "atum", "bacon", "banha", "caldo de carne", "caldo de galinha"
    ];

    public static IReadOnlyList<string> SugarTerms { get; } =
    [
        "açúcar", "acucar", "sacarose", "xarope de glicose", "xarope", "glicose", "glucose", "frutose", "maltodextrina", "dextrose", "mel", "açúcar invertido", "acucar invertido"
    ];

    public static IReadOnlyList<string> ContextualSugarExclusionTerms { get; } =
    [
        "sabor doce como açúcar", "sabor doce como acucar", "equivale ao açúcar", "equivale ao acucar", "equivalem ao açúcar", "equivalem ao acucar",
        "adoçante", "adocante", "substitui açúcar", "substitui acucar", "poder adoçante", "poder adocante"
    ];

    public static IReadOnlyList<string> NutritionFactTerms { get; } =
    [
        "valor energético", "valor energetico", "calorias", "kcal", "carboidratos", "carboidrato", "proteínas", "proteinas", "proteína", "proteina",
        "gorduras totais", "gordura total", "gorduras saturadas", "gordura saturada", "gorduras trans", "gordura trans", "fibra alimentar", "fibras", "sódio", "sodio", "%vd"
    ];

    public static IReadOnlyList<string> IncompleteIngredientSuffixes { get; } =
    [
        " de", " da", " do", " das", " dos", " com", " e"
    ];

    public static IReadOnlyList<string> ChildAttentionTerms { get; } =
    [
        "cafeína", "cafeina", "taurina", "bebida alcoólica", "bebida alcoolica", "álcool", "alcool", "guaraná", "guarana", "energético", "energetico"
    ];

    public static IReadOnlyList<string> ProcessingMarkerTerms { get; } =
    [
        "aromatizante", "aroma", "corante", "estabilizante", "antioxidante", "ácido ascórbico", "acido ascorbico", "acidulante", "emulsificante", "adoçante", "adocante",
        "conservante", "sucralose", "aspartame", "acesulfame", "sacarina", "ciclamato", "benzoato", "sorbato",
        "proteína isolada", "proteina isolada", "isolado proteico", "gordura hidrogenada", "gordura vegetal hidrogenada", "barra proteica", "barra de proteína"
    ];

    public static IReadOnlyList<string> IndustrialClaimTerms { get; } =
    [
        "barra proteica", "barra de proteína", "barra de proteina", "fonte de proteína", "fonte de proteina", "alto em fibras",
        "zero açúcar", "zero acucar", "sem açúcar", "sem acucar", "sem adição de açúcar", "sem adicao de acucar",
        "protein", "fit", "light", "diet"
    ];

    public static IReadOnlyList<string> ArtificialSweetenerTerms { get; } =
    [
        "adoçante", "adocante", "edulcorante", "sucralose", "aspartame", "acesulfame", "sacarina", "ciclamato", "sorbitol", "manitol", "xilitol", "maltitol", "eritritol"
    ];

    public static IReadOnlyList<string> PreservativeTerms { get; } =
    [
        "conservante", "benzoato", "benzoico", "metilparabeno", "parabeno", "sorbato", "nitrito", "nitrato"
    ];

    public static IReadOnlyList<string> UltraProcessingCategories { get; } =
    [
        "artificial_sweetener", "polyol_sweetener", "preservative", "acidulant", "colorant", "emulsifier", "flavoring", "stabilizer", "antioxidant", "hydrogenated_fat", "processed_fat", "processed_carbohydrate"
    ];

    public static IReadOnlyList<string> PositiveIngredientCategories { get; } =
    [
        "vitamin", "mineral", "fiber", "protein", "whole_grain"
    ];

    public static IReadOnlyList<string> ControversialTerms { get; } =
    [
        "adoçante", "adocante", "sucralose", "aspartame", "corante", "aromatizante", "conservante", "xarope de glicose", "maltodextrina"
    ];
}
