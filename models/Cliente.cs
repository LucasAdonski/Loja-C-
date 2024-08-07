using System.ComponentModel.DataAnnotations;

namespace loja.models
{
    public class Cliente
    {
        [Key]
        public int Id { get; set;}
        [Required]

        public String Nome { get; set;}
        [Required]
        public String Cpf { get; set;}
        [Required]

        public String Email { get; set;}
    }
}