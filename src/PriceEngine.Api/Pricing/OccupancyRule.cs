using Microsoft.Extensions.Logging;
using PriceEngine.Api.Models;

namespace PriceEngine.Api.Pricing;

public sealed class OccupancyRule : IPricingRule
{
    public string Name => nameof(OccupancyRule);

    private readonly int _lowThreshold;
    private readonly int _highThreshold;
    private readonly int _lowDiscountPercent;
    private readonly int _highIncreasePercent;
    private readonly ILogger<OccupancyRule> _logger;

    public OccupancyRule(int lowThreshold, int highThreshold, int lowDiscountPercent, int highIncreasePercent,
                         ILogger<OccupancyRule> logger)
    {
        _lowThreshold = lowThreshold;
        _highThreshold = highThreshold;
        _lowDiscountPercent = lowDiscountPercent;
        _highIncreasePercent = highIncreasePercent;
        _logger = logger;
    }

    public (bool applied, PriceBreakdown step) Apply(decimal currentPrice, PriceRequest req)
    {
        var occ = req.Capacity == 0 ? 0 : (int)Math.Round((req.SoldSeats * 100m) / req.Capacity);
        var reason = $"Doluluk %{occ}";

        decimal delta = 0m;
        if (occ < _lowThreshold)
        {
            delta = -currentPrice * _lowDiscountPercent / 100m;
            reason += $" < %{_lowThreshold} ⇒ %{_lowDiscountPercent} indirim";
        }
        else if (occ > _highThreshold)
        {
            delta = currentPrice * _highIncreasePercent / 100m;
            reason += $" > %{_highThreshold} ⇒ %{_highIncreasePercent} zam";
        }
        else
        {
            reason += $" (eşik yok)";
        }

        var result = currentPrice + delta;

        _logger.LogInformation("Rule {Rule} | Occ={Occ}% Cap={Cap} Sold={Sold} Before={Before} Delta={Delta} After={After}",
            Name, occ, req.Capacity, req.SoldSeats, currentPrice, delta, result);

        return (delta != 0m, new PriceBreakdown(Name, reason, delta, result));
    }
}
