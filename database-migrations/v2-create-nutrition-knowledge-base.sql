-- =====================================================
-- MODELO OTIMIZADO: BASE DE CONHECIMENTO NUTRICIONAL
-- Sistema de fallback nutricional por categoria
-- Versão: 2.0 (Otimizada)
-- =====================================================

CREATE EXTENSION IF NOT EXISTS unaccent;

-- =====================================================
-- 1. TABELA: CATEGORIAS NUTRICIONAIS
-- =====================================================

DROP TABLE IF EXISTS category_mappings CASCADE;
DROP TABLE IF EXISTS nutrition_category_profile CASCADE;
DROP TABLE IF EXISTS nutrition_category_alias CASCADE;
DROP TABLE IF EXISTS nutrition_category CASCADE;

CREATE TABLE nutrition_category (
    id SERIAL PRIMARY KEY,
    code VARCHAR(100) NOT NULL UNIQUE,
    name VARCHAR(200) NOT NULL,
    description TEXT,
    parent_code VARCHAR(100),
    
    -- Metadata
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    display_order INTEGER DEFAULT 0,
    
    -- Audit
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    -- Constraints
    CONSTRAINT fk_parent_category 
        FOREIGN KEY (parent_code) 
        REFERENCES nutrition_category(code) 
        ON DELETE SET NULL,
    
    CONSTRAINT chk_code_format 
        CHECK (code ~ '^[a-z0-9_]+$')
);

-- Índices para performance
CREATE INDEX idx_nutrition_category_code ON nutrition_category(code);
CREATE INDEX idx_nutrition_category_parent ON nutrition_category(parent_code) WHERE parent_code IS NOT NULL;
CREATE INDEX idx_nutrition_category_active ON nutrition_category(is_active) WHERE is_active = TRUE;
CREATE INDEX idx_nutrition_category_name_search ON nutrition_category USING gin(to_tsvector('portuguese', name));

-- Comentários
COMMENT ON TABLE nutrition_category IS 'Categorias nutricionais normalizadas para classificação de alimentos';
COMMENT ON COLUMN nutrition_category.code IS 'Código único da categoria em snake_case (ex: laticinio_cremoso)';
COMMENT ON COLUMN nutrition_category.parent_code IS 'Código da categoria pai para hierarquia (ex: laticinio -> laticinio_cremoso)';
COMMENT ON COLUMN nutrition_category.display_order IS 'Ordem de exibição em listagens';

-- =====================================================
-- 2. TABELA: ALIASES DE CATEGORIAS (MAPEAMENTOS)
-- =====================================================

CREATE TABLE nutrition_category_alias (
    id SERIAL PRIMARY KEY,
    category_code VARCHAR(100) NOT NULL,
    alias_name VARCHAR(300) NOT NULL,
    alias_name_normalized VARCHAR(300) NOT NULL,
    
    -- Metadata
    confidence DECIMAL(3,2) NOT NULL DEFAULT 1.00,
    match_type VARCHAR(50) DEFAULT 'exact', -- exact, partial, fuzzy
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    usage_count INTEGER DEFAULT 0,
    
    -- Audit
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    -- Constraints
    CONSTRAINT fk_category_alias 
        FOREIGN KEY (category_code) 
        REFERENCES nutrition_category(code) 
        ON DELETE CASCADE,
    
    CONSTRAINT chk_confidence_range 
        CHECK (confidence >= 0.0 AND confidence <= 1.0),
    
    CONSTRAINT uq_alias_name 
        UNIQUE (alias_name_normalized, category_code)
);

-- Índices para busca rápida
CREATE INDEX idx_alias_category_code ON nutrition_category_alias(category_code);
CREATE INDEX idx_alias_name_normalized ON nutrition_category_alias(alias_name_normalized);
CREATE INDEX idx_alias_name_search ON nutrition_category_alias USING gin(to_tsvector('portuguese', alias_name));
CREATE INDEX idx_alias_active_confidence ON nutrition_category_alias(is_active, confidence DESC) WHERE is_active = TRUE;

-- Comentários
COMMENT ON TABLE nutrition_category_alias IS 'Mapeamento de nomes detectados pela IA para categorias normalizadas';
COMMENT ON COLUMN nutrition_category_alias.alias_name IS 'Nome original detectado (ex: "Creme de Queijo Light")';
COMMENT ON COLUMN nutrition_category_alias.alias_name_normalized IS 'Nome normalizado para busca (lowercase, sem acentos)';
COMMENT ON COLUMN nutrition_category_alias.confidence IS 'Confiança do mapeamento (0.0 a 1.0)';
COMMENT ON COLUMN nutrition_category_alias.match_type IS 'Tipo de match: exact, partial, fuzzy';
COMMENT ON COLUMN nutrition_category_alias.usage_count IS 'Contador de uso para machine learning';

-- =====================================================
-- 2.1. TABELA DE COMPATIBILIDADE LEGACY: CATEGORY_MAPPINGS
-- =====================================================

CREATE TABLE category_mappings (
    id SERIAL PRIMARY KEY,
    raw_category_name VARCHAR(300) NOT NULL,
    normalized_category_code VARCHAR(100) NOT NULL,
    confidence DECIMAL(3,2) NOT NULL DEFAULT 1.00,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_category_mappings_category
        FOREIGN KEY (normalized_category_code)
        REFERENCES nutrition_category(code)
        ON DELETE CASCADE,

    CONSTRAINT uq_category_mappings_raw_normalized
        UNIQUE (raw_category_name, normalized_category_code)
);

CREATE INDEX idx_category_mappings_raw ON category_mappings(raw_category_name);
CREATE INDEX idx_category_mappings_normalized ON category_mappings(normalized_category_code);
CREATE INDEX idx_category_mappings_active ON category_mappings(is_active) WHERE is_active = TRUE;

COMMENT ON TABLE category_mappings IS 'Tabela de compatibilidade para o código legado de fallback nutricional';
COMMENT ON COLUMN category_mappings.raw_category_name IS 'Nome bruto detectado pela IA';
COMMENT ON COLUMN category_mappings.normalized_category_code IS 'Código da categoria normalizada';

-- =====================================================
-- 3. TABELA: PERFIS NUTRICIONAIS POR CATEGORIA
-- =====================================================

CREATE TABLE nutrition_category_profile (
    id SERIAL PRIMARY KEY,
    category_code VARCHAR(100) NOT NULL,
    
    -- === VALORES NUTRICIONAIS POR 100g (ou 100ml) ===
    
    -- Calorias
    calories_per_100g DECIMAL(10,2),
    calories_min DECIMAL(10,2),
    calories_max DECIMAL(10,2),
    
    -- Proteína
    protein_per_100g DECIMAL(10,2),
    protein_min DECIMAL(10,2),
    protein_max DECIMAL(10,2),
    
    -- Gordura Total
    fat_per_100g DECIMAL(10,2),
    fat_min DECIMAL(10,2),
    fat_max DECIMAL(10,2),
    
    -- Gordura Saturada
    saturated_fat_per_100g DECIMAL(10,2),
    saturated_fat_min DECIMAL(10,2),
    saturated_fat_max DECIMAL(10,2),
    
    -- Gordura Trans
    trans_fat_per_100g DECIMAL(10,2),
    trans_fat_min DECIMAL(10,2),
    trans_fat_max DECIMAL(10,2),
    
    -- Carboidratos
    carbohydrates_per_100g DECIMAL(10,2),
    carbohydrates_min DECIMAL(10,2),
    carbohydrates_max DECIMAL(10,2),
    
    -- Açúcar
    sugar_per_100g DECIMAL(10,2),
    sugar_min DECIMAL(10,2),
    sugar_max DECIMAL(10,2),
    
    -- Fibra
    fiber_per_100g DECIMAL(10,2),
    fiber_min DECIMAL(10,2),
    fiber_max DECIMAL(10,2),
    
    -- Sódio
    sodium_per_100g DECIMAL(10,2),
    sodium_min DECIMAL(10,2),
    sodium_max DECIMAL(10,2),
    
    -- === METADATA ===
    
    confidence_level DECIMAL(3,2) NOT NULL DEFAULT 0.70,
    data_source VARCHAR(200),
    reference_year INTEGER,
    sample_size INTEGER,
    notes TEXT,
    is_liquid BOOLEAN DEFAULT FALSE,
    
    -- Metadata
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    version INTEGER DEFAULT 1,
    
    -- Audit
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    -- Constraints
    CONSTRAINT fk_category_profile 
        FOREIGN KEY (category_code) 
        REFERENCES nutrition_category(code) 
        ON DELETE CASCADE,
    
    CONSTRAINT chk_confidence_level 
        CHECK (confidence_level >= 0.0 AND confidence_level <= 1.0),
    
    CONSTRAINT chk_calories_positive 
        CHECK (calories_per_100g >= 0),
    
    CONSTRAINT chk_protein_positive 
        CHECK (protein_per_100g >= 0),
    
    CONSTRAINT chk_ranges_valid 
        CHECK (
            (calories_min IS NULL OR calories_max IS NULL OR calories_min <= calories_max) AND
            (protein_min IS NULL OR protein_max IS NULL OR protein_min <= protein_max) AND
            (fat_min IS NULL OR fat_max IS NULL OR fat_min <= fat_max) AND
            (sugar_min IS NULL OR sugar_max IS NULL OR sugar_min <= sugar_max)
        )
);

-- Índices para performance
CREATE INDEX idx_profile_category_code ON nutrition_category_profile(category_code);
CREATE INDEX idx_profile_active ON nutrition_category_profile(is_active) WHERE is_active = TRUE;
CREATE INDEX idx_profile_confidence ON nutrition_category_profile(confidence_level DESC);
CREATE INDEX idx_profile_source ON nutrition_category_profile(data_source);

-- Comentários
COMMENT ON TABLE nutrition_category_profile IS 'Perfis nutricionais típicos por categoria para fallback inteligente';
COMMENT ON COLUMN nutrition_category_profile.calories_per_100g IS 'Valor típico/médio de calorias por 100g';
COMMENT ON COLUMN nutrition_category_profile.confidence_level IS 'Nível de confiança do perfil (0.0 a 1.0)';
COMMENT ON COLUMN nutrition_category_profile.data_source IS 'Fonte dos dados (TACO, IBGE, Anvisa, USDA, etc)';
COMMENT ON COLUMN nutrition_category_profile.is_liquid IS 'TRUE para bebidas (valores por 100ml)';

-- =====================================================
-- 4. FUNÇÕES E TRIGGERS
-- =====================================================

-- Função para atualizar updated_at automaticamente
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Triggers para cada tabela
CREATE TRIGGER update_nutrition_category_updated_at 
    BEFORE UPDATE ON nutrition_category
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_nutrition_category_alias_updated_at 
    BEFORE UPDATE ON nutrition_category_alias
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_category_mappings_updated_at
    BEFORE UPDATE ON category_mappings
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_nutrition_category_profile_updated_at 
    BEFORE UPDATE ON nutrition_category_profile
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- Função para normalizar alias_name automaticamente
CREATE OR REPLACE FUNCTION normalize_alias_name()
RETURNS TRIGGER AS $$
BEGIN
    NEW.alias_name_normalized = LOWER(
        UNACCENT(
            REGEXP_REPLACE(TRIM(NEW.alias_name), '\s+', ' ', 'g')
        )
    );
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Trigger para normalização automática
CREATE TRIGGER normalize_alias_name_trigger
    BEFORE INSERT OR UPDATE ON nutrition_category_alias
    FOR EACH ROW EXECUTE FUNCTION normalize_alias_name();

-- =====================================================
-- 5. VIEWS ÚTEIS
-- =====================================================

-- View: Categorias com contagem de aliases e perfis
CREATE OR REPLACE VIEW v_category_summary AS
SELECT 
    c.id,
    c.code,
    c.name,
    c.description,
    c.parent_code,
    c.is_active,
    COUNT(DISTINCT a.id) as alias_count,
    COUNT(DISTINCT p.id) as profile_count,
    c.created_at
FROM nutrition_category c
LEFT JOIN nutrition_category_alias a ON c.code = a.category_code AND a.is_active = TRUE
LEFT JOIN nutrition_category_profile p ON c.code = p.category_code AND p.is_active = TRUE
WHERE c.is_active = TRUE
GROUP BY c.id, c.code, c.name, c.description, c.parent_code, c.is_active, c.created_at
ORDER BY c.display_order, c.name;

-- View: Perfis com informações da categoria
CREATE OR REPLACE VIEW v_category_profiles_detailed AS
SELECT 
    p.id,
    p.category_code,
    c.name as category_name,
    c.description as category_description,
    p.calories_per_100g,
    p.protein_per_100g,
    p.fat_per_100g,
    p.sugar_per_100g,
    p.sodium_per_100g,
    p.fiber_per_100g,
    p.confidence_level,
    p.data_source,
    p.is_liquid,
    p.notes,
    p.created_at,
    p.updated_at
FROM nutrition_category_profile p
INNER JOIN nutrition_category c ON p.category_code = c.code
WHERE p.is_active = TRUE AND c.is_active = TRUE
ORDER BY c.display_order, c.name;

-- View: Aliases mais usados por categoria
CREATE OR REPLACE VIEW v_popular_aliases AS
SELECT 
    a.category_code,
    c.name as category_name,
    a.alias_name,
    a.confidence,
    a.usage_count,
    a.match_type
FROM nutrition_category_alias a
INNER JOIN nutrition_category c ON a.category_code = c.code
WHERE a.is_active = TRUE AND c.is_active = TRUE
ORDER BY a.usage_count DESC, a.confidence DESC
LIMIT 100;

-- =====================================================
-- 6. FUNÇÕES DE BUSCA
-- =====================================================

-- Função: Buscar categoria por alias (com fuzzy matching)
CREATE OR REPLACE FUNCTION find_category_by_alias(
    search_text VARCHAR(300),
    min_confidence DECIMAL DEFAULT 0.7
)
RETURNS TABLE (
    category_code VARCHAR(100),
    category_name VARCHAR(200),
    matched_alias VARCHAR(300),
    confidence DECIMAL(3,2),
    match_type VARCHAR(50)
) AS $$
BEGIN
    -- Normalizar texto de busca
    search_text := LOWER(UNACCENT(TRIM(search_text)));
    
    -- Busca exata primeiro
    RETURN QUERY
    SELECT 
        a.category_code,
        c.name,
        a.alias_name,
        a.confidence,
        a.match_type
    FROM nutrition_category_alias a
    INNER JOIN nutrition_category c ON a.category_code = c.code
    WHERE a.alias_name_normalized = search_text
      AND a.is_active = TRUE
      AND c.is_active = TRUE
      AND a.confidence >= min_confidence
    ORDER BY a.confidence DESC, a.usage_count DESC
    LIMIT 1;
    
    -- Se não encontrou, busca parcial
    IF NOT FOUND THEN
        RETURN QUERY
        SELECT 
            a.category_code,
            c.name,
            a.alias_name,
            a.confidence,
            a.match_type
        FROM nutrition_category_alias a
        INNER JOIN nutrition_category c ON a.category_code = c.code
        WHERE (
            a.alias_name_normalized LIKE '%' || search_text || '%' OR
            search_text LIKE '%' || a.alias_name_normalized || '%'
        )
        AND a.is_active = TRUE
        AND c.is_active = TRUE
        AND a.confidence >= min_confidence
        ORDER BY 
            LENGTH(a.alias_name_normalized) ASC,
            a.confidence DESC,
            a.usage_count DESC
        LIMIT 5;
    END IF;
END;
$$ LANGUAGE plpgsql;

-- =====================================================
-- FIM DA ESTRUTURA
-- =====================================================

-- Estatísticas
SELECT 'Estrutura do banco criada com sucesso!' as status;
