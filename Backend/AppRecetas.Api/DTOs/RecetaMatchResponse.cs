using System.Collections.Generic;

namespace AppRecetas.Api.DTOs
{
    public class RecetaMatchResponse
    {
        public int RecetaId { get; set; }
        public string RecetaNombre { get; set; } = string.Empty;
        public string Instrucciones { get; set; } = string.Empty;
        public string ImagenUrl { get; set; } = string.Empty;
        public int TiempoPreparacion { get; set; }
        
        public int TotalIngredientes { get; set; }
        public int IngredientesQueTengo { get; set; }
        public double PorcentajeMatch { get; set; }
        
        // Postgres Mapper devolverá fácilmente los arrays a [] de C#
        public string[] IngredientesFaltantes { get; set; } = System.Array.Empty<string>();
        
        // Propiedad calculada para facilitarte el coloreado de tus tarjetas en Bootstrap
        public string Clasificacion => PorcentajeMatch >= 100 
            ? "Listas para cocinar" 
            : "Sugerencias incompletas";
    }
}
