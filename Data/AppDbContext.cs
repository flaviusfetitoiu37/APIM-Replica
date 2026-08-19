using Microsoft.EntityFrameworkCore;
using ApimReplica.Models;

namespace ApimReplica.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Api> Apis => Set<Api>();
    public DbSet<ApiSchemaVersion> SchemaVersions => Set<ApiSchemaVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Two concurrent refreshes used to hand out the same version number.
        // Also serves the "latest version for this API" lookup.
        modelBuilder.Entity<ApiSchemaVersion>()
            .HasIndex(v => new { v.ApiId, v.VersionNumber })
            .IsUnique();

        // The case-insensitive unique index on Apis."Name" is created with raw SQL in
        // the AddUniqueConstraints migration: route keys are lower-cased, so "Petstore"
        // and "PETSTORE" would collide into one /proxy path.
    }
}
