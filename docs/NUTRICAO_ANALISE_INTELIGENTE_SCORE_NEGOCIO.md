# Documento de Negócio — Score Nutricional do endpoint `nutricao-analise-inteligente`

## 1. Objetivo

Este documento explica, em linguagem de negócio, como a API `POST /api/nutrition/nutricao-analise-inteligente` gera:

- o `score.global`, que representa a avaliação nutricional geral do produto;
- os scores por perfil em `score.profiles`:
  - `diabetico`;
  - `hipertensao`;
  - `emagrecimento`;
  - `ganho_massa`.

O objetivo do score é orientar o usuário de forma simples, rápida e informativa, sem substituir a avaliação de um profissional de saúde.

---

## 2. Visão geral do fluxo

O endpoint segue este fluxo de decisão:

1. Recebe uma imagem do produto ou da tabela nutricional.
2. Tenta detectar código de barras.
3. Se encontrar o produto com dados úteis no Open Food Facts, usa esses dados.
4. Se não encontrar, usa OpenAI Vision para extrair os dados visíveis da tabela nutricional.
5. Converte os dados para uma base comparável por `100g` ou `100ml`.
6. Valida se existem dados mínimos suficientes para calcular o score.
7. Calcula o score geral.
8. Calcula os scores por perfil.
9. Retorna textos explicativos para o front:
   - `resumoRapido`;
   - `explicacaoScore`;
   - `pontoPrincipal`;
   - `strengths`;
   - `weaknesses`;
   - `profiles[].reasons`;
   - `processing`.

---

## 3. Base usada no cálculo

O score sempre usa a base normalizada em `nutrition.per100`.

Ou seja:

- para sólidos, o cálculo usa valores por `100g`;
- para líquidos, o cálculo usa valores por `100ml`;
- quando o rótulo traz apenas valores por porção, a API pode derivar uma base por `100g` ou `100ml`, desde que exista porção declarada;
- quando o rótulo já traz coluna `100g` ou `100ml`, essa coluna é usada diretamente.

Campos relacionados:

| Campo | Significado |
|---|---|
| `nutrition.asLabel` | Valores exibidos conforme o rótulo, normalmente por porção quando disponível. |
| `nutrition.per100` | Valores normalizados usados no score. |
| `nutrition.perServing` | Valores por porção, quando existentes ou derivados para exibição. |
| `displayBasis` | Diz ao front qual bloco deve ser exibido como tabela principal. |
| `per100Source` | Indica se `per100` veio direto do rótulo (`direct`) ou foi calculado (`derived`). |
| `scoreBasis` | Indica se o score foi calculado como `per100g` ou `per100ml`. |

Regra importante: o score não é calculado sobre a tabela exibida no front, mas sobre `nutrition.per100`.

---

## 4. Quando o score não é calculado

Para análises vindas de OpenAI Vision (`source = "openai-vision"`), a API só calcula score se conseguir ler com segurança os campos mínimos abaixo:

- calorias;
- carboidratos;
- gorduras totais;
- gordura saturada;
- sódio.

Se qualquer um desses campos estiver ausente, o campo `score` fica `null` e a API adiciona um aviso em `diagnostics.warnings`.

Motivo de negócio: campo não lido não deve ser tratado como zero. Por exemplo, se a imagem não permitiu ler carboidratos, a API não deve dizer que o produto tem `0g` de carboidratos nem gerar um score artificialmente alto.

Observação: açúcar não é campo mínimo obrigatório porque nem todo rótulo declara açúcar separadamente. Quando o açúcar é lido, ele entra no cálculo. Quando não é lido, ele não gera penalidade específica.

---

## 5. Score geral

O score geral começa em `100` pontos e recebe penalidades e bônus conforme a composição nutricional do produto.

A lógica considera múltiplos fatores ao mesmo tempo:

- sódio;
- açúcar;
- açúcar adicionado;
- carboidratos com impacto glicêmico estimado;
- polióis;
- gordura saturada;
- calorias;
- gordura total;
- proteína;
- fibra;
- nível de processamento quando disponível.

### 5.1 Penalidade por sódio

O sódio é um dos fatores mais importantes porque está associado ao risco cardiovascular e à hipertensão.

| Sódio por `100g` ou `100ml` | Efeito no score geral |
|---:|---|
| `< 120mg` | Sem penalidade. |
| `120mg` a `< 400mg` | Penalidade leve. |
| `400mg` a `< 800mg` | Penalidade moderada. |
| `800mg` a `< 1500mg` | Penalidade alta. |
| `1500mg` a `< 3000mg` | Penalidade crítica. |
| `>= 3000mg` | Penalidade extrema. |

Além da penalidade, existem travas de teto. Produtos com sódio alto não podem receber score alto apenas por terem proteína, fibra ou outros pontos positivos.

### 5.2 Penalidade por açúcar

Para sólidos, a escala é baseada em açúcar por `100g`.

| Açúcar por `100g` | Efeito no score geral |
|---:|---|
| `< 5g` | Sem penalidade. |
| `5g` a `< 10g` | Penalidade leve a moderada. |
| `10g` a `< 15g` | Penalidade alta. |
| `15g` a `< 30g` | Penalidade muito alta. |
| `>= 30g` | Penalidade crítica. |

Para líquidos, a regra é mais rígida, porque açúcar em bebida tende a ter absorção rápida e menor saciedade.

| Açúcar por `100ml` | Efeito no score geral |
|---:|---|
| `0g` | Sem penalidade. |
| `> 0g` a `< 2.5g` | Penalidade pequena. |
| `2.5g` a `< 5g` | Penalidade moderada. |
| `5g` a `< 9g` | Penalidade alta. |
| `>= 9g` | Penalidade muito alta. |

Açúcar adicionado aumenta a penalidade. Em bebidas, a penalidade por açúcar adicionado é mais forte.

### 5.3 Penalidade por impacto glicêmico estimado

A API calcula uma aproximação de carboidratos com impacto glicêmico:

`carboidratos com impacto = carboidratos totais - 50% dos polióis`

A ideia é considerar que polióis geralmente têm impacto glicêmico menor que açúcar e amido, mas não são neutros. Eles ainda podem ter algum impacto metabólico, calórico e gastrointestinal.

| Carboidratos com impacto por `100g` ou `100ml` | Efeito |
|---:|---|
| `< 30g` | Sem penalidade relevante. |
| `30g` a `< 45g` | Penalidade leve. |
| `45g` a `< 60g` | Penalidade moderada. |
| `>= 60g` | Penalidade alta. |

Polióis em quantidade elevada também podem gerar penalidade leve, por poderem somar calorias e causar desconforto intestinal.

### 5.4 Penalidade por gordura saturada

| Gordura saturada por `100g` ou `100ml` | Efeito no score geral |
|---:|---|
| `< 1.5g` | Sem penalidade. |
| `1.5g` a `< 3g` | Penalidade leve. |
| `3g` a `< 6g` | Penalidade moderada. |
| `6g` a `< 10g` | Penalidade alta. |
| `>= 10g` | Penalidade crítica. |

Também existem travas de teto para impedir que produtos com muita gordura saturada recebam classificação alta por terem outros nutrientes positivos.

### 5.5 Penalidade por calorias e gordura total

Calorias e gordura total são avaliadas juntas porque gordura aumenta a densidade calórica do produto.

| Calorias por `100g` ou `100ml` | Efeito |
|---:|---|
| `< 150 kcal` | Sem penalidade. |
| `150` a `< 300 kcal` | Penalidade leve. |
| `300` a `< 450 kcal` | Penalidade moderada. |
| `>= 450 kcal` | Penalidade alta. |

| Gordura total por `100g` ou `100ml` | Efeito |
|---:|---|
| `< 10g` | Sem penalidade. |
| `10g` a `< 20g` | Penalidade leve. |
| `>= 20g` | Penalidade moderada a alta. |

Também existe uma trava genérica para produtos com densidade energética, carboidrato e gordura relevantes, mas baixa proteína. Essa regra evita que produtos desse tipo recebam nota quase perfeita.

### 5.6 Bônus por proteína

Proteína pode melhorar o score geral, mas não deve mascarar excesso de sódio, açúcar ou gordura saturada.

| Proteína por `100g` ou `100ml` | Bônus |
|---:|---:|
| `< 5g` | `0` |
| `5g` a `< 10g` | Bônus baixo. |
| `10g` a `< 15g` | Bônus moderado. |
| `15g` a `< 20g` | Bônus alto. |
| `>= 20g` | Bônus máximo. |

### 5.7 Bônus por fibra

| Fibra por `100g` ou `100ml` | Bônus |
|---:|---:|
| `< 1.5g` | `0` |
| `1.5g` a `< 3g` | Bônus baixo. |
| `3g` a `< 6g` | Bônus moderado. |
| `>= 6g` | Bônus máximo. |

### 5.8 Penalidade por ultraprocessamento

Quando o produto é classificado como `ultraprocessado`, recebe penalidade adicional relevante no score geral.

Quando o produto é classificado como `in_natura` ou `minimamente_processado`, pode receber um pequeno favorecimento.

Quando o nível de processamento é desconhecido, essa penalidade não é aplicada.

Além disso, o bônus de proteína é reduzido quando o produto também apresenta sinais negativos, como açúcar elevado, açúcar adicionado, gordura saturada alta, excesso calórico associado a gordura ou perfil ultraprocessado. Essa regra evita que produtos “fitness” ou proteicos pareçam saudáveis apenas por terem proteína.

---

## 6. Travas de teto do score geral

Além de somar penalidades e bônus, a API usa travas de teto para evitar scores enganosamente altos.

Exemplos de regra de negócio:

- produto com sódio muito alto não pode ser `Excelente`;
- produto com açúcar alto não pode ser `Excelente`;
- bebida açucarada não pode receber nota alta apenas por ter baixo sódio ou alguma fibra;
- produto com gordura saturada alta não pode ser considerado excelente;
- produto com densidade energética, gordura, carboidrato e baixa proteína não deve receber nota quase perfeita.
- produto ultraprocessado não deve receber nota quase perfeita apenas por ter proteína ou fibra.

Essas travas são importantes porque refletem uma visão de risco nutricional: pontos positivos não devem apagar alertas relevantes.

---

## 7. Classificação do score geral

Depois do cálculo, o score geral é limitado entre `0` e `100`.

| Score geral | Rótulo exibido |
|---:|---|
| `90` a `100` | `Excelente` |
| `70` a `89` | `Bom` |
| `60` a `69` | `Moderado` |
| `40` a `59` | `Atenção` |
| `20` a `39` | `Evitar` |
| `0` a `19` | `Muito ruim` |

---

## 8. Principal ponto de atenção

A API identifica o `principalOffender`, que é o nutriente ou fator que mais prejudicou o score.

Possíveis valores principais:

- `sódio`;
- `açúcar`;
- `impacto glicêmico`;
- `gordura saturada`;
- `calorias`;
- `nenhum relevante`.

A API só marca um principal ofensor quando o impacto é relevante. Pequenas penalidades não viram alerta principal.

Esse campo alimenta textos como:

- `explicacaoScore`;
- `pontoPrincipal`;
- cards de atenção no front.

---

## 9. Textos retornados para o front

### 9.1 `resumoRapido`

Resume a leitura nutricional em linguagem simples.

Exemplos:

- `Produto com boa composição nutricional. Contém 213 kcal por 100g.`
- `Produto razoável, com alguns pontos de atenção. Contém 280 kcal por 100g.`
- `Bebida açucarada. O consumo frequente deve ser evitado, especialmente por conter açúcar adicionado.`

### 9.2 `explicacaoScore`

Explica o principal motivo da nota.

Quando não há problema relevante, retorna mensagem de equilíbrio nutricional.

Quando há ofensor, explica o impacto. Exemplo:

- açúcar alto pode favorecer picos de glicemia e ganho de peso;
- sódio alto pode elevar a pressão arterial;
- gordura saturada alta está associada a risco cardiovascular;
- gordura total elevada aumenta a densidade calórica;
- muitas calorias exigem atenção à porção.

### 9.3 `pontoPrincipal`

É a frase mais direta para o usuário.

Exemplos:

- `Excesso de sódio está ligado à hipertensão arterial.`
- `Consumo excessivo de açúcar pode levar ao diabetes tipo 2 e obesidade.`
- `Gordura saturada em excesso aumenta o risco de doenças cardiovasculares.`
- `Produto calórico — controle o tamanho da porção.`

---

## 10. Destaques positivos e alertas

### 10.1 `strengths`

Destaques positivos podem incluir:

- baixo teor de açúcar;
- baixo teor de sódio;
- boa fonte de proteína;
- boa fonte de fibra.

### 10.2 `weaknesses`

Alertas podem incluir:

- sódio elevado ou muito alto;
- açúcar alto;
- açúcar adicionado;
- polióis elevados;
- gordura saturada alta;
- baixo teor de proteína.

---

## 11. Scores por perfil

Os scores por perfil são calculados separadamente do score geral.

Eles usam a mesma base `nutrition.per100`, mas mudam o peso dos nutrientes conforme o objetivo do perfil.

A classificação dos perfis é:

| Score por perfil | Rótulo |
|---:|---|
| `80` a `100` | `Adequado` |
| `60` a `79` | `Moderado` |
| `40` a `59` | `Atenção` |
| `0` a `39` | `Evitar` |

Cada perfil retorna também uma lista de `reasons`, usada pelo front para explicar o motivo da nota.

---

## 12. Perfil `diabetico`

### 12.1 Objetivo

Avaliar se o produto é adequado para pessoas que precisam controlar glicemia e risco cardiometabólico.

### 12.2 Principais fatores negativos

O perfil diabético penaliza:

- açúcar total;
- açúcar adicionado;
- carboidratos com impacto glicêmico estimado;
- sódio elevado;
- gordura saturada elevada.

### 12.3 Açúcar e açúcar adicionado

Para sólidos:

- açúcar acima de `5g/100g` já começa a gerar atenção;
- acima de `8g/100g` vira moderado;
- acima de `12.5g/100g` vira alto;
- acima de `18g/100g` vira muito elevado.

Para líquidos, a escala é mais rígida:

- `2.5g/100ml` já é moderado;
- `5g/100ml` é alto;
- `9g/100ml` ou mais é muito alto.

Açúcar adicionado pesa mais, principalmente em bebidas.

### 12.4 Carboidratos com impacto estimado

A API calcula:

`carboidratos com impacto = carboidratos totais - polióis`

Quanto maior esse valor, maior a atenção para o perfil diabético.

### 12.5 Sódio e gordura saturada

Mesmo sendo um perfil focado em glicemia, a API também considera risco cardiovascular, porque diabetes e risco cardíaco frequentemente estão associados.

Por isso, sódio alto e gordura saturada alta reduzem o score para diabético.

### 12.6 Bônus

O perfil diabético recebe bônus por:

- proteína relevante;
- fibras;
- gordura saturada baixa;
- sódio baixo com gordura saturada baixa.

### 12.7 Travas específicas

Bebidas com açúcar relevante recebem teto rígido. Mesmo que tenham outros pontos positivos, não devem ser classificadas como adequadas para diabéticos.

Sódio crítico e gordura saturada elevada também limitam o score.

---

## 13. Perfil `hipertensao`

### 13.1 Objetivo

Avaliar se o produto é adequado para pessoas com preocupação com pressão alta e risco cardiovascular.

### 13.2 Principal fator

O principal fator é sódio.

| Sódio por `100g` ou `100ml` | Efeito no perfil hipertensão |
|---:|---|
| `0` ou muito baixo | Favorável. |
| `> 200mg` | Atenção ao consumo frequente. |
| `> 400mg` | Consumir com moderação. |
| `> 800mg` | Evitar. |
| `> 1500mg` | Evitar fortemente. |
| `> 3000mg` | Evitar totalmente. |

### 13.3 Outros fatores negativos

O perfil também penaliza:

- gordura saturada;
- gordura trans;
- açúcar adicionado;
- açúcar total muito alto.

Motivo de negócio: hipertensão é analisada junto com risco cardiovascular geral, não apenas com sódio.

### 13.4 Travas específicas

Gordura saturada alta limita o score máximo. Assim, um produto com pouco sódio, mas muita gordura saturada, não fica automaticamente adequado.

---

## 14. Perfil `emagrecimento`

### 14.1 Objetivo

Avaliar se o produto ajuda ou atrapalha uma estratégia de controle de peso.

### 14.2 Principais fatores negativos

O perfil emagrecimento penaliza:

- calorias altas;
- calorias líquidas em bebidas;
- açúcar;
- açúcar adicionado;
- polióis em quantidade elevada;
- gordura total;
- gordura saturada;
- sódio alto.

### 14.3 Calorias

Para sólidos:

| Calorias por `100g` | Efeito no perfil emagrecimento |
|---:|---|
| `<= 100 kcal` | Favorável. |
| `> 150 kcal` | Controlar porção. |
| `> 250 kcal` | Atenção à quantidade. |
| `> 300 kcal` | Calorias elevadas. |
| `> 400 kcal` | Dificulta emagrecimento. |

Para bebidas, a regra é mais rígida:

- bebidas com `30 kcal/100ml` já exigem controle;
- bebidas com `60 kcal/100ml` ou mais são consideradas calóricas para emagrecimento.

### 14.4 Açúcar e gordura

Açúcar e gordura elevam a densidade calórica e reduzem a adequação ao perfil de emagrecimento.

Bebidas açucaradas recebem teto específico, porque calorias líquidas tendem a gerar menor saciedade.

### 14.5 Bônus

O perfil recebe bônus por:

- proteína, por ajudar na saciedade;
- fibra, por ajudar na saciedade.

O bônus é limitado para não compensar excessos relevantes.

---

## 15. Perfil `ganho_massa`

### 15.1 Objetivo

Avaliar se o produto é útil para uma estratégia de ganho de massa muscular.

### 15.2 Principal fator positivo

Proteína é o fator mais importante.

| Proteína por `100g` ou `100ml` | Efeito no perfil ganho de massa |
|---:|---|
| `>= 25g` | Excelente para hipertrofia. |
| `>= 15g` | Ótima fonte para recuperação muscular. |
| `>= 10g` | Boa fonte de proteína. |
| `>= 5g` e `< 10g` | Proteína baixa; penaliza bastante. |
| `< 5g` ou ausente | Não contribui para ganho de massa. |

### 15.3 Calorias

Calorias podem ser positivas para ganho de massa, porque ajudam no superávit calórico.

| Calorias por `100g` ou `100ml` | Efeito |
|---:|---|
| `>= 350 kcal` | Ajuda no superávit calórico. |
| `>= 250 kcal` | Auxilia no superávit. |
| `>= 150 kcal` | Moderado; pode exigir maior quantidade. |
| `< 150 kcal` | Pode ser insuficiente para superávit. |

### 15.4 Carboidratos

Carboidratos podem ser positivos por fornecer energia para treino, especialmente quando estão em quantidade relevante.

### 15.5 Fatores negativos

Mesmo para ganho de massa, o perfil penaliza:

- açúcar excessivo;
- açúcar adicionado;
- gordura saturada elevada;
- sódio muito alto.

### 15.6 Travas específicas

O score é limitado quando:

- proteína é baixa;
- proteína baixa aparece junto com sódio alto;
- açúcar crítico aparece junto com gordura saturada crítica;
- o produto é bebida açucarada.

Regra de negócio: um produto não deve ser considerado bom para ganho muscular apenas por ter calorias, se tiver pouca proteína ou perfil nutricional ruim.

---

## 16. Diferença entre score geral e score por perfil

O score geral responde:

> “Este produto é nutricionalmente bom de forma ampla?”

Os scores por perfil respondem:

> “Este produto é adequado para este objetivo ou condição específica?”

Por isso, é possível um produto ter:

- score geral bom;
- score baixo para ganho de massa, se tiver pouca proteína;
- score baixo para hipertensão, se tiver muito sódio;
- score baixo para diabético, se tiver muito açúcar ou alto impacto glicêmico;
- score baixo para emagrecimento, se for muito calórico ou muito gorduroso.

---

## 17. Exemplos de interpretação

### 17.1 Produto com baixo sódio, pouca gordura saturada e boa fibra

Tende a receber:

- score geral alto;
- bom score para hipertensão;
- bom score para emagrecimento, se não for muito calórico;
- bom score para diabético, se não tiver açúcar alto;
- score para ganho de massa depende da proteína.

### 17.2 Produto com muito sódio

Mesmo que tenha proteína ou fibra, tende a receber:

- score geral baixo;
- score muito baixo para hipertensão;
- redução nos demais perfis por risco cardiovascular.

### 17.3 Produto doce ou bebida açucarada

Tende a receber:

- penalidade no score geral;
- score baixo para diabético;
- score menor para emagrecimento;
- alertas sobre açúcar e açúcar adicionado.

### 17.4 Produto calórico com baixa proteína

Pode receber:

- score geral limitado;
- score de ganho de massa baixo, se a proteína for insuficiente;
- atenção para emagrecimento por densidade calórica.

---

## 18. Campos do contrato consumidos pelo front

O front deve usar principalmente:

| Campo | Uso recomendado |
|---|---|
| `score.global` | Número central do score geral. |
| `score.globalLabel` | Label do score geral. |
| `score.resumoRapido` | Texto curto no topo do resultado. |
| `score.explicacaoScore` | Explicação detalhada da nota. |
| `score.pontoPrincipal` | Principal destaque ou alerta. |
| `score.principalOffender` | Nutriente/fator que mais prejudicou a nota. |
| `score.strengths` | Pontos positivos. |
| `score.weaknesses` | Pontos de atenção. |
| `score.profiles.diabetico` | Score e motivos para perfil diabético. |
| `score.profiles.hipertensao` | Score e motivos para perfil pressão alta. |
| `score.profiles.emagrecimento` | Score e motivos para emagrecimento. |
| `score.profiles.ganho_massa` | Score e motivos para ganho de massa. |
| `score.processing` | Nota e explicação específica do nível de processamento. |
| `diagnostics.warnings` | Avisos técnicos ou de qualidade de extração. |
| `imageQuality.retryRequested` | Indica se o front deve pedir nova foto. |

Se `score` vier `null`, o front não deve exibir nota. Deve explicar que não foi possível calcular score com segurança por falta de dados legíveis.

---

## 19. Observações importantes de negócio

1. O score é informativo, não diagnóstico médico.
2. A API trabalha com valores por `100g` ou `100ml` para permitir comparação justa entre produtos.
3. Bônus de proteína e fibra melhoram a nota, mas não anulam riscos importantes.
4. Sódio, açúcar e gordura saturada têm travas de teto porque são fatores críticos.
5. Bebidas açucaradas são avaliadas com regra mais rígida.
6. O score depende da qualidade da extração da tabela nutricional.
7. Quando faltam dados mínimos, a API não calcula score para evitar conclusões incorretas.
8. O front deve respeitar `displayBasis` para exibir a tabela e `scoreBasis` para explicar a base do score.

---

## 20. Fonte técnica de implementação

Este documento descreve a regra de negócio implementada atualmente nos seguintes componentes:

- `NutritionController.NutricaoAnaliseInteligente`;
- `IntelligentAnalysisScoreService`;
- `NutritionScoringServiceV2`;
- `IntelligentAnalysisResponse`.

A implementação oficial do motor de score nutricional é `NutritionScoringServiceV2`.
