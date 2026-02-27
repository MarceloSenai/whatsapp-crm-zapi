namespace WhatsAppCrm.Web.Entities;

public class CampaignSpendDaily
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..25];
    public string CampaignId { get; set; } = string.Empty;
    public Campaign Campaign { get; set; } = null!;
    public DateOnly Date { get; set; }
    public double Amount { get; set; }
}
