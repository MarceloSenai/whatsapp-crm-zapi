using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using WhatsAppCrm.Web.Data;

namespace WhatsAppCrm.Web.Services;

public interface ICampaignQueue
{
    void Enqueue(string campaignId);
}

public class CampaignRunner : BackgroundService, ICampaignQueue
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CampaignRunner> _logger;

    public CampaignRunner(IServiceScopeFactory scopeFactory, ILogger<CampaignRunner> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void Enqueue(string campaignId) => _channel.Writer.TryWrite(campaignId);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var campaignId in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessCampaignAsync(campaignId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing campaign {CampaignId}", campaignId);
            }
        }
    }

    private async Task ProcessCampaignAsync(string campaignId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var zapi = scope.ServiceProvider.GetRequiredService<IZApiService>();
        var rng = Random.Shared;

        var campaign = await db.Campaigns
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == campaignId, ct);

        if (campaign is null) return;

        var pendingMessages = campaign.Messages
            .Where(m => m.Status == "pending")
            .ToList();

        var rateLimit = campaign.RateLimit > 0 ? campaign.RateLimit : 30;
        var intervalMs = (int)(60.0 / rateLimit * 1000);

        // =============================================
        // MODO Z-API: Envio real via Z-API
        // =============================================
        if (!zapi.IsSimulationMode)
        {
            _logger.LogInformation("[Z-API Campaign] Iniciando envio real. Campaign={Id}, Messages={Count}", campaignId, pendingMessages.Count);

            foreach (var msg in pendingMessages)
            {
                if (ct.IsCancellationRequested) break;

                // Buscar contato para pegar o telefone
                var contact = await db.Contacts.FindAsync([msg.ContactId], ct);
                if (contact == null || contact.OptedOut)
                {
                    msg.Status = "failed";
                    await db.SaveChangesAsync(ct);
                    continue;
                }

                // Substituir variaveis no template
                var messageText = campaign.TemplateText
                    .Replace("{{nome}}", contact.Name)
                    .Replace("{{1}}", contact.Name)
                    .Replace("{{telefone}}", contact.Phone);

                // Enviar via Z-API
                var result = await zapi.SendTextAsync(contact.Phone, messageText);

                if (result != null)
                {
                    msg.Status = "sent";
                    msg.SentAt = DateTime.UtcNow;
                    msg.DeliveredAt = null;
                    msg.ReadAt = null;

                    _logger.LogInformation("[Z-API Campaign] Mensagem enviada para {Phone}, zaapId={ZaapId}",
                        contact.Phone, result.ZaapId);
                }
                else
                {
                    msg.Status = "failed";
                    _logger.LogWarning("[Z-API Campaign] Falha ao enviar para {Phone}", contact.Phone);
                }

                await db.SaveChangesAsync(ct);

                // Respeitar rate limit
                await Task.Delay(intervalMs, ct);
            }
        }
        else
        {
            // =============================================
            // MODO SIMULACAO: Comportamento original (demo)
            // =============================================
            foreach (var msg in pendingMessages)
            {
                if (ct.IsCancellationRequested) break;

                // 1. Mark as sent (immediate)
                msg.Status = "sent";
                msg.SentAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);

                // 2. After 1-3s random delay: mark as delivered
                await Task.Delay(rng.Next(1000, 3001), ct);
                msg.Status = "delivered";
                msg.DeliveredAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);

                // 3. 50% chance after 3-8s: mark as read
                if (rng.NextDouble() < 0.5)
                {
                    await Task.Delay(rng.Next(3000, 8001), ct);
                    msg.Status = "read";
                    msg.ReadAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                }

                // 4. 20% chance after 8-15s: mark as replied
                if (rng.NextDouble() < 0.2)
                {
                    await Task.Delay(rng.Next(8000, 15001), ct);
                    msg.Status = "replied";
                    msg.RepliedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                }

                // Wait between messages according to rate limit
                await Task.Delay(intervalMs, ct);
            }
        }

        // Mark campaign as completed
        campaign.Status = "completed";
        campaign.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Campaign {CampaignId} completed. Processed {Count} messages.", campaignId, pendingMessages.Count);
    }
}
