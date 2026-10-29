using LabelWise.Application.Services.Glp1;

namespace LabelWise.NutricaoFacil.Tests.Glp1;

public class Glp1BodyDataParserTests
{
    [Theory]
    [InlineData("72 kg, 1,65 m, feminino", 72, 165, "Feminino")]
    [InlineData("72kg 165cm feminino", 72, 165, "Feminino")]
    [InlineData("Peso 80,5 kg altura 1.80 m sexo masculino", 80.5, 180, "Masculino")]
    [InlineData("1m70, 90 quilos, homem", 90, 170, "Masculino")]
    [InlineData("sou mulher, 1,58 m e 64 kg", 64, 158, "Feminino")]
    public void MensagemCompleta_LeTudo(string texto, double peso, double altura, string sexo)
    {
        var lido = Glp1BodyDataParser.Ler(texto, true, true, true);

        Assert.Equal((decimal)peso, lido.PesoKg);
        Assert.Equal((decimal)altura, lido.AlturaCm);
        Assert.Equal(sexo, lido.Sexo);
    }

    [Fact]
    public void NumeroSemUnidade_FaltandoPesoEAltura_NaoAdivinha()
    {
        var lido = Glp1BodyDataParser.Ler("72", true, true, true);
        Assert.True(lido.Vazio);
    }

    [Fact]
    public void AlturaEmMetrosSemUnidade_EhReconhecida()
    {
        var lido = Glp1BodyDataParser.Ler("72 kg 1,65", true, true, true);
        Assert.Equal(72m, lido.PesoKg);
        Assert.Equal(165m, lido.AlturaCm);
    }

    [Theory]
    [InlineData("165", 165)]
    [InlineData("1,65", 165)]
    public void SoFaltaAltura_AceitaNumeroSemUnidade(string texto, double altura)
    {
        var lido = Glp1BodyDataParser.Ler(texto, faltaPeso: false, faltaAltura: true, faltaSexo: false);
        Assert.Equal((decimal)altura, lido.AlturaCm);
    }

    [Fact]
    public void SoFaltaSexo_AceitaLetra()
    {
        Assert.Equal("Feminino", Glp1BodyDataParser.Ler("F", false, false, true).Sexo);
        Assert.Equal("Masculino", Glp1BodyDataParser.Ler("m", false, false, true).Sexo);
    }

    [Theory]
    [InlineData("5 kg")]
    [InlineData("500 kg")]
    [InlineData("3 m")]
    public void ValoresForaDaFaixa_SaoIgnorados(string texto)
    {
        Assert.True(Glp1BodyDataParser.Ler(texto, true, true, true).Vazio);
    }
}
