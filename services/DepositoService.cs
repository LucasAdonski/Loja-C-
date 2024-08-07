using loja.data;
using loja.models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace loja.services
{
    public class DepositoService
    {
        private readonly LojaDbContext _dbContext;

        public DepositoService(LojaDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<Deposito>> GetAllDepositosAsync()
        {
            return await _dbContext.Depositos.ToListAsync();
        }

        public async Task<Deposito?> GetDepositoByIdAsync(int id)
        {
            return await _dbContext.Depositos.FindAsync(id);
        }

        public async Task AddDepositoAsync(Deposito deposito)
        {
            _dbContext.Depositos.Add(deposito);
            await _dbContext.SaveChangesAsync();
        }
        public async Task UpdateDepositoAsync(Deposito deposito)
        {
            _dbContext.Entry(deposito).State = EntityState.Modified;
            await _dbContext.SaveChangesAsync();
        }

        public async Task DeleteDepositoAsync(int id)
        {
            var deposito = await _dbContext.Depositos.FindAsync(id);
            if (deposito != null)
            {
                _dbContext.Depositos.Remove(deposito);
                await _dbContext.SaveChangesAsync();
            }
        }

        public async Task<List<dynamic>> GetProdutosNoDepositoSumarizadaAsync(int depositoId)
        {
            return await _dbContext.Produtos
                                .Where(p => p.DepositoId == depositoId)
                                .Select(p => new 
                                {
                                    p.Nome,
                                    p.Quantidade
                                })
                                .ToListAsync<dynamic>();
        }
    }
}
