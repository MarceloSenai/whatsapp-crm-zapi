namespace WhatsAppCrm.Web.Entities;

public class Conversion
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..25];
    public string ContactId { get; set; } = string.Empty;
    public Contact Contact { get; set; } = null!;
    public string? CampaignId { get; set; }
    public Campaign? Campaign { get; set; }
    public string DealId { get; set; } = string.Empty;
    public Deal Deal { get; set; } = null!;
    public double Value { get; set; }
    public DateTime ConvertedAt { get; set; } = DateTime.UtcNow;
}
