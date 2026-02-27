using Microsoft.EntityFrameworkCore;
using WhatsAppCrm.Web.Data;
using WhatsAppCrm.Web.Entities;

namespace WhatsAppCrm.Web.Api;

public static class ContactFeedbacksApi
{
    private record CreateFeedbackRequest(string Author, string Text, string Type);

    public static IEndpointRouteBuilder MapContactFeedbacksApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/contacts/{id}/feedbacks", async (string id, AppDbContext db) =>
        {
            var feedbacks = await db.ContactFeedbacks
                .AsNoTracking()
                .Where(f => f.ContactId == id)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new { f.Id, f.ContactId, f.Author, f.Text, f.Type, f.CreatedAt })
                .ToListAsync();

            return Results.Ok(feedbacks);
        });

        app.MapPost("/api/contacts/{id}/feedbacks", async (string id, CreateFeedbackRequest request, AppDbContext db) =>
        {
            if (string.IsNullOrEmpty(request.Author) || string.IsNullOrEmpty(request.Text))
                return Results.BadRequest(new { error = "author and text required" });

            var contact = await db.Contacts.FindAsync(id);
            if (contact is null)
                return Results.NotFound(new { error = "Contact not found" });

            var feedback = new ContactFeedback
            {
                ContactId = id,
                Author = request.Author,
                Text = request.Text,
                Type = request.Type ?? "note"
            };

            db.ContactFeedbacks.Add(feedback);
            await db.SaveChangesAsync();

            return Results.Created($"/api/contacts/{id}/feedbacks/{feedback.Id}", feedback);
        });

        return app;
    }
}
