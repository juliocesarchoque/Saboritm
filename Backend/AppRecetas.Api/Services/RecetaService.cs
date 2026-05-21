using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using AppRecetas.Api.Data;
using AppRecetas.Api.DTOs;
using Dapper;
using Microsoft.EntityFrameworkCore;

namespace AppRecetas.Api.Services
{
    public interface IRecetaService
    {
        Task<IEnumerable<RecetaMatchResponse>> GetRecetasPorIngredientes(List<int> ingredientesSeleccionados);
    }

    public class RecetaService : IRecetaService
    {
        private readonly AppRecetasDbContext _context;

        public RecetaService(AppRecetasDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<RecetaMatchResponse>> GetRecetasPorIngredientes(List<int> ingredientesSeleccionados)
        {
            if (ingredientesSeleccionados == null || !ingredientesSeleccionados.Any())
                return Enumerable.Empty<RecetaMatchResponse>();

            // Extraemos la conexión de PostgreSQL nativa que ya maneja Entity Framework
            var dbConnection = _context.Database.GetDbConnection();

            // Usamos nombres en minúsculas para coincidir con el script SQL (tables.sql) en PostgreSQL
            const string sql = @"
                SELECT 
                    r.id_receta AS RecetaId, 
                    r.nombre AS RecetaNombre, 
                    r.instrucciones AS Instrucciones, 
                    r.imagen_url AS ImagenUrl,
                    COALESCE(r.tiempo_preparacion, 0) AS TiempoPreparacion,
                    COUNT(ri.id_ingrediente) AS TotalIngredientes,
                    
                    COUNT(CASE WHEN ri.id_ingrediente = ANY(@Ids) THEN 1 END) AS IngredientesQueTengo,
                    
                    ROUND(
                        (COUNT(CASE WHEN ri.id_ingrediente = ANY(@Ids) THEN 1 END) * 100.0) 
                        / COUNT(ri.id_ingrediente)
                    , 2) AS PorcentajeMatch,
                    
                    COALESCE(
                        ARRAY_AGG(i.nombre) FILTER (WHERE NOT (ri.id_ingrediente = ANY(@Ids))), 
                        '{}'
                    ) AS IngredientesFaltantes
                    
                FROM recetas r
                JOIN receta_ingredientes ri ON r.id_receta = ri.id_receta
                JOIN ingredientes i ON ri.id_ingrediente = i.id_ingrediente
                GROUP BY 
                    r.id_receta, r.nombre, r.instrucciones, r.tiempo_preparacion, r.imagen_url
                HAVING 
                    COUNT(CASE WHEN ri.id_ingrediente = ANY(@Ids) THEN 1 END) > 0
                ORDER BY 
                    PorcentajeMatch DESC, r.nombre ASC;";

            bool closeConnection = false;
            
            // Administramos correctamente la apertura de la conexión si EF Core no la tiene abierta
            if (dbConnection.State == ConnectionState.Closed)
            {
                await dbConnection.OpenAsync();
                closeConnection = true;
            }

            try
            {
                // Npgsql y Dapper se encargan del casteo C# List<int> a INT[] de PostgreSQL
                var parameters = new { Ids = ingredientesSeleccionados.ToArray() };
                return await dbConnection.QueryAsync<RecetaMatchResponse>(sql, parameters);
            }
            finally
            {
                if (closeConnection)
                    await dbConnection.CloseAsync();
            }
        }
    }
}
