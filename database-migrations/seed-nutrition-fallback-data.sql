-- =====================================================
-- SEED INICIAL: CATEGORIAS E PERFIS NUTRICIONAIS
-- Dados baseados em TACO, IBGE e Anvisa
-- =====================================================

-- =====================================================
-- 1. CATEGORIAS NUTRICIONAIS
-- =====================================================

-- Laticínios
INSERT INTO nutrition_categories (code, name, description, parent_code) VALUES
('laticinio', 'Laticínios', 'Produtos derivados de leite', NULL),
('laticinio_cremoso', 'Laticínios Cremosos', 'Requeijão, cream cheese, catupiry', 'laticinio'),
('laticinio_cremoso_light', 'Laticínios Cremosos Light', 'Versões light/reduzidas em gordura', 'laticinio'),
('queijo_duro', 'Queijos Duros', 'Parmesão, mussarela, cheddar', 'laticinio'),
('queijo_ralado', 'Queijos Ralados', 'Queijos ralados (parmesão, etc)', 'laticinio'),
('iogurte_natural', 'Iogurte Natural', 'Iogurte sem açúcar adicionado', 'laticinio'),
('iogurte_adocicado', 'Iogurte Adoçado', 'Iogurte com açúcar/sabores', 'laticinio'),
('sobremesa_lactea', 'Sobremesa Láctea', 'Petit suisse, danette, chandelle', 'laticinio');

-- Carboidratos Base
INSERT INTO nutrition_categories (code, name, description, parent_code) VALUES
('carboidrato', 'Carboidratos Base', 'Grãos, cereais, massas', NULL),
('arroz_branco', 'Arroz Branco', 'Arroz branco polido', 'carboidrato'),
('arroz_integral', 'Arroz Integral', 'Arroz integral/parboilizado', 'carboidrato'),
('macarrao', 'Macarrão', 'Massas e macarrão', 'carboidrato'),
('pao', 'Pão', 'Produtos panificados básicos', 'carboidrato'),
('cereal', 'Cereal Matinal', 'Cereais matinais tradicionais', 'carboidrato'),
('cereal_acucarado', 'Cereal Açucarado', 'Cereais com alto teor de açúcar', 'carboidrato');

-- Ultraprocessados
INSERT INTO nutrition_categories (code, name, description, parent_code) VALUES
('ultraprocessado', 'Ultraprocessados', 'Produtos industrializados', NULL),
('biscoito_recheado', 'Biscoito Recheado', 'Biscoitos e bolachas recheadas', 'ultraprocessado'),
('biscoito_simples', 'Biscoito Simples', 'Biscoitos cream cracker, maria', 'ultraprocessado'),
('snack_salgado', 'Snack Salgado', 'Salgadinhos, chips', 'ultraprocessado'),
('chocolate', 'Chocolate', 'Chocolates e barras', 'ultraprocessado'),
('achocolatado_po', 'Achocolatado em Pó', 'Nescau, Toddy, etc', 'ultraprocessado');

-- Bebidas
INSERT INTO nutrition_categories (code, name, description, parent_code) VALUES
('bebida', 'Bebidas', 'Bebidas em geral', NULL),
('refrigerante', 'Refrigerante', 'Refrigerantes tradicionais', 'bebida'),
('refrigerante_zero', 'Refrigerante Zero', 'Refrigerantes zero/diet', 'bebida'),
('suco_industrializado', 'Suco Industrializado', 'Sucos e néctares industrializados', 'bebida'),
('bebida_acucarada', 'Bebida Açucarada', 'Bebidas com açúcar adicionado', 'bebida');

-- =====================================================
-- 2. PERFIS NUTRICIONAIS POR CATEGORIA
-- =====================================================

-- === LATICÍNIOS ===

-- Laticínio Cremoso (Requeijão, Cream Cheese)
INSERT INTO category_nutrition_profiles (
    category_code, 
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    carbohydrates_per_100g, carbohydrates_min, carbohydrates_max,
    sugar_per_100g, sugar_min, sugar_max,
    fiber_per_100g, fiber_min, fiber_max,
    sodium_per_100g, sodium_min, sodium_max,
    confidence_level, data_source, notes
) VALUES (
    'laticinio_cremoso',
    220, 180, 280,
    8, 5, 12,
    20, 15, 30,
    4, 2, 8,
    2, 0, 5,
    0, 0, 0,
    450, 300, 700,
    0.85, 'TACO/IBGE', 'Perfil típico de laticínios cremosos tradicionais'
);

-- Laticínio Cremoso Light
INSERT INTO category_nutrition_profiles (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    carbohydrates_per_100g, carbohydrates_min, carbohydrates_max,
    sugar_per_100g, sugar_min, sugar_max,
    fiber_per_100g, fiber_min, fiber_max,
    sodium_per_100g, sodium_min, sodium_max,
    confidence_level, data_source, notes
) VALUES (
    'laticinio_cremoso_light',
    140, 100, 180,
    10, 6, 14,
    8, 5, 15,
    5, 3, 8,
    2, 0, 4,
    0, 0, 0,
    400, 250, 600,
    0.80, 'TACO/IBGE', 'Versões light com gordura reduzida'
);

-- Queijo Duro
INSERT INTO category_nutrition_profiles (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    carbohydrates_per_100g, carbohydrates_min, carbohydrates_max,
    sugar_per_100g, sugar_min, sugar_max,
    fiber_per_100g, fiber_min, fiber_max,
    sodium_per_100g, sodium_min, sodium_max,
    confidence_level, data_source, notes
) VALUES (
    'queijo_duro',
    320, 250, 400,
    28, 20, 35,
    24, 15, 32,
    1, 0, 3,
    0.5, 0, 2,
    0, 0, 0,
    650, 400, 900,
    0.90, 'TACO/IBGE', 'Queijos duros e semi-duros'
);

-- Queijo Ralado
INSERT INTO category_nutrition_profiles (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    carbohydrates_per_100g, carbohydrates_min, carbohydrates_max,
    sugar_per_100g, sugar_min, sugar_max,
    fiber_per_100g, fiber_min, fiber_max,
    sodium_per_100g, sodium_min, sodium_max,
    confidence_level, data_source, notes
) VALUES (
    'queijo_ralado',
    400, 350, 450,
    36, 30, 42,
    28, 20, 35,
    1, 0, 4,
    0.5, 0, 2,
    0, 0, 0,
    1000, 800, 1400,
    0.85, 'TACO/IBGE', 'Queijos ralados com alto teor de sódio'
);

-- Iogurte Natural
INSERT INTO category_nutrition_profiles (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    carbohydrates_per_100g, carbohydrates_min, carbohydrates_max,
    sugar_per_100g, sugar_min, sugar_max,
    fiber_per_100g, fiber_min, fiber_max,
    sodium_per_100g, sodium_min, sodium_max,
    confidence_level, data_source, notes
) VALUES (
    'iogurte_natural',
    70, 50, 90,
    4.5, 3, 6,
    2, 0, 4,
    6, 4, 8,
    5, 4, 6,
    0, 0, 0,
    60, 40, 90,
    0.85, 'TACO/IBGE', 'Açúcar natural do leite (lactose) apenas'
);

-- Iogurte Adoçado
INSERT INTO category_nutrition_profiles (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    carbohydrates_per_100g, carbohydrates_min, carbohydrates_max,
    sugar_per_100g, sugar_min, sugar_max,
    fiber_per_100g, fiber_min, fiber_max,
    sodium_per_100g, sodium_min, sodium_max,
    confidence_level, data_source, notes
) VALUES (
    'iogurte_adocicado',
    100, 80, 130,
    3.5, 2.5, 5,
    1.5, 0, 3,
    16, 12, 20,
    14, 10, 18,
    0, 0, 1,
    65, 40, 100,
    0.80, 'TACO/IBGE', 'Com açúcar adicionado'
);

-- Sobremesa Láctea
INSERT INTO category_nutrition_profiles (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    carbohydrates_per_100g, carbohydrates_min, carbohydrates_max,
    sugar_per_100g, sugar_min, sugar_max,
    fiber_per_100g, fiber_min, fiber_max,
    sodium_per_100g, sodium_min, sodium_max,
    confidence_level, data_source, notes
) VALUES (
    'sobremesa_lactea',
    150, 120, 200,
    3.5, 2, 5,
    5, 3, 8,
    23, 18, 30,
    20, 15, 25,
    0.5, 0, 1,
    80, 50, 120,
    0.85, 'TACO/IBGE', 'Alto teor de açúcar'
);

-- === CARBOIDRATOS ===

-- Arroz Branco
INSERT INTO category_nutrition_profiles (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    carbohydrates_per_100g, carbohydrates_min, carbohydrates_max,
    sugar_per_100g, sugar_min, sugar_max,
    fiber_per_100g, fiber_min, fiber_max,
    sodium_per_100g, sodium_min, sodium_max,
    confidence_level, data_source, notes
) VALUES (
    'arroz_branco',
    360, 340, 380,
    7, 6, 9,
    0.5, 0.3, 1.5,
    78, 74, 82,
    0.2, 0, 1,
    1, 0.5, 2,
    5, 0, 20,
    0.95, 'TACO', 'Arroz branco polido'
);

-- Arroz Integral
INSERT INTO category_nutrition_profiles (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    carbohydrates_per_100g, carbohydrates_min, carbohydrates_max,
    sugar_per_100g, sugar_min, sugar_max,
    fiber_per_100g, fiber_min, fiber_max,
    sodium_per_100g, sodium_min, sodium_max,
    confidence_level, data_source, notes
) VALUES (
    'arroz_integral',
    370, 350, 390,
    8, 7, 10,
    2, 1.5, 3,
    77, 73, 80,
    0.5, 0, 1.5,
    4, 3, 6,
    10, 5, 25,
    0.90, 'TACO', 'Maior teor de fibras'
);

-- Macarrão
INSERT INTO category_nutrition_profiles (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    carbohydrates_per_100g, carbohydrates_min, carbohydrates_max,
    sugar_per_100g, sugar_min, sugar_max,
    fiber_per_100g, fiber_min, fiber_max,
    sodium_per_100g, sodium_min, sodium_max,
    confidence_level, data_source, notes
) VALUES (
    'macarrao',
    360, 340, 380,
    12, 10, 14,
    1.5, 1, 3,
    74, 70, 78,
    3, 2, 5,
    3, 2, 5,
    20, 10, 40,
    0.90, 'TACO', 'Massa seca'
);

-- Pão
INSERT INTO category_nutrition_profiles (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    carbohydrates_per_100g, carbohydrates_min, carbohydrates_max,
    sugar_per_100g, sugar_min, sugar_max,
    fiber_per_100g, fiber_min, fiber_max,
    sodium_per_100g, sodium_min, sodium_max,
    confidence_level, data_source, notes
) VALUES (
    'pao',
    270, 240, 300,
    9, 7, 12,
    4, 2, 6,
    52, 48, 58,
    6, 3, 10,
    3, 2, 5,
    450, 350, 600,
    0.85, 'TACO', 'Pão de forma típico'
);

-- Cereal Matinal
INSERT INTO category_nutrition_profiles (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    carbohydrates_per_100g, carbohydrates_min, carbohydrates_max,
    sugar_per_100g, sugar_min, sugar_max,
    fiber_per_100g, fiber_min, fiber_max,
    sodium_per_100g, sodium_min, sodium_max,
    confidence_level, data_source, notes
) VALUES (
    'cereal',
    380, 350, 410,
    10, 7, 14,
    4, 2, 8,
    78, 70, 85,
    12, 5, 18,
    6, 3, 10,
    250, 150, 400,
    0.80, 'TACO/Anvisa', 'Cereal matinal tradicional'
);

-- Cereal Açucarado
INSERT INTO category_nutrition_profiles (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    carbohydrates_per_100g, carbohydrates_min, carbohydrates_max,
    sugar_per_100g, sugar_min, sugar_max,
    fiber_per_100g, fiber_min, fiber_max,
    sodium_per_100g, sodium_min, sodium_max,
    confidence_level, data_source, notes
) VALUES (
    'cereal_acucarado',
    400, 370, 430,
    7, 5, 10,
    4, 2, 8,
    82, 75, 88,
    30, 20, 40,
    3, 1, 6,
    350, 200, 500,
    0.85, 'TACO/Anvisa', 'Alto teor de açúcar'
);

-- === ULTRAPROCESSADOS ===

-- Biscoito Recheado
INSERT INTO category_nutrition_profiles (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    carbohydrates_per_100g, carbohydrates_min, carbohydrates_max,
    sugar_per_100g, sugar_min, sugar_max,
    fiber_per_100g, fiber_min, fiber_max,
    sodium_per_100g, sodium_min, sodium_max,
    confidence_level, data_source, notes
) VALUES (
    'biscoito_recheado',
    490, 450, 530,
    5.5, 4, 7,
    21, 15, 28,
    70, 65, 75,
    32, 25, 40,
    2.5, 1.5, 4,
    380, 250, 550,
    0.85, 'TACO/Anvisa', 'Alto açúcar e gordura'
);

-- Biscoito Simples
INSERT INTO category_nutrition_profiles (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    carbohydrates_per_100g, carbohydrates_min, carbohydrates_max,
    sugar_per_100g, sugar_min, sugar_max,
    fiber_per_100g, fiber_min, fiber_max,
    sodium_per_100g, sodium_min, sodium_max,
    confidence_level, data_source, notes
) VALUES (
    'biscoito_simples',
    460, 420, 500,
    8, 6, 11,
    16, 10, 24,
    68, 60, 75,
    10, 5, 18,
    3, 2, 5,
    550, 400, 800,
    0.80, 'TACO/Anvisa', 'Cream cracker, maria'
);

-- Snack Salgado
INSERT INTO category_nutrition_profiles (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    carbohydrates_per_100g, carbohydrates_min, carbohydrates_max,
    sugar_per_100g, sugar_min, sugar_max,
    fiber_per_100g, fiber_min, fiber_max,
    sodium_per_100g, sodium_min, sodium_max,
    confidence_level, data_source, notes
) VALUES (
    'snack_salgado',
    530, 480, 580,
    6, 4, 9,
    32, 25, 38,
    58, 50, 65,
    3, 1, 6,
    3.5, 2, 6,
    950, 700, 1300,
    0.85, 'TACO/Anvisa', 'Salgadinhos, chips'
);

-- Chocolate
INSERT INTO category_nutrition_profiles (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    carbohydrates_per_100g, carbohydrates_min, carbohydrates_max,
    sugar_per_100g, sugar_min, sugar_max,
    fiber_per_100g, fiber_min, fiber_max,
    sodium_per_100g, sodium_min, sodium_max,
    confidence_level, data_source, notes
) VALUES (
    'chocolate',
    545, 500, 590,
    7, 5, 10,
    32, 25, 40,
    58, 50, 65,
    50, 40, 58,
    4, 2, 7,
    80, 40, 150,
    0.85, 'TACO/Anvisa', 'Chocolate ao leite'
);

-- Achocolatado em Pó
INSERT INTO category_nutrition_profiles (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    carbohydrates_per_100g, carbohydrates_min, carbohydrates_max,
    sugar_per_100g, sugar_min, sugar_max,
    fiber_per_100g, fiber_min, fiber_max,
    sodium_per_100g, sodium_min, sodium_max,
    confidence_level, data_source, notes
) VALUES (
    'achocolatado_po',
    385, 360, 410,
    4, 3, 6,
    3, 1.5, 5,
    87, 82, 92,
    77, 70, 83,
    3.5, 2, 6,
    180, 100, 280,
    0.90, 'TACO/Anvisa', 'Açúcar extremamente elevado'
);

-- === BEBIDAS (por 100ml) ===

-- Refrigerante
INSERT INTO category_nutrition_profiles (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    carbohydrates_per_100g, carbohydrates_min, carbohydrates_max,
    sugar_per_100g, sugar_min, sugar_max,
    fiber_per_100g, fiber_min, fiber_max,
    sodium_per_100g, sodium_min, sodium_max,
    confidence_level, data_source, notes
) VALUES (
    'refrigerante',
    45, 38, 55,
    0, 0, 0.5,
    0, 0, 0,
    11, 9, 14,
    10.8, 9, 13,
    0, 0, 0,
    12, 5, 25,
    0.90, 'TACO/Anvisa', 'Valores por 100ml'
);

-- Refrigerante Zero
INSERT INTO category_nutrition_profiles (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    carbohydrates_per_100g, carbohydrates_min, carbohydrates_max,
    sugar_per_100g, sugar_min, sugar_max,
    fiber_per_100g, fiber_min, fiber_max,
    sodium_per_100g, sodium_min, sodium_max,
    confidence_level, data_source, notes
) VALUES (
    'refrigerante_zero',
    0.5, 0, 2,
    0, 0, 0,
    0, 0, 0,
    0, 0, 0.5,
    0, 0, 0,
    0, 0, 0,
    15, 5, 35,
    0.95, 'TACO/Anvisa', 'Zero calorias, valores por 100ml'
);

-- Suco Industrializado
INSERT INTO category_nutrition_profiles (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    carbohydrates_per_100g, carbohydrates_min, carbohydrates_max,
    sugar_per_100g, sugar_min, sugar_max,
    fiber_per_100g, fiber_min, fiber_max,
    sodium_per_100g, sodium_min, sodium_max,
    confidence_level, data_source, notes
) VALUES (
    'suco_industrializado',
    50, 40, 65,
    0.3, 0, 1,
    0, 0, 0.5,
    12, 10, 16,
    11, 9, 15,
    0.2, 0, 0.5,
    10, 5, 20,
    0.80, 'TACO/Anvisa', 'Néctares e sucos, valores por 100ml'
);

-- Bebida Açucarada Genérica
INSERT INTO category_nutrition_profiles (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    carbohydrates_per_100g, carbohydrates_min, carbohydrates_max,
    sugar_per_100g, sugar_min, sugar_max,
    fiber_per_100g, fiber_min, fiber_max,
    sodium_per_100g, sodium_min, sodium_max,
    confidence_level, data_source, notes
) VALUES (
    'bebida_acucarada',
    47, 38, 60,
    0.2, 0, 0.5,
    0, 0, 0.2,
    11.5, 9, 15,
    11, 9, 14,
    0, 0, 0.3,
    12, 5, 25,
    0.75, 'TACO/Anvisa', 'Bebidas açucaradas genéricas, valores por 100ml'
);

-- =====================================================
-- 3. MAPEAMENTOS DE CATEGORIAS (ALIASES)
-- =====================================================

-- Laticínios Cremosos
INSERT INTO category_mappings (raw_category_name, normalized_category_code, confidence) VALUES
('requeijão', 'laticinio_cremoso', 1.00),
('requeijão cremoso', 'laticinio_cremoso', 1.00),
('cream cheese', 'laticinio_cremoso', 1.00),
('queijo cremoso', 'laticinio_cremoso', 0.95),
('catupiry', 'laticinio_cremoso', 1.00),
('requeijão light', 'laticinio_cremoso_light', 1.00),
('cream cheese light', 'laticinio_cremoso_light', 1.00),
('requeijão cremoso light', 'laticinio_cremoso_light', 1.00),
('creme de queijo', 'laticinio_cremoso', 0.90),
('creme de queijo light', 'laticinio_cremoso_light', 0.90);

-- Queijos
INSERT INTO category_mappings (raw_category_name, normalized_category_code, confidence) VALUES
('queijo parmesão', 'queijo_duro', 1.00),
('queijo mussarela', 'queijo_duro', 1.00),
('queijo cheddar', 'queijo_duro', 1.00),
('queijo prato', 'queijo_duro', 1.00),
('queijo coalho', 'queijo_duro', 1.00),
('parmesão ralado', 'queijo_ralado', 1.00),
('queijo ralado', 'queijo_ralado', 1.00),
('queijo parmesão ralado', 'queijo_ralado', 1.00);

-- Iogurtes
INSERT INTO category_mappings (raw_category_name, normalized_category_code, confidence) VALUES
('iogurte', 'iogurte_natural', 0.70),
('iogurte natural', 'iogurte_natural', 1.00),
('iogurte grego', 'iogurte_natural', 0.95),
('iogurte com sabor', 'iogurte_adocicado', 1.00),
('iogurte de morango', 'iogurte_adocicado', 1.00),
('iogurte adoçado', 'iogurte_adocicado', 1.00);

-- Sobremesas Lácteas
INSERT INTO category_mappings (raw_category_name, normalized_category_code, confidence) VALUES
('sobremesa láctea', 'sobremesa_lactea', 1.00),
('petit suisse', 'sobremesa_lactea', 1.00),
('danoninho', 'sobremesa_lactea', 1.00),
('danette', 'sobremesa_lactea', 1.00),
('chandelle', 'sobremesa_lactea', 1.00);

-- Arroz
INSERT INTO category_mappings (raw_category_name, normalized_category_code, confidence) VALUES
('arroz', 'arroz_branco', 0.85),
('arroz branco', 'arroz_branco', 1.00),
('arroz polido', 'arroz_branco', 1.00),
('arroz tipo 1', 'arroz_branco', 1.00),
('arroz tipo 2', 'arroz_branco', 1.00),
('arroz integral', 'arroz_integral', 1.00),
('arroz parboilizado', 'arroz_integral', 0.90);

-- Massas
INSERT INTO category_mappings (raw_category_name, normalized_category_code, confidence) VALUES
('macarrão', 'macarrao', 1.00),
('massa', 'macarrao', 1.00),
('espaguete', 'macarrao', 1.00),
('penne', 'macarrao', 1.00),
('parafuso', 'macarrao', 1.00);

-- Pães
INSERT INTO category_mappings (raw_category_name, normalized_category_code, confidence) VALUES
('pão', 'pao', 1.00),
('pão de forma', 'pao', 1.00),
('pão integral', 'pao', 0.95),
('pão francês', 'pao', 1.00);

-- Cereais
INSERT INTO category_mappings (raw_category_name, normalized_category_code, confidence) VALUES
('cereal', 'cereal', 0.80),
('cereal matinal', 'cereal', 1.00),
('sucrilhos', 'cereal_acucarado', 1.00),
('cereal açucarado', 'cereal_acucarado', 1.00);

-- Biscoitos
INSERT INTO category_mappings (raw_category_name, normalized_category_code, confidence) VALUES
('biscoito', 'biscoito_simples', 0.75),
('bolacha', 'biscoito_simples', 0.75),
('biscoito recheado', 'biscoito_recheado', 1.00),
('bolacha recheada', 'biscoito_recheado', 1.00),
('cream cracker', 'biscoito_simples', 1.00),
('biscoito maria', 'biscoito_simples', 1.00),
('wafer', 'biscoito_recheado', 1.00);

-- Snacks
INSERT INTO category_mappings (raw_category_name, normalized_category_code, confidence) VALUES
('salgadinho', 'snack_salgado', 1.00),
('chips', 'snack_salgado', 1.00),
('doritos', 'snack_salgado', 1.00),
('cheetos', 'snack_salgado', 1.00),
('ruffles', 'snack_salgado', 1.00);

-- Chocolates
INSERT INTO category_mappings (raw_category_name, normalized_category_code, confidence) VALUES
('chocolate', 'chocolate', 0.90),
('barra de chocolate', 'chocolate', 1.00),
('achocolatado', 'achocolatado_po', 1.00),
('achocolatado em pó', 'achocolatado_po', 1.00),
('nescau', 'achocolatado_po', 1.00),
('toddy', 'achocolatado_po', 1.00);

-- Bebidas
INSERT INTO category_mappings (raw_category_name, normalized_category_code, confidence) VALUES
('refrigerante', 'refrigerante', 1.00),
('refrigerante zero', 'refrigerante_zero', 1.00),
('refrigerante diet', 'refrigerante_zero', 1.00),
('suco', 'suco_industrializado', 0.85),
('suco industrializado', 'suco_industrializado', 1.00),
('néctar', 'suco_industrializado', 1.00),
('bebida açucarada', 'bebida_acucarada', 1.00);

-- =====================================================
-- FIM DO SEED
-- =====================================================

-- Estatísticas
SELECT 'Categorias cadastradas: ' || COUNT(*) FROM nutrition_categories;
SELECT 'Perfis nutricionais cadastrados: ' || COUNT(*) FROM category_nutrition_profiles;
SELECT 'Mapeamentos cadastrados: ' || COUNT(*) FROM category_mappings;
