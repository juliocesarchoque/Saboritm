using System.Net;
using System.Text.Json;

namespace AppRecetas.Api.Middleware
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;
        private readonly IHostEnvironment _env;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                var statusCode = (int)HttpStatusCode.InternalServerError;
                var message = "Algo salió mal, intenta más tarde";
                string? details = null;

                if (ex is BusinessException businessEx)
                {
                    // Errores controlados de lógica de negocio
                    statusCode = businessEx.StatusCode;
                    message = businessEx.Message;
                    _logger.LogWarning("[NEGOCIO] {Message}", message);
                }
                else
                {
                    // Errores no controlados del sistema
                    _logger.LogError(ex, "[ERROR NO CONTROLADO] {Message}", ex.Message);
                    if (_env.IsDevelopment())
                    {
                        message = ex.Message;
                        details = ex.StackTrace?.ToString();
                    }
                }

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = statusCode;

                var response = new ApiException(statusCode, message, details);
                var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                var json = JsonSerializer.Serialize(response, options);

                await context.Response.WriteAsync(json);
            }
        }
    }

    public class ApiException
    {
        public ApiException(int statusCode, string message, string? details = null)
        {
            StatusCode = statusCode;
            Message = message;
            Details = details;
        }

        public int StatusCode { get; set; }
        public string Message { get; set; }
        public string? Details { get; set; }
    }
}
