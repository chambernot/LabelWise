namespace LabelWise.Application.Services.Glp1;

// Textos livres dos lembretes do Modo GLP-1 (enviados quando a pessoa falou com o bot nas últimas 24 h).
// Fora dessa janela vão os templates de docs/glp1-templates.md. Nada sobre dose.
public static class Glp1ReminderTexts
{
    public const string LembreteAplicacao =
        "📅 *Hoje é o dia da sua aplicação.*\n\n" +
        "Para o dia ser mais tranquilo:\n" +
        "• prefira refeições menores, a cada 3 ou 4 horas;\n" +
        "• beba água aos poucos ao longo do dia;\n" +
        "• inclua uma fonte de proteína em cada refeição.\n\n" +
        "Se sentir algum desconforto, me conte por aqui. 💚\n\n" +
        "_Para parar estes lembretes, mande: parar lembretes glp1_";

    public const string AlertaIngestao =
        "💛 *Cuidando de você*\n\n" +
        "Nos últimos 3 dias você comeu menos do que o recomendado no seu acompanhamento. " +
        "Isso pode acontecer no tratamento, mas o corpo precisa de energia e proteína.\n\n" +
        "• Tente refeições pequenas e nutritivas ao longo do dia;\n" +
        "• Beba água aos poucos.\n\n" +
        "Vale conversar com sua nutricionista ou seu médico sobre isso. 🩺\n\n" +
        "_Para parar estes lembretes, mande: parar lembretes glp1_";
}
