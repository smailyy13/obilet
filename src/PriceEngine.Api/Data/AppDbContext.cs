using Microsoft.EntityFrameworkCore;
using PriceEngine.Api.Models;

namespace PriceEngine.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<Bus> Buses => Set<Bus>();
    public DbSet<BackgroundJob> Jobs => Set<BackgroundJob>();   // << EKLENDİ

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Coupon>().HasIndex(c => c.Code).IsUnique();
        modelBuilder.Entity<Bus>().Property(b => b.BasePrice).HasPrecision(18, 2);

        modelBuilder.Entity<BackgroundJob>().HasIndex(j => j.EnqueuedAt);
        base.OnModelCreating(modelBuilder);
    }
}
