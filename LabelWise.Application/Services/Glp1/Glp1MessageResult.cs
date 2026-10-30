namespace LabelWise.Application.Services.Glp1;

// Resultado do Glp1WhatsAppHandler para o WhatsAppController.
// Handled: a mensagem já foi respondida pelo Modo GLP-1.
// TextoParaAnexar: sintoma + comida; acrescentar no fim da resposta do registro da refeição.
// RespostaSeNaoHouverRefeicao: o que mandar se a mensagem não chegar ao registro da refeição (ex.: barreira anti-custo).
public sealed record Glp1MessageResult(bool Handled, string? TextoParaAnexar = null, string? RespostaSeNaoHouverRefeicao = null)
{
    public static readonly Glp1MessageResult NaoTratado = new(false);
    public static readonly Glp1MessageResult Tratado = new(true);
}
