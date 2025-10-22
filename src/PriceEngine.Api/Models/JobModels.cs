namespace PriceEngine.Api.Models;

public enum JobStatus { Queued, Running, Succeeded, Failed }

public class BackgroundJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = default!;            // "BulkPriceUpdate"
    public string? PayloadJson { get; set; }                // {"percent":10}
    public JobStatus Status { get; set; } = JobStatus.Queued;
    public int? Total { get; set; }                         // toplam kayıt sayısı
    public int? Processed { get; set; }                     // işlenen
    public string? Error { get; set; }                      // hata detayı
    public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}

public record BulkPriceUpdateRequest(decimal Percent);  // +%10 için 10, indirim için -10
