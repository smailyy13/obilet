using Microsoft.EntityFrameworkCore;
using PriceEngine.Api.Data;
using PriceEngine.Api.Models;

namespace PriceEngine.Api.Services;

public sealed class EfCouponRepository : ICouponRepository
{
    private readonly AppDbContext _db;
    public EfCouponRepository(AppDbContext db) => _db = db;

    public Coupon? GetByCode(string code)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return _db.Coupons.AsNoTracking().FirstOrDefault(c => c.Code == normalized);
    }

    public Task<Coupon?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return _db.Coupons.AsNoTracking().FirstOrDefaultAsync(c => c.Code == normalized, ct);
    }
}
