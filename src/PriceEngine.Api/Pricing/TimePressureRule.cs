using Microsoft.Extensions.Logging;
using PriceEngine.Api.Models;

namespace PriceEngine.Api.Pricing;

public sealed class TimePressureRule : IPricingRule
{
    public string Name => nameof(TimePressureRule);

    private readonly int _increasePercent;   // < HoursThreshold içinde
    private readonly int _discountPercent;   // > DaysThreshold uzaksa
    private readonly int _hoursThreshold;
    private readonly int _daysThreshold;
    private readonly ILogger<TimePressureRule> _logger;

    public TimePressureRule(int increasePercent, int discountPercent, int hoursThreshold, int daysThreshold,
                            ILogger<TimePressureRule> logger)
    {
        _increasePercent = increasePercent;
        _discountPercent = discountPercent;
        _hoursThreshold = hoursThreshold;
        _daysThreshold = daysThreshold;
        _logger = logger;
    }

    public (bool applied, PriceBreakdown step) Apply(decimal currentPrice, PriceRequest req)
    {
        var now = DateTime.UtcNow;
        var diff = req.DepartureTime - now;
        var hours = diff.TotalHours;
        var days = diff.TotalDays;

        decimal delta = 0m;
        string reason;

        if (hours <= _hoursThreshold)
        {
            delta = currentPrice * _increasePercent / 100m;
            reason = $"Kalan süre ≤ {_hoursThreshold} saat ⇒ %{_increasePercent} zam";
        }
        else if (days >= _daysThreshold)
        {
            delta = -currentPrice * _discountPercent / 100m;
            reason = $"Kalan süre ≥ {_daysThreshold} gün ⇒ %{_discountPercent} indirim";
        }
        else
        {
            reason = $"Kalan {Math.Round(hours)} saat (eşik yok)";
        }

        var result = currentPrice + delta;

        _logger.LogInformation("Rule {Rule} | HoursLeft={Hours:0.0} DaysLeft={Days:0.0} Before={Before} Delta={Delta} After={After}",
            Name, hours, days, currentPrice, delta, result);

        return (delta != 0m, new PriceBreakdown(Name, reason, delta, result));
    }
}
