using System.ComponentModel.DataAnnotations;

namespace AppRecetas.Api.Models
{
    public class Categoria
    {
        [Key]
        public int IdCategoria { get; set; }
        public string Nombre { get; set; } = string.Empty;
    }
}