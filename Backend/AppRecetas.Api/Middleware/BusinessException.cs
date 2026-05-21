namespace AppRecetas.Api.Middleware
{
    /// <summary>
    /// Excepción personalizada para errores de reglas de negocio controlados.
    /// Estos errores no son fallos del sistema, sino validaciones de lógica (ej: "Nombre duplicado").
    /// </summary>
    public class BusinessException : Exception
    {
        public int StatusCode { get; }

        public BusinessException(string message, int statusCode = 400) : base(message)
        {
            StatusCode = statusCode;
        }
    }
}
