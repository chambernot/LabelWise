using System;
using System.Collections.Generic;
using System.Text;

namespace LabelWise.Domain.Entities
{
    public class LeadRequest
    {
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Telefone { get; set; } = string.Empty;
        public string GuestId { get; set; } = string.Empty; // Identificador anônimo do browser
    }
}
