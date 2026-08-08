using System;
using System.Collections.Generic;
using System.Text;

namespace LabelWise.Domain.Models.Tributario
{
    public sealed class EmpresaDiagnosticoRequest
    {
        public Guid Id { get; set; }

        public string? RazaoSocial { get; set; }

        public string? NomeFantasia { get; set; }

        public string? Cnpj { get; set; }

        public string? RegimeTributario { get; set; }

        public string? CnaePrincipal { get; set; }

        public IEnumerable<string> CnaesSecundarios { get; set; } = [];

        public string? Cidade { get; set; }

        public string? Uf { get; set; }

        public decimal FaturamentoAnual { get; set; }

        public string? Situacao { get; set; }

        public string? Observacoes { get; set; }
    }
}
