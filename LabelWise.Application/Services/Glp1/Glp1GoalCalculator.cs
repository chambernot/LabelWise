using LabelWise.Shared.Options;

namespace LabelWise.Application.Services.Glp1;

public sealed record Glp1Goals(int ProteinTargetG, int CalorieFloorKcal, int WaterTargetMl);

// Metas do Modo GLP-1, sem IA e sem acesso a banco:
// proteína = peso × ProteinGPerKg, limitada entre ProteinMinG e ProteinMaxG; piso calórico fixo; água = peso × WaterMlPerKg.
public class Glp1GoalCalculator
{
    private readonly Glp1Options _options;

    public Glp1GoalCalculator(Glp1Options options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public Glp1Goals Calcular(decimal pesoKg)
    {
        if (pesoKg <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pesoKg), "O peso precisa ser maior que zero.");
        }

        var proteina = (int)Math.Round(pesoKg * _options.ProteinGPerKg, MidpointRounding.AwayFromZero);
        proteina = Math.Clamp(proteina, _options.ProteinMinG, _options.ProteinMaxG);

        var agua = (int)Math.Round(pesoKg * _options.WaterMlPerKg, MidpointRounding.AwayFromZero);

        return new Glp1Goals(proteina, _options.CalorieFloorKcal, agua);
    }
}
