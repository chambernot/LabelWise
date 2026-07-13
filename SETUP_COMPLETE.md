# 🎉 SETUP COMPLETO - LABELWISE API

## ✅ Status Atual

| Componente | Status |
|------------|--------|
| PostgreSQL | ✅ Rodando |
| Database | ✅ Criado (labelwise_db) |
| Migrations | ✅ Aplicadas |
| Tabelas | ✅ 11 tabelas criadas |
| Compilação | ✅ Bem-sucedida |

## 📊 Tabelas Criadas

1. `__EFMigrationsHistory` - Controle de migrations
2. `users` - Usuários do sistema
3. `user_profiles` - Perfis dos usuários
4. `products` - Produtos analisados
5. `product_labels` - Etiquetas dos produtos
6. `product_ingredients` - Ingredientes dos produtos
7. `product_allergens` - Alérgenos dos produtos
8. `nutritional_infos` - Informações nutricionais
9. `product_analyses` - Análises realizadas
10. `analysis_alerts` - Alertas gerados
11. `analysis_recommendations` - Recomendações

## 🚀 Como Iniciar a API

### Opção 1: Script PowerShell
```powershell
.\run-api.ps1
```

### Opção 2: Comando direto
```powershell
dotnet run --project LabelWise.Api
```

## 📚 Endpoints Disponíveis

### Swagger UI
- https://localhost:7001/swagger
- http://localhost:5000/swagger

### Autenticação (`/api/auth`)
- `POST /api/auth/register` - Registrar usuário
- `POST /api/auth/login` - Fazer login

### Perfil (`/api/profile`)
- `GET /api/profile` - Buscar perfil (requer autenticação)
- `PUT /api/profile` - Atualizar perfil (requer autenticação)

### Análise de Produto (`/api/product-analysis`)
- `POST /api/product-analysis/analyze` - Analisar produto manualmente
- `POST /api/product-analysis/pipeline` - Pipeline completo (upload + OCR + análise)

### Histórico (`/api/history`)
- `GET /api/history` - Buscar histórico de análises
- `GET /api/history/{id}` - Detalhes de uma análise específica

## 🧪 Testar a API

### 1. Usando PowerShell
```powershell
# Executar script de testes automatizados
.\test-api-endpoints.ps1
```

### 2. Usando cURL
```bash
# Registrar usuário
curl -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"teste@example.com","password":"Senha@123","name":"Usuario Teste"}'

# Login
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"teste@example.com","password":"Senha@123"}'
```

### 3. Usando Postman/Insomnia
Importe a coleção de endpoints do Swagger

## 🗄️ Conectar ao Banco de Dados

### Via Docker
```powershell
docker exec -it labelwise-postgres psql -U postgres -d labelwise_db
```

### Via pgAdmin / DBeaver
```
Host: localhost
Port: 5432
Database: labelwise_db
Username: postgres
Password: postgres
```

## 🔧 Comandos Úteis

### Reiniciar PostgreSQL
```powershell
docker compose restart
```

### Ver logs do PostgreSQL
```powershell
docker logs labelwise-postgres
```

### Parar PostgreSQL
```powershell
docker compose down
```

### Recriar database (CUIDADO: apaga todos os dados)
```powershell
docker exec labelwise-postgres psql -U postgres -c "DROP DATABASE labelwise_db;"
docker exec labelwise-postgres psql -U postgres -c "CREATE DATABASE labelwise_db OWNER postgres;"
dotnet ef database update --project LabelWise.Infrastructure --startup-project LabelWise.Api
```

### Adicionar nova migration
```powershell
dotnet ef migrations add NomeDaMigration --project LabelWise.Infrastructure --startup-project LabelWise.Api
dotnet ef database update --project LabelWise.Infrastructure --startup-project LabelWise.Api
```

## 📝 Próximos Passos

1. ✅ Iniciar a API
2. ⏭️ Testar endpoints de autenticação
3. ⏭️ Testar pipeline de análise de produtos
4. ⏭️ Integrar com frontend
5. ⏭️ Configurar Azure Computer Vision (OCR real)
6. ⏭️ Configurar Azure OpenAI (análise com IA)

## 🎯 Testes Recomendados

### Teste 1: Registrar e Fazer Login
```powershell
$register = @{
    email = "teste@labelwise.com"
    password = "Senha@123"
    name = "Usuario Teste"
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/register" -Method POST -ContentType "application/json" -Body $register

$token = $response.token
Write-Host "Token: $token"
```

### Teste 2: Atualizar Perfil
```powershell
$headers = @{ "Authorization" = "Bearer $token" }

$profile = @{
    name = "Usuario Atualizado"
    age = 30
    sex = "Male"
    activityLevel = "Moderate"
    dietaryGoal = "WeightLoss"
    healthGoals = @("WeightLoss")
    restrictions = @("Lactose")
    allergies = @("Milk")
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5000/api/profile" -Method PUT -Headers $headers -ContentType "application/json" -Body $profile
```

## ❓ Troubleshooting

### API não inicia
```powershell
# Verificar logs
dotnet run --project LabelWise.Api --verbosity detailed
```

### Erro de conexão com PostgreSQL
```powershell
# Verificar se PostgreSQL está rodando
docker ps

# Ver logs do PostgreSQL
docker logs labelwise-postgres
```

### Porta já em uso
```powershell
# Verificar o que está usando a porta 5000
netstat -ano | findstr :5000

# Ou mudar a porta em launchSettings.json
```

## 📞 Suporte

- Documentação: Swagger UI
- Issues: Criar issue no repositório
- Stack Overflow: Tag `labelwise`

---

**✨ Tudo pronto! Inicie a API com: `.\run-api.ps1`**
