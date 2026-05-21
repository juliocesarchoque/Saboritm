namespace AppRecetas.Api.Models
{
    public class RecetaIngrediente
    {
        public int IdReceta { get; set; }
        public int IdIngrediente { get; set; }
        public string Cantidad { get; set; } = string.Empty;

        public Receta? Receta { get; set; }
        public Ingrediente? Ingrediente { get; set; }
    }
}