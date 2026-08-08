using LabelWise.Domain.Models.Tributario;
using System;
using System.Collections.Generic;
using System.Text;

namespace LabelWise.Application.Interfaces
{
    public interface IOpenAIDiagnosticoTributarioService
    {
        Task<DiagnosticoTributarioResult?> AnalyzeAsync(
            EmpresaDiagnosticoRequest request,
            CancellationToken cancellationToken = default);
    }
}
