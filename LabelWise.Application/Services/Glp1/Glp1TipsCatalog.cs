using LabelWise.Domain.Entities.Glp1;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LabelWise.Application.Services.Glp1;

public sealed record Glp1Tip(string Texto, IReadOnlyList<string> Alimentos);

// Dicas do Modo GLP-1, lidas de content/glp1-tips.json (recurso embutido no assembly).
// O arquivo precisa de revisão de nutricionista; o campo "_revisao" no topo registra isso.
public static class Glp1TipsCatalog
{
    public const string Nausea = "nausea";
    public const string Constipacao = "constipacao";
    public const string Diarreia = "diarreia";
    public const string Refluxo = "refluxo";
    public const string SemApetite = "sem_apetite";
    public const string Hidratacao = "hidratacao";
    public const string ProteinaPorcaoPequena = "proteina_porcao_pequena";

    // Usada só se todas as dicas da categoria e de hidratação forem filtradas
    public const string DicaPadrao = "Se o sintoma continuar ou piorar, converse com o médico que acompanha seu tratamento.";

    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<Glp1Tip>>> Dicas = new(Carregar);

    public static IReadOnlyList<Glp1Tip> Todas(string categoria) =>
        Dicas.Value.TryGetValue(categoria, out var lista) ? lista : Array.Empty<Glp1Tip>();

    // Vômito usa as dicas de hidratação
    public static string Categoria(Glp1Symptom sintoma) => sintoma switch
    {
        Glp1Symptom.Nausea => Nausea,
        Glp1Symptom.Vomito => Hidratacao,
        Glp1Symptom.Constipacao => Constipacao,
        Glp1Symptom.Diarreia => Diarreia,
        Glp1Symptom.Refluxo => Refluxo,
        Glp1Symptom.SemApetite => SemApetite,
        _ => Hidratacao
    };

    // Dicas da categoria que não citam nenhum alimento das restrições ou aversões do usuário
    public static IReadOnlyList<Glp1Tip> Permitidas(string categoria, string? restricoes, string? aversoes)
    {
        var proibido = Glp1BodyDataParser.SemAcentos($"{restricoes} {aversoes}").ToLowerInvariant();
        return Todas(categoria)
            .Where(d => !d.Alimentos.Any(a => Regex.IsMatch(proibido, $@"\b{Regex.Escape(Glp1BodyDataParser.SemAcentos(a).ToLowerInvariant())}")))
            .ToList();
    }

    public static string Escolher(string categoria, string? restricoes, string? aversoes, Random? random = null)
    {
        var opcoes = Permitidas(categoria, restricoes, aversoes);
        if (opcoes.Count == 0 && categoria != Hidratacao)
        {
            opcoes = Permitidas(Hidratacao, restricoes, aversoes);
        }

        return opcoes.Count == 0 ? DicaPadrao : opcoes[(random ?? Random.Shared).Next(opcoes.Count)].Texto;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<Glp1Tip>> Carregar()
    {
        var assembly = typeof(Glp1TipsCatalog).Assembly;
        var recurso = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("glp1-tips.json", StringComparison.OrdinalIgnoreCase))
                      ?? throw new InvalidOperationException("content/glp1-tips.json não foi embutido no assembly.");

        using var stream = assembly.GetManifestResourceStream(recurso)!;
        using var doc = JsonDocument.Parse(stream);

        var resultado = new Dictionary<string, IReadOnlyList<Glp1Tip>>(StringComparer.OrdinalIgnoreCase);
        foreach (var categoria in doc.RootElement.GetProperty("dicas").EnumerateObject())
        {
            resultado[categoria.Name] = categoria.Value.EnumerateArray()
                .Select(d => new Glp1Tip(
                    d.GetProperty("texto").GetString() ?? string.Empty,
                    d.TryGetProperty("alimentos", out var a) ? a.EnumerateArray().Select(x => x.GetString() ?? "").ToList() : new List<string>()))
                .Where(d => !string.IsNullOrWhiteSpace(d.Texto))
                .ToList();
        }
        return resultado;
    }
}
