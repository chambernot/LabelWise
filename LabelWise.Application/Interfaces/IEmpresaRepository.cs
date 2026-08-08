using LabelWise.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LabelWise.Application.Interfaces
{
    public interface IEmpresaRepository
    {
        Task InserirAsync(Empresa empresa);
        Task<IEnumerable<Empresa>> ObterPorGuestIdAsync(string guestId);

        Task<bool> AtualizarAsync(string id, Empresa empresa);
    }
}