using Microsoft.EntityFrameworkCore;
using PriceEngine.Api.Models;

namespace PriceEngine.Api.Data;

public static class DbInitializer
{
    public static void MigrateAndSeed(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();

        // Kuponlar
        if (!db.Coupons.Any())
        {
            db.Coupons.AddRange(
                new Coupon { Code = "EARLY10", Percent = 10, ExpireAt = DateTime.UtcNow.AddMonths(12) },
                new Coupon { Code = "WELCOME5", Percent = 5, ExpireAt = DateTime.UtcNow.AddMonths(6) }
            );
            db.SaveChanges();
        }

        // Otobüsler: en az 100 kayıt olsun
        EnsureBuses(db, target: 100);
    }

    /// <summary>Veritabanında en az 'target' kadar örnek otobüs verisi olmasını sağlar.</summary>
    public static void EnsureBuses(AppDbContext db, int target = 100, bool reset = false)
    {
        if (reset)
        {
            db.Buses.RemoveRange(db.Buses);
            db.SaveChanges();
        }

        if (db.Buses.Count() >= target) return;

        var cities = new[]
        {
            "İstanbul","Ankara","İzmir","Bursa","Antalya","Konya","Adana","Gaziantep","Kayseri","Kocaeli",
            "Mersin","Eskişehir","Diyarbakır","Samsun","Şanlıurfa","Sakarya","Trabzon","Van","Aydın","Muğla",
            "Malatya","Manisa","Hatay","Tekirdağ","Balıkesir","Ordu","Erzurum","Sivas","Kahramanmaraş","Elazığ",
            "Çanakkale","Zonguldak","Denizli","Afyonkarahisar","Bolu","Kırıkkale","Yalova","Kütahya","Niğde","Isparta"
        };

        // sabit tohumlu Random: her çalıştırmada benzer ama yeterince çeşitli veri
        var rng = new Random(421337);

        // rastgele değer üreten yardımcılar
        int NextCapacity() => new[] { 38, 46, 50, 54 }.OrderBy(_ => rng.Next()).First();
        decimal NextBasePrice()
    {
        var val = rng.Next(250, 850) + (decimal)rng.NextDouble(); // 250.00 – 850.99
        return Math.Round(val, 2, MidpointRounding.AwayFromZero);
    }

        DateTime NextDeparture()
        {
            // 1–21 gün içinde rastgele saat (06:00–23:00 arası)
            var days = rng.Next(1, 22);
            var hour = rng.Next(6, 24);
            var minute = rng.Next(0, 2) * 30; // 00 ya da 30
            return new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, 0, 0, 0, DateTimeKind.Utc)
                .AddDays(days)
                .AddHours(hour)
                .AddMinutes(minute);
        }

        var created = 0;
        while (db.Buses.Count() + created < target)
        {
            var from = cities[rng.Next(cities.Length)];
            string to;
            do { to = cities[rng.Next(cities.Length)]; } while (to == from);

            var cap = NextCapacity();
            // Doluluk %10–%95 arası
            var sold = rng.Next((int)(cap * 0.10), (int)(cap * 0.95));
            var price = NextBasePrice();
            var dep = NextDeparture();

            db.Buses.Add(new Bus
            {
                Name = $"{from} → {to}",
                Capacity = cap,
                SoldSeats = sold,
                BasePrice = price,
                DepartureTime = dep
            });
            created++;
        }

        if (created > 0) db.SaveChanges();
    }
}
