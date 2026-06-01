using Microsoft.AspNetCore.Mvc;
using AppRecetas.Api.Data;
using System;
using System.Threading.Tasks;

namespace AppRecetas.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public class HealthController : ControllerBase
    {
        private readonly AppRecetasDbContext _context;

        public HealthController(AppRecetasDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> CheckHealth()
        {
            try
            {
                // CanConnectAsync realiza una consulta rápida (SELECT 1 o similar) para validar la conexión con Supabase
                var canConnect = await _context.Database.CanConnectAsync();
                
                if (canConnect)
                {
                    return Ok(new 
                    { 
                        status = "Healthy", 
                        database = "Connected", 
                        timestamp = DateTime.UtcNow 
                    });
                }
                
                return StatusCode(500, new 
                { 
                    status = "Unhealthy", 
                    database = "Disconnected", 
                    timestamp = DateTime.UtcNow 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new 
                { 
                    status = "Unhealthy", 
                    error = ex.Message, 
                    timestamp = DateTime.UtcNow 
                });
            }
        }
    }
}
