using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AppRecetas.Api.Models
{
    public class Ingrediente
    {
        private string _nombre = string.Empty;
        public int IdIngrediente { get; set; }
        public string Nombre { get => _nombre; set => _nombre = value ?? string.Empty; }
        public bool EsPrincipal { get; set; }

        // Navegación para la relación muchos a muchos
        [JsonIgnore]
        public ICollection<RecetaIngrediente>? RecetaIngredientes { get; set; }

        [JsonIgnore]
        public ICollection<IngredienteSinonimo>? Sinonimos { get; set; }
    }
}