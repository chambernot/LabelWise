# Templates do Modo GLP-1 para a Meta

Dois templates usados por `POST /api/whatsapp/send-glp1-reminders` quando a pessoa **não** falou com o bot nas últimas 24 horas. Dentro dessa janela, o bot manda o mesmo conteúdo como texto livre (`Glp1ReminderTexts`), a não ser que a chamada use `forceTemplate=true`.

Regras para os dois:
- **Categoria:** Utilidade (Utility). Os dois tratam de um acompanhamento que a própria pessoa ativou, sem promoção nem oferta, para não cair em Marketing (erro 131049).
- **Idioma:** Português (BR), `pt_BR`.
- **Exatamente uma variável, `{{1}}`, no corpo.** Cabeçalho e rodapé sem variáveis. Se o template aprovado tiver outra quantidade de variáveis, a Meta recusa o envio (erro 132000).
- **Sem botões.** O texto de descadastro vai no rodapé.
- O **nome** precisa ser exatamente o configurado em `Glp1Options`. Para usar outro nome, configure `Glp1__TemplateLembreteAplicacao` ou `Glp1__TemplateAlertaIngestao` no Render.
- A Meta pede um **exemplo** para cada variável na criação; use os exemplos indicados abaixo.

---

## 1. `glp1_lembrete_aplicacao`

Enviado quando o cron chama `?tipo=lembrete` (às 9h de Brasília), para quem tem o dia da aplicação igual a hoje.

| Variável | Conteúdo | Exemplo para a Meta |
|---|---|---|
| `{{1}}` | Meta diária de proteína do Modo GLP-1, só o número em gramas (`ProteinTargetG`) | `86` |

**Cabeçalho (texto):**
```
Lembrete do seu acompanhamento
```

**Corpo:**
```
Olá! Hoje é o dia da sua aplicação, conforme você informou no Modo GLP-1 do Nutrição Fácil.

Para o dia ser mais tranquilo:
• prefira refeições menores, a cada 3 ou 4 horas;
• beba água aos poucos ao longo do dia;
• inclua uma fonte de proteína em cada refeição. Sua meta é de {{1}} g por dia.

Se sentir algum desconforto, responda esta mensagem e me conte.
```

**Rodapé:**
```
Para parar, responda: parar lembretes glp1
```

---

## 2. `glp1_alerta_ingestao`

Enviado quando o cron chama `?tipo=alerta` (às 21h de Brasília), se a pessoa registrou refeições nos últimos 3 dias e ficou, na média, abaixo de 50% da meta de proteína ou abaixo do mínimo de calorias. No máximo uma vez a cada 3 dias.

| Variável | Conteúdo | Exemplo para a Meta |
|---|---|---|
| `{{1}}` | Mínimo do critério que disparou: `"<meta> g de proteína"` ou `"<piso> kcal"`. Se os dois dispararem, vai o de proteína. | `86 g de proteína` |

**Cabeçalho (texto):**
```
Cuidado com a sua alimentação
```

**Corpo:**
```
Olá! Pelos registros dos últimos 3 dias no seu acompanhamento do Modo GLP-1, você ficou abaixo do mínimo recomendado de {{1}} por dia.

Isso pode acontecer no tratamento, mas o corpo precisa de energia e proteína. Tente fazer refeições pequenas e nutritivas ao longo do dia e beber água aos poucos.

Vale conversar com sua nutricionista ou seu médico sobre isso.
```

**Rodapé:**
```
Para parar, responda: parar lembretes glp1
```

---

## Chamadas do endpoint

Todas com o header `X-Cron-Secret: <CronSecret>`. O parâmetro `tipo` é obrigatório: sem ele, ou com outro valor, a resposta é 400.

| Uso | Chamada |
|---|---|
| Cron das 9h | `POST /api/whatsapp/send-glp1-reminders?tipo=lembrete` |
| Cron das 21h | `POST /api/whatsapp/send-glp1-reminders?tipo=alerta` |
| Ver quem receberia, sem enviar | `...?tipo=lembrete&dryRun=true` |
| Rodar só para um telefone | `...?tipo=lembrete&phone=55SEUNUMERO` |
| Testar o template aprovado | `...?tipo=lembrete&phone=55SEUNUMERO&forceTemplate=true` |

- `forceTemplate=true` só é aceito junto com `phone=` (400 sem ele). Ele envia pelo template mesmo se você tiver falado com o bot nas últimas 24 h.
- As regras continuam valendo com `phone=`. O lembrete só sai no dia da aplicação; o alerta, só com ingestão baixa nos 3 dias. Também só sai um envio de cada tipo por dia.
- No log do Render, cada template mostra se a Meta aceitou (com o `MessageId`) ou o código e a mensagem de erro, sem o valor da variável e só com o final do telefone.

## Depois da aprovação

1. Confira na Meta se os dois ficaram com o status **Ativo** e a categoria **Utilidade**. Se a Meta mudar para Marketing, os envios fora da janela de 24 h podem ser bloqueados.
2. Faça o teste do template com `forceTemplate=true` e o seu número, no dia da sua aplicação.
3. Agende no cron externo as duas chamadas: `tipo=lembrete` às 9h e `tipo=alerta` às 21h (Brasília).
