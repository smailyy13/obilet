using Microsoft.Extensions.Logging;
using PriceEngine.Api.Models;
using PriceEngine.Api.Services;

namespace PriceEngine.Api.Pricing;

public sealed class CouponRule : IPricingRule
{
    public string Name => nameof(CouponRule);

    private readonly ICouponRepository _repo;
    private readonly ILogger<CouponRule> _logger;

    public CouponRule(ICouponRepository repo, ILogger<CouponRule> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public (bool applied, PriceBreakdown step) Apply(decimal currentPrice, PriceRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.CouponCode))
        {
            var skip = new PriceBreakdown(Name, "Kupon kodu yok", 0m, currentPrice);
            _logger.LogInformation("Rule {Rule} | No coupon", Name);
            return (false, skip);
        }

        var c = _repo.GetByCode(req.CouponCode!);
        if (c is null)
        {
            var skip = new PriceBreakdown(Name, $"Geçersiz kupon {req.CouponCode}", 0m, currentPrice);
            _logger.LogInformation("Rule {Rule} | Coupon {Code} invalid", Name, req.CouponCode);
            return (false, skip);
        }

        if (c.IsExpired(DateTime.UtcNow))
        {
            var skip = new PriceBreakdown(Name, $"Süresi dolmuş kupon {c.Code}", 0m, currentPrice);
            _logger.LogInformation("Rule {Rule} | Coupon {Code} expired at {Exp}", Name, c.Code, c.ExpireAt);
            return (false, skip);
        }

        var delta = -currentPrice * c.Percent / 100m;
        var result = currentPrice + delta;
        var reason = $"Geçerli kupon {c.Code} ⇒ %{c.Percent} indirim (son: {c.ExpireAt:yyyy-MM-dd})";

        _logger.LogInformation("Rule {Rule} | Coupon={Code} Percent={Pct} Before={Before} Delta={Delta} After={After}",
            Name, c.Code, c.Percent, currentPrice, delta, result);

        return (true, new PriceBreakdown(Name, reason, delta, result));
    }
}
