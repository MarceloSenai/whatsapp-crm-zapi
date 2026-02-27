namespace WhatsAppCrm.Web.Entities;

public class ContactFeedback
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..25];
    public string ContactId { get; set; } = string.Empty;
    public Contact Contact { get; set; } = null!;
    public string Author { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Type { get; set; } = "note"; // note|call|meeting|task
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
