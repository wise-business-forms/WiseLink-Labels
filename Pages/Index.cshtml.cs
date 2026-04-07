using CERM.DataAccess;
using CERM.DataAccess.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WiseLabels.Services;

namespace WiseLabels.Pages
{
    public class IndexModel : PageModel
    {
        private readonly CermDbContext _context;
        private readonly IUserImpersonationService _impersonationService;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(
            CermDbContext context,
            IUserImpersonationService impersonationService,
            ILogger<IndexModel> logger)
        {
            _context = context;
            _impersonationService = impersonationService;
            _logger = logger;
        }

        public List<Estimate> RecentQuotes { get; set; } = new();
        public int TotalQuotesCount { get; set; }
        public int QuotesThisMonth { get; set; }
        public string? CurrentView { get; set; }
        public string? SearchTerm { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalPages { get; set; }

        // Chart data properties
        public List<string> MonthLabels { get; set; } = new();
        public List<int> MonthlyQuoteCounts { get; set; } = new();
        public List<string> DailyLabels { get; set; } = new();
        public List<int> DailyQuoteCounts { get; set; } = new();

        public async Task OnGetAsync(string? view = null, string? search = null, int page = 1)
        {
            CurrentView = view;
            SearchTerm = search;
            CurrentPage = page;

            // Check if user should see filtered data
            var shouldFilter = await _impersonationService.ShouldFilterDataAsync();
            var effectiveUserId = await _impersonationService.GetEffectiveUserIdAsync();

            // Build quote query
            var quotesQuery = _context.Estimates.AsNoTracking();

            // Apply filtering if needed
            if (shouldFilter && !string.IsNullOrWhiteSpace(effectiveUserId))
            {
                quotesQuery = quotesQuery.Where(e => e.SalesRepresentativeId == effectiveUserId);
                _logger.LogInformation("Filtering quotes for user {UserId}", effectiveUserId);
            }
            else
            {
                _logger.LogInformation("Showing all quotes (admin with no impersonation)");
            }

            // Get total count
            TotalQuotesCount = await quotesQuery.CountAsync();

            // Get count for this month
            var firstDayOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            QuotesThisMonth = await quotesQuery
                .Where(e => e.OrderDate >= firstDayOfMonth)
                .CountAsync();

            // Handle drill-down views
            if (!string.IsNullOrWhiteSpace(view))
            {
                switch (view.ToLower())
                {
                    case "total":
                        await LoadAllQuotesAsync(quotesQuery, search, page);
                        break;
                    case "month":
                        await LoadMonthQuotesAsync(quotesQuery, search, page);
                        break;
                    case "recent":
                        await LoadRecentQuotesAsync(quotesQuery, search, page);
                        break;
                    default:
                        await LoadDefaultQuotesAsync(quotesQuery);
                        break;
                }
            }
            else
            {
                // Default view - show recent quotes
                await LoadDefaultQuotesAsync(quotesQuery);
            }

            _logger.LogInformation("Loaded {Count} quotes. Total: {Total}, This Month: {ThisMonth}",
                RecentQuotes.Count, TotalQuotesCount, QuotesThisMonth);

            // Load chart data
            await LoadChartDataAsync(quotesQuery);
        }

        private async Task LoadChartDataAsync(IQueryable<Estimate> baseQuery)
        {
            // Monthly data for past 12 months
            var twelveMonthsAgo = DateTime.Now.AddMonths(-11).Date;
            var firstOfTwelveMonthsAgo = new DateTime(twelveMonthsAgo.Year, twelveMonthsAgo.Month, 1);

            var monthlyData = await baseQuery
                .Where(e => e.OrderDate >= firstOfTwelveMonthsAgo)
                .GroupBy(e => new { Year = e.OrderDate.Value.Year, Month = e.OrderDate.Value.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .OrderBy(g => g.Year).ThenBy(g => g.Month)
                .ToListAsync();

            // Generate all 12 months
            for (int i = 11; i >= 0; i--)
            {
                var date = DateTime.Now.AddMonths(-i);
                var monthStart = new DateTime(date.Year, date.Month, 1);
                MonthLabels.Add(monthStart.ToString("MMM yyyy"));

                var dataPoint = monthlyData.FirstOrDefault(d => d.Year == date.Year && d.Month == date.Month);
                MonthlyQuoteCounts.Add(dataPoint?.Count ?? 0);
            }

            // Daily data for past 30 days
            var thirtyDaysAgo = DateTime.Now.AddDays(-29).Date;

            var dailyData = await baseQuery
                .Where(e => e.OrderDate >= thirtyDaysAgo)
                .GroupBy(e => e.OrderDate.Value.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(g => g.Date)
                .ToListAsync();

            // Generate all 30 days
            for (int i = 29; i >= 0; i--)
            {
                var date = DateTime.Now.AddDays(-i).Date;
                DailyLabels.Add(date.ToString("M/d"));

                var dataPoint = dailyData.FirstOrDefault(d => d.Date == date);
                DailyQuoteCounts.Add(dataPoint?.Count ?? 0);
            }
        }

        private async Task LoadDefaultQuotesAsync(IQueryable<Estimate> baseQuery)
        {
            RecentQuotes = await baseQuery
                .OrderByDescending(e => e.OrderDate)
                .Take(20)
                .ToListAsync();
        }

        private async Task LoadAllQuotesAsync(IQueryable<Estimate> baseQuery, string? search, int page)
        {
            var query = baseQuery;

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(e => 
                    e.EstimateId.Contains(search) ||
                    (e.CustomerName != null && e.CustomerName.Contains(search)) ||
                    (e.Description != null && e.Description.Contains(search)) ||
                    (e.CustomerId != null && e.CustomerId.Contains(search)));
            }

            // Calculate pagination
            var totalCount = await query.CountAsync();
            TotalPages = (int)Math.Ceiling(totalCount / (double)PageSize);
            CurrentPage = Math.Max(1, Math.Min(page, Math.Max(1, TotalPages)));

            RecentQuotes = await query
                .OrderByDescending(e => e.OrderDate)
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }

        private async Task LoadMonthQuotesAsync(IQueryable<Estimate> baseQuery, string? search, int page)
        {
            var firstDayOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var query = baseQuery.Where(e => e.OrderDate >= firstDayOfMonth);

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(e => 
                    e.EstimateId.Contains(search) ||
                    (e.CustomerName != null && e.CustomerName.Contains(search)) ||
                    (e.Description != null && e.Description.Contains(search)) ||
                    (e.CustomerId != null && e.CustomerId.Contains(search)));
            }

            // Calculate pagination
            var totalCount = await query.CountAsync();
            TotalPages = (int)Math.Ceiling(totalCount / (double)PageSize);
            CurrentPage = Math.Max(1, Math.Min(page, Math.Max(1, TotalPages)));

            RecentQuotes = await query
                .OrderByDescending(e => e.OrderDate)
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }

        private async Task LoadRecentQuotesAsync(IQueryable<Estimate> baseQuery, string? search, int page)
        {
            var query = baseQuery.OrderByDescending(e => e.OrderDate).Take(100);

            // Apply search filter (on the recent 100)
            var allRecent = await query.ToListAsync();

            if (!string.IsNullOrWhiteSpace(search))
            {
                allRecent = allRecent.Where(e => 
                    e.EstimateId.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (e.CustomerName != null && e.CustomerName.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (e.Description != null && e.Description.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (e.CustomerId != null && e.CustomerId.Contains(search, StringComparison.OrdinalIgnoreCase))).ToList();
            }

            // Calculate pagination
            var totalCount = allRecent.Count;
            TotalPages = (int)Math.Ceiling(totalCount / (double)PageSize);
            CurrentPage = Math.Max(1, Math.Min(page, Math.Max(1, TotalPages)));

            RecentQuotes = allRecent
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();
        }
    }
}
