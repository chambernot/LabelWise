# ⚡ Quick Start: Motor de Fallback Nutricional (PostgreSQL)

## 🎯 O Que É

Motor de fallback nutricional configurável via **PostgreSQL**, substituindo estimativas genéricas hardcoded por perfis nutricionais calibrados por categoria.

---

## 🚀 Como Aplicar (5 Minutos)

### 1. Aplicar Migration

```powershell
.\apply-nutrition-fallback-migration.ps1
```

**O que faz:**
- ✅ Cria 3 tabelas no PostgreSQL
- ✅ Insere 30+ categorias nutricionais
- ✅ Cadastra perfis com dados de TACO/IBGE
- ✅ Popula 80+ mapeamentos de aliases

---

### 2. Reiniciar API

```powershell
dotnet run --project LabelWise.Api
```

**O que acontece:**
- ✅ Repositórios registrados no DI
- ✅ Serviço de fallback disponível
- ✅ Integração automática no fluxo

---

### 3. Testar

```powershell
# Teste com imagem SEM tabela nutricional
.\test-nutrition-endpoint.ps1 -ImagePath "test-images\achocolatado-frente.jpg"

# Teste com imagem COM tabela nutricional
.\test-nutrition-endpoint.ps1 -ImagePath "test-images\achocolatado-verso.jpg"
```

**Resultado Esperado:**

**SEM tabela** → Usa perfil da categoria `achocolatado_po`:
```json
{
  "estimatedNutritionProfile": {
    "caloriesPer100g": 385,
    "sugarPer100g": 77,
    "proteinPer100g": 4,
    "basis": "Valores estimados por perfil da categoria Achocolatado em Pó (fonte: TACO/Anvisa) com alto grau de confiança."
  }
}
```

**COM tabela** → Usa dados reais:
```json
{
  "estimatedNutritionProfile": {
    "caloriesPer100g": 390,  // Real
    "sugarPer100g": 75.5,     // Real
    "proteinPer100g": 4.2,    // Real
    "basis": "Dados extraídos da tabela nutricional presente no rótulo"
  }
}
```

---

## 📊 Categorias Cadastradas

### Laticínios:
- `laticinio_cremoso` (requeijão, cream cheese)
- `laticinio_cremoso_light`
- `queijo_duro` (parmesão, mussarela)
- `queijo_ralado`
- `iogurte_natural`
- `iogurte_adocicado`
- `sobremesa_lactea` (danoninho, danette)

### Carboidratos:
- `arroz_branco`
- `arroz_integral`
- `macarrao`
- `pao`
- `cereal`
- `cereal_acucarado`

### Ultraprocessados:
- `biscoito_recheado`
- `biscoito_simples`
- `snack_salgado`
- `chocolate`
- `achocolatado_po`

### Bebidas:
- `refrigerante`
- `refrigerante_zero`
- `suco_industrializado`
- `bebida_acucarada`

---

## 🔍 Como Funciona

### 1. Detecção de Categoria
```
API detecta "Requeijão Cremoso Light"
    ↓
Busca em category_mappings
    ↓
Mapeia para: laticinio_cremoso_light
```

### 2. Busca de Perfil
```
Busca perfil em category_nutrition_profiles
WHERE category_code = 'laticinio_cremoso_light'
    ↓
Retorna: 
- Calorias: 140 kcal/100g
- Proteína: 10g/100g
- Gordura: 8g/100g
- Açúcar: 2g/100g
- Sódio: 400mg/100g
- Confiança: 80%
```

### 3. Aplicação Inteligente
```
SE tabela nutricional está presente:
    - Usar dados reais
    - Completar campos faltantes com perfil
SENÃO:
    - Usar perfil completo da categoria
```

---

## 🛠️ Adicionar Nova Categoria

### 1. Inserir Categoria

```sql
INSERT INTO nutrition_categories (code, name, description) VALUES
('minha_categoria', 'Minha Categoria', 'Descrição');
```

### 2. Inserir Perfil

```sql
INSERT INTO category_nutrition_profiles (
    category_code, 
    calories_per_100g, protein_per_100g, fat_per_100g, 
    sugar_per_100g, sodium_per_100g, fiber_per_100g,
    confidence_level, data_source
) VALUES (
    'minha_categoria',
    250, 10, 15, 5, 300, 3,
    0.85, 'TACO'
);
```

### 3. Inserir Mapeamentos

```sql
INSERT INTO category_mappings (raw_category_name, normalized_category_code) VALUES
('produto A', 'minha_categoria'),
('produto B', 'minha_categoria');
```

### 4. Reiniciar API

```powershell
dotnet run --project LabelWise.Api
```

**Pronto!** A nova categoria já está disponível.

---

## 🐛 Troubleshooting

### Erro: "Could not resolve category"
```sql
-- Verificar se mapeamento existe
SELECT * FROM category_mappings 
WHERE raw_category_name ILIKE '%produto%';

-- Se não existir, adicionar:
INSERT INTO category_mappings (raw_category_name, normalized_category_code) 
VALUES ('meu produto', 'categoria_correta');
```

### Erro: "No nutrition profile found"
```sql
-- Verificar se perfil existe
SELECT * FROM category_nutrition_profiles 
WHERE category_code = 'minha_categoria';

-- Se não existir, adicionar perfil
```

### Dados Inconsistentes
```sql
-- Verificar ranges do perfil
SELECT category_code, 
       sugar_per_100g, sugar_min, sugar_max
FROM category_nutrition_profiles
WHERE category_code = 'minha_categoria';

-- Se valores estiverem fora do range, ajustar:
UPDATE category_nutrition_profiles
SET sugar_min = 0, sugar_max = 10
WHERE category_code = 'minha_categoria';
```

---

## 📚 Documentação Completa

Para detalhes técnicos completos, veja:
- `INTELLIGENT_FALLBACK_DOCUMENTATION.md` - Arquitetura completa
- `database-migrations/` - Scripts SQL

---

## ✅ Checklist de Validação

- [ ] Migration aplicada com sucesso
- [ ] Seed inseriu 30+ categorias
- [ ] API reiniciada sem erros
- [ ] Teste SEM tabela → usa perfil da categoria
- [ ] Teste COM tabela → usa dados reais
- [ ] Basis indica fonte correta dos dados
- [ ] Confiança reflete qualidade dos dados

---

**Tempo estimado:** 5 minutos  
**Dificuldade:** ⭐ Fácil  
**Status:** ✅ Pronto para usar
