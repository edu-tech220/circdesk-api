using CircDesk.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CircDesk.Api.Data;

public class CircDeskDbContext : DbContext
{
    public CircDeskDbContext(DbContextOptions<CircDeskDbContext> options) : base(options)
    {
    }

    public DbSet<Book> Books => Set<Book>();
    public DbSet<Copy> Copies => Set<Copy>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<Hold> Holds => Set<Hold>();
    public DbSet<Fine> Fines => Set<Fine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Book>(entity =>
        {
            entity.Property(b => b.Title).IsRequired().HasMaxLength(300);
            entity.Property(b => b.Author).IsRequired().HasMaxLength(200);
            entity.Property(b => b.Isbn).IsRequired().HasMaxLength(20);
            entity.HasIndex(b => b.Title);
            entity.HasIndex(b => b.Author);
        });

        modelBuilder.Entity<Copy>(entity =>
        {
            entity.Property(c => c.InventoryCode).IsRequired().HasMaxLength(40);
            entity.HasIndex(c => c.InventoryCode).IsUnique();
            entity.Property(c => c.RowVersion).IsConcurrencyToken();
            entity.HasOne(c => c.Book)
                .WithMany(b => b.Copies)
                .HasForeignKey(c => c.BookId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Member>(entity =>
        {
            entity.Property(m => m.Username).IsRequired().HasMaxLength(100);
            entity.HasIndex(m => m.Username).IsUnique();
        });

        modelBuilder.Entity<Loan>(entity =>
        {
            entity.HasOne(l => l.Copy)
                .WithMany()
                .HasForeignKey(l => l.CopyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(l => l.Member)
                .WithMany(m => m.Loans)
                .HasForeignKey(l => l.MemberId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(l => new { l.MemberId, l.Status });
        });

        modelBuilder.Entity<Hold>(entity =>
        {
            entity.HasOne(h => h.Book)
                .WithMany(b => b.Holds)
                .HasForeignKey(h => h.BookId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(h => h.Member)
                .WithMany(m => m.Holds)
                .HasForeignKey(h => h.MemberId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(h => new { h.BookId, h.Status, h.CreatedAt });
        });

        modelBuilder.Entity<Fine>(entity =>
        {
            entity.Property(f => f.Amount).HasPrecision(10, 2);
            entity.HasOne(f => f.Member)
                .WithMany(m => m.Fines)
                .HasForeignKey(f => f.MemberId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(f => f.Loan)
                .WithMany(l => l.Fines)
                .HasForeignKey(f => f.LoanId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampRowVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampRowVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Stamps a new concurrency token on every modified IHasRowVersion entity. SQL Server's
    /// native `rowversion` type has no SQLite equivalent, so the token is generated here in
    /// application code, which behaves identically against both providers (and in tests).
    /// </summary>
    private void StampRowVersions()
    {
        foreach (var entry in ChangeTracker.Entries<IHasRowVersion>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.RowVersion = Guid.NewGuid().ToByteArray();
            }
        }
    }
}
