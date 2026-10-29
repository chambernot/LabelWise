using LabelWise.Application.Services.Glp1;
using LabelWise.Shared.Options;

namespace LabelWise.NutricaoFacil.Tests.Glp1;

public class Glp1GoalCalculatorTests
{
    private readonly Glp1GoalCalculator _calculator = new(new Glp1Options());

    [Fact]
    public void Peso50Kg_ProteinaSobeParaOMinimo()
    {
        var metas = _calculator.Calcular(50m);

        Assert.Equal(80, metas.ProteinTargetG); // 50 × 1,2 = 60 → mínimo de 80 g
        Assert.Equal(1200, metas.CalorieFloorKcal);
        Assert.Equal(1750, metas.WaterTargetMl); // 50 × 35
    }

    [Fact]
    public void Peso72Kg_ProteinaDentroDaFaixa()
    {
        var metas = _calculator.Calcular(72m);

        Assert.Equal(86, metas.ProteinTargetG); // 72 × 1,2 = 86,4
        Assert.Equal(1200, metas.CalorieFloorKcal);
        Assert.Equal(2520, metas.WaterTargetMl); // 72 × 35
    }

    [Fact]
    public void Peso130Kg_ProteinaLimitadaAoMaximo()
    {
        var metas = _calculator.Calcular(130m);

        Assert.Equal(120, metas.ProteinTargetG); // 130 × 1,2 = 156 → máximo de 120 g
        Assert.Equal(1200, metas.CalorieFloorKcal);
        Assert.Equal(4550, metas.WaterTargetMl); // 130 × 35
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void PesoInvalido_LancaExcecao(decimal peso)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.Calcular(peso));
    }
}
