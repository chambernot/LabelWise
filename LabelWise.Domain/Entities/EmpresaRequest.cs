using System;
using System.Collections.Generic;
using System.Text;

namespace LabelWise.Domain.Entities
{
    public class EmpresaRequest
    {
        public string GuestId { get; set; } = string.Empty;
        public string RazaoSocial { get; set; } = string.Empty;
        public string NomeFantasia { get; set; } = string.Empty;
        public string Cnpj { get; set; } = string.Empty;
        public string RegimeTributario { get; set; } = string.Empty;
        public string CnaePrincipal { get; set; } = string.Empty;
        public string Cidade { get; set; } = string.Empty;
        public string Uf { get; set; } = string.Empty;
        public decimal FaturamentoAnual { get; set; }
        public string Situacao { get; set; } = string.Empty;
    }
}
