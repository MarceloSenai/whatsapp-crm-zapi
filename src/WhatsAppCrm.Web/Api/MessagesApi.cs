using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WhatsAppCrm.Web.Data;
using WhatsAppCrm.Web.Entities;
using WhatsAppCrm.Web.Services;

namespace WhatsAppCrm.Web.Api;

public static class MessagesApi
{
    private record SendMessageRequest(string ConversationId, string Content);

    public static IEndpointRouteBuilder MapMessagesApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/messages", async (string conversationId, AppDbContext db) =>
        {
            if (string.IsNullOrEmpty(conversationId))
                return Results.BadRequest(new { error = "conversationId required" });

            var messages = await db.Messages
                .Where(m => m.ConversationId == conversationId)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();

            // Mark conversation as read
            await db.Conversations
                .Where(c => c.Id == conversationId)
                .ExecuteUpdateAsync(c => c.SetProperty(x => x.UnreadCount, 0));

            return Results.Ok(messages);
        });

        app.MapPost("/api/messages", async (
            SendMessageRequest request,
            AppDbContext db,
            IServiceScopeFactory scopeFactory,
            IZApiService zapi) =>
        {
            if (string.IsNullOrEmpty(request.ConversationId) || string.IsNullOrEmpty(request.Content))
                return Results.BadRequest(new { error = "conversationId and content required" });

            // Buscar conversa + contato para pegar o telefone
            var conversation = await db.Conversations
                .Include(c => c.Contact)
                .FirstOrDefaultAsync(c => c.Id == request.ConversationId);

            if (conversation == null)
                return Results.NotFound(new { error = "Conversation not found" });

            // =============================================
            // MODO Z-API: Envia via Z-API real
            // =============================================
            if (!zapi.IsSimulationMode)
            {
                var phone = conversation.Contact.Phone;
                var sendResult = await zapi.SendTextAsync(phone, request.Content);

                // Salvar mensagem outbound com referencia Z-API
                var outbound = new Message
                {
                    ConversationId = request.ConversationId,
                    Direction = "outbound",
                    Content = request.Content,
                    Status = sendResult != null ? "sent" : "failed",
                    Metadata = JsonSerializer.Serialize(new
                    {
                        zapiMessageId = sendResult?.MessageId,
                        zapiZaapId = sendResult?.ZaapId,
                        phone,
                        source = "zapi"
                    })
                };
                db.Messages.Add(outbound);

                // Atualizar conversa
                conversation.LastMessageAt = DateTime.UtcNow;
                conversation.Status = "open";
                await db.SaveChangesAsync();

                return Results.Ok(outbound);
            }

            // =============================================
            // MODO SIMULACAO: Comportamento original (demo)
            // =============================================
            var simOutbound = new Message
            {
                ConversationId = request.ConversationId,
                Direction = "outbound",
                Content = request.Content,
                Status = "sent"
            };
            db.Messages.Add(simOutbound);
            await db.SaveChangesAsync();

            // Update conversation
            await db.Conversations
                .Where(c => c.Id == request.ConversationId)
                .ExecuteUpdateAsync(c => c
                    .SetProperty(x => x.LastMessageAt, DateTime.UtcNow)
                    .SetProperty(x => x.Status, "open"));

            // Capture values for the background task
            var outboundId = simOutbound.Id;
            var conversationId2 = request.ConversationId;
            var content = request.Content;

            // Fire-and-forget: simulate status progression and auto-response
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var bgDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    // Delivered after 1s
                    await Task.Delay(1000);
                    await bgDb.Messages
                        .Where(m => m.Id == outboundId)
                        .ExecuteUpdateAsync(m => m.SetProperty(x => x.Status, "delivered"));

                    // Read after 2s
                    await Task.Delay(2000);
                    await bgDb.Messages
                        .Where(m => m.Id == outboundId)
                        .ExecuteUpdateAsync(m => m.SetProperty(x => x.Status, "read"));

                    // Auto-response after random delay
                    var delay = WhatsAppSimulator.RandomDelayMs();
                    await Task.Delay(delay);

                    var responseText = WhatsAppSimulator.GenerateResponse(content);
                    bgDb.Messages.Add(new Message
                    {
                        ConversationId = conversationId2,
                        Direction = "inbound",
                        Content = responseText,
                        Status = "read"
                    });
                    await bgDb.SaveChangesAsync();

                    // Increment unread count and update last message time
                    await bgDb.Conversations
                        .Where(c => c.Id == conversationId2)
                        .ExecuteUpdateAsync(c => c
                            .SetProperty(x => x.LastMessageAt, DateTime.UtcNow)
                            .SetProperty(x => x.UnreadCount, x => x.UnreadCount + 1));
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Background message simulation error: {ex.Message}");
                }
            });

            return Results.Ok(simOutbound);
        });

        return app;
    }
}
