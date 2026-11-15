using Microsoft.EntityFrameworkCore;
using StudyPilot.Api.Models;

namespace StudyPilot.Api.Data;

public class AppDbContext : DbContext
{
  public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
  {
  }

  public DbSet<User> Users => Set<User>();
  public DbSet<Document> Documents => Set<Document>();
  public DbSet<Quiz> Quizzes => Set<Quiz>();
  public DbSet<Question> Questions => Set<Question>();
  public DbSet<QuizAttempt> QuizAttempts => Set<QuizAttempt>();
  public DbSet<Answer> Answers => Set<Answer>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    modelBuilder.Entity<User>(entity =>
    {
      entity.HasIndex(u => u.Email).IsUnique();
      entity.Property(u => u.Email).HasMaxLength(256);
      entity.Property(u => u.DisplayName).HasMaxLength(100);
    });

    modelBuilder.Entity<Document>(entity =>
    {
      entity.Property(d => d.Title).HasMaxLength(200);
      entity.Property(d => d.OriginalFileName).HasMaxLength(260);
    });

    modelBuilder.Entity<Quiz>(entity =>
    {
      entity.Property(q => q.Title).HasMaxLength(200);
    });

    modelBuilder.Entity<Question>(entity =>
    {
      entity.Property(q => q.Type)
                .HasConversion<string>()
                .HasMaxLength(20);
    });
  }
}