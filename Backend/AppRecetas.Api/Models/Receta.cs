using System;
using System.Collections.Generic;

namespace AppRecetas.Api.Models
{
    public class Receta
    {
        private string _nombre = string.Empty;
        private string _instrucciones = string.Empty;

        public int IdReceta { get; set; }
        public string Nombre { get => _nombre; set => _nombre = value ?? string.Empty; }
        public string Instrucciones { get => _instrucciones; set => _instrucciones = value ?? string.Empty; }
        public int? IdCategoria { get; set; }
        public DateTime FechaCreacion { get; set; }
        
        // (Agregado para coincidir con el requerimiento: tiempo_preparacion)
        public int TiempoPreparacion { get; set; }

        private string _imagenUrl = string.Empty;
        public string ImagenUrl { get => _imagenUrl; set => _imagenUrl = value ?? string.Empty; }

        public Categoria? Categoria { get; set; }
        
        // Navegación para la relación muchos a muchos
        public ICollection<RecetaIngrediente>? RecetaIngredientes { get; set; }
    }
}