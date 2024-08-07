using System.ComponentModel.DataAnnotations;

namespace loja.models
{
    public class Fornecedor{
        [Key]
        public int Id { get; set;}
        [Required]
        public String Cnpj { get; set;}
        [Required]
        public String Nome { get; set;}
        [Required]
        public String Endereco { get; set;}
        [Required]
        public String Email { get; set;}
        [Required]
        public String Telefone { get; set;}
    }
}