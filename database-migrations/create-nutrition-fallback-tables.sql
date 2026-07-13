-- =====================================================
-- MOTOR DE FALLBACK NUTRICIONAL POR CATEGORIA
-- Sistema de perfis nutricionais configurável via DB
-- =====================================================

-- Tabela: Categorias Nutricionais Normalizadas
CREATE TABLE nutrition_categories (
    id SERIAL PRIMARY KEY,
    code VARCHAR(100) NOT NULL UNIQUE,
    name VARCHAR(200) NOT NULL,
    description TEXT,
    parent_code VARCHAR(100),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Índices para performance
CREATE INDEX idx_nutrition_categories_code ON nutrition_categories(code);
CREATE INDEX idx_nutrition_categories_parent ON nutrition_categories(parent_code);
CREATE INDEX idx_nutrition_categories_active ON nutrition_categories(is_active);

-- Comentários
COMMENT ON TABLE nutrition_categories IS 'Categorias nutricionais normalizadas para classificação de alimentos';
COMMENT ON COLUMN nutrition_categories.code IS 'Código único da categoria (ex: laticinio_cremoso)';
COMMENT ON COLUMN nutrition_categories.parent_code IS 'Código da categoria pai para hierarquia (ex: laticinio -> laticinio_cremoso)';

-- =====================================================

-- Tabela: Perfis Nutricionais por Categoria
CREATE TABLE category_nutrition_profiles (
    id SERIAL PRIMARY KEY,
    category_code VARCHAR(100) NOT NULL,
    
    -- Valores nutricionais por 100g (ou 100ml para líquidos)
    calories_per_100g DECIMAL(10,2),
    calories_min DECIMAL(10,2),
    calories_max DECIMAL(10,2),
    
    protein_per_100g DECIMAL(10,2),
    protein_min DECIMAL(10,2),
    protein_max DECIMAL(10,2),
    
    fat_per_100g DECIMAL(10,2),
    fat_min DECIMAL(10,2),
    fat_max DECIMAL(10,2),
    
    carbohydrates_per_100g DECIMAL(10,2),
    carbohydrates_min DECIMAL(10,2),
    carbohydrates_max DECIMAL(10,2),
    
    sugar_per_100g DECIMAL(10,2),
    sugar_min DECIMAL(10,2),
    sugar_max DECIMAL(10,2),
    
    fiber_per_100g DECIMAL(10,2),
    fiber_min DECIMAL(10,2),
    fiber_max DECIMAL(10,2),
    
    sodium_per_100g DECIMAL(10,2),
    sodium_min DECIMAL(10,2),
    sodium_max DECIMAL(10,2),
    
    -- Metadados
    confidence_level DECIMAL(3,2) NOT NULL DEFAULT 0.70,
    data_source VARCHAR(200),
    notes TEXT,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    -- Constraint
    FOREIGN KEY (category_code) REFERENCES nutrition_categories(code) ON DELETE CASCADE
);

-- Índices para performance
CREATE INDEX idx_category_profiles_code ON category_nutrition_profiles(category_code);
CREATE INDEX idx_category_profiles_active ON category_nutrition_profiles(is_active);

-- Comentários
COMMENT ON TABLE category_nutrition_profiles IS 'Perfis nutricionais típicos por categoria para fallback inteligente';
COMMENT ON COLUMN category_nutrition_profiles.calories_per_100g IS 'Valor típico/médio de calorias';
COMMENT ON COLUMN category_nutrition_profiles.calories_min IS 'Valor mínimo esperado';
COMMENT ON COLUMN category_nutrition_profiles.calories_max IS 'Valor máximo esperado';
COMMENT ON COLUMN category_nutrition_profiles.confidence_level IS 'Nível de confiança do perfil (0.0 a 1.0)';
COMMENT ON COLUMN category_nutrition_profiles.data_source IS 'Fonte dos dados (TACO, IBGE, Anvisa, etc)';

-- =====================================================

-- Tabela: Mapeamento de Categorias (Aliases)
CREATE TABLE category_mappings (
    id SERIAL PRIMARY KEY,
    raw_category_name VARCHAR(300) NOT NULL,
    normalized_category_code VARCHAR(100) NOT NULL,
    confidence DECIMAL(3,2) NOT NULL DEFAULT 1.00,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    -- Constraint
    FOREIGN KEY (normalized_category_code) REFERENCES nutrition_categories(code) ON DELETE CASCADE,
    UNIQUE(raw_category_name, normalized_category_code)
);

-- Índices para performance
CREATE INDEX idx_category_mappings_raw ON category_mappings(raw_category_name);
CREATE INDEX idx_category_mappings_normalized ON category_mappings(normalized_category_code);
CREATE INDEX idx_category_mappings_active ON category_mappings(is_active);

-- Comentários
COMMENT ON TABLE category_mappings IS 'Mapeamento de nomes detectados pela IA para categorias normalizadas';
COMMENT ON COLUMN category_mappings.raw_category_name IS 'Nome detectado pela IA (ex: "creme de queijo light")';
COMMENT ON COLUMN category_mappings.normalized_category_code IS 'Código da categoria normalizada correspondente';
COMMENT ON COLUMN category_mappings.confidence IS 'Confiança do mapeamento (0.0 a 1.0)';

-- =====================================================

-- Tabela: Histórico de Uso do Fallback (Opcional - para análise)
CREATE TABLE nutrition_fallback_usage_log (
    id SERIAL PRIMARY KEY,
    user_id INTEGER,
    detected_category VARCHAR(300),
    normalized_category_code VARCHAR(100),
    analysis_mode VARCHAR(50),
    fields_used_fallback JSONB,
    confidence_level DECIMAL(3,2),
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    -- Constraint
    FOREIGN KEY (normalized_category_code) REFERENCES nutrition_categories(code) ON DELETE SET NULL
);

-- Índices para análise
CREATE INDEX idx_fallback_log_category ON nutrition_fallback_usage_log(normalized_category_code);
CREATE INDEX idx_fallback_log_user ON nutrition_fallback_usage_log(user_id);
CREATE INDEX idx_fallback_log_date ON nutrition_fallback_usage_log(created_at);

-- Comentários
COMMENT ON TABLE nutrition_fallback_usage_log IS 'Log de uso do fallback para análise e melhoria contínua';
COMMENT ON COLUMN nutrition_fallback_usage_log.fields_used_fallback IS 'JSON com campos que usaram fallback';

-- =====================================================

-- Função para atualizar updated_at automaticamente
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Triggers para atualização automática
CREATE TRIGGER update_nutrition_categories_updated_at BEFORE UPDATE ON nutrition_categories
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_category_profiles_updated_at BEFORE UPDATE ON category_nutrition_profiles
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_category_mappings_updated_at BEFORE UPDATE ON category_mappings
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- =====================================================
-- FIM DA ESTRUTURA
-- =====================================================
