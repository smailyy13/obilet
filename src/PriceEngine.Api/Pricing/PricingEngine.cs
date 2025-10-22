using Microsoft.Extensions.Logging;
using PriceEngine.Api.Models;

namespace PriceEngine.Api.Pricing;

public sealed class PricingEngine
{
    private readonly IEnumerable<IPricingRule> _rules;
    private readonly ILogger<PricingEngine> _logger;

    public PricingEngine(IEnumerable<IPricingRule> rules, ILogger<PricingEngine> logger)
    {
        _rules = rules;
        _logger = logger;
    }

    public PriceResponse Calculate(PriceRequest req)
    {
        _logger.LogInformation("PriceCalc START Base={Base} Cap={Cap} Sold={Sold} Dep={Dep:o} Coupon={Coupon}",
            req.BasePrice, req.Capacity, req.SoldSeats, req.DepartureTime, req.CouponCode);

        var steps = new List<PriceBreakdown>();
        decimal price = req.BasePrice;

        foreach (var rule in _rules)
        {
            var (applied, step) = rule.Apply(price, req);
            steps.Add(step);

            if (applied)
                _logger.LogInformation("APPLIED {Rule}: {Reason} | Δ={Delta} => {After}",
                    step.Rule, step.Reason, step.Delta, step.ResultPrice);
            else
                _logger.LogInformation("SKIPPED {Rule}: {Reason} | Price={Price}",
                    rule.Name, step.Reason, price);

            price = step.ResultPrice;
        }

        _logger.LogInformation("PriceCalc END Final={Final} Steps={Count}", price, steps.Count);
        return new PriceResponse(price, steps);
    }
}
