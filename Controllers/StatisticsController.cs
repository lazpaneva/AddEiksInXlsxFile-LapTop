using System;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AddEiksInXlsxFile.Data;
using AddEiksInXlsxFile.Models;
using AddEiksInXlsxFile.Services;

namespace AddEiksInXlsxFile.Controllers
{
    [Authorize]
    public class StatisticsController : Controller
    {
        private readonly ApplicationDbContext _db;

        public StatisticsController(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index(DateTime? from, DateTime? to, string? userId)
        {
            var vm = new StatisticsViewModel();

            var start = from?.Date ?? DateTime.UtcNow.Date.AddDays(-7);
            var end = to?.Date.AddDays(1).AddTicks(-1) ?? DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);

            var operatorStats = _db.ProcessingStatistics.AsQueryable()
                .Where(s =>
                    (s.OutputFilePath != null && s.OutputFilePath.EndsWith("-operator-result.xlsx")) ||
                    (s.InputFile2 != null && s.InputFile2.EndsWith("-operator-result.xlsx")));

            vm.From = start;
            vm.To = end;
            vm.Users = await _db.Users
                .Where(u => u.UserName != null && u.UserName != string.Empty)
                .Select(u => u.UserName!)
                .OrderBy(u => u)
                .ToListAsync();

            var currentUser = User?.Identity?.Name;
            vm.SelectedUserId = !string.IsNullOrWhiteSpace(userId)
                ? userId
                : vm.Users.FirstOrDefault(u => u == currentUser) ?? vm.Users.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(vm.SelectedUserId))
            {
                return View(vm);
            }

            var periodStats = await operatorStats
                .Where(s => s.TimestampUtc >= start && s.TimestampUtc <= end)
                .Where(s => s.UserId == vm.SelectedUserId)
                .Select(s => new OperatorStatSnapshot
                {
                    Id = s.Id,
                    TimestampUtc = s.TimestampUtc,
                    FileNameOrPath = s.OutputFilePath ?? s.InputFile2 ?? string.Empty,
                    MatchedCount = s.MatchedCount,
                    UniqueEiksCount = s.UniqueEiksCount,
                    TotalRows = s.TotalRows
                })
                .ToListAsync();

            var aggregated = StatisticsCalculationService.AggregateLatestByFile(periodStats);
            vm.ProcessedExclamations = aggregated.ProcessedExclamations;
            vm.UniqueEiksFromProcessedExclamations = aggregated.UniqueEiksFromProcessedExclamations;
            vm.TotalExclamationsAtPeriodStart = aggregated.TotalExclamationsAtPeriodStart;
            vm.Files = aggregated.Files;

            return View(vm);
        }
    }
}
