using Microsoft.AspNetCore.Mvc;
using AppRecetas.Api.Services;
using AppRecetas.Api.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace AppRecetas.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class RecetasController : ControllerBase
    {
        private readonly IRecetaService _recetaService;
        private readonly AppRecetas.Api.Data.AppRecetasDbContext _context;

        public RecetasController(IRecetaService recetaService, AppRecetas.Api.Data.AppRecetasDbContext context)
        {
            _recetaService = recetaService;
            _context = context;
        }

        [HttpGet]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public async Task<ActionResult<IEnumerable<Receta>>> GetRecetas()
        {
            return await _context.Recetas.Include(r => r.Categoria).ToListAsync();
        }

        [HttpGet("{id}")]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public async Task<ActionResult<object>> GetReceta(int id)
        {
            var receta = await _context.Recetas
                .Include(r => r.Categoria)
                .Include(r => r.RecetaIngredientes!)
                    .ThenInclude(ri => ri.Ingrediente)
                .FirstOrDefaultAsync(r => r.IdReceta == id);

            if (receta == null) return NotFound();

            return new {
                idReceta = receta.IdReceta,
                nombre = receta.Nombre,
                instrucciones = receta.Instrucciones,
                idCategoria = receta.IdCategoria,
                imagenUrl = receta.ImagenUrl,
                fechaCreacion = receta.FechaCreacion,
                tiempoPreparacion = receta.TiempoPreparacion,
                categoria = receta.Categoria != null ? new { nombre = receta.Categoria.Nombre } : null,
                recetaIngredientes = (receta.RecetaIngredientes ?? new List<RecetaIngrediente>()).Select(ri => new {
                    idIngrediente = ri.IdIngrediente,
                    cantidad = ri.Cantidad,
                    nombre = ri.Ingrediente?.Nombre ?? "Ingrediente Desconocido"
                }).ToList()
            };
        }

        [HttpPost]
        public async Task<ActionResult<Receta>> PostReceta(Receta receta)
        {
            _context.Recetas.Add(receta);
            await _context.SaveChangesAsync();

            var ingredientes = receta.RecetaIngredientes ?? new List<RecetaIngrediente>();
            foreach (var ri in ingredientes)
            {
                var nuevoRi = new RecetaIngrediente
                {
                    IdReceta = receta.IdReceta,
                    IdIngrediente = ri.IdIngrediente,
                    Cantidad = ri.Cantidad
                };
                _context.RecetaIngredientes.Add(nuevoRi);
            }
            if (ingredientes.Any())
                await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetReceta), new { id = receta.IdReceta }, receta);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutReceta(int id, Receta receta)
        {
            if (receta == null) return BadRequest("Payload nulo");

            if (id != receta.IdReceta)
            {
                return BadRequest(new { message = "ID mismatch" });
            }

            var existingReceta = await _context.Recetas
                .Include(r => r.RecetaIngredientes)
                .FirstOrDefaultAsync(r => r.IdReceta == id);

            if (existingReceta == null) return NotFound();

            try 
            {
                existingReceta.Nombre = receta.Nombre;
                existingReceta.Instrucciones = receta.Instrucciones;
                existingReceta.IdCategoria = receta.IdCategoria;
                existingReceta.TiempoPreparacion = receta.TiempoPreparacion;
                
                if (!string.IsNullOrEmpty(receta.ImagenUrl))
                {
                    existingReceta.ImagenUrl = receta.ImagenUrl;
                }

                if (receta.RecetaIngredientes != null)
                {
                    if (existingReceta.RecetaIngredientes != null && existingReceta.RecetaIngredientes.Any())
                    {
                        _context.RecetaIngredientes.RemoveRange(existingReceta.RecetaIngredientes);
                        await _context.SaveChangesAsync();
                    }

                    var ingredientesUnicos = receta.RecetaIngredientes
                        .GroupBy(ri => ri.IdIngrediente)
                        .Select(g => g.First())
                        .ToList();

                    foreach (var ri in ingredientesUnicos)
                    {
                        var nuevoRi = new RecetaIngrediente {
                            IdReceta = id,
                            IdIngrediente = ri.IdIngrediente,
                            Cantidad = ri.Cantidad ?? string.Empty
                        };
                        _context.RecetaIngredientes.Add(nuevoRi);
                    }
                    await _context.SaveChangesAsync();
                }

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR FATAL] Error actualizando receta {id}: {ex.Message}");
                return StatusCode(500, new { message = "Error interno del servidor", details = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReceta(int id)
        {
            var receta = await _context.Recetas.FindAsync(id);
            if (receta == null) return NotFound();

            var ri = _context.RecetaIngredientes.Where(x => x.IdReceta == id);
            _context.RecetaIngredientes.RemoveRange(ri);
            _context.Recetas.Remove(receta);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private static string NormalizarTexto(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return string.Empty;
            
            var normalizedString = texto.Normalize(System.Text.NormalizationForm.FormD);
            var stringBuilder = new System.Text.StringBuilder(capacity: normalizedString.Length);

            foreach (var c in normalizedString)
            {
                var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(System.Text.NormalizationForm.FormC).ToLowerInvariant();
        }

        [HttpGet("match")]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public async Task<ActionResult<IEnumerable<DTOs.RecetaMatchResponse>>> GetRecetasByIngredientes([FromQuery] string ingredientes)
        {
            if (string.IsNullOrEmpty(ingredientes)) return BadRequest("Debe proporcionar ingredientes.");

            var listaBuscada = ingredientes.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                              .Select(i => NormalizarTexto(i.Trim()))
                                              .ToList();

            var recetasDb = await _context.Recetas
                .Include(r => r.Categoria)
                .Include(r => r.RecetaIngredientes!)
                    .ThenInclude(ri => ri.Ingrediente)
                        .ThenInclude(i => i!.Sinonimos)
                .ToListAsync();

            var recetasMatch = recetasDb.Where(r => 
                r.RecetaIngredientes!.Any(ri => 
                    listaBuscada.Contains(NormalizarTexto(ri.Ingrediente!.Nombre)) ||
                    (ri.Ingrediente.Sinonimos != null && ri.Ingrediente.Sinonimos.Any(s => listaBuscada.Contains(NormalizarTexto(s.TerminoBusqueda))))
                )
            ).ToList();

            var response = recetasMatch.Select(r => {
                var ingEncontrados = r.RecetaIngredientes!
                    .Where(ri => 
                        listaBuscada.Contains(NormalizarTexto(ri.Ingrediente!.Nombre)) ||
                        (ri.Ingrediente.Sinonimos != null && ri.Ingrediente.Sinonimos.Any(s => listaBuscada.Contains(NormalizarTexto(s.TerminoBusqueda))))
                    )
                    .Select(ri => ri.Ingrediente!.Nombre)
                    .ToList();

                var faltantes = r.RecetaIngredientes!
                    .Where(ri => 
                        !listaBuscada.Contains(NormalizarTexto(ri.Ingrediente!.Nombre)) &&
                        !(ri.Ingrediente.Sinonimos != null && ri.Ingrediente.Sinonimos.Any(s => listaBuscada.Contains(NormalizarTexto(s.TerminoBusqueda))))
                    )
                    .Select(ri => ri.Ingrediente!.Nombre)
                    .ToArray();

                return new DTOs.RecetaMatchResponse {
                    RecetaId = r.IdReceta,
                    RecetaNombre = r.Nombre,
                    ImagenUrl = r.ImagenUrl ?? string.Empty,
                    PorcentajeMatch = (double)ingEncontrados.Count / (ingEncontrados.Count + faltantes.Length) * 100,
                    IngredientesFaltantes = faltantes,
                    TotalIngredientes = ingEncontrados.Count + faltantes.Length,
                    IngredientesQueTengo = ingEncontrados.Count
                };
            }).OrderByDescending(r => r.PorcentajeMatch).ToList();

            return Ok(response);
        }
    }
}