using LabelWise.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace LabelWise.Application.Interfaces
{
    public interface ILeadRepository
    {
        Task InserirAsync(Lead lead);

        Task<bool> ExisteEmailAsync(string email);
    }
}
