using System.ComponentModel.DataAnnotations.Schema;

namespace PriceEngine.Api.Models;

// === Request/Response modelleri ===
public record PriceRequest(
    decimal BasePrice,
    int Capacity,
    int SoldSeats,
    DateTime DepartureTime,
    string? CouponCode
);

public record PriceBreakdown(
    string Rule,
    string Reason,
    decimal Delta,
    decimal ResultPrice
);

public record PriceResponse(
   decimal FinalPrice,
   List<PriceBreakdown> Steps
);

// === Entity'ler ===
public class Coupon
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public int Percent { get; set; }
    public DateTime ExpireAt { get; set; }
    public bool IsExpired(DateTime now) => now >= ExpireAt;
}

public class Bus
{
    public int Id { get; set; }
    public required string Name { get; set; }     // "İstanbul → Ankara"
    public int Capacity { get; set; }
    public int SoldSeats { get; set; }            // DOLULUK artık DB'de
    public decimal BasePrice { get; set; }
    public DateTime DepartureTime { get; set; }   // Kalkış gün/saat (DB)
}

// Admin DTO’ları
public record CreateCouponRequest(string Code, int Percent, DateTime ExpireAt);
public record CreateBusRequest(string Name, int Capacity, decimal BasePrice, DateTime DepartureTime, int SoldSeats);
