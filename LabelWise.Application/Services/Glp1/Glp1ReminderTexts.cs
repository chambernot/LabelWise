namespace LabelWise.Application.Services.Glp1;

// Textos livres dos lembretes do Modo GLP-1 (enviados quando a pessoa falou com o bot nas últimas 24 h).
// Fora dessa janela vão os templates de docs/glp1-templates.md, com os mesmos valores em {{1}}. Nada sobre dose.
public static class Glp1ReminderTexts
{
    public static string LembreteAplicacao(int proteinaMetaG) =>
        "📅 *Hoje é o dia da sua aplicação.*\n\n" +
        "Para o dia ser mais tranquilo:\n" +
        "• prefira refeições menores, a cada 3 ou 4 horas;\n" +
        "• beba água aos poucos ao longo do dia;\n" +
        $"• inclua uma fonte de proteína em cada refeição (sua meta é {proteinaMetaG} g por dia).\n\n" +
        "Se sentir algum desconforto, me conte por aqui. 💚\n\n" +
        "_Para parar estes lembretes, mande: parar lembretes glp1_";

    // minimo: "86 g de proteína" ou "1.200 kcal", conforme o critério que disparou o alerta
    public static string AlertaIngestao(string minimo) =>
        "💛 *Cuidando de você*\n\n" +
        $"Nos últimos 3 dias você ficou abaixo do mínimo recomendado no seu acompanhamento ({minimo} por dia). " +
        "Isso pode acontecer no tratamento, mas o corpo precisa de energia e proteína.\n\n" +
        "• Tente refeições pequenas e nutritivas ao longo do dia;\n" +
        "• Beba água aos poucos.\n\n" +
        "Vale conversar com sua nutricionista ou seu médico sobre isso. 🩺\n\n" +
        "_Para parar estes lembretes, mande: parar lembretes glp1_";
}
