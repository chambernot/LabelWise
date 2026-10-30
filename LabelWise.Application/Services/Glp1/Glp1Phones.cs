using LabelWise.Shared.Options;

namespace LabelWise.Application.Services.Glp1;

// Lista de telefones liberados para o Modo GLP-1 (Glp1:AllowedPhones), comparando só os dígitos
public static class Glp1Phones
{
    public static string SomenteDigitos(string? phone) => new((phone ?? string.Empty).Where(char.IsDigit).ToArray());

    public static HashSet<string> Liberados(Glp1Options options) =>
        (options.AllowedPhones ?? Array.Empty<string>())
            .Select(SomenteDigitos)
            .Where(x => x.Length > 0)
            .ToHashSet();
}
