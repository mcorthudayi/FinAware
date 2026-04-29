using Microsoft.EntityFrameworkCore;
using FinAware.API.Models;

namespace FinAware.API.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<Saving> Savings { get; set; }
        public DbSet<SavingTransaction> SavingTransactions { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Budget> Budgets { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<Transaction>()
                .Property(t => t.Amount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Transaction>()
                .Property(t => t.OriginalAmount)
                .HasPrecision(18, 4);

            modelBuilder.Entity<Transaction>()
                .Property(t => t.ExchangeRate)
                .HasPrecision(18, 4);

            modelBuilder.Entity<Saving>()
                .Property(s => s.TargetAmount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Saving>()
                .Property(s => s.CurrentAmount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Budget>()
                .Property(b => b.LimitAmount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<SavingTransaction>()
                .Property(st => st.Amount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<SavingTransaction>()
                .Property(st => st.OriginalAmount)
                .HasPrecision(18, 4);

            modelBuilder.Entity<SavingTransaction>()
                .Property(st => st.ExchangeRate)
                .HasPrecision(18, 4);

            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.Category)
                .WithMany(c => c.Transactions)
                .HasForeignKey(t => t.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Category>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Saving>()
                .HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SavingTransaction>()
                .HasOne(st => st.Saving)
                .WithMany(s => s.Transactions)
                .HasForeignKey(st => st.SavingId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Budget>()
                .HasOne(b => b.User)
                .WithMany()
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Budget>()
                .HasOne(b => b.Category)
                .WithMany()
                .HasForeignKey(b => b.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}