using AppRecetas.Api.Data;
using AppRecetas.Api.Services;
using AppRecetas.Api.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

// Configurar Logs de consola detallados
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Add services to the container.
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = string.Join(" | ", context.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage));
            Console.WriteLine($"[VALIDACION FALLIDA] {errors}");
            return new BadRequestObjectResult(new { message = "Error de validación", details = errors });
        };
    })
    .AddJsonOptions(options =>
    {
        // Ignorar ciclos para evitar errores de referencias circulares con EF Core
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();

// Configure CORS for frontend access
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins",
        corsBuilder =>
        {
            corsBuilder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader();
        });
});

// Configure PostgreSQL connection (Forced to ensure project-specific Username)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Validamos que la cadena de conexión exista
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");
}
// Log de depuración para verificar el usuario en consola (sin mostrar password completo)
Console.WriteLine($"[DB DEBUG] Usando ConnectionString con Usuario: {connectionString.Split(';').FirstOrDefault(x => x.Contains("Username"))}");

builder.Services.AddDbContext<AppRecetasDbContext>(options =>
{
    options.UseNpgsql(connectionString, npgsqlOptions => 
    {
        npgsqlOptions.CommandTimeout(30);
    });
    
    // Solo habilitar errores detallados en desarrollo sin loguear datos sensibles (para velocidad)
    if (builder.Environment.IsDevelopment())
    {
        options.EnableDetailedErrors();
    }
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var supabaseUrl = builder.Configuration["Supabase:Url"];
        options.Authority = $"{supabaseUrl}/auth/v1";
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidIssuer = $"{supabaseUrl}/auth/v1",
            ValidateAudience = true,
            ValidAudience = "authenticated",
            ValidateLifetime = true,
            ClockSkew = System.TimeSpan.Zero
        };
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"[AUTH FALLIDA] {context.Exception.Message}");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Configure Supabase Client (for Storage and API access)
builder.Services.AddScoped<Supabase.Client>(_ => 
{
    var url = builder.Configuration["Supabase:Url"];
    var anonKey = builder.Configuration["Supabase:AnonKey"];

    if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(anonKey))
    {
        throw new InvalidOperationException("La configuración de Supabase (Url o AnonKey) no es válida en appsettings.json.");
    }

    return new Supabase.Client(url, anonKey, new Supabase.SupabaseOptions { AutoConnectRealtime = false });
});

builder.Services.AddScoped<IRecetaService, RecetaService>();

var app = builder.Build();

// Registrar Middleware de Manejo de Excepciones Global
app.UseMiddleware<ExceptionMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles(); 

app.UseCors("AllowSpecificOrigins"); 

app.UseAuthentication(); 
app.UseAuthorization();

app.MapControllers();

app.Run();
