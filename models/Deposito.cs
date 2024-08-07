using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace loja.models
{
    public class Deposito{
        [Key]
        public int Id { get; set; }
        public string Nome { get; set; }
    }
}