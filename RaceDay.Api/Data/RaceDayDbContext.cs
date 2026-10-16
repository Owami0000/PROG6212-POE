using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;
using RaceDay.Contracts;

namespace RaceDay.Api.Data;

public class RaceDayDbContext : DbContext
{
    public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : base(options) { }

    public DbSet<Role> Roles { get; set; }
    public DbSet<AppUser> Users { get; set; }
    public DbSet<Event> Events { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Enrolment> Enrolments { get; set; }
    public DbSet<Result> Results { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Roles are fixed lookup data, so registration can only pick a valid role.
        modelBuilder.Entity<Role>(e =>
        {
            e.Property(r => r.Name).HasMaxLength(50).IsRequired();
            e.HasIndex(r => r.Name).IsUnique();
            e.HasData(
                new Role { RoleId = 1, Name = RoleNames.Organiser },
                new Role { RoleId = 2, Name = RoleNames.Participant });
        });

        modelBuilder.Entity<AppUser>(e =>
        {
            e.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
            e.Property(u => u.LastName).HasMaxLength(100).IsRequired();
            e.Property(u => u.Email).HasMaxLength(256).IsRequired();
            e.Property(u => u.PasswordHash).HasMaxLength(512).IsRequired();
            e.Property(u => u.PhoneNumber).HasMaxLength(30);
            e.HasIndex(u => u.Email).IsUnique();
            e.HasOne(u => u.Role).WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Event>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1000).IsRequired();
            e.Property(x => x.Location).HasMaxLength(200).IsRequired();
            e.Property(x => x.EventType).HasMaxLength(20).IsRequired();
            e.Property(x => x.DistanceKm).HasPrecision(8, 2);
            e.HasOne(x => x.Organiser).WithMany(u => u.OrganisedEvents)
                .HasForeignKey(x => x.OrganiserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Category>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(100).IsRequired();
            e.Property(c => c.Description).HasMaxLength(300);
            e.HasOne(c => c.Event).WithMany(ev => ev.Categories)
                .HasForeignKey(c => c.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        // Restrict (not cascade) avoids SQL Server "multiple cascade paths" errors.
        modelBuilder.Entity<Enrolment>(e =>
        {
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.HasOne(x => x.Participant).WithMany(u => u.Enrolments)
                .HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Event).WithMany(ev => ev.Enrolments)
                .HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Category).WithMany(c => c.Enrolments)
                .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            // A participant can only enter an event once.
            e.HasIndex(x => new { x.ParticipantId, x.EventId }).IsUnique();
        });

        modelBuilder.Entity<Result>(e =>
        {
            e.HasOne(r => r.Enrolment).WithOne(en => en.Result)
                .HasForeignKey<Result>(r => r.EnrolmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => r.EnrolmentId).IsUnique();
        });
    }
}
