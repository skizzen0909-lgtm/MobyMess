using Microsoft.EntityFrameworkCore;
using SecureLink.Server.Core.Models;

namespace SecureLink.Server.Core.Data;

public class AppDbContext : DbContext
{
    public DbSet<Message> Messages { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Contact> Contacts { get; set; }
    public DbSet<ChatGroup> ChatGroups { get; set; }

    private readonly string _dbPath;

    public AppDbContext(string dbPath)
    {
        _dbPath = dbPath;
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={_dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SenderId).IsRequired();
            entity.Property(e => e.Content).IsRequired();
            entity.HasIndex(e => e.RecipientId);
            entity.HasIndex(e => e.GroupId);
            // Настройка свойства FileSize по умолчанию
            entity.Property(e => e.FileSize).HasDefaultValue(0);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PhoneNumber).IsUnique();
        });

        modelBuilder.Entity<Contact>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.ContactPhoneNumber }).IsUnique();
        });

        modelBuilder.Entity<ChatGroup>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MemberIds)
                  .HasConversion(
                      v => string.Join(",", v),
                      v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
        });
    }
}
