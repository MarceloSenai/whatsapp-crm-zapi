using Microsoft.EntityFrameworkCore;
using WhatsAppCrm.Web.Data;

namespace WhatsAppCrm.Web.Api;

public static class DashboardApi
{
    public static IEndpointRouteBuilder MapDashboardApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/dashboard", async (string? from, string? to, string? campaignId, string? source, AppDbContext db) =>
        {
            var dateFrom = !string.IsNullOrEmpty(from)
                ? DateTime.SpecifyKind(DateTime.Parse(from), DateTimeKind.Utc)
                : DateTime.UtcNow.AddDays(-30);
            var dateTo = !string.IsNullOrEmpty(to)
                ? DateTime.SpecifyKind(DateTime.Parse(to), DateTimeKind.Utc).AddDays(1)
                : DateTime.UtcNow.AddDays(1);

            // Base queries with date filter
            var contactsQuery = db.Contacts.AsNoTracking()
                .Where(c => c.CreatedAt >= dateFrom && c.CreatedAt < dateTo);

            if (!string.IsNullOrEmpty(campaignId))
                contactsQuery = contactsQuery.Where(c => c.LeadCampaignId == campaignId);
            if (!string.IsNullOrEmpty(source))
                contactsQuery = contactsQuery.Where(c => c.UtmSource == source);

            var totalLeads = await contactsQuery.CountAsync();

            // Spend
            var spendQuery = db.CampaignSpendDailies.AsNoTracking()
                .Where(s => s.Date >= DateOnly.FromDateTime(dateFrom) && s.Date <= DateOnly.FromDateTime(dateTo));
            if (!string.IsNullOrEmpty(campaignId))
                spendQuery = spendQuery.Where(s => s.CampaignId == campaignId);

            var totalSpend = await spendQuery.SumAsync(s => s.Amount);

            // Conversions
            var conversionsQuery = db.Conversions.AsNoTracking()
                .Where(c => c.ConvertedAt >= dateFrom && c.ConvertedAt < dateTo);
            if (!string.IsNullOrEmpty(campaignId))
                conversionsQuery = conversionsQuery.Where(c => c.CampaignId == campaignId);

            var conversions = await conversionsQuery.CountAsync();
            var revenue = await conversionsQuery.SumAsync(c => c.Value);

            // KPI calculations
            var cpl = totalLeads > 0 ? totalSpend / totalLeads : 0;
            var roas = totalSpend > 0 ? revenue / totalSpend : 0;
            var roi = totalSpend > 0 ? (revenue - totalSpend) / totalSpend * 100 : 0;

            // Funnel
            var funnel = await db.Stages.AsNoTracking()
                .OrderBy(s => s.Order)
                .Select(s => new
                {
                    stage = s.Name,
                    count = s.Deals.Count,
                    value = s.Deals.Sum(d => d.Value)
                })
                .ToListAsync();

            // Leads over time (GroupBy Date in DB, format in memory)
            var leadsOverTimeRaw = await contactsQuery
                .GroupBy(c => c.CreatedAt.Date)
                .Select(g => new { date = g.Key, count = g.Count() })
                .OrderBy(x => x.date)
                .ToListAsync();

            var leadsOverTime = leadsOverTimeRaw.Select(x => new
            {
                date = x.date.ToString("yyyy-MM-dd"),
                count = x.count
            }).ToList();

            // Spend vs Revenue over time
            var spendByDate = await spendQuery
                .GroupBy(s => s.Date)
                .Select(g => new { date = g.Key, spend = g.Sum(s => s.Amount) })
                .OrderBy(x => x.date)
                .ToListAsync();

            var conversionsByDateRaw = await conversionsQuery
                .GroupBy(c => c.ConvertedAt.Date)
                .Select(g => new { date = g.Key, revenue = g.Sum(c => c.Value) })
                .ToListAsync();

            var conversionsByDate = conversionsByDateRaw.Select(x => new
            {
                date = DateOnly.FromDateTime(x.date),
                revenue = x.revenue
            }).ToList();

            var allDates = spendByDate.Select(s => s.date)
                .Union(conversionsByDate.Select(c => c.date))
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            var spendVsRevenue = allDates.Select(d => new
            {
                date = d.ToString("yyyy-MM-dd"),
                spend = spendByDate.FirstOrDefault(s => s.date == d)?.spend ?? 0,
                revenue = conversionsByDate.FirstOrDefault(c => c.date == d)?.revenue ?? 0
            }).ToList();

            // Source distribution (coalesce in DB, group in memory to be safe)
            var sourceRaw = await contactsQuery
                .Select(c => c.UtmSource)
                .ToListAsync();

            var sourceDistribution = sourceRaw
                .GroupBy(s => s ?? "direto")
                .Select(g => new { source = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .ToList();

            // Campaign ranking
            var campaignRanking = await db.Campaigns.AsNoTracking()
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.Platform,
                    spend = c.SpendDaily
                        .Where(s => s.Date >= DateOnly.FromDateTime(dateFrom) && s.Date <= DateOnly.FromDateTime(dateTo))
                        .Sum(s => s.Amount),
                    leads = c.LeadsFromThis
                        .Count(ct => ct.CreatedAt >= dateFrom && ct.CreatedAt < dateTo),
                    conversions = c.Conversions
                        .Count(cv => cv.ConvertedAt >= dateFrom && cv.ConvertedAt < dateTo),
                    revenue = c.Conversions
                        .Where(cv => cv.ConvertedAt >= dateFrom && cv.ConvertedAt < dateTo)
                        .Sum(cv => cv.Value)
                })
                .Where(c => c.spend > 0 || c.leads > 0)
                .ToListAsync();

            var ranking = campaignRanking.Select(c => new
            {
                c.Id,
                c.Name,
                c.Platform,
                c.spend,
                c.leads,
                c.conversions,
                c.revenue,
                cpl = c.leads > 0 ? c.spend / c.leads : 0,
                roas = c.spend > 0 ? c.revenue / c.spend : 0
            }).OrderByDescending(c => c.revenue).ToList();

            return Results.Ok(new
            {
                kpi = new { totalSpend, totalLeads, cpl, conversions, revenue, roas, roi },
                funnel,
                leadsOverTime,
                spendVsRevenue,
                sourceDistribution,
                campaignRanking = ranking
            });
        });

        return app;
    }
}
