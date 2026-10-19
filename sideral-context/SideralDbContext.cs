using Microsoft.EntityFrameworkCore;
using sideral_context.Users;
using sideral_context.Wall;

namespace sideral_context;

public class SideralDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<PostComment> PostComments => Set<PostComment>();
    public DbSet<PostLike> PostLikes => Set<PostLike>();

    public SideralDbContext(DbContextOptions<SideralDbContext> options) : base(options)
    {
    }

    /// <summary>Configuration Npgsql commune à l'API et au design-time (migrations).</summary>
    public static void Configure(DbContextOptionsBuilder builder, string connectionString)
    {
        builder.UseNpgsql(connectionString)
               .UseSnakeCaseNamingConvention();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Login).IsUnique();
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Theme).HasDefaultValue(UserPreferences.THEME_LIGHT);
            e.Property(u => u.AccentColor).HasDefaultValue(UserPreferences.DEFAULT_ACCENT);

            e.HasMany(u => u.Roles)
             .WithMany(r => r.Users)
             .UsingEntity(j => j.ToTable("user_roles"));
        });

        modelBuilder.Entity<Role>(e =>
        {
            e.HasIndex(r => r.Key).IsUnique();
            e.HasData(
                new Role { Id = 1, Key = Role.ADMIN, Name = "Administrateur" },
                new Role { Id = 2, Key = Role.USER, Name = "Utilisateur" }
            );
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasIndex(t => t.TokenHash).IsUnique();
            e.HasOne(t => t.User)
             .WithMany(u => u.RefreshTokens)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Post>(e =>
        {
            e.HasIndex(p => p.CreatedOn);
            e.HasOne(p => p.Author)
             .WithMany()
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PostComment>(e =>
        {
            e.HasIndex(c => new { c.PostId, c.CreatedOn });
            e.HasOne(c => c.Post)
             .WithMany(p => p.Comments)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.Author)
             .WithMany()
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PostLike>(e =>
        {
            e.HasKey(l => new { l.PostId, l.UserId });
            e.HasOne(l => l.Post)
             .WithMany(p => p.Likes)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(l => l.User)
             .WithMany()
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
