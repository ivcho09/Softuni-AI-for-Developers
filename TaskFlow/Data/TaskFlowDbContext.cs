using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TaskFlow.Models;

namespace TaskFlow.Data;

/// <summary>
/// EF Core unit of work for TaskFlow. Backed by SQLite.
/// </summary>
public class TaskFlowDbContext(DbContextOptions<TaskFlowDbContext> options) : DbContext(options)
{
    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    public DbSet<Project> Projects => Set<Project>();

    /// <summary>
    /// SQLite has no native date type, so EF stores dates as text and loses
    /// <see cref="DateTimeKind"/> on the way back. These converters normalise every
    /// value to UTC on write and re-tag it as UTC on read, which keeps overdue and
    /// statistics comparisons correct regardless of the server's local time zone.
    /// </summary>
    private static readonly ValueConverter<DateTime, DateTime> UtcConverter =
        new(value => value.ToUniversalTime(),
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static readonly ValueConverter<DateTime?, DateTime?> NullableUtcConverter =
        new(value => value.HasValue ? value.Value.ToUniversalTime() : value,
            value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : value);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(project =>
        {
            project.HasKey(p => p.Id);

            project.Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(ValidationLimits.ProjectNameMaxLength);

            project.Property(p => p.Description)
                .HasMaxLength(ValidationLimits.DescriptionMaxLength);

            project.Property(p => p.CreatedAt)
                .IsRequired()
                .HasConversion(UtcConverter);

            project.HasIndex(p => p.Name);
        });

        modelBuilder.Entity<TaskItem>(task =>
        {
            task.HasKey(t => t.Id);

            task.Property(t => t.Title)
                .IsRequired()
                .HasMaxLength(ValidationLimits.TaskTitleMaxLength);

            task.Property(t => t.Description)
                .HasMaxLength(ValidationLimits.DescriptionMaxLength);

            // Enums are stored as integers so that ORDER BY Priority ranks
            // Low < Medium < High < Urgent instead of sorting alphabetically.
            task.Property(t => t.Status)
                .IsRequired()
                .HasConversion<int>();

            task.Property(t => t.Priority)
                .IsRequired()
                .HasConversion<int>();

            task.Property(t => t.CreatedAt)
                .IsRequired()
                .HasConversion(UtcConverter);

            task.Property(t => t.DueDate).HasConversion(NullableUtcConverter);
            task.Property(t => t.CompletedAt).HasConversion(NullableUtcConverter);

            // Deleting a project must not delete its tasks: the FK is set to NULL.
            task.HasOne(t => t.Project)
                .WithMany(p => p.Tasks)
                .HasForeignKey(t => t.ProjectId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes backing the filtering, searching and sorting endpoints.
            task.HasIndex(t => t.Status);
            task.HasIndex(t => t.Priority);
            task.HasIndex(t => t.DueDate);
            task.HasIndex(t => t.ProjectId);
        });
    }
}
