using Microsoft.EntityFrameworkCore;
using sideral_context.Dms;
using sideral_context.Groups;
using sideral_context.Settings;
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
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<Folder> Folders => Set<Folder>();
    public DbSet<FolderPermission> FolderPermissions => Set<FolderPermission>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<DmsActivity> DmsActivities => Set<DmsActivity>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

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

        modelBuilder.Entity<Group>(e =>
        {
            e.HasIndex(g => g.Name).IsUnique();
            e.HasMany(g => g.Members)
             .WithMany()
             .UsingEntity(j => j.ToTable("group_members"));
        });

        // ---- GED : la suppression définitive d'un dossier (vidage de la corbeille) supprime en cascade
        // ses sous-dossiers, documents, versions, droits et journal
        modelBuilder.Entity<Folder>(e =>
        {
            e.HasIndex(f => f.ParentId);
            e.HasOne(f => f.Parent)
             .WithMany(f => f.Children)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(f => f.CreatedBy).WithMany().OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.DeletedBy).WithMany().OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FolderPermission>(e =>
        {
            e.HasIndex(p => new { p.FolderId, p.UserId }).IsUnique().HasFilter("user_id IS NOT NULL");
            e.HasIndex(p => new { p.FolderId, p.GroupId }).IsUnique().HasFilter("group_id IS NOT NULL");
            e.ToTable(t => t.HasCheckConstraint("ck_folder_permissions_principal", "(user_id IS NULL) <> (group_id IS NULL)"));
            e.HasOne(p => p.Folder).WithMany(f => f.Permissions).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.User).WithMany().OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.Group).WithMany().OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Document>(e =>
        {
            e.HasIndex(d => d.FolderId);
            e.HasIndex(d => d.CheckedOutById);
            e.HasIndex(d => new { d.FullTextStatus, d.FullTextNextAttemptOn });
            e.HasIndex(d => d.FullTextIdentifier).IsUnique();
            e.Property(d => d.FullTextIdentifier).HasDefaultValueSql("gen_random_uuid()");
            e.Property(d => d.FullTextStatus).HasConversion<string>().HasMaxLength(20).HasDefaultValue(FullTextStatus.PENDING);
            e.HasOne(d => d.Folder).WithMany(f => f.Documents).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(d => d.CreatedBy).WithMany().OnDelete(DeleteBehavior.Restrict);
            e.HasOne(d => d.CheckedOutBy).WithMany().OnDelete(DeleteBehavior.Restrict);
            e.HasOne(d => d.DeletedBy).WithMany().OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DocumentVersion>(e =>
        {
            e.HasIndex(v => new { v.DocumentId, v.Major, v.Minor }).IsUnique();
            e.HasOne(v => v.Document).WithMany(d => d.Versions).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(v => v.CreatedBy).WithMany().OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DmsActivity>(e =>
        {
            e.HasIndex(a => new { a.FolderId, a.CreatedOn });
            e.HasIndex(a => new { a.DocumentId, a.CreatedOn });
            e.Property(a => a.Action).HasConversion<string>();
            e.HasOne(a => a.Folder).WithMany().OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.Document).WithMany().OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.User).WithMany().OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AppSetting>(e =>
        {
            e.HasOne(s => s.UpdatedBy).WithMany().OnDelete(DeleteBehavior.SetNull);
        });
    }
}
