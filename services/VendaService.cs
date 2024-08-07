using loja.data;
using loja.models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace loja.services
{
    public class VendaService
    {
        private readonly LojaDbContext _dbContext;

        public VendaService(LojaDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<Venda>> GetAllVendasAsync()
        {
            return await _dbContext.Vendas
                                 .Include(v => v.Produto)
                                 .Include(v => v.Produto.Deposito)
                                 .Include(v => v.Cliente)
                                 .ToListAsync();
        }

        public async Task<Venda> GetVendaByIdAsync(int id)
        {
            return await _dbContext.Vendas
                                 .Include(v => v.Produto)
                                 .Include(v => v.Produto.Deposito)
                                 .Include(v => v.Cliente)
                                 .FirstOrDefaultAsync(v => v.Id == id);
        }

        public async Task AddVendaAsync(Venda venda)
        {
            var cliente = await _dbContext.Clientes.FirstOrDefaultAsync(c => c.Id == venda.ClienteId);
            if (cliente == null)
            {
                throw new InvalidOperationException("Cliente não encontrado.");
            }
            // Verificar se o produto existe e incluir o depósito
            var produto = await _dbContext.Produtos.Include(p => p.Deposito).FirstOrDefaultAsync(p => p.Id == venda.ProdutoId);
            if (produto == null)
            {
                throw new InvalidOperationException("Produto não encontrado.");
            }

            // Verificar se a quantidade disponível do produto é suficiente
            if (produto.Quantidade < venda.Quantidade)
            {
                throw new InvalidOperationException("Quantidade insuficiente no depósito.");
            }

            // Subtrair a quantidade do produto
            produto.Quantidade -= venda.Quantidade;

            // Adicionar a venda
            _dbContext.Vendas.Add(venda);
            _dbContext.Entry(venda).Reference(v => v.Cliente).Load();

            // Salvar as mudanças
            await _dbContext.SaveChangesAsync();
        }

        public async Task UpdateVendaAsync(Venda venda)
        {
            _dbContext.Entry(venda).State = EntityState.Modified;
            await _dbContext.SaveChangesAsync();
        }

        public async Task DeleteVendaAsync(int id)
        {
            var venda = await _dbContext.Vendas
                .Include(v => v.Produto)
                .Include(v => v.Cliente)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (venda != null)
            {
                // Adiciona a quantidade vendida de volta ao estoque
                venda.Produto.Quantidade += venda.Quantidade;

                // Remove a venda do contexto
                _dbContext.Vendas.Remove(venda);

                // Salva as mudanças
                await _dbContext.SaveChangesAsync();
            }
        }

        public async Task<List<dynamic>> GetVendasPorProdutoDetalhadasAsync(int produtoId)
        {
            return await _dbContext.Vendas
                                .Include(v => v.Cliente)
                                .Include(v => v.Produto)
                                .Where(v => v.ProdutoId == produtoId)
                                .Select(v => new {
                                    v.Produto.Nome,
                                    v.DataVenda,
                                    IdVenda = v.Id,
                                    ClienteNome = v.Cliente.Nome,
                                    v.Quantidade,
                                    v.PrecoUnitario
                                })
                                .ToListAsync<dynamic>();
        }

        public async Task<object> GetVendasPorProdutoSumarizadasAsync(int produtoId)
        {
            return await _dbContext.Vendas
                                .Where(v => v.ProdutoId == produtoId)
                                .GroupBy(v => new { v.ProdutoId, v.Produto.Nome })
                                .Select(g => new
                                {
                                    ProdutoId = g.Key.ProdutoId,
                                    ProdutoNome = g.Key.Nome,
                                    TotalQuantidadeVendida = g.Sum(v => v.Quantidade),
                                    TotalPrecoCobrado = g.Sum(v => v.PrecoUnitario * v.Quantidade)
                                })
                                .FirstOrDefaultAsync();
        }
        public async Task<List<dynamic>> GetVendasPorClienteDetalhadasAsync(int clienteId)
        {
            return await _dbContext.Vendas
                                .Where(v => v.ClienteId == clienteId)
                                .Include(v => v.Produto) // Inclui o produto para obter o nome do produto
                                .Select(v => new 
                                {
                                    v.Produto.Nome,
                                    DataVenda = v.DataVenda,
                                    IdVenda = v.Id,
                                    v.Quantidade,
                                    v.PrecoUnitario
                                })
                                .ToListAsync<dynamic>();
        }

        public async Task<object> GetVendasPorClienteSumarizadasAsync(int clienteId)
        {
            return await _dbContext.Vendas
                                    .Where(v => v.ClienteId == clienteId)
                                    .GroupBy(v => new { v.ProdutoId, v.Produto.Nome })
                                    .Select(g => new
                                    {
                                        ProdutoId = g.Key.ProdutoId,
                                        ProdutoNome = g.Key.Nome,
                                        TotalQuantidadeVendida = g.Sum(v => v.Quantidade),
                                        TotalPrecoCobrado = g.Sum(v => v.PrecoUnitario * v.Quantidade)
                                    })
                                    .ToListAsync();
        }
    }
}
