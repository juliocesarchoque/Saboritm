using System.Threading;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using AppRecetas.Api.Models;

namespace AppRecetas.Api.Data
{
    public class AppRecetasDbContext : DbContext
    {
        private readonly IHttpContextAccessor? _httpContextAccessor;

        public AppRecetasDbContext(DbContextOptions<AppRecetasDbContext> options, IHttpContextAccessor? httpContextAccessor = null) : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        private void SetSupabaseAuth()
        {
            var userId = _httpContextAccessor?.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier) 
                         ?? _httpContextAccessor?.HttpContext?.User?.FindFirstValue("sub");

            if (!string.IsNullOrEmpty(userId))
            {
                Console.WriteLine($"[RLS DEBUG] Enviando auth.uid = {userId} a Postgres...");
                // SET LOCAL auth.uid = '...'; usando set_config para soportar parámetros seguros (ExecuteSql)
                Database.ExecuteSql($"SELECT set_config('auth.uid', {userId}, true);");
            }
            else 
            {
                Console.WriteLine("[RLS WARNING] No se encontró ID de usuario en el token para RLS.");
            }
        }

        private async Task SetSupabaseAuthAsync()
        {
            var userId = _httpContextAccessor?.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier) 
                         ?? _httpContextAccessor?.HttpContext?.User?.FindFirstValue("sub");

            if (!string.IsNullOrEmpty(userId))
            {
                Console.WriteLine($"[RLS DEBUG] Configurando auth.uid = {userId}");
                await Database.ExecuteSqlAsync($"SELECT set_config('auth.uid', {userId}, true);");
            }
            else
            {
                Console.WriteLine("[RLS WARNING] No se detectó userId en el token. Las políticas RLS podrían fallar.");
            }
        }

        public override int SaveChanges()
        {
            if (Database.CurrentTransaction == null)
            {
                using var transaction = Database.BeginTransaction();
                SetSupabaseAuth();
                var result = base.SaveChanges();
                transaction.Commit();
                return result;
            }
            else
            {
                SetSupabaseAuth();
                return base.SaveChanges();
            }
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (Database.CurrentTransaction == null)
            {
                using var transaction = await Database.BeginTransactionAsync(cancellationToken);
                await SetSupabaseAuthAsync();
                var result = await base.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            else
            {
                await SetSupabaseAuthAsync();
                return await base.SaveChangesAsync(cancellationToken);
            }
        }

        public DbSet<Categoria> Categorias { get; set; }
        public DbSet<Ingrediente> Ingredientes { get; set; }
        public DbSet<Receta> Recetas { get; set; }
        public DbSet<RecetaIngrediente> RecetaIngredientes { get; set; }
        public DbSet<IngredienteSinonimo> IngredienteSinonimos { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. Categoria
            modelBuilder.Entity<Categoria>().ToTable("categorias")
                .HasKey(c => c.IdCategoria);
            modelBuilder.Entity<Categoria>().Property(c => c.IdCategoria).HasColumnName("id_categoria");
            modelBuilder.Entity<Categoria>().Property(c => c.Nombre).HasColumnName("nombre");

            // 2. Ingrediente
            modelBuilder.Entity<Ingrediente>().ToTable("ingredientes")
                .HasKey(i => i.IdIngrediente);
            modelBuilder.Entity<Ingrediente>().Property(i => i.IdIngrediente).HasColumnName("id_ingrediente");
            modelBuilder.Entity<Ingrediente>().Property(i => i.Nombre).HasColumnName("nombre");
            modelBuilder.Entity<Ingrediente>().Property(i => i.EsPrincipal).HasColumnName("es_principal");

            // 3. Receta
            modelBuilder.Entity<Receta>().ToTable("recetas")
                .HasKey(r => r.IdReceta);
            modelBuilder.Entity<Receta>().Property(r => r.IdReceta).HasColumnName("id_receta");
            modelBuilder.Entity<Receta>().Property(r => r.Nombre).HasColumnName("nombre");
            modelBuilder.Entity<Receta>().Property(r => r.Instrucciones).HasColumnName("instrucciones");
            modelBuilder.Entity<Receta>().Property(r => r.IdCategoria).HasColumnName("id_categoria");
            modelBuilder.Entity<Receta>().Property(r => r.FechaCreacion).HasColumnName("fecha_creacion");
            modelBuilder.Entity<Receta>().Property(r => r.TiempoPreparacion).HasColumnName("tiempo_preparacion");
            modelBuilder.Entity<Receta>().Property(r => r.ImagenUrl).HasColumnName("imagen_url");

            // Evitar CategoriaIdCategoria
            modelBuilder.Entity<Receta>()
                .HasOne(r => r.Categoria)
                .WithMany()
                .HasForeignKey(r => r.IdCategoria);

            // 4. RecetaIngrediente
            modelBuilder.Entity<RecetaIngrediente>().ToTable("receta_ingredientes");
            modelBuilder.Entity<RecetaIngrediente>().HasKey(ri => new { ri.IdReceta, ri.IdIngrediente });
            modelBuilder.Entity<RecetaIngrediente>().Property(ri => ri.IdReceta).HasColumnName("id_receta");
            modelBuilder.Entity<RecetaIngrediente>().Property(ri => ri.IdIngrediente).HasColumnName("id_ingrediente");
            modelBuilder.Entity<RecetaIngrediente>().Property(ri => ri.Cantidad).HasColumnName("cantidad");

            modelBuilder.Entity<RecetaIngrediente>()
                .HasOne(ri => ri.Receta)
                .WithMany(r => r.RecetaIngredientes)
                .HasForeignKey(ri => ri.IdReceta);

            modelBuilder.Entity<RecetaIngrediente>()
                .HasOne(ri => ri.Ingrediente)
                .WithMany(i => i.RecetaIngredientes)
                .HasForeignKey(ri => ri.IdIngrediente);

            // 5. IngredienteSinonimo
            modelBuilder.Entity<IngredienteSinonimo>().ToTable("ingrediente_sinonimos")
                .HasKey(s => s.IdSinonimo);
            modelBuilder.Entity<IngredienteSinonimo>().Property(s => s.IdSinonimo).HasColumnName("id_sinonimo");
            modelBuilder.Entity<IngredienteSinonimo>().Property(s => s.IdIngrediente).HasColumnName("id_ingrediente");
            modelBuilder.Entity<IngredienteSinonimo>().Property(s => s.TerminoBusqueda).HasColumnName("termino_busqueda");

            modelBuilder.Entity<IngredienteSinonimo>()
                .HasOne(s => s.Ingrediente)
                .WithMany(i => i.Sinonimos)
                .HasForeignKey(s => s.IdIngrediente);
        }
    }
}