namespace LabelWise.Application.Models.Nutrition;

/// <summary>
/// Tipologia alimentar padronizada para classificação nutricional genérica.
/// Baseada em características nutricionais e processamento, não em heurísticas de produto específico.
/// </summary>
public enum FoodTypology
{
    /// <summary>
    /// Categoria não identificada ou mapeada.
    /// </summary>
    Unknown,

    // === LATICÍNIOS ===
    
    /// <summary>
    /// Laticínios cremosos tradicionais (requeijão, cream cheese, catupiry).
    /// Perfil: alto teor de gordura (15-30%), proteína moderada (5-10%), baixo açúcar.
    /// </summary>
    DairyCreamyFull,

    /// <summary>
    /// Laticínios cremosos light/reduzidos em gordura.
    /// Perfil: gordura reduzida (5-15%), proteína moderada (6-12%), baixo açúcar.
    /// </summary>
    DairyCreamyLight,

    /// <summary>
    /// Queijos duros/semi-duros (parmesão, mussarela, cheddar).
    /// Perfil: alto teor de proteína (20-35%), gordura moderada-alta (15-30%), sódio elevado.
    /// </summary>
    CheeseHard,

    /// <summary>
    /// Queijos ralados (parmesão ralado, queijo ralado).
    /// Perfil: proteína muito alta (30-40%), gordura moderada (20-30%), sódio muito elevado.
    /// </summary>
    CheeseGrated,

    /// <summary>
    /// Iogurtes naturais sem açúcar adicionado.
    /// Perfil: proteína moderada (3-6%), gordura baixa-moderada (0-4%), açúcar natural (4-6%).
    /// </summary>
    YogurtNatural,

    /// <summary>
    /// Iogurtes com açúcar adicionado ou aromatizados.
    /// Perfil: proteína moderada (3-5%), açúcar elevado (10-15%), gordura baixa.
    /// </summary>
    YogurtSweetened,

    /// <summary>
    /// Sobremesas lácteas (danette, chandelle, petit suisse).
    /// Perfil: açúcar muito elevado (15-25%), gordura moderada (3-6%), proteína baixa (2-4%).
    /// </summary>
    DessertDairy,

    // === CARBOIDRATOS BASE ===

    /// <summary>
    /// Cereais e grãos (arroz, feijão, lentilha, grão-de-bico).
    /// Perfil: carboidratos elevados (70-80%), proteína moderada (6-10%), gordura muito baixa (0-2%).
    /// </summary>
    GrainCereal,

    /// <summary>
    /// Massas e macarrão (espaguete, penne, lasanha).
    /// Perfil: carboidratos elevados (70-75%), proteína moderada (10-13%), gordura baixa (1-2%).
    /// </summary>
    Pasta,

    /// <summary>
    /// Pães e produtos panificados básicos.
    /// Perfil: carboidratos moderados (50-60%), proteína moderada (8-12%), sódio moderado-alto.
    /// </summary>
    BreadBasic,

    /// <summary>
    /// Cereais matinais tradicionais (sem açúcar adicionado).
    /// Perfil: carboidratos elevados (70-80%), fibra moderada (3-8%), açúcar baixo (<10%).
    /// </summary>
    CerealBreakfast,

    /// <summary>
    /// Cereais matinais açucarados.
    /// Perfil: carboidratos elevados (75-85%), açúcar muito elevado (20-40%), fibra baixa.
    /// </summary>
    CerealSweetened,

    // === ULTRAPROCESSADOS ===

    /// <summary>
    /// Biscoitos e bolachas recheadas.
    /// Perfil: açúcar muito elevado (25-40%), gordura elevada (15-25%), sódio moderado.
    /// </summary>
    CookieFilled,

    /// <summary>
    /// Biscoitos e bolachas simples/salgados.
    /// Perfil: gordura moderada (10-20%), sódio elevado (400-800mg), açúcar moderado (5-15%).
    /// </summary>
    CookiePlain,

    /// <summary>
    /// Salgadinhos e snacks (chips, doritos, cheetos).
    /// Perfil: gordura muito elevada (25-35%), sódio muito elevado (800-1200mg), proteína baixa.
    /// </summary>
    SnackSalty,

    /// <summary>
    /// Chocolates e produtos achocolatados.
    /// Perfil: açúcar extremamente elevado (40-60%), gordura moderada-alta (15-30%).
    /// </summary>
    Chocolate,

    /// <summary>
    /// Achocolatado em pó.
    /// Perfil: açúcar extremamente elevado (70-80%), gordura baixa (2-5%), proteína muito baixa (3-5%).
    /// </summary>
    ChocolatePowder,

    // === BEBIDAS ===

    /// <summary>
    /// Bebidas açucaradas (refrigerantes, sucos industrializados).
    /// Perfil: açúcar muito elevado (10-12g/100ml), calorias baixas (40-50kcal/100ml), zero proteína.
    /// </summary>
    BeverageSweetened,

    /// <summary>
    /// Bebidas zero/diet.
    /// Perfil: zero calorias, zero açúcar, zero proteína, sódio variável.
    /// </summary>
    BeverageZero,

    // === PROTEICOS ===

    /// <summary>
    /// Produtos com alto teor proteico (whey, barras proteicas, iogurtes proteicos).
    /// Perfil: proteína muito elevada (15-30%), gordura baixa (0-5%), açúcar variável.
    /// </summary>
    ProteinEnriched,

    // === GORDURAS E ÓLEOS ===

    /// <summary>
    /// Óleos e gorduras (azeite, óleo, manteiga, margarina).
    /// Perfil: gordura extremamente elevada (80-100%), zero carboidratos, zero proteína.
    /// </summary>
    OilFat,

    // === OUTROS ===

    /// <summary>
    /// Produtos integrais/light com perfil nutricional melhorado.
    /// Perfil: fibra elevada (5-10%), gordura reduzida, sódio reduzido.
    /// </summary>
    HealthierVariant,

    /// <summary>
    /// Condimentos e temperos (sal, açúcar, temperos).
    /// Perfil: concentrado em um nutriente específico (sal=sódio, açúcar=carboidratos).
    /// </summary>
    Condiment
}
