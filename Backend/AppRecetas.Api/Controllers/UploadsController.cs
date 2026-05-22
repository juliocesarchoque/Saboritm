using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AppRecetas.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class UploadsController : ControllerBase
    {
        private readonly Supabase.Client _supabase;
        private readonly IWebHostEnvironment _env;

        public UploadsController(Supabase.Client supabase, IWebHostEnvironment env)
        {
            _supabase = supabase;
            _env = env;
        }

        [HttpPost("image")]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Ningún archivo seleccionado." });

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (Array.IndexOf(allowedExtensions, extension) < 0)
                return BadRequest(new { message = "Formato de imagen no permitido." });

            var uniqueFileName = Guid.NewGuid().ToString() + extension;

            // Lógica de Producción (Supabase Storage) vs Desarrollo (Local)
            if (!_env.IsDevelopment())
            {
                try
                {
                    using var stream = new MemoryStream();
                    await file.CopyToAsync(stream);
                    var bytes = stream.ToArray();

                    // El bucket debe llamarse 'recetas' y ser público
                    var bucket = "recetas";
                    await _supabase.Storage.From(bucket).Upload(bytes, uniqueFileName);

                    // Obtener la URL pública
                    var publicUrl = _supabase.Storage.From(bucket).GetPublicUrl(uniqueFileName);
                    return Ok(new { url = publicUrl });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { message = "Error al subir a Supabase Storage", details = ex.Message });
                }
            }
            else
            {
                // Desarrollo: Guardar localmente en wwwroot/uploads
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var relativeUrl = $"/uploads/{uniqueFileName}";
                return Ok(new { url = relativeUrl });
            }
        }

        [HttpPost("migrate-local-images")]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public async Task<IActionResult> MigrateLocalImages()
        {
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(uploadsFolder))
            {
                return NotFound(new { message = "El directorio local 'wwwroot/uploads' no existe." });
            }

            var files = Directory.GetFiles(uploadsFolder);
            var uploaded = new System.Collections.Generic.List<string>();
            var errors = new System.Collections.Generic.List<string>();
            var bucket = "recetas";

            foreach (var filePath in files)
            {
                var fileName = Path.GetFileName(filePath);
                try
                {
                    var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
                    // Subir a Supabase Storage con Upsert = true para evitar duplicaciones
                    await _supabase.Storage.From(bucket).Upload(
                        bytes, 
                        fileName, 
                        new Supabase.Storage.FileOptions { Upsert = true }
                    );
                    uploaded.Add(fileName);
                }
                catch (Exception ex)
                {
                    errors.Add($"Error al subir {fileName}: {ex.Message}");
                }
            }

            return Ok(new { 
                message = "Migración completada", 
                totalFiles = files.Length,
                uploadedCount = uploaded.Count, 
                failedCount = errors.Count,
                uploadedFiles = uploaded,
                errors = errors
            });
        }
    }
}
