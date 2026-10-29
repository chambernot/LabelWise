# Contexto do projeto para o Modo GLP-1

Reconhecimento feito em 29/10/2026, no commit `0b1eff1` (branch `master`). Nenhum arquivo de código foi alterado.

O repositório se chama **LabelWise** e começou como um analisador de rótulos por OCR. A maior parte das pastas, documentos `.md` da raiz e testes ainda é dessa época. O produto ativo hoje é o **Nutrição Fácil**: assistente nutricional no WhatsApp (B2C e B2B) com um painel web para nutricionistas e um painel do dono. Este documento cobre só o Nutrição Fácil.

---

## 1. Stack

| Item | Detalhe |
|---|---|
| Linguagem / runtime | C# / .NET 10 (`net10.0` em todos os projetos) |
| Framework | ASP.NET Core Web API (controllers), `LabelWise.Api/Program.cs` |
| Arquitetura | Camadas: `LabelWise.Api` → `LabelWise.Application` → `LabelWise.Domain`, `LabelWise.Infrastructure`, `LabelWise.Shared` |
| Banco de dados | **MongoDB** (Atlas). Driver `MongoDB.Driver` 3.1.0 |
| ORM | **Nenhum.** Acesso direto pelo driver: entidades tipadas (`MealLog`, `DailyNutritionGoal`, `Nutritionist`…) e muito `BsonDocument` cru nos controllers |
| Conexão | `MongoDbSettings:ConnectionString` e `MongoDbSettings:DatabaseName`, registrados em `LabelWise.Infrastructure/Extensions/ServiceCollectionExtensions.cs` (`IMongoDatabase` singleton) |
| IA | Google Gemini pelo endpoint compatível com OpenAI (`https://generativelanguage.googleapis.com/v1beta/openai/chat/completions`), chamado com `HttpClient`. Modelo na configuração `Model` (hoje `gemini-3.1-flash-lite`) |
| Frontend | Páginas estáticas em `LabelWise.Api/wwwroot` com Tailwind (CDN) + Alpine.js (+ Chart.js no portal) |
| Deploy | Docker (`Dockerfile` na raiz) no Render. **Cada push na `master` publica em produção** (`https://labelwise-1.onrender.com`) |
| Legado sem uso no Nutrição Fácil | `docker-compose.yml` (Postgres), pasta `database-migrations`, OCR (Tesseract/Azure), OpenCvSharp |

### Rodar local
```powershell
dotnet build LabelWise.sln
dotnet run --project LabelWise.Api/LabelWise.Api.csproj --launch-profile LabelWise.Api
# Swagger: https://localhost:7319/swagger  (http://localhost:5319)
```
- O `Program.cs` exige `GeminiApiKey` e `Jwt:Key` configurados. O `NutritionAgentService` também exige `GeminiApiKey` **não vazia**: sem ela, o `WhatsAppController` nem é criado (HTTP 500). Para testar sem IA real, rode com `$env:GeminiApiKey='qualquer-coisa'`.
- Segredos esperados por variável de ambiente: `AdminSecret`, `CronSecret`, `NutritionistDefaultKey`, `MetaWhatsApp__AccessToken`, `MetaWhatsApp__PhoneNumberId`, `MetaWhatsApp__VerifyToken`, `InfinitePay__ApiKey`, `InfinitePay__WebhookSecret`, `MetaWhatsApp__TemplateLembreteRegistro` (opcional).
- ⚠️ **O `appsettings.json` local aponta para o mesmo banco de produção** (`hcanutricao_dev`, confirmado nos logs do Render). Qualquer teste local grava em dados reais.

### Rodar os testes
```powershell
dotnet test LabelWise.sln                                                  # roda só LabelWise.Tests
dotnet test LabelWise.Application.Tests/LabelWise.Application.Tests.csproj # fora da .sln; rodar à parte
```
- xUnit. `LabelWise.Tests` tem 56 testes, com 1 falha antiga em `IngredientAnalysisSemanticTests` (OCR de rótulo).
- **Não existe nenhum teste do bot do WhatsApp, do `NutritionService` nem do `NutritionAgentService`.** Todos os testes são do analisador de rótulos legado.

---

## 2. Integração com WhatsApp

| Item | Onde |
|---|---|
| Provedor | **WhatsApp Cloud API da Meta** (Graph API), sem biblioteca: `HttpClient` puro |
| Envio | `LabelWise.Infrastructure/Services/WhatsAppSenderService.cs`: `SendTextMessageAsync` (texto livre, Graph v25.0), `SendTemplateMessageAsync` e `SendTemplateMessageWithResultAsync` (templates, Graph v20.0, idioma fixo `pt_BR`, só variáveis no corpo) |
| Download de mídia | `LabelWise.Infrastructure/Services/MetaMediaService.cs`: `DownloadMediaAsBase64Async` (imagem) e `DownloadMediaAsBytesAsync` (áudio) |
| Webhook | `LabelWise.Api/Controllers/WhatsAppController.cs`: `GET /api/whatsapp/webhook` (`VerifyWebhook`) e **`POST /api/whatsapp/webhook` (`ReceiveMessage`)** |
| Payload | Classes aninhadas no fim do controller: `MetaWebhookPayload`, `MetaMessage`, `MetaStatus` (avisos de entrega)… |

### Roteamento de uma mensagem (`ReceiveMessage`)
Uma cadeia de `if` em ordem fixa, sem roteador nem classificador de intenção. **O primeiro bloco que casa responde e retorna.** O texto é normalizado em `textoLimpoCmd` (sem pontuação, minúsculas).

1. `RegistrarStatusDeEntregaAsync`: avisos de entrega (sent/delivered/read/failed) dos templates enviados pelo painel → `AdminMessageLog`.
2. Extrai `senderPhone` e o tipo da mensagem. `UserActivityTracker.RegistrarMensagemRecebidaAsync` grava a atividade (`UserActivity`, `UserActivityDays`).
3. **Classificação do usuário:** `isB2bPatient` (`Nutrition_Patients.ProfessionalId` ≠ `b2c_autonomous_user`), `isPremium` e `isPartner` (`B2C_Trial_Users`), `isTrialLimitedUser`.
4. Mídia: antes de baixar, verifica se o teste expirou e o limite diário (`TrialExpirado`, `LimiteDiarioTrialAtingido`).
   - Áudio: `TranscreverAudioComGeminiAsync`.
   - Imagem: vira base64, com o texto fixo "Analise esta imagem nutricional."
   - Botão / interactive: usa o título do botão como texto.
5. **Comandos fixos:** `resumo|relatorio|progresso|painel` (`ObterResumoFormatadoWhatsappAsync`) · `quero ver a novidade` · `agora não` · `assinar|pagar|plano|assinatura` (link InfinitePay) · `já paguei`.
6. **Bloco B2C não pago** (`!isB2bPatient && (!isPremium || isPartner)`):
   - boas-vindas do parceiro;
   - criação do teste + boas-vindas;
   - teste expirado (`EnviarMensagemTrialExpiradoAsync`);
   - `meta|perfil|configurar|ajustar` (refaz o onboarding);
   - máquina de estados do onboarding (seção 4).
7. `substituir …` / `trocar …` → `INutritionAgentService.GenerateSmartSubstitutionAsync`.
8. Remoção: `remover`/`apagar`… (lista as refeições); `remover N`, **ou qualquer número isolado** (apaga a refeição N).
9. `minha dieta|cardapio|menu|dieta` → link `paciente.html?token=…` (`PatientAccessTokens.CriarAsync`).
10. Saudações (`oi`, `bom dia`…).
11. Água: volume sem dizer a bebida → pergunta "O que você bebeu?" (`EhRegistroSoDeVolume`); "água"/"h2o" → `WaterLogs`.
12. Peso: regex com "kg" → `WeightLogs`.
13. Treino: palavras-chave → `EstimarGastoCaloricoComGeminiAsync` → `MealLog` com `MealType = "Treino"` e calorias negativas.
14. Esclarecimento pendente (`ObterClarificacaoPendenteAsync`, validade de 15 min) ou modo correção (palavras "esqueci", "tira", "corrigir"…).
15. Barreira anti-custo: `EhIntencaoRefeicaoOuComida`. Se o texto não parecer comida, responde com orientação.
16. Reserva do limite do teste (`ReserveTrialInteractionOrNotifyAsync`, 4 análises de IA por dia) → **registro de refeição** (seção 3).

### Onde as respostas são montadas
- Refeição: `FormatarRespostaParaWhatsApp(MealAnalysisResponseDto, DailyStatusResponseDto?)`.
- Painel "resumo": `ObterResumoFormatadoWhatsappAsync` + `GerarBarraProgresso`.
- Todo o resto: strings fixas dentro dos próprios blocos de `ReceiveMessage` (onboarding, água, peso, treino, pagamento, erros).

---

## 3. Fluxo de registro de refeição

```
WhatsAppController.ReceiveMessage
 ├─ imagem → MetaMediaService.DownloadMediaAsBase64Async
 ├─ áudio  → MetaMediaService.DownloadMediaAsBytesAsync → TranscreverAudioComGeminiAsync (texto)
 ├─ (esclarecimento pendente ou modo correção reescrevem o texto para a IA)
 ├─ new ParseMealRequestDto(UserId, TextInput, Base64Image, LocalTime, ClinicalProtocol)
 ▼
NutritionService.ProcessMealEntryAsync            (LabelWise.Application/Services/NutritionService.cs)
 ├─ INutritionRepository.ObterUltimasMensagensAsync(6)   → histórico das últimas 2 h (sem a mensagem atual)
 ├─ ObterPacientePorIdAsync / ObterMetaDiariaAsync / ObterRefeicoesDoDiaAsync
 ├─ monta "contextoPaciente" (objetivo, restrições, aversões, protocolo, meta e consumo do dia)
 ├─ INutritionAgentService.ExtractMealDataAsync(request, chatHistory)
 │    (LabelWise.Infrastructure/AI/NutritionAgentService.cs)
 │    ├─ BuildRequestBodyWithHistory → SystemPrompt + histórico + texto + protocolo + UserPromptInstructions (+ image_url)
 │    ├─ CallGeminiAsync (timeout 50 s)
 │    └─ ParseMealResponse → MealAnalysisResponseDto
 ├─ isAdvice ou requiresUserClarification → retorna sem gravar
 ├─ total = soma dos itens
 └─ INutritionRepository.InserirMealLogAsync(MealLog) → coleção Nutrition_MealLogs
 ▼
WhatsAppController
 ├─ grava a mensagem do usuário em WhatsAppChatHistory (depois da análise)
 ├─ esclarecimento → SalvarClarificacaoPendenteAsync (Nutrition_PendingClarifications)
 ├─ registro → NutritionService.GetDailyStatusAndSuggestionAsync (gera 3 sugestões com IA)
 └─ FormatarRespostaParaWhatsApp → SendTextMessageAsync
```

- **`MealLog`** (`LabelWise.Domain/Entities/Nutrition/MealLog.cs`) guarda só totais: `UserId`, `MealType`, `DishName`, `Calories`, `ProteinG`, `CarbsG`, `FatG`, `LoggedAt`. **Os itens da refeição não são gravados**, e não há fibra, açúcar, sódio nem volume.
- **Treinos** usam o mesmo `MealLog`, com calorias negativas (`MealLog.TipoTreino`, `IsTreino`).
- **Dia** sempre calculado em UTC-3 fixo (`AddHours(-3)`) em vários pontos.

---

## 4. Cadastro B2C, perfil e metas

### Onboarding (máquina de estados em `ReceiveMessage`, campo `B2C_Trial_Users.OnboardingStep`)
| Passo | Estado | Pergunta | Onde grava |
|---|---|---|---|
| 1 | `WaitingForGoal` | Objetivo: 1 Emagrecimento, 2 Hipertrofia, 3 Manutenção | `TempGoal` |
| 2 | `WaitingForProfile` | "Peso, altura, idade, sexo e rotina de atividade numa só mensagem" | O texto vai **só** para `CalculateNutritionalGoalsAsync`. Grava `TempCalories`, `TempProtein`, `TempCarbs`, `TempFat`, `TempWater` |
| 3 | `WaitingForRestrictions` | Alergias / intolerâncias / restrições médicas | `TempRestrictions` (texto livre) |
| 4 | `WaitingForAversions` | Aversões alimentares | Finaliza (abaixo) |

Na conclusão:
- `Nutrition_Patients` (**ReplaceOne**: substitui o documento inteiro) = `{ _id: telefone, ProfessionalId: "b2c_autonomous_user", MainGoal, MedicalRestrictions, FoodAversions }`;
- `DailyGoals` = `{ UserId, NutritionistId: "b2c_autonomous_user", TargetDate, TargetCalories, TargetProteinG, TargetCarbsG, TargetFatG, TargetWaterMl, DietaryRestrictions, FavoriteFoods (= aversões!), PrescribedMealPlan }`;
- `ProfileConfigured = true`.

### Onde ficam os dados
| Dado | Onde | Observação |
|---|---|---|
| Objetivo | `Nutrition_Patients.MainGoal` | Texto: "Emagrecimento"/"Hipertrofia"/"Manutenção" |
| **Peso** | `WeightLogs` (`UserId`, `WeightKg`, `LoggedAt`), 1 por dia | **Só** quando o usuário manda "peso 72kg". O peso do onboarding **não é gravado** |
| **Altura, idade, sexo, atividade** | **Em lugar nenhum** | Só existem no texto enviado à IA durante o onboarding |
| Metas | `DailyGoals` (`DailyNutritionGoal`) | `ObterMetaDiariaAsync`: meta exata do dia, senão a mais recente |
| Alergias / intolerâncias / doenças | `Nutrition_Patients.MedicalRestrictions` e `DailyGoals.DietaryRestrictions` | Um único texto livre: não há campo de doenças nem de medicamentos |
| Aversões | `Nutrition_Patients.FoodAversions` e `DailyGoals.FavoriteFoods` | O nome `FavoriteFoods` é enganoso |
| Protocolo clínico (B2B) | `Nutrition_Patients.ClinicalProtocol` | Definido pela nutricionista; entra no prompt como regra absoluta |
| Plano / acesso | `B2C_Trial_Users` | `TrialStartDate`, `Premium`, `Partner*`, `DailyMessageCount`, `LastInteractionDate`… |

### Como as metas são calculadas
- **B2C:** pela **IA**, não por código. `NutritionAgentService.CalculateNutritionalGoalsAsync(userProfileText, mainGoal)`:
  - instrui o Gemini a usar Mifflin-St Jeor, fator de atividade 1,2 a 1,9 e déficit/superávit de 300 a 500 kcal;
  - proteína de cerca de 2,0 g/kg, gordura de 0,8 a 1,0 g/kg e o resto em carboidrato; água de 35 a 40 ml/kg;
  - faltando dados, **assume 70 kg, 170 cm e 30 anos**. Se der erro, usa 2000 kcal / 150 g P / 200 g C / 60 g G / 2500 ml.
  - O resultado não é validado por código.
- **B2B:** a nutricionista define no portal (`NutritionistPortalController.SalvarOuAtualizarDietaAsync`), manualmente ou extraindo de texto, foto ou PDF (`ExtractDietGoalsFromDocumentAsync`).

---

## 5. Modo Nutri (prompt, modelo e contexto)

Tudo em `LabelWise.Infrastructure/AI/NutritionAgentService.cs`:

| Constante / método | Uso |
|---|---|
| `SystemPrompt` | Define **MODO 1 (registro)** e **MODO 2 (SOS / conselho)**. Pergunta ou pedido de orientação vira `isAdvice: true` + `adviceText`, que o controller mostra como "💡 Conselho do Nutri" |
| `UserPromptInstructions` | Regras de quantidade, porção padrão, "perguntar em vez de adivinhar" (`requiresUserClarification`), 0 kcal só para água/café/chá, e o formato JSON de saída |
| `BuildRequestBodyWithHistory` | Monta `messages`: system → histórico (`WhatsAppChatHistory`, 6 mais recentes em 2 h) → usuário (texto + `contextoPaciente` + protocolo + instruções [+ imagem]) |
| `GenerateProactiveSuggestionsAsync` | 3 sugestões para a próxima refeição, a partir do saldo |
| `GenerateSmartSubstitutionAsync` | Comando "substituir" |
| `CalculateNutritionalGoalsAsync` | Metas do onboarding |
| `GenerateWeeklyProgressSummaryAsync` | Relatório semanal (`ProgressReportController`) |
| `ExtractDietGoalsFromDocumentAsync` | Leitura de dieta (B2B) |

Fora desse serviço, há outras chamadas diretas ao Gemini, com prompts próprios:
- `WhatsAppController`: `TranscreverAudioComGeminiAsync`, `EstimarGastoCaloricoComGeminiAsync`, `GerarFeedbackDiarioComGeminiAsync`;
- `NutritionistPortalController.GenerateMealPlan`.

**Contexto do usuário** (`NutritionService.ProcessMealEntryAsync`): objetivo, restrições, aversões, protocolo clínico, meta calórica, consumo e saldo do dia, metas de macros e quantidade de refeições do dia. **Não entram:** peso, altura, idade, sexo, histórico de peso, medicamentos nem doenças estruturadas.

---

## 6. Agendamentos

**O agendador é externo** (um serviço de cron chama os endpoints). Não há fila nem scheduler no repositório.

| Endpoint (`WhatsAppController`) | Auth | O que faz |
|---|---|---|
| `POST send-daily-reminders` | `X-Cron-Secret` | Template `lembrete_diario_nutri` (café/almoço/jantar conforme a hora de Brasília). Evita duplicar via `CronExecutionLogs` |
| `POST send-water-reminders` | `X-Cron-Secret` | Template `lembrete_agua`, das 9 h às 21 h, 1 vez por hora, só para quem não bateu a meta |
| `POST send-end-of-day-feedback` | `X-Cron-Secret` | Mensagem motivacional gerada por IA, **enviada como texto livre** |
| `POST send-trial-reminders` | `X-Cron-Secret` | Dias 3, 7 e 13 do teste: `lembrete_registro_refeicoes` (Utilidade), com recuo para `trial_engajamento` |
| `POST send-expired-offers` | `X-Cron-Secret` | Template `oferta_fim_trial` para quem terminou o teste |
| `POST broadcast-novidade` | `X-Admin-Secret` (comparado com `CronSecret`) | Template `alerta_novidade_app` para todos os usuários de teste |
| `POST send-reminder` | **nenhuma** | Envio manual de `lembrete_diario_nutri` |

Serviços em segundo plano (`Program.cs`):
- `PatientAdherenceBackgroundService`: a cada 6 h recalcula `Nutrition_Patients.ClinicalStatus` ("Em Dia", "Atenção" ou "Inativo");
- `MongoIndexInitializerHostedService`: cria índices;
- `ProactiveNotificationBackgroundService`: **comentado**, desativado.

**Todos os horários são globais**, não por usuário. Não existe agenda individual (ex.: "toda segunda às 20 h para o usuário X").

---

## 7. Painel B2B e outras telas

| Tela | Arquivo | Acesso | Dados de |
|---|---|---|---|
| **Portal da nutricionista** | `wwwroot/index.html` | Chave `X-Nutri-Key` (salva no `localStorage`) | `NutritionistPortalController` (`/api/nutritionist/*`) |
| Página do paciente | `wwwroot/paciente.html` | Token de 30 dias (`PatientAccessTokens`) | `PatientPortalController` (`GET /api/patient/dashboard?token=`) |
| Painel do dono | `wwwroot/admin.html` | `X-Admin-Secret` (`AdminSecret`) | `AdminUsersController` (`/api/admin/users*`) |
| Página pública "Como funciona" | `wwwroot/parceiros.html` | Pública | — |
| Política de privacidade | `wwwroot/politica-privacidade.html` | Pública | — |

### Portal da nutricionista (`index.html`: Alpine + Tailwind + Chart.js)
- **Login:** `GET verify-key`. A chave mestre `NutritionistDefaultKey` vê todos os pacientes; as demais vêm da coleção `Nutritionists.ApiKey`.
- **"Perfil Clínico e Segurança" + "Metas Diárias"** (prescrição):
  - preenchimento manual, ou por IA a partir de texto, foto ou PDF (`POST extract`);
  - geração de cardápio (`POST generate-meal-plan`);
  - salvamento (`POST confirm` → `SalvarOuAtualizarDietaAsync`, que respeita `MaxPatients`).
- **"Seus Pacientes":** `GET patients`, a partir de `DailyGoals` + `Nutrition_Patients`, com `ClinicalStatus` e `ClinicalProtocol`.
- **"Dashboard de Consumo":**
  - `GET patient-logs/{phone}`: `Nutrition_MealLogs` + a meta mais recente;
  - gráfico de linha (Chart.js) das calorias por dia contra a meta, mais a lista agrupada por dia. Treinos aparecem na lista, mas não entram no total;
  - também: relatório pré-consulta em HTML (`GET patient-report/{phone}`) e exclusão de paciente (`DELETE patient/{phone}`).

### Outras APIs B2B, não usadas pelo portal
Todas com `[AllowAnonymous]`, **sem autenticação**:
- `DashboardController` (`summary`, `attention-list`);
- `PatientAnalyticsController` (`{phone}/evolution`, `{phone}/weekly-summary`: refeições, água e peso);
- `PatientManagementController`, `ProfessionalController`, `DietImportController`, `SmartSubstitutionController`, `ProgressReportController`, `AdminGoalsController`.

---

## 8. Consentimento e termos hoje

- **Não existe fluxo de consentimento no bot.** O onboarding começa direto no "Passo 1 de 4", sem aceite de termos, sem link para a política e sem registro de data/versão do aceite.
- **Não existe página de Termos de Uso.** Só a `politica-privacidade.html`, com 17 seções: controlador, coleta, IA, compartilhamento, retenção, exclusão por e-mail, câmera, menores, terceiros, transferência internacional, direitos. Essa política:
  - foi escrita para o app de análise por imagem/câmera e **não menciona o WhatsApp**;
  - **não trata de dados de saúde** (dados sensíveis, art. 11 da LGPD) nem de medicamentos.
- **Não há verificação de idade.**
- **Não há comando de descadastro** (ex.: "PARAR") para mensagens ativas ou templates de marketing.
- **Exclusão de dados é parcial:** `DELETE /api/nutritionist/patient/{phone}` apaga `DailyGoals`, `Nutrition_MealLogs` e `Nutrition_Patients`, mas deixa `WaterLogs`, `WeightLogs`, `WhatsAppChatHistory`, `B2C_Trial_Users`, `UserActivity*`, `AdminMessageLog`, `PaymentTracking` e `PatientAccessTokens`.
- **`WhatsAppChatHistory`** guarda o texto integral das mensagens (inclui relatos de saúde), sem prazo de retenção.

---

## Achados que afetam o GLP-1 diretamente

1. **Valores inventados quando a IA falha.** Em `NutritionAgentService`:
   - `ParseMealResponse` grava **400 kcal / 18 g P / 45 g C / 12 g G** quando a IA devolve 0 kcal sem itens;
   - o `catch` de `ExtractMealDataAsync` devolve a mesma refeição falsa (e ela é gravada);
   - `ExtractContentFromResponse` monta uma refeição de **350 kcal** quando a resposta não é JSON.

   Para quem usa GLP-1 e come pouco, uma refeição falsa de 400 kcal distorce todo o dia.
2. **Dados biométricos não persistidos.** Altura, idade e sexo não ficam salvos, e o peso só entra se o usuário mandar. Sem isso, não dá para calcular IMC, proteína por kg nem acompanhar a perda de massa.
3. **Indicadores pensados para comer "na meta":**
   - a sequência no foco exige 70% a 110% da meta (`CalcularOfensivaStreakAsync`);
   - a adesão marca "Atenção" abaixo de 70% (`PatientAdherenceBackgroundService`).

   Quem usa GLP-1 costuma ficar abaixo de 70% e seria penalizado.
4. **Itens da refeição não são gravados.** Isso impede analisar proteína por refeição, fibras e o tamanho das porções, que são pontos centrais no acompanhamento de GLP-1.

---

## Riscos para implementar o Modo GLP-1

| # | Risco | Por quê |
|---|---|---|
| R1 | **Dado sensível de saúde (LGPD art. 11)** | Saber que a pessoa usa semaglutida/tirzepatida é dado de saúde. Exige consentimento específico e destacado, finalidade clara e política atualizada. Hoje não há nada disso (seção 8) |
| R2 | **Limite médico/regulatório** | O bot não pode prescrever, sugerir ou ajustar dose, nem decidir sobre efeitos colaterais. Precisa de linguagem de "não substitui o médico" e de encaminhamento em sinais de alerta (dor abdominal forte e persistente, vômitos que não param, desidratação, hipoglicemia em quem usa insulina ou sulfonilureia) |
| R3 | **Metas e mensagens erradas para quem come pouco** | O cálculo atual aplica déficit e "faltam X kcal"; as sugestões empurram a comer mais; a sequência e a adesão penalizam comer pouco. O GLP-1 pede foco em proteína, hidratação, fibra e um piso calórico, não em bater calorias |
| R4 | **Refeições falsas de 400/350 kcal** | Ver "Achados", item 1 |
| R5 | **Agendamento por usuário inexistente** | Lembrete de aplicação semanal exige dia e hora por pessoa. Os crons atuais são globais. Fora da janela de 24 h, só template aprovado é entregue: `send-end-of-day-feedback` usa texto livre e provavelmente não é entregue a quem não falou com o bot no dia |
| R6 | **Templates de Marketing bloqueados (131049)** | Lembretes de GLP-1 precisam ser **Utilidade**, com texto neutro ligado à conta do usuário |
| R7 | **Roteamento frágil por palavras-chave** | Comandos novos ("dose", "apliquei", "enjoo") podem colidir com os existentes. **Um número isolado apaga uma refeição**, e "kg" ativa o registro de peso (ex.: "dose de 2,5 mg" ou "perdi 2 kg" podem ser mal interpretados). O `WhatsAppController` tem cerca de 2.300 linhas e uma ordem de `if` sensível |
| R8 | **APIs B2B sem autenticação** | Expõem refeições, peso e água por telefone. Com dados de GLP-1, a exposição fica mais grave |
| R9 | **Banco local = produção e credencial no repositório** | O `appsettings.json` versionado tem a string do MongoDB com usuário e senha, e aponta para o banco usado em produção. Testar o GLP-1 localmente gravaria dados reais. A credencial deveria ser trocada e removida do Git |
| R10 | **Sem testes automatizados do bot** | Qualquer mudança no roteamento só é validada em produção |
| R11 | **Fuso fixo UTC-3 e B2C sobrescrevendo dados** | Horários de aplicação dependem de fuso. O fim do onboarding B2C faz `ReplaceOne` em `Nutrition_Patients`: campos novos (ex.: medicamento) seriam apagados se não forem incluídos ali |

## Dúvidas para decidir antes de implementar

1. **Público:** o Modo GLP-1 é para B2C, B2B ou os dois? Quem ativa: o próprio usuário no WhatsApp, a nutricionista no portal ou você no painel do dono?
2. **O que registrar do tratamento:** medicamento (semaglutida, tirzepatida, liraglutida…), dose, dia da aplicação, local e efeitos colaterais (náusea, constipação, saciedade)? Ou só "usa GLP-1: sim/não"?
3. **Exigir prescrição?** Vamos pedir confirmação de que há acompanhamento médico? Isso muda o texto jurídico e o que o bot pode falar.
4. **Regras nutricionais:** quem define as metas de quem usa GLP-1? Proteína mínima (ex.: 1,2 a 1,6 g/kg?), piso calórico, meta de fibra e de água: valores fixos, calculados ou definidos pela nutricionista?
5. **Lembretes:** lembrete de aplicação (semanal, no dia e hora de cada usuário)? De hidratação e proteína? Quem cria e aprova os templates de Utilidade na Meta?
6. **Painel B2B:** o que a nutricionista precisa ver? Por exemplo: aplicações, efeitos colaterais, curva de peso, proteína diária, alertas.
7. **Consentimento:** quem redige o texto (jurídico)? O aceite será por botão no WhatsApp, com registro de data e versão? A política de privacidade será atualizada para cobrir WhatsApp e dados de saúde?
8. **Indicadores:** a sequência e a adesão mudam para usuários GLP-1 (ex.: foco em proteína e registro, não em calorias)?
9. **Negócio:** é um recurso Premium, faz parte do teste ou só existe no B2B?
10. **Dados já existentes:** o peso do onboarding passará a ser salvo (e altura, idade e sexo também), inclusive pedindo esses dados de novo a quem já está cadastrado?
11. **Pré-requisitos:** antes do GLP-1, vamos tratar os riscos que já existem hoje (R4, R8 e R9)? Eles afetam todos os usuários, não só os de GLP-1.
