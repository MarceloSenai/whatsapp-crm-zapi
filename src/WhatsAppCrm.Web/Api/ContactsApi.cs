using Microsoft.EntityFrameworkCore;
using WhatsAppCrm.Web.Data;
using WhatsAppCrm.Web.Entities;

namespace WhatsAppCrm.Web.Api;

public static class ContactsApi
{
    private record ToggleOptOutRequest(string Id, bool OptedOut);
    private record CreateContactRequest(
        string Name, string Phone, string? Email, string? Tags,
        string? UtmSource, string? UtmMedium, string? UtmCampaign,
        string? UtmContent, string? UtmTerm, string? Gclid, string? Fbclid,
        string? LeadCampaignId);

    public static IEndpointRouteBuilder MapContactsApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/contacts", async (AppDbContext db) =>
        {
            var contacts = await db.Contacts
                .AsNoTracking()
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.Phone,
                    c.Email,
                    c.AvatarUrl,
                    c.Tags,
                    c.OptedOut,
                    c.OptOutAt,
                    c.UtmSource,
                    c.CreatedAt,
                    c.UpdatedAt,
                    ConversationCount = c.Conversations.Count,
                    DealCount = c.Deals.Count
                })
                .ToListAsync();

            return Results.Ok(contacts);
        });

        app.MapPost("/api/contacts", async (CreateContactRequest request, AppDbContext db) =>
        {
            if (string.IsNullOrEmpty(request.Name) || string.IsNullOrEmpty(request.Phone))
                return Results.BadRequest(new { error = "name and phone required" });

            var contact = new Contact
            {
                Name = request.Name,
                Phone = request.Phone,
                Email = request.Email,
                Tags = request.Tags ?? "[]",
                UtmSource = request.UtmSource,
                UtmMedium = request.UtmMedium,
                UtmCampaign = request.UtmCampaign,
                UtmContent = request.UtmContent,
                UtmTerm = request.UtmTerm,
                Gclid = request.Gclid,
                Fbclid = request.Fbclid,
                LeadCampaignId = request.LeadCampaignId
            };

            db.Contacts.Add(contact);
            await db.SaveChangesAsync();

            return Results.Created($"/api/contacts/{contact.Id}", contact);
        });

        app.MapPatch("/api/contacts", async (ToggleOptOutRequest request, AppDbContext db) =>
        {
            if (string.IsNullOrEmpty(request.Id))
                return Results.BadRequest(new { error = "id required" });

            var contact = await db.Contacts.FindAsync(request.Id);
            if (contact is null)
                return Results.NotFound(new { error = "Contact not found" });

            contact.OptedOut = request.OptedOut;
            contact.OptOutAt = request.OptedOut ? DateTime.UtcNow : null;
            contact.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(contact);
        });

        return app;
    }
}
