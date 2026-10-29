using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace LabelWise.Application.Services.Glp1;

public sealed record Glp1BodyData(decimal? PesoKg, decimal? AlturaCm, string? Sexo)
{
    public bool Vazio => PesoKg == null && AlturaCm == null && Sexo == null;
}

// Lê peso, altura e sexo de uma mensagem livre ("72 kg, 1,65 m, feminino").
// Com unidade (kg, m, cm) o valor sempre é aceito. Número sem unidade só é aceito quando falta exatamente
// um dado numérico (ex.: o bot perguntou só a altura e a pessoa respondeu "165"), para não adivinhar.
public static class Glp1BodyDataParser
{
    public const string Feminino = "Feminino";
    public const string Masculino = "Masculino";

    private const decimal PesoMinKg = 30, PesoMaxKg = 300;
    private const decimal AlturaMinCm = 120, AlturaMaxCm = 230;

    public static Glp1BodyData Ler(string? texto, bool faltaPeso, bool faltaAltura, bool faltaSexo)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return new Glp1BodyData(null, null, null);
        }

        // "1,65" → "1.65"; sem acentos; minúsculas
        var t = Regex.Replace(SemAcentos(texto).ToLowerInvariant(), @"(\d),(\d)", "$1.$2");

        decimal? peso = null, altura = null;
        string? sexo = null;

        var mPeso = Regex.Match(t, @"(\d+(?:\.\d+)?)\s*(?:kg|kgs|quilos?|kilos?)\b");
        if (mPeso.Success)
        {
            peso = Dentro(Numero(mPeso.Groups[1].Value), PesoMinKg, PesoMaxKg);
            t = t.Remove(mPeso.Index, mPeso.Length).Insert(mPeso.Index, " ");
        }

        // "1m65" / "1 m 65"
        var mAltura = Regex.Match(t, @"\b(\d)\s*m\s*(\d{2})\b");
        if (mAltura.Success)
        {
            altura = Dentro(Numero(mAltura.Groups[1].Value) * 100 + Numero(mAltura.Groups[2].Value), AlturaMinCm, AlturaMaxCm);
        }
        else
        {
            mAltura = Regex.Match(t, @"(\d+(?:\.\d+)?)\s*(?:cm|centimetros?)\b");
            if (mAltura.Success)
            {
                altura = Dentro(Numero(mAltura.Groups[1].Value), AlturaMinCm, AlturaMaxCm);
            }
            else
            {
                mAltura = Regex.Match(t, @"(\d(?:\.\d{1,2})?)\s*(?:m|mt|mts|metros?)\b");
                if (mAltura.Success)
                {
                    altura = Dentro(Numero(mAltura.Groups[1].Value) * 100, AlturaMinCm, AlturaMaxCm);
                }
            }
        }
        if (mAltura.Success)
        {
            t = t.Remove(mAltura.Index, mAltura.Length).Insert(mAltura.Index, " ");
        }

        if (Regex.IsMatch(t, @"\b(feminino|feminina|fem|mulher)\b"))
        {
            sexo = Feminino;
        }
        else if (Regex.IsMatch(t, @"\b(masculino|masc|homem)\b"))
        {
            sexo = Masculino;
        }
        else if (faltaSexo && Regex.IsMatch(t.Trim(), @"^(f|m)$"))
        {
            sexo = t.Trim() == "f" ? Feminino : Masculino;
        }

        // Números sem unidade que sobraram
        var soltos = Regex.Matches(t, @"\d+(?:\.\d+)?").Select(m => Numero(m.Value)).ToList();
        foreach (var valor in soltos)
        {
            // Decimal entre 1,20 e 2,30 só pode ser altura em metros
            if (altura == null && faltaAltura && valor % 1 != 0 && valor >= 1.2m && valor <= 2.3m)
            {
                altura = valor * 100;
                continue;
            }

            bool pesoAindaFalta = faltaPeso && peso == null;
            bool alturaAindaFalta = faltaAltura && altura == null;

            if (pesoAindaFalta && !alturaAindaFalta)
            {
                peso = Dentro(valor, PesoMinKg, PesoMaxKg);
            }
            else if (alturaAindaFalta && !pesoAindaFalta)
            {
                altura = Dentro(valor < 3 ? valor * 100 : valor, AlturaMinCm, AlturaMaxCm);
            }
            // Faltando os dois, um número solto é ambíguo: não usa
        }

        return new Glp1BodyData(peso, altura, sexo);
    }

    private static decimal Numero(string valor) => decimal.Parse(valor, CultureInfo.InvariantCulture);

    private static decimal? Dentro(decimal valor, decimal min, decimal max) => valor >= min && valor <= max ? valor : null;

    public static string SemAcentos(string texto)
    {
        var decomposto = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposto.Length);
        foreach (var c in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
