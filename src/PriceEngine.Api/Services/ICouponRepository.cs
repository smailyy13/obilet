using PriceEngine.Api.Models;

namespace PriceEngine.Api.Services;

public interface ICouponRepository
{
    // Senkron erişim (CouponRule içinde kullanıyoruz)
    Coupon? GetByCode(string code);

    // İstersen başka yerlerde async de kullanabil diye ekledim
    Task<Coupon?> GetByCodeAsync(string code, CancellationToken ct = default);
}
