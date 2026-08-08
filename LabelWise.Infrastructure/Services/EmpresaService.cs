using LabelWise.Application.Interfaces;
using LabelWise.Domain.Entities;
using LabelWise.Infrastructure.Repositories;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LabelWise.Infrastructure.Services
{
    public class EmpresaService
    {
        private readonly IEmpresaRepository _repository;

        public EmpresaService(IEmpresaRepository repository)
        {
            _repository = repository;
        }
        public async Task<bool> AtualizarAsync(string id, Empresa empresaPayload)
        {
            // Garante que o ID do payload corresponde ao ID da rota
            empresaPayload.Id = id;

            // Chama o método correspondente do repositório que utiliza o _collection.ReplaceOneAsync
            return await _repository.AtualizarAsync(id, empresaPayload);
        }

        public async Task SalvarAsync(EmpresaRequest request)
        {
            var empresa = new Empresa
            {
                GuestId = request.GuestId,
                RazaoSocial = request.RazaoSocial,
                NomeFantasia = request.NomeFantasia,
                Cnpj = request.Cnpj,
                RegimeTributario = request.RegimeTributario,
                CnaePrincipal = request.CnaePrincipal,
                Cidade = request.Cidade,
                Uf = request.Uf,
                FaturamentoAnual = request.FaturamentoAnual,
                Situacao = request.Situacao,
                DataCadastro = DateTime.UtcNow
            };

            await _repository.InserirAsync(empresa);
        }

        public async Task<IEnumerable<Empresa>> ObterPorGuestIdAsync(string guestId)
        {
            return await _repository.ObterPorGuestIdAsync(guestId);
        }
    }
}