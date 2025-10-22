namespace PriceEngine.Api.Configuration;

public sealed class PricingRulesOptions
{
    public OccupancyOptions Occupancy { get; set; } = new();
    public TimePressureOptions TimePressure { get; set; } = new();

    public sealed class OccupancyOptions
    {
        public int LowThreshold { get; set; } = 20;
        public int HighThreshold { get; set; } = 80;
        public int LowDiscountPercent { get; set; } = 10;
        public int HighIncreasePercent { get; set; } = 20;
    }

    public sealed class TimePressureOptions
    {
        public int IncreasePercent { get; set; } = 15;
        public int DiscountPercent { get; set; } = 15;
        public int HoursThreshold { get; set; } = 24;
        public int DaysThreshold { get; set; } = 30;
    }
}
