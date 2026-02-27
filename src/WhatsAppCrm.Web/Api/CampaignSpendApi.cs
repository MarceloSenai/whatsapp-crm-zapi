using Microsoft.EntityFrameworkCore;
using WhatsAppCrm.Web.Data;
using WhatsAppCrm.Web.Entities;

namespace WhatsAppCrm.Web.Api;

public static class CampaignSpendApi
{
    private record UpsertSpendRequest(string Date, double Amount);

    public static IEndpointRouteBuilder MapCampaignSpendApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/campaigns/{id}/spend", async (string id, AppDbContext db) =>
        {
            var spend = await db.CampaignSpendDailies
                .AsNoTracking()
                .Where(s => s.CampaignId == id)
                .OrderBy(s => s.Date)
                .Select(s => new { s.Id, s.CampaignId, Date = s.Date.ToString("yyyy-MM-dd"), s.Amount })
                .ToListAsync();

            return Results.Ok(spend);
        });

        app.MapPost("/api/campaigns/{id}/spend", async (string id, UpsertSpendRequest request, AppDbContext db) =>
        {
            if (string.IsNullOrEmpty(request.Date))
                return Results.BadRequest(new { error = "date required" });

            var campaign = await db.Campaigns.FindAsync(id);
            if (campaign is null)
                return Results.NotFound(new { error = "Campaign not found" });

            var date = DateOnly.Parse(request.Date);

            var existing = await db.CampaignSpendDailies
                .FirstOrDefaultAsync(s => s.CampaignId == id && s.Date == date);

            if (existing is not null)
            {
                existing.Amount = request.Amount;
            }
            else
            {
                db.CampaignSpendDailies.Add(new CampaignSpendDaily
                {
                    CampaignId = id,
                    Date = date,
                    Amount = request.Amount
                });
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { ok = true });
        });

        return app;
    }
}
