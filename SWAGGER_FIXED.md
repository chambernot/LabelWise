# ✅ SWAGGER CORRIGIDO - GUIA DE TESTE RÁPIDO

## 🎉 Problema Resolvido!

O erro 500 no Swagger foi corrigido. O problema era que o Swagger não conseguia gerar documentação para endpoints com `IFormFile` e `[FromForm]`.

### 🔧 Correção Aplicada

1. **Criado**: `LabelWise.Api/Swagger/FileUploadOperationFilter.cs`
   - Filtro customizado para suportar file uploads no Swagger
   - Configura corretamente a documentação para multipart/form-data

2. **Atualizado**: `LabelWise.Api/Program.cs`
   - Adicionado: `cfg.OperationFilter<FileUploadOperationFilter>();`
   - Adicionado using: `using LabelWise.Api.Swagger;`

---

## 🚀 Como Testar Agora

### Passo 1: Iniciar PostgreSQL
```powershell
.\start-postgres.bat
```

### Passo 2: Aplicar Migrações (se necessário)
```powershell
cd LabelWise.Api
dotnet ef database update --project ../LabelWise.Infrastructure
```

### Passo 3: Iniciar a API
```powershell
.\run-api.ps1
```

A API iniciará e mostrará as URLs:
```
========================================
LabelWise API is ready!
Swagger UI: https://localhost:7001/swagger
Environment: Development
========================================
Now listening on: https://localhost:XXXX
Now listening on: http://localhost:XXXX
```

**IMPORTANTE**: Use a porta HTTPS mostrada no console (ex: 7319)

### Passo 4: Abrir Swagger
```
https://localhost:{porta-mostrada}/swagger
```

Exemplo: `https://localhost:7319/swagger`

---

## 🧪 Testando o Endpoint no Swagger

### 1. Encontrar o Endpoint
No Swagger UI, procure por:
- **POST /api/products/analyze-image** (endpoint principal)
- **POST /api/pipeline/analyze-image** (endpoint técnico)

### 2. Testar com Arquivo

#### a) Clique em "Try it out"
#### b) Clique em "Choose File" ou "Escolher arquivo"
#### c) Selecione qualquer imagem (.jpg, .png, .jpeg, .webp)
   - **NOTA**: Com MockOCR ativo, não importa qual imagem você enviar
   - O OCR retornará um dos 3 rótulos simulados (cereal, biscoito ou iogurte)

#### d) Clique em "Execute"

### 3. Ver Resultado

Você deve receber uma resposta **200 OK** com JSON completo:

```json
{
  "analysisId": "uuid-gerado",
  "productId": "uuid-gerado",
  "productName": "CEREAL MATINAL INTEGRAL",
  "brand": null,
  "summary": "Produto com perfil nutricional moderado...",
  "shortSummary": "Produto seguro (nota 7.0/10). Pode consumir com tranquilidade.",
  "generalScore": 0.7,
  "personalizedScore": 0.7,
  "classification": "Safe",
  "confidenceLevel": "Alto",
  "alerts": [
    "Contains gluten or gluten-derived ingredients"
  ],
  "recommendations": [
    "Considere produtos com menos açúcar"
  ],
  "extractedIngredients": [
    "farinha de trigo enriquecida com ferro e ácido fólico",
    "açúcar",
    "gordura vegetal",
    "sal",
    "bicarbonato de sódio",
    "bicarbonato de amônio",
    "pirofosfato ácido de sódio",
    "lecitina de soja",
    "aromatizante"
  ],
  "extractedAllergens": [
    "glúten",
    "trigo",
    "soja",
    "leite",
    "centeio",
    "cevada",
    "aveia"
  ],
  "extractedText": "CONTÉM GLÚTEN, CONTÉM DERIVADOS DE TRIGO E SOJA, PODE CONTER LEITE",
  "createdAt": "2024-01-15T14:30:45.123Z"
}
```

---

## 📊 Variações do MockOCR

O MockOCR retorna aleatoriamente 3 tipos de produtos:

### 1. CEREAL MATINAL INTEGRAL
- Contém glúten
- 130kcal por porção
- Ingredientes: farinha de trigo, açúcar, gordura vegetal

### 2. BISCOITO RECHEADO
- Contém glúten, leite
- 150kcal por porção
- Ingredientes: farinha, açúcar, gordura hidrogenada

### 3. IOGURTE NATURAL INTEGRAL
- Contém leite
- NÃO contém glúten
- 115kcal por porção
- Ingredientes: leite integral, açúcar, fermentos lácteos

Cada vez que você executar o endpoint, receberá um destes produtos aleatoriamente.

---

## 🔐 Testando com Autenticação

### 1. Registrar Usuário
No Swagger, encontre **POST /api/auth/register**:
```json
{
  "email": "test@example.com",
  "password": "Test@123",
  "name": "Test User"
}
```

### 2. Fazer Login
No Swagger, encontre **POST /api/auth/login**:
```json
{
  "email": "test@example.com",
  "password": "Test@123"
}
```

**Copie o token retornado**.

### 3. Autorizar no Swagger
1. Clique no botão **"Authorize"** no topo da página (cadeado verde)
2. Cole o token (sem "Bearer", apenas o token)
3. Clique em "Authorize"
4. Clique em "Close"

### 4. Criar Perfil
Encontre **POST /api/profile**:
```json
{
  "lactoseIntolerance": true,
  "glutenFree": false,
  "diabetes": false,
  "sodiumControl": true,
  "goal": "WeightLoss",
  "preferredExplanationLevel": "Detailed"
}
```

### 5. Analisar Imagem Autenticado
Agora quando executar **POST /api/products/analyze-image**, a análise será vinculada ao seu usuário e o `personalizedScore` será calculado baseado no seu perfil!

---

## 🗄️ Validar Persistência no Banco

### Via pgAdmin ou psql:
```sql
-- Conectar
psql -h localhost -U postgres -d labelwise_db

-- Ver produtos criados
SELECT id, name, brand, created_at 
FROM products 
ORDER BY created_at DESC 
LIMIT 5;

-- Ver análises
SELECT id, product_id, user_id, classification, confidence 
FROM product_analyses 
ORDER BY analyzed_at DESC 
LIMIT 5;

-- Ver ingredientes do último produto
SELECT name, "order" 
FROM product_ingredients 
WHERE product_id = (SELECT id FROM products ORDER BY created_at DESC LIMIT 1)
ORDER BY "order";

-- Ver alérgenos do último produto
SELECT allergen_name 
FROM product_allergens 
WHERE product_id = (SELECT id FROM products ORDER BY created_at DESC LIMIT 1);
```

---

## ❓ Troubleshooting

### Swagger ainda mostra erro 500
**Solução**: 
1. Parar a API completamente
2. Fazer rebuild: `dotnet build --no-incremental`
3. Iniciar novamente: `.\run-api.ps1`

### Arquivo não foi aceito
**Possíveis causas**:
- Extensão não suportada (aceita: .jpg, .jpeg, .png, .webp)
- Arquivo maior que 5MB
- Campo não foi preenchido

### Dados estão iguais sempre
**Isso é esperado!** O MockOCR retorna 1 dos 3 produtos simulados aleatoriamente. Para processar imagens reais, configure Tesseract ou Azure Computer Vision (consulte `OCR_PROVIDERS_CONFIGURATION.md`).

### PostgreSQL não está rodando
```powershell
# Verificar
netstat -an | findstr 5432

# Iniciar
.\start-postgres.bat
```

### Erro ao salvar no banco
- Verificar se as migrações foram aplicadas
- Verificar connection string em appsettings.json

---

## 🎯 Próximos Passos

1. ✅ Swagger funcionando corretamente
2. ✅ Endpoints documentados
3. ✅ File upload suportado
4. ⏳ Testar todos os endpoints
5. 🔄 Trocar para OCR real (Tesseract/Azure) quando necessário

---

## 📚 Documentação Relacionada

- `QUICK_START_TESTING.md` - Testes detalhados com cURL/PowerShell
- `OCR_PROVIDERS_CONFIGURATION.md` - Como trocar de MockOCR para OCR real
- `IMPLEMENTATION_COMPLETE.md` - Documentação técnica completa
- `VALIDATION_CHECKLIST.md` - Checklist completo de validação

---

## ✨ Status

| Componente | Status |
|------------|--------|
| Build | ✅ Sucesso |
| Swagger | ✅ Funcionando |
| File Upload | ✅ Suportado |
| Endpoints | ✅ Documentados |
| OCR | ✅ MockOCR ativo |
| Persistência | ✅ PostgreSQL |
| Autenticação | ✅ JWT |

---

**SWAGGER ESTÁ PRONTO PARA USO!** 🚀

Abra `https://localhost:{porta}/swagger` e comece a testar!

---

**Criado em**: 2024  
**Status**: ✅ Problema Resolvido
