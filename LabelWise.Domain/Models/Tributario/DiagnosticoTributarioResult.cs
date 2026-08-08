using System;
using System.Collections.Generic;
using System.Text;

namespace LabelWise.Domain.Models.Tributario
{
    public sealed class DiagnosticoTributarioResult
    {
        public string CorRisco { get; set; } = "";

        public string Icone { get; set; } = "";
        public string Headline { get; set; } = "";
        public int ConfiancaAnalise { get; set; }
        public int Score { get; set; }

        public string NivelRisco { get; set; } = "";

        public string Impacto { get; set; } = "";

        public string ResumoExecutivo { get; set; } = "";

        public List<string> PrincipaisRiscos { get; set; } = [];

        public List<string> Oportunidades { get; set; } = [];

        public List<string> Recomendacoes { get; set; } = [];

        public List<string> AcoesImediatas { get; set; } = [];

        public PlanoAcao PlanoAcao { get; set; } = new();

        public IndicadoresPreparacao Indicadores { get; set; } = new();

        public EconomiaPotencial Economia { get; set; } = new();

        public DateTime DataAnalise { get; set; }

        public string ModeloIA { get; set; } = "";

        public long TempoProcessamentoMs { get; set; }
    }

    public sealed class PlanoAcao
    {
        public List<string> Dias30 { get; set; } = [];

        public List<string> Dias90 { get; set; } = [];

        public List<string> Dias180 { get; set; } = [];
    }

    public sealed class IndicadoresPreparacao
    {
        public int PreparacaoFiscal { get; set; }

        public int PreparacaoTecnologica { get; set; }

        public int Compliance { get; set; }

        public int Processos { get; set; }
    }

    public sealed class EconomiaPotencial
    {
        public string Nivel { get; set; } = "";

        public string FaixaEstimada { get; set; } = "";

        public string Justificativa { get; set; } = "";
    }
}
