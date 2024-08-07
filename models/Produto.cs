using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace loja.models
{
    public class Produto{
        [Key]
        public int Id { get; set; }
        public String Nome { get; set; }
        public Double Preco { get; set; }
        public int Quantidade { get; set; }
        public int DepositoId { get; set; }
        public Deposito Deposito { get; set; }
    }
}