-- =====================================================
-- SEED OTIMIZADO: BASE DE CONHECIMENTO NUTRICIONAL
-- Dados baseados em TACO 2011, IBGE, Anvisa e USDA
-- Versão: 2.0 (Completa e Otimizada)
-- =====================================================

-- =====================================================
-- 1. CATEGORIAS NUTRICIONAIS (Hierárquicas)
-- =====================================================

-- Categorias Pai
INSERT INTO nutrition_category (code, name, description, display_order) VALUES
('laticinio', 'Laticínios', 'Produtos derivados de leite', 10),
('carboidrato', 'Carboidratos Base', 'Grãos, cereais, massas e tubérculos', 20),
('ultraprocessado', 'Ultraprocessados', 'Produtos industrializados com alto processamento', 30),
('bebida', 'Bebidas', 'Bebidas em geral (não lácteas)', 40),
('proteico', 'Ricos em Proteína', 'Produtos com alto teor proteico', 50),
('gordura', 'Gorduras e Óleos', 'Óleos, manteigas, margarinas', 60),
('fruta_vegetal', 'Frutas e Vegetais', 'Produtos de origem vegetal in natura', 70);

-- === SUBCATEGORIAS: LATICÍNIOS ===
INSERT INTO nutrition_category (code, name, description, parent_code, display_order) VALUES
('laticinio_cremoso', 'Laticínio Cremoso Tradicional', 'Requeijão, cream cheese, catupiry', 'laticinio', 11),
('laticinio_cremoso_light', 'Laticínio Cremoso Light', 'Versões light/reduzidas em gordura', 'laticinio', 12),
('queijo_duro', 'Queijo Duro/Semi-Duro', 'Parmesão, mussarela, cheddar', 'laticinio', 13),
('queijo_ralado', 'Queijo Ralado', 'Queijos ralados (parmesão, etc)', 'laticinio', 14),
('queijo_minas', 'Queijo Minas Frescal', 'Queijo minas frescal e similares', 'laticinio', 15),
('iogurte_natural', 'Iogurte Natural', 'Iogurte sem açúcar adicionado', 'laticinio', 16),
('iogurte_adocicado', 'Iogurte Adoçado', 'Iogurte com açúcar/sabores', 'laticinio', 17),
('iogurte_grego', 'Iogurte Grego', 'Iogurte grego tradicional', 'laticinio', 18),
('sobremesa_lactea', 'Sobremesa Láctea', 'Petit suisse, danette, chandelle', 'laticinio', 19);

-- === SUBCATEGORIAS: CARBOIDRATOS ===
INSERT INTO nutrition_category (code, name, description, parent_code, display_order) VALUES
('arroz_branco', 'Arroz Branco', 'Arroz branco polido', 'carboidrato', 21),
('arroz_integral', 'Arroz Integral', 'Arroz integral/parboilizado', 'carboidrato', 22),
('macarrao', 'Macarrão', 'Massas e macarrão', 'carboidrato', 23),
('pao', 'Pão Tradicional', 'Pão de forma, francês', 'carboidrato', 24),
('pao_integral', 'Pão Integral', 'Pães integrais', 'carboidrato', 25),
('cereal', 'Cereal Matinal', 'Cereais matinais tradicionais', 'carboidrato', 26),
('cereal_acucarado', 'Cereal Açucarado', 'Cereais com alto teor de açúcar', 'carboidrato', 27),
('feijao', 'Feijão', 'Feijão carioca, preto, etc', 'carboidrato', 28),
('batata', 'Batata', 'Batata inglesa, doce', 'carboidrato', 29);

-- === SUBCATEGORIAS: ULTRAPROCESSADOS ===
INSERT INTO nutrition_category (code, name, description, parent_code, display_order) VALUES
('biscoito_recheado', 'Biscoito Recheado', 'Biscoitos e bolachas recheadas', 'ultraprocessado', 31),
('biscoito_simples', 'Biscoito Simples', 'Biscoitos cream cracker, maria, água e sal', 'ultraprocessado', 32),
('snack_salgado', 'Snack Salgado', 'Salgadinhos, chips', 'ultraprocessado', 33),
('chocolate', 'Chocolate', 'Chocolates e barras', 'ultraprocessado', 34),
('achocolatado_po', 'Achocolatado em Pó', 'Nescau, Toddy, etc', 'ultraprocessado', 35),
('embutido', 'Embutido', 'Salsicha, linguiça, presunto', 'ultraprocessado', 36),
('macarrao_instantaneo', 'Macarrão Instantâneo', 'Miojo e similares', 'ultraprocessado', 37);

-- === SUBCATEGORIAS: BEBIDAS ===
INSERT INTO nutrition_category (code, name, description, parent_code, display_order) VALUES
('refrigerante', 'Refrigerante Tradicional', 'Refrigerantes com açúcar', 'bebida', 41),
('refrigerante_zero', 'Refrigerante Zero', 'Refrigerantes zero/diet', 'bebida', 42),
('suco_industrializado', 'Suco Industrializado', 'Sucos e néctares industrializados', 'bebida', 43),
('suco_natural', 'Suco Natural', 'Sucos naturais de fruta', 'bebida', 44),
('bebida_acucarada', 'Bebida Açucarada', 'Bebidas com açúcar adicionado', 'bebida', 45),
('cha_industrializado', 'Chá Industrializado', 'Chás prontos (gelado, mate)', 'bebida', 46);

-- === SUBCATEGORIAS: PROTEICOS ===
INSERT INTO nutrition_category (code, name, description, parent_code, display_order) VALUES
('whey_protein', 'Whey Protein', 'Suplementos de whey protein', 'proteico', 51),
('barra_proteica', 'Barra Proteica', 'Barras com alto teor proteico', 'proteico', 52),
('iogurte_proteico', 'Iogurte Proteico', 'Iogurtes com proteína adicionada', 'proteico', 53),
('carne_vermelha', 'Carne Vermelha', 'Bovina, suína', 'proteico', 54),
('frango', 'Frango', 'Carne de frango', 'proteico', 55),
('peixe', 'Peixe', 'Peixes e frutos do mar', 'proteico', 56),
('ovo', 'Ovo', 'Ovos de galinha', 'proteico', 57);

-- === SUBCATEGORIAS: GORDURAS ===
INSERT INTO nutrition_category (code, name, description, parent_code, display_order) VALUES
('oleo_vegetal', 'Óleo Vegetal', 'Óleo de soja, girassol, canola', 'gordura', 61),
('azeite', 'Azeite', 'Azeite de oliva', 'gordura', 62),
('manteiga', 'Manteiga', 'Manteiga tradicional', 'gordura', 63),
('margarina', 'Margarina', 'Margarina e cremes vegetais', 'gordura', 64);

-- =====================================================
-- 2. PERFIS NUTRICIONAIS POR CATEGORIA
-- =====================================================

-- === LATICÍNIOS ===

-- Laticínio Cremoso Tradicional
INSERT INTO nutrition_category_profile (
    category_code, 
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    saturated_fat_per_100g, saturated_fat_min, saturated_fat_max,
    trans_fat_per_100g, trans_fat_min, trans_fat_max,
    carbohydrates_per_100g, carbohydrates_min, carbohydrates_max,
    sugar_per_100g, sugar_min, sugar_max,
    fiber_per_100g, fiber_min, fiber_max,
    sodium_per_100g, sodium_min, sodium_max,
    confidence_level, data_source, reference_year, notes
) VALUES (
    'laticinio_cremoso',
    220, 180, 280,
    8, 5, 12,
    20, 15, 30,
    12, 9, 18,
    0, 0, 0.5,
    4, 2, 8,
    2, 0, 5,
    0, 0, 0,
    450, 300, 700,
    0.85, 'TACO 2011, IBGE', 2023, 'Perfil típico de requeijão cremoso e cream cheese tradicionais'
);

-- Laticínio Cremoso Light
INSERT INTO nutrition_category_profile (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    saturated_fat_per_100g, saturated_fat_min, saturated_fat_max,
    trans_fat_per_100g, trans_fat_min, trans_fat_max,
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
    5, 3, 9,
    0, 0, 0.3,
    5, 3, 8,
    2, 0, 4,
    0, 0, 0,
    400, 250, 600,
    0.80, 'TACO 2011, Anvisa', 'Versões light com gordura reduzida em 25-50%'
);

-- Queijo Duro/Semi-Duro
INSERT INTO nutrition_category_profile (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    saturated_fat_per_100g, saturated_fat_min, saturated_fat_max,
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
    15, 10, 20,
    1, 0, 3,
    0.5, 0, 2,
    0, 0, 0,
    650, 400, 900,
    0.90, 'TACO 2011', 'Parmesão, mussarela, cheddar, prato'
);

-- Queijo Ralado
INSERT INTO nutrition_category_profile (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    saturated_fat_per_100g, saturated_fat_min, saturated_fat_max,
    carbohydrates_per_100g, carbohydrates_min, carbohydrates_max,
    sugar_per_100g, sugar_min, sugar_max,
    sodium_per_100g, sodium_min, sodium_max,
    confidence_level, data_source, notes
) VALUES (
    'queijo_ralado',
    400, 350, 450,
    36, 30, 42,
    28, 20, 35,
    17, 12, 22,
    1, 0, 4,
    0.5, 0, 2,
    1000, 800, 1400,
    0.85, 'TACO 2011, Anvisa', 'Alto teor de sódio devido ao processamento'
);

-- Queijo Minas Frescal
INSERT INTO nutrition_category_profile (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    saturated_fat_per_100g, saturated_fat_min, saturated_fat_max,
    carbohydrates_per_100g, carbohydrates_min, carbohydrates_max,
    sugar_per_100g, sugar_min, sugar_max,
    sodium_per_100g, sodium_min, sodium_max,
    confidence_level, data_source, notes
) VALUES (
    'queijo_minas',
    240, 200, 280,
    17, 14, 20,
    18, 14, 22,
    11, 9, 14,
    1, 0, 2,
    1, 0, 2,
    380, 300, 500,
    0.85, 'TACO 2011', 'Queijo fresco com alto teor de umidade'
);

-- Iogurte Natural
INSERT INTO nutrition_category_profile (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    saturated_fat_per_100g, saturated_fat_min, saturated_fat_max,
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
    1.3, 0, 2.5,
    6, 4, 8,
    5, 4, 6,
    0, 0, 0,
    60, 40, 90,
    0.85, 'TACO 2011', 'Açúcar natural do leite (lactose) apenas'
);

-- Iogurte Adoçado
INSERT INTO nutrition_category_profile (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    saturated_fat_per_100g, saturated_fat_min, saturated_fat_max,
    carbohydrates_per_100g, carbohydrates_min, carbohydrates_max,
    sugar_per_100g, sugar_min, sugar_max,
    sodium_per_100g, sodium_min, sodium_max,
    confidence_level, data_source, notes
) VALUES (
    'iogurte_adocicado',
    100, 80, 130,
    3.5, 2.5, 5,
    1.5, 0, 3,
    1, 0, 2,
    16, 12, 20,
    14, 10, 18,
    65, 40, 100,
    0.80, 'TACO 2011, Anvisa', 'Com açúcar adicionado e aromatizantes'
);

-- Iogurte Grego
INSERT INTO nutrition_category_profile (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    saturated_fat_per_100g, saturated_fat_min, saturated_fat_max,
    carbohydrates_per_100g, carbohydrates_min, carbohydrates_max,
    sugar_per_100g, sugar_min, sugar_max,
    sodium_per_100g, sodium_min, sodium_max,
    confidence_level, data_source, notes
) VALUES (
    'iogurte_grego',
    97, 80, 130,
    9, 7, 11,
    4, 2, 6,
    2.5, 1.5, 4,
    5, 3, 7,
    4, 3, 5,
    50, 35, 70,
    0.85, 'TACO 2011, USDA', 'Maior teor proteico devido ao processo de coagem'
);

-- Sobremesa Láctea
INSERT INTO nutrition_category_profile (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    saturated_fat_per_100g, saturated_fat_min, saturated_fat_max,
    carbohydrates_per_100g, carbohydrates_min, carbohydrates_max,
    sugar_per_100g, sugar_min, sugar_max,
    sodium_per_100g, sodium_min, sodium_max,
    confidence_level, data_source, notes
) VALUES (
    'sobremesa_lactea',
    150, 120, 200,
    3.5, 2, 5,
    5, 3, 8,
    3, 2, 5,
    23, 18, 30,
    20, 15, 25,
    80, 50, 120,
    0.85, 'TACO 2011, Anvisa', 'Petit suisse, danette, chandelle - alto teor de açúcar'
);

-- === CARBOIDRATOS ===

-- Arroz Branco
INSERT INTO nutrition_category_profile (
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
    0.95, 'TACO 2011', 'Arroz branco polido, tipo 1 e tipo 2'
);

-- Arroz Integral
INSERT INTO nutrition_category_profile (
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
    0.90, 'TACO 2011', 'Maior teor de fibras e micronutrientes'
);

-- Macarrão
INSERT INTO nutrition_category_profile (
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
    0.90, 'TACO 2011', 'Massa seca de semolina'
);

-- Pão Tradicional
INSERT INTO nutrition_category_profile (
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
    0.85, 'TACO 2011', 'Pão de forma branco e francês'
);

-- Pão Integral
INSERT INTO nutrition_category_profile (
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
    'pao_integral',
    250, 230, 280,
    10, 8, 13,
    4, 2, 6,
    48, 44, 54,
    5, 2, 8,
    6, 4, 9,
    420, 320, 550,
    0.85, 'TACO 2011, Anvisa', 'Maior teor de fibras'
);

-- Cereal Matinal
INSERT INTO nutrition_category_profile (
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
    0.80, 'TACO 2011, Anvisa', 'Cereais matinais tradicionais'
);

-- Cereal Açucarado
INSERT INTO nutrition_category_profile (
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
    0.85, 'TACO 2011, Anvisa', 'Sucrilhos e similares - alto teor de açúcar'
);

-- Feijão
INSERT INTO nutrition_category_profile (
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
    'feijao',
    330, 310, 350,
    20, 18, 23,
    1.5, 1, 2.5,
    60, 55, 65,
    2, 1, 3,
    18, 15, 22,
    10, 5, 20,
    0.95, 'TACO 2011', 'Feijão carioca, preto - cru'
);

-- === ULTRAPROCESSADOS ===

-- Biscoito Recheado
INSERT INTO nutrition_category_profile (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    saturated_fat_per_100g, saturated_fat_min, saturated_fat_max,
    trans_fat_per_100g, trans_fat_min, trans_fat_max,
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
    9, 6, 14,
    1, 0.5, 3,
    70, 65, 75,
    32, 25, 40,
    2.5, 1.5, 4,
    380, 250, 550,
    0.85, 'TACO 2011, Anvisa', 'Alto açúcar, gordura e aditivos'
);

-- Biscoito Simples
INSERT INTO nutrition_category_profile (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    saturated_fat_per_100g, saturated_fat_min, saturated_fat_max,
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
    7, 4, 12,
    68, 60, 75,
    10, 5, 18,
    3, 2, 5,
    550, 400, 800,
    0.80, 'TACO 2011, Anvisa', 'Cream cracker, maria, água e sal'
);

-- Snack Salgado
INSERT INTO nutrition_category_profile (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    saturated_fat_per_100g, saturated_fat_min, saturated_fat_max,
    trans_fat_per_100g, trans_fat_min, trans_fat_max,
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
    13, 10, 18,
    0.5, 0, 2,
    58, 50, 65,
    3, 1, 6,
    3.5, 2, 6,
    950, 700, 1300,
    0.85, 'TACO 2011, Anvisa', 'Chips, doritos, cheetos - altíssimo sódio'
);

-- Chocolate
INSERT INTO nutrition_category_profile (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    saturated_fat_per_100g, saturated_fat_min, saturated_fat_max,
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
    19, 15, 25,
    58, 50, 65,
    50, 40, 58,
    4, 2, 7,
    80, 40, 150,
    0.85, 'TACO 2011, USDA', 'Chocolate ao leite'
);

-- Achocolatado em Pó
INSERT INTO nutrition_category_profile (
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
    0.90, 'TACO 2011, Anvisa', 'Nescau, Toddy - açúcar extremamente elevado'
);

-- === BEBIDAS (valores por 100ml) ===

-- Refrigerante
INSERT INTO nutrition_category_profile (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    carbohydrates_per_100g, carbohydrates_min, carbohydrates_max,
    sugar_per_100g, sugar_min, sugar_max,
    sodium_per_100g, sodium_min, sodium_max,
    confidence_level, data_source, is_liquid, notes
) VALUES (
    'refrigerante',
    45, 38, 55,
    0, 0, 0.5,
    0, 0, 0,
    11, 9, 14,
    10.8, 9, 13,
    12, 5, 25,
    0.90, 'TACO 2011, Anvisa', TRUE, 'Valores por 100ml'
);

-- Refrigerante Zero
INSERT INTO nutrition_category_profile (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    carbohydrates_per_100g, carbohydrates_min, carbohydrates_max,
    sugar_per_100g, sugar_min, sugar_max,
    sodium_per_100g, sodium_min, sodium_max,
    confidence_level, data_source, is_liquid, notes
) VALUES (
    'refrigerante_zero',
    0.5, 0, 2,
    0, 0, 0,
    0, 0, 0,
    0, 0, 0.5,
    0, 0, 0,
    15, 5, 35,
    0.95, 'TACO 2011, Anvisa', TRUE, 'Zero calorias, valores por 100ml'
);

-- Suco Industrializado
INSERT INTO nutrition_category_profile (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    carbohydrates_per_100g, carbohydrates_min, carbohydrates_max,
    sugar_per_100g, sugar_min, sugar_max,
    sodium_per_100g, sodium_min, sodium_max,
    confidence_level, data_source, is_liquid, notes
) VALUES (
    'suco_industrializado',
    50, 40, 65,
    0.3, 0, 1,
    0, 0, 0.5,
    12, 10, 16,
    11, 9, 15,
    10, 5, 20,
    0.80, 'TACO 2011, Anvisa', TRUE, 'Néctares e sucos de caixinha, valores por 100ml'
);

-- Suco Natural
INSERT INTO nutrition_category_profile (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    carbohydrates_per_100g, carbohydrates_min, carbohydrates_max,
    sugar_per_100g, sugar_min, sugar_max,
    fiber_per_100g, fiber_min, fiber_max,
    sodium_per_100g, sodium_min, sodium_max,
    confidence_level, data_source, is_liquid, notes
) VALUES (
    'suco_natural',
    45, 35, 60,
    0.5, 0.3, 1,
    0.2, 0, 0.5,
    11, 8, 15,
    10, 7, 14,
    0.3, 0, 1,
    5, 2, 10,
    0.85, 'TACO 2011', TRUE, 'Suco natural sem açúcar adicionado, valores por 100ml'
);

-- === PROTEICOS ===

-- Whey Protein
INSERT INTO nutrition_category_profile (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    carbohydrates_per_100g, carbohydrates_min, carbohydrates_max,
    sugar_per_100g, sugar_min, sugar_max,
    sodium_per_100g, sodium_min, sodium_max,
    confidence_level, data_source, notes
) VALUES (
    'whey_protein',
    380, 350, 420,
    75, 70, 85,
    5, 2, 10,
    12, 5, 20,
    5, 0, 15,
    350, 200, 600,
    0.80, 'Fabricantes, USDA', 'Suplementos de whey protein concentrado/isolado'
);

-- Frango
INSERT INTO nutrition_category_profile (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    saturated_fat_per_100g, saturated_fat_min, saturated_fat_max,
    carbohydrates_per_100g, carbohydrates_min, carbohydrates_max,
    sodium_per_100g, sodium_min, sodium_max,
    confidence_level, data_source, notes
) VALUES (
    'frango',
    165, 140, 190,
    31, 28, 34,
    3.6, 2, 6,
    1, 0.5, 2,
    0, 0, 0,
    70, 50, 100,
    0.95, 'TACO 2011', 'Peito de frango sem pele, cru'
);

-- Ovo
INSERT INTO nutrition_category_profile (
    category_code,
    calories_per_100g, calories_min, calories_max,
    protein_per_100g, protein_min, protein_max,
    fat_per_100g, fat_min, fat_max,
    saturated_fat_per_100g, saturated_fat_min, saturated_fat_max,
    carbohydrates_per_100g, carbohydrates_min, carbohydrates_max,
    sodium_per_100g, sodium_min, sodium_max,
    confidence_level, data_source, notes
) VALUES (
    'ovo',
    155, 145, 165,
    13, 12, 14,
    11, 10, 12,
    3.3, 3, 3.8,
    1.1, 0.8, 1.5,
    140, 120, 160,
    0.95, 'TACO 2011', 'Ovo de galinha inteiro'
);

-- =====================================================
-- 3. ALIASES DE CATEGORIAS (MAPEAMENTOS)
-- =====================================================

-- === LATICÍNIOS CREMOSOS ===
INSERT INTO nutrition_category_alias (category_code, alias_name, confidence, match_type) VALUES
('laticinio_cremoso', 'requeijão', 1.00, 'exact'),
('laticinio_cremoso', 'requeijão cremoso', 1.00, 'exact'),
('laticinio_cremoso', 'cream cheese', 1.00, 'exact'),
('laticinio_cremoso', 'queijo cremoso', 0.95, 'partial'),
('laticinio_cremoso', 'catupiry', 1.00, 'exact'),
('laticinio_cremoso', 'processado cremoso', 0.90, 'partial'),
('laticinio_cremoso', 'creme de queijo', 0.90, 'partial'),
('laticinio_cremoso_light', 'requeijão light', 1.00, 'exact'),
('laticinio_cremoso_light', 'cream cheese light', 1.00, 'exact'),
('laticinio_cremoso_light', 'requeijão cremoso light', 1.00, 'exact'),
('laticinio_cremoso_light', 'creme de queijo light', 0.90, 'partial'),
('laticinio_cremoso_light', 'requeijão tradicional light', 0.95, 'partial');

-- === QUEIJOS ===
INSERT INTO nutrition_category_alias (category_code, alias_name, confidence, match_type) VALUES
('queijo_duro', 'queijo parmesão', 1.00, 'exact'),
('queijo_duro', 'queijo mussarela', 1.00, 'exact'),
('queijo_duro', 'queijo cheddar', 1.00, 'exact'),
('queijo_duro', 'queijo prato', 1.00, 'exact'),
('queijo_duro', 'queijo coalho', 1.00, 'exact'),
('queijo_duro', 'queijo provolone', 1.00, 'exact'),
('queijo_duro', 'queijo gouda', 1.00, 'exact'),
('queijo_ralado', 'parmesão ralado', 1.00, 'exact'),
('queijo_ralado', 'queijo ralado', 1.00, 'exact'),
('queijo_ralado', 'queijo parmesão ralado', 1.00, 'exact'),
('queijo_minas', 'queijo minas', 1.00, 'exact'),
('queijo_minas', 'queijo minas frescal', 1.00, 'exact'),
('queijo_minas', 'minas frescal', 1.00, 'exact');

-- === IOGURTES ===
INSERT INTO nutrition_category_alias (category_code, alias_name, confidence, match_type) VALUES
('iogurte_natural', 'iogurte', 0.70, 'partial'),
('iogurte_natural', 'iogurte natural', 1.00, 'exact'),
('iogurte_natural', 'iogurte integral', 0.95, 'partial'),
('iogurte_grego', 'iogurte grego', 1.00, 'exact'),
('iogurte_grego', 'greek yogurt', 1.00, 'exact'),
('iogurte_adocicado', 'iogurte com sabor', 1.00, 'exact'),
('iogurte_adocicado', 'iogurte de morango', 1.00, 'exact'),
('iogurte_adocicado', 'iogurte de frutas', 1.00, 'exact'),
('iogurte_adocicado', 'iogurte adoçado', 1.00, 'exact'),
('iogurte_proteico', 'iogurte proteico', 1.00, 'exact'),
('iogurte_proteico', 'iogurte protein', 1.00, 'exact');

-- === SOBREMESAS LÁCTEAS ===
INSERT INTO nutrition_category_alias (category_code, alias_name, confidence, match_type) VALUES
('sobremesa_lactea', 'sobremesa láctea', 1.00, 'exact'),
('sobremesa_lactea', 'petit suisse', 1.00, 'exact'),
('sobremesa_lactea', 'danoninho', 1.00, 'exact'),
('sobremesa_lactea', 'danette', 1.00, 'exact'),
('sobremesa_lactea', 'chandelle', 1.00, 'exact'),
('sobremesa_lactea', 'pudim', 0.90, 'partial'),
('sobremesa_lactea', 'mousse', 0.85, 'partial');

-- === ARROZ ===
INSERT INTO nutrition_category_alias (category_code, alias_name, confidence, match_type) VALUES
('arroz_branco', 'arroz', 0.85, 'partial'),
('arroz_branco', 'arroz branco', 1.00, 'exact'),
('arroz_branco', 'arroz polido', 1.00, 'exact'),
('arroz_branco', 'arroz tipo 1', 1.00, 'exact'),
('arroz_branco', 'arroz tipo 2', 1.00, 'exact'),
('arroz_branco', 'arroz agulhinha', 0.95, 'partial'),
('arroz_integral', 'arroz integral', 1.00, 'exact'),
('arroz_integral', 'arroz parboilizado', 0.90, 'partial'),
('arroz_integral', 'arroz cateto integral', 1.00, 'exact');

-- === MASSAS ===
INSERT INTO nutrition_category_alias (category_code, alias_name, confidence, match_type) VALUES
('macarrao', 'macarrão', 1.00, 'exact'),
('macarrao', 'massa', 1.00, 'exact'),
('macarrao', 'espaguete', 1.00, 'exact'),
('macarrao', 'penne', 1.00, 'exact'),
('macarrao', 'fusilli', 1.00, 'exact'),
('macarrao', 'parafuso', 1.00, 'exact'),
('macarrao', 'farfalle', 1.00, 'exact'),
('macarrao_instantaneo', 'miojo', 1.00, 'exact'),
('macarrao_instantaneo', 'macarrão instantâneo', 1.00, 'exact'),
('macarrao_instantaneo', 'nissin', 0.95, 'partial'),
('macarrao_instantaneo', 'cup noodles', 1.00, 'exact');

-- === PÃES ===
INSERT INTO nutrition_category_alias (category_code, alias_name, confidence, match_type) VALUES
('pao', 'pão', 1.00, 'exact'),
('pao', 'pão de forma', 1.00, 'exact'),
('pao', 'pão francês', 1.00, 'exact'),
('pao', 'pão branco', 1.00, 'exact'),
('pao_integral', 'pão integral', 1.00, 'exact'),
('pao_integral', 'pão de forma integral', 1.00, 'exact'),
('pao_integral', 'pão 100% integral', 1.00, 'exact');

-- === CEREAIS ===
INSERT INTO nutrition_category_alias (category_code, alias_name, confidence, match_type) VALUES
('cereal', 'cereal', 0.80, 'partial'),
('cereal', 'cereal matinal', 1.00, 'exact'),
('cereal', 'granola', 0.90, 'partial'),
('cereal_acucarado', 'sucrilhos', 1.00, 'exact'),
('cereal_acucarado', 'corn flakes', 1.00, 'exact'),
('cereal_acucarado', 'cereal açucarado', 1.00, 'exact'),
('cereal_acucarado', 'nescau cereal', 1.00, 'exact');

-- === FEIJÃO ===
INSERT INTO nutrition_category_alias (category_code, alias_name, confidence, match_type) VALUES
('feijao', 'feijão', 1.00, 'exact'),
('feijao', 'feijão carioca', 1.00, 'exact'),
('feijao', 'feijão preto', 1.00, 'exact'),
('feijao', 'feijão vermelho', 1.00, 'exact'),
('feijao', 'feijão branco', 1.00, 'exact');

-- === BISCOITOS ===
INSERT INTO nutrition_category_alias (category_code, alias_name, confidence, match_type) VALUES
('biscoito_recheado', 'biscoito', 0.75, 'partial'),
('biscoito_recheado', 'bolacha', 0.75, 'partial'),
('biscoito_recheado', 'biscoito recheado', 1.00, 'exact'),
('biscoito_recheado', 'bolacha recheada', 1.00, 'exact'),
('biscoito_recheado', 'wafer', 1.00, 'exact'),
('biscoito_recheado', 'negresco', 0.95, 'partial'),
('biscoito_recheado', 'oreo', 0.95, 'partial'),
('biscoito_simples', 'cream cracker', 1.00, 'exact'),
('biscoito_simples', 'biscoito maria', 1.00, 'exact'),
('biscoito_simples', 'biscoito água e sal', 1.00, 'exact'),
('biscoito_simples', 'cracker', 1.00, 'exact');

-- === SNACKS ===
INSERT INTO nutrition_category_alias (category_code, alias_name, confidence, match_type) VALUES
('snack_salgado', 'salgadinho', 1.00, 'exact'),
('snack_salgado', 'chips', 1.00, 'exact'),
('snack_salgado', 'doritos', 1.00, 'exact'),
('snack_salgado', 'cheetos', 1.00, 'exact'),
('snack_salgado', 'ruffles', 1.00, 'exact'),
('snack_salgado', 'fandangos', 1.00, 'exact'),
('snack_salgado', 'batata chips', 1.00, 'exact');

-- === CHOCOLATES ===
INSERT INTO nutrition_category_alias (category_code, alias_name, confidence, match_type) VALUES
('chocolate', 'chocolate', 0.90, 'partial'),
('chocolate', 'barra de chocolate', 1.00, 'exact'),
('chocolate', 'chocolate ao leite', 1.00, 'exact'),
('chocolate', 'bombom', 0.95, 'partial'),
('achocolatado_po', 'achocolatado', 1.00, 'exact'),
('achocolatado_po', 'achocolatado em pó', 1.00, 'exact'),
('achocolatado_po', 'chocolate em pó', 1.00, 'exact'),
('achocolatado_po', 'nescau', 1.00, 'exact'),
('achocolatado_po', 'toddy', 1.00, 'exact'),
('achocolatado_po', 'ovomaltine', 1.00, 'exact');

-- === BEBIDAS ===
INSERT INTO nutrition_category_alias (category_code, alias_name, confidence, match_type) VALUES
('refrigerante', 'refrigerante', 1.00, 'exact'),
('refrigerante', 'coca-cola', 0.95, 'partial'),
('refrigerante', 'pepsi', 0.95, 'partial'),
('refrigerante', 'guaraná', 0.95, 'partial'),
('refrigerante_zero', 'refrigerante zero', 1.00, 'exact'),
('refrigerante_zero', 'coca zero', 1.00, 'exact'),
('refrigerante_zero', 'refrigerante diet', 1.00, 'exact'),
('refrigerante_zero', 'pepsi zero', 1.00, 'exact'),
('suco_industrializado', 'suco', 0.85, 'partial'),
('suco_industrializado', 'suco de caixinha', 1.00, 'exact'),
('suco_industrializado', 'suco industrializado', 1.00, 'exact'),
('suco_industrializado', 'néctar', 1.00, 'exact'),
('suco_industrializado', 'suco del valle', 0.95, 'partial'),
('suco_natural', 'suco natural', 1.00, 'exact'),
('suco_natural', 'suco de laranja natural', 1.00, 'exact'),
('suco_natural', 'suco fresco', 1.00, 'exact');

-- === PROTEICOS ===
INSERT INTO nutrition_category_alias (category_code, alias_name, confidence, match_type) VALUES
('whey_protein', 'whey', 1.00, 'exact'),
('whey_protein', 'whey protein', 1.00, 'exact'),
('whey_protein', 'proteína do soro do leite', 1.00, 'exact'),
('barra_proteica', 'barra proteica', 1.00, 'exact'),
('barra_proteica', 'barra de proteína', 1.00, 'exact'),
('barra_proteica', 'protein bar', 1.00, 'exact'),
('frango', 'frango', 1.00, 'exact'),
('frango', 'peito de frango', 1.00, 'exact'),
('frango', 'filé de frango', 1.00, 'exact'),
('ovo', 'ovo', 1.00, 'exact'),
('ovo', 'ovo de galinha', 1.00, 'exact'),
('ovo', 'ovo caipira', 0.95, 'partial');

-- === GORDURAS ===
INSERT INTO nutrition_category_alias (category_code, alias_name, confidence, match_type) VALUES
('oleo_vegetal', 'óleo', 0.90, 'partial'),
('oleo_vegetal', 'óleo de soja', 1.00, 'exact'),
('oleo_vegetal', 'óleo de girassol', 1.00, 'exact'),
('oleo_vegetal', 'óleo de canola', 1.00, 'exact'),
('azeite', 'azeite', 1.00, 'exact'),
('azeite', 'azeite de oliva', 1.00, 'exact'),
('manteiga', 'manteiga', 1.00, 'exact'),
('manteiga', 'manteiga com sal', 1.00, 'exact'),
('manteiga', 'manteiga sem sal', 1.00, 'exact'),
('margarina', 'margarina', 1.00, 'exact'),
('margarina', 'creme vegetal', 1.00, 'exact');

-- =====================================================
-- 3.1. POPULAR TABELA LEGACY DE COMPATIBILIDADE
-- =====================================================

INSERT INTO category_mappings (raw_category_name, normalized_category_code, confidence, is_active)
SELECT
    alias_name,
    category_code,
    confidence,
    is_active
FROM nutrition_category_alias;

INSERT INTO nutrition_category_alias (category_code, alias_name, confidence, match_type, is_active, usage_count)
SELECT
    category_code,
    unaccent(alias_name),
    confidence,
    match_type,
    is_active,
    usage_count
FROM nutrition_category_alias
WHERE alias_name <> unaccent(alias_name)
ON CONFLICT (alias_name_normalized, category_code) DO NOTHING;

INSERT INTO category_mappings (raw_category_name, normalized_category_code, confidence, is_active)
SELECT
    unaccent(alias_name),
    category_code,
    confidence,
    is_active
FROM nutrition_category_alias
WHERE alias_name <> unaccent(alias_name)
ON CONFLICT (raw_category_name, normalized_category_code) DO NOTHING;

-- =====================================================
-- 4. VERIFICAÇÃO E ESTATÍSTICAS
-- =====================================================

-- Estatísticas
SELECT '✅ SEED CONCLUÍDO COM SUCESSO!' as status;
SELECT '' as separator;
SELECT 'ESTATÍSTICAS:' as info;
SELECT CONCAT('- Categorias cadastradas: ', COUNT(*)) as stat FROM nutrition_category;
SELECT CONCAT('- Perfis nutricionais: ', COUNT(*)) as stat FROM nutrition_category_profile;
SELECT CONCAT('- Aliases cadastrados: ', COUNT(*)) as stat FROM nutrition_category_alias;
SELECT CONCAT('- Legacy mappings cadastrados: ', COUNT(*)) as stat FROM category_mappings;
SELECT '' as separator;
SELECT 'Top 10 categorias com mais aliases:' as info;

SELECT 
    c.code,
    c.name,
    COUNT(a.id) as alias_count
FROM nutrition_category c
LEFT JOIN nutrition_category_alias a ON c.code = a.category_code
GROUP BY c.id, c.code, c.name
ORDER BY alias_count DESC
LIMIT 10;
