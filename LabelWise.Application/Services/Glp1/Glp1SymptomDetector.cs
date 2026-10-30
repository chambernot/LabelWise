using LabelWise.Domain.Entities.Glp1;
using System.Text.RegularExpressions;

namespace LabelWise.Application.Services.Glp1;

public sealed record Glp1SymptomDetection(IReadOnlyList<Glp1Symptom> Sintomas, IReadOnlyList<string> SinaisDeAlerta, bool TemComida)
{
    public bool Vazio => Sintomas.Count == 0 && SinaisDeAlerta.Count == 0;
    public bool TemSinalDeAlerta => SinaisDeAlerta.Count > 0;
}

// Detecta sintomas e sinais de alerta do Modo GLP-1 por palavras-chave (sem IA), em texto ou áudio já transcrito.
// Também diz se a mensagem fala de comida ("comi pizza e fiquei enjoada"), para o registro da refeição seguir normalmente.
public static class Glp1SymptomDetector
{
    private static readonly (Glp1Symptom Sintoma, Regex Padrao)[] PadroesSintomas =
    {
        (Glp1Symptom.Nausea, new Regex(@"\b(enjo\w*|nause\w*|ansia de vomito)")),
        (Glp1Symptom.Vomito, new Regex(@"\bvomit\w*")),
        (Glp1Symptom.Constipacao, new Regex(@"\b(intestino (preso|travado)|prisao de ventre|constipa\w*)")),
        (Glp1Symptom.Diarreia, new Regex(@"\b(diarreia|intestino solto)")),
        (Glp1Symptom.Refluxo, new Regex(@"\b(azia|refluxo|queimacao)\b")),
        (Glp1Symptom.SemApetite, new Regex(@"\b(sem (fome|apetite)|falta de apetite|nao (tenho|to|estou com|sinto) fome|perdi (a fome|o apetite))\b"))
    };

    // "não estou enjoada", "não tive azia": o sintoma foi negado
    private static readonly Regex NegacaoAntes = new(@"\bnao (estou|to|fiquei|tive|tenho|senti|sinto|estava)( mais| muito| nenhum| nenhuma)? $");

    private static readonly (string Nome, Func<string, bool> Detecta)[] SinaisDeAlerta =
    {
        ("vômito que não para", t =>
            Regex.IsMatch(t, @"\bvomit\w*.{0,25}\b(o dia todo|o dia inteiro|sem parar|toda hora|nao para|nao passa|varias vezes|a noite toda)\b") ||
            Regex.IsMatch(t, @"\bnao (paro|para|consigo parar) de vomitar\b")),
        ("sangue", t => Regex.IsMatch(t, @"\bsang(ue|rando|ramento)\w*")),
        ("desmaio", t => Regex.IsMatch(t, @"\bdesmai\w*")),
        ("dor abdominal forte", t =>
            Regex.IsMatch(t, @"\bdor(es)?\b.{0,30}\b(barriga|abdomen|abdominal|estomago)\b|\b(barriga|abdomen|estomago)\b.{0,15}\bdo(i|endo|r)\b") &&
            Regex.IsMatch(t, @"\b(forte|fortissima|insuportavel|intensa|muita|horrivel|terrivel)\b")),
        ("não consegue beber água", t =>
            Regex.IsMatch(t, @"\bnao (consigo|consegui|to conseguindo|estou conseguindo) (beber|tomar) (agua|nada|liquido\w*)") ||
            Regex.IsMatch(t, @"\bnem agua (para|fica|parou)")),
        ("urina muito escura", t => Regex.IsMatch(t, @"\b(urina|xixi)\b.{0,15}\bescur\w*")),
        ("tremor com suor frio", t => Regex.IsMatch(t, @"\btrem\w*") && Regex.IsMatch(t, @"\b(suor frio|suando frio)\b")),
        ("confusão", t => Regex.IsMatch(t, @"\b(confusao mental|(estou|to|fiquei|sinto|me sinto) (meio |muito )?(confus\w*|desorientad\w*))"))
    };

    // Frases que parecem comida mas não são registro de refeição
    private static readonly Regex NaoEhComida = new(
        @"\b(nao (comi|consigo comer|consegui comer|to comendo|estou comendo)( nada)?|sem comer|tomei (o |a |meu |minha )?(remedio|medicamento|injecao|dose|caneta|ozempic|wegovy|mounjaro|saxenda))\b");

    private static readonly Regex Comida = new(
        @"\b(comi|almocei|jantei|lanchei|belisquei|provei|consumi|tomei|bebi)\b|" +
        @"\b\d+\s*(g|gr|gramas?|ml|colher\w*|fatias?|unidades?|copos?|xicaras?|pedacos?)\b|" +
        @"\b(arroz|feijao|frango|carne|ovos?|pao|salada|macarrao|massa|peixe|batata|queijo|leite|iogurte|fruta|banana|maca|pizza|lanche|hamburguer|sanduiche|bolo|doce|chocolate|sopa|tapioca|cuscuz|biscoito|bolacha|sorvete)\b");

    public static Glp1SymptomDetection Detectar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return new Glp1SymptomDetection(Array.Empty<Glp1Symptom>(), Array.Empty<string>(), false);
        }

        var t = Normalizar(texto);

        var sintomas = new List<Glp1Symptom>();
        foreach (var (sintoma, padrao) in PadroesSintomas)
        {
            // "ânsia de vômito" é enjoo, não vômito
            var alvo = sintoma == Glp1Symptom.Vomito ? t.Replace("ansia de vomito", " ") : t;
            foreach (Match m in padrao.Matches(alvo))
            {
                if (!NegacaoAntes.IsMatch(alvo[..m.Index]))
                {
                    sintomas.Add(sintoma);
                    break;
                }
            }
        }

        var sinais = SinaisDeAlerta.Where(s => s.Detecta(t)).Select(s => s.Nome).ToList();

        var semFalsosPositivos = NaoEhComida.Replace(t, " ");
        bool temComida = Comida.IsMatch(semFalsosPositivos);

        return new Glp1SymptomDetection(sintomas, sinais, temComida);
    }

    public static string Nome(Glp1Symptom sintoma) => sintoma switch
    {
        Glp1Symptom.Nausea => "enjoo",
        Glp1Symptom.Vomito => "vômito",
        Glp1Symptom.Constipacao => "intestino preso",
        Glp1Symptom.Diarreia => "diarreia",
        Glp1Symptom.Refluxo => "azia/refluxo",
        Glp1Symptom.SemApetite => "falta de apetite",
        _ => "sintoma"
    };

    // "Náusea!" → " nausea "; espaços nas pontas para os \b funcionarem
    private static string Normalizar(string texto)
    {
        var t = Regex.Replace(Glp1BodyDataParser.SemAcentos(texto).ToLowerInvariant(), @"[^a-z0-9 ]", " ");
        return " " + Regex.Replace(t, @"\s+", " ").Trim() + " ";
    }
}
