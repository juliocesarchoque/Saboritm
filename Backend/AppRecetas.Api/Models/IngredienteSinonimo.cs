namespace AppRecetas.Api.Models
{
    public class IngredienteSinonimo
    {
        public int IdSinonimo { get; set; }
        public int? IdIngrediente { get; set; }
        public string TerminoBusqueda { get; set; } = string.Empty;

        public Ingrediente? Ingrediente { get; set; }
    }
}
