using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WhatsAppCrm.Web.Data;
using WhatsAppCrm.Web.Entities;

namespace WhatsAppCrm.Web.Api;

/// <summary>
/// Endpoints para receber webhooks da Z-API:
/// - POST /api/zapi/webhook/receive      → mensagens recebidas
/// - POST /api/zapi/webhook/status        → status de mensagens (sent, delivered, read)
/// - POST /api/zapi/webhook/disconnected  → notificacao de desconexao
/// - GET  /api/zapi/status                → status da conexao Z-API
/// </summary>
public static class ZApiWebhookApi
{
    public static IEndpointRouteBuilder MapZApiWebhookApi(this IEndpointRouteBuilder app)
    {
        // ========================
        // Webhook: Mensagem Recebida
        // ========================
        app.MapPost("/api/zapi/webhook/receive", async (HttpContext ctx, AppDbContext db, ILogger<Program> logger) =>
        {
            try
            {
                using var reader = new StreamReader(ctx.Request.Body);
                var body = await reader.ReadToEndAsync();
                logger.LogInformation("[Z-API Webhook] Receive: {Body}", body);

                var payload = JsonSerializer.Deserialize<JsonElement>(body);

                // Ignorar mensagens proprias (fromMe)
                if (payload.TryGetProperty("fromMe", out var fromMe) && fromMe.GetBoolean())
                    return Results.Ok(new { ok = true, skipped = "fromMe" });

                // Ignorar grupos
                if (payload.TryGetProperty("isGroup", out var isGroup) && isGroup.GetBoolean())
                    return Results.Ok(new { ok = true, skipped = "isGroup" });

                // Extrair dados da mensagem
                var phone = payload.TryGetProperty("phone", out var p) ? p.GetString() : null;
                var messageId = payload.TryGetProperty("messageId", out var mid) ? mid.GetString() : null;
                var senderName = payload.TryGetProperty("senderName", out var sn) ? sn.GetString() : null;
                var senderPhoto = payload.TryGetProperty("senderPhoto", out var sp) ? sp.GetString() : null;

                if (string.IsNullOrEmpty(phone)) return Results.BadRequest(new { error = "phone missing" });

                // Extrair texto da mensagem
                string? messageText = null;
                if (payload.TryGetProperty("text", out var textObj))
                {
                    if (textObj.ValueKind == JsonValueKind.Object &&
                        textObj.TryGetProperty("message", out var msg))
                    {
                        messageText = msg.GetString();
                    }
                    else if (textObj.ValueKind == JsonValueKind.String)
                    {
                        messageText = textObj.GetString();
                    }
                }

                // Para imagens com caption
                if (string.IsNullOrEmpty(messageText) &&
                    payload.TryGetProperty("image", out var imgObj) &&
                    imgObj.TryGetProperty("caption", out var caption))
                {
                    messageText = $"[Imagem] {caption.GetString()}";
                }

                // Para audio
                if (string.IsNullOrEmpty(messageText) && payload.TryGetProperty("audio", out _))
                {
                    messageText = "[Audio]";
                }

                // Para documento
                if (string.IsNullOrEmpty(messageText) &&
                    payload.TryGetProperty("document", out var docObj))
                {
                    var fileName = docObj.TryGetProperty("fileName", out var fn) ? fn.GetString() : "documento";
                    messageText = $"[Documento: {fileName}]";
                }

                // Para sticker
                if (string.IsNullOrEmpty(messageText) && payload.TryGetProperty("sticker", out _))
                {
                    messageText = "[Sticker]";
                }

                // Para localizacao
                if (string.IsNullOrEmpty(messageText) && payload.TryGetProperty("location", out _))
                {
                    messageText = "[Localizacao]";
                }

                // Para contato
                if (string.IsNullOrEmpty(messageText) && payload.TryGetProperty("contact", out _))
                {
                    messageText = "[Contato compartilhado]";
                }

                if (string.IsNullOrEmpty(messageText))
                    messageText = "[Mensagem nao suportada]";

                // Normalizar telefone para busca
                var normalizedPhone = NormalizePhoneForSearch(phone);

                // Buscar ou criar contato
                var contact = await db.Contacts
                    .FirstOrDefaultAsync(c => c.Phone.Contains(normalizedPhone.Substring(Math.Max(0, normalizedPhone.Length - 11))));

                if (contact == null)
                {
                    // Criar novo contato automaticamente
                    contact = new Contact
                    {
                        Name = senderName ?? $"WhatsApp {phone}",
                        Phone = FormatPhone(phone),
                        AvatarUrl = senderPhoto,
                        Tags = "[\"lead\",\"zapi\"]"
                    };
                    db.Contacts.Add(contact);
                    await db.SaveChangesAsync();
                    logger.LogInformation("[Z-API] Novo contato criado: {Name} ({Phone})", contact.Name, contact.Phone);
                }

                // Buscar ou criar conversa
                var conversation = await db.Conversations
                    .FirstOrDefaultAsync(c => c.ContactId == contact.Id);

                if (conversation == null)
                {
                    conversation = new Conversation
                    {
                        ContactId = contact.Id,
                        Status = "open",
                        Channel = "whatsapp-zapi",
                        LastMessageAt = DateTime.UtcNow,
                        UnreadCount = 1
                    };
                    db.Conversations.Add(conversation);
                    await db.SaveChangesAsync();
                    logger.LogInformation("[Z-API] Nova conversa criada para {ContactName}", contact.Name);
                }
                else
                {
                    conversation.LastMessageAt = DateTime.UtcNow;
                    conversation.UnreadCount++;
                    conversation.Status = "open";
                    await db.SaveChangesAsync();
                }

                // Criar mensagem inbound
                var message = new Message
                {
                    ConversationId = conversation.Id,
                    Direction = "inbound",
                    Content = messageText,
                    Status = "read",
                    Metadata = JsonSerializer.Serialize(new
                    {
                        zapiMessageId = messageId,
                        phone,
                        senderName,
                        source = "zapi-webhook"
                    })
                };
                db.Messages.Add(message);
                await db.SaveChangesAsync();

                logger.LogInformation("[Z-API] Mensagem recebida de {Phone}: {Text}", phone, messageText);
                return Results.Ok(new { ok = true, messageId = message.Id });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Z-API Webhook] Erro ao processar mensagem recebida");
                return Results.Ok(new { ok = false, error = ex.Message });
            }
        });

        // ========================
        // Webhook: Status de Mensagem
        // ========================
        app.MapPost("/api/zapi/webhook/status", async (HttpContext ctx, AppDbContext db, ILogger<Program> logger) =>
        {
            try
            {
                using var reader = new StreamReader(ctx.Request.Body);
                var body = await reader.ReadToEndAsync();
                logger.LogInformation("[Z-API Webhook] Status: {Body}", body);

                var payload = JsonSerializer.Deserialize<JsonElement>(body);

                var status = payload.TryGetProperty("status", out var s) ? s.GetString()?.ToUpperInvariant() : null;
                var ids = new List<string>();

                if (payload.TryGetProperty("ids", out var idsArr) && idsArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var id in idsArr.EnumerateArray())
                    {
                        var val = id.GetString();
                        if (val != null) ids.Add(val);
                    }
                }

                if (string.IsNullOrEmpty(status) || ids.Count == 0)
                    return Results.Ok(new { ok = true, skipped = "no status or ids" });

                // Mapear status Z-API para nosso modelo
                var newStatus = status switch
                {
                    "SENT" => "sent",
                    "RECEIVED" => "delivered",
                    "READ" => "read",
                    "PLAYED" => "read",
                    _ => (string?)null
                };

                if (newStatus == null)
                    return Results.Ok(new { ok = true, skipped = $"unhandled status: {status}" });

                // Atualizar mensagens que tenham o zapiMessageId no metadata
                foreach (var zapiId in ids)
                {
                    var messages = await db.Messages
                        .Where(m => m.Metadata != null && m.Metadata.Contains(zapiId))
                        .ToListAsync();

                    foreach (var msg in messages)
                    {
                        // Só atualiza se for progressao (sent → delivered → read)
                        var currentPriority = StatusPriority(msg.Status);
                        var newPriority = StatusPriority(newStatus);
                        if (newPriority > currentPriority)
                        {
                            msg.Status = newStatus;
                            logger.LogInformation("[Z-API] Status atualizado: {Id} → {Status}", msg.Id, newStatus);
                        }
                    }
                }

                await db.SaveChangesAsync();
                return Results.Ok(new { ok = true, status = newStatus, updated = ids.Count });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Z-API Webhook] Erro ao processar status");
                return Results.Ok(new { ok = false, error = ex.Message });
            }
        });

        // ========================
        // Webhook: Desconexao
        // ========================
        app.MapPost("/api/zapi/webhook/disconnected", async (HttpContext ctx, ILogger<Program> logger) =>
        {
            using var reader = new StreamReader(ctx.Request.Body);
            var body = await reader.ReadToEndAsync();
            logger.LogWarning("[Z-API Webhook] Desconectado: {Body}", body);
            return Results.Ok(new { ok = true });
        });

        // ========================
        // Status da Conexao Z-API (para UI)
        // ========================
        app.MapGet("/api/zapi/status", async (Services.IZApiService zapi) =>
        {
            var status = await zapi.GetConnectionStatusAsync();
            return Results.Ok(new
            {
                configured = zapi.IsConfigured,
                simulationMode = zapi.IsSimulationMode,
                connected = status.Connected,
                phone = status.Phone,
                smartphoneConnected = status.SmartphoneConnected
            });
        });

        // ========================
        // QR Code para conexao
        // ========================
        app.MapGet("/api/zapi/qrcode", async (Services.IZApiService zapi) =>
        {
            var qrCode = await zapi.GetQrCodeAsync();
            return Results.Ok(new { qrCode });
        });

        return app;
    }

    private static int StatusPriority(string status) => status switch
    {
        "sent" => 1,
        "delivered" => 2,
        "read" => 3,
        _ => 0
    };

    private static string NormalizePhoneForSearch(string phone)
    {
        return new string(phone.Where(char.IsDigit).ToArray());
    }

    private static string FormatPhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length >= 12 && digits.StartsWith("55"))
        {
            var ddd = digits.Substring(2, 2);
            var number = digits.Substring(4);
            if (number.Length == 9)
                return $"+55 ({ddd}) {number.Substring(0, 5)}-{number.Substring(5)}";
            if (number.Length == 8)
                return $"+55 ({ddd}) {number.Substring(0, 4)}-{number.Substring(4)}";
        }
        return $"+{digits}";
    }
}
