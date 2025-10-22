using PriceEngine.Api.Models;

namespace PriceEngine.Api.Pricing;

public interface IPricingRule
{
    string Name { get; }
    // applied = kural etkiledi mi? (delta ≠ 0)
    (bool applied, PriceBreakdown step) Apply(decimal currentPrice, PriceRequest request);
}
