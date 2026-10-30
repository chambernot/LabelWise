# Templates do Modo GLP-1 para a Meta

Dois templates usados por `POST /api/whatsapp/send-glp1-reminders` quando a pessoa **não** falou com o bot nas últimas 24 horas. Dentro dessa janela, o bot manda o mesmo conteúdo como texto livre (`Glp1ReminderTexts`).

Regras para os dois:
- **Categoria:** Utilidade (Utility). Os dois tratam de um acompanhamento que a própria pessoa ativou, sem promoção nem oferta, para não cair em Marketing (erro 131049).
- **Idioma:** Português (BR), `pt_BR`.
- **Sem variáveis.** O sistema envia sem parâmetros; se o template tiver `{{1}}`, a Meta recusa o envio (erro 132000).
- **Sem botões.** O texto de descadastro vai no rodapé.
- O **nome** precisa ser exatamente o configurado em `Glp1Options`. Para usar outro nome, configure `Glp1__TemplateLembreteAplicacao` ou `Glp1__TemplateAlertaIngestao` no Render.

---

## 1. `glp1_lembrete_aplicacao`

Enviado às 9h (Brasília) no dia da semana em que a pessoa aplica o medicamento.

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
• inclua uma fonte de proteína em cada refeição.

Se sentir algum desconforto, responda esta mensagem e me conte.
```

**Rodapé:**
```
Para parar, responda: parar lembretes glp1
```

---

## 2. `glp1_alerta_ingestao`

Enviado às 21h (Brasília) quando a pessoa registrou refeições nos últimos 3 dias e ficou, na média, abaixo de 50% da meta de proteína ou abaixo do mínimo de calorias. No máximo uma vez a cada 3 dias.

**Cabeçalho (texto):**
```
Cuidado com a sua alimentação
```

**Corpo:**
```
Olá! Pelos registros dos últimos 3 dias no seu acompanhamento do Modo GLP-1, você comeu menos do que o recomendado.

Isso pode acontecer no tratamento, mas o corpo precisa de energia e proteína. Tente fazer refeições pequenas e nutritivas ao longo do dia e beber água aos poucos.

Vale conversar com sua nutricionista ou seu médico sobre isso.
```

**Rodapé:**
```
Para parar, responda: parar lembretes glp1
```

---

## Depois da aprovação

1. Confira na Meta se os dois ficaram com o status **Ativo** e a categoria **Utilidade**. Se a Meta mudar a categoria para Marketing, os envios para quem está fora da janela de 24 h podem ser bloqueados.
2. Teste sem enviar nada:
   ```
   POST /api/whatsapp/send-glp1-reminders?dryRun=true&phone=55SEUNUMERO
   Header: X-Cron-Secret: <CronSecret>
   ```
3. Teste o envio real só para o seu número (ignora a hora, mas respeita as regras):
   ```
   POST /api/whatsapp/send-glp1-reminders?phone=55SEUNUMERO
   ```
4. Agende no cron externo a chamada **de hora em hora**, sem parâmetros. O endpoint só envia às 9h e às 21h.
