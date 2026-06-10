using System;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AddEiksInXlsxFile.Data;
using AddEiksInXlsxFile.Models;

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
                .Select(s => new
                {
                    s.Id,
                    s.TimestampUtc,
                    FileNameOrPath = s.OutputFilePath ?? s.InputFile2 ?? string.Empty,
                    s.MatchedCount,
                    s.UniqueEiksCount,
                    s.TotalRows
                })
                .ToListAsync();

            var latestStatsByFile = periodStats
                .GroupBy(s => Path.GetFileName(s.FileNameOrPath), StringComparer.OrdinalIgnoreCase)
                .Select(g => g
                    .OrderByDescending(s => s.TimestampUtc)
                    .ThenByDescending(s => s.Id)
                    .First())
                .ToList();

            vm.ProcessedExclamations = latestStatsByFile.Sum(s => s.MatchedCount);
            vm.UniqueEiksFromProcessedExclamations = latestStatsByFile.Sum(s => s.UniqueEiksCount);
            vm.TotalExclamationsAtPeriodStart = latestStatsByFile.Sum(s => s.TotalRows);
            vm.Files = latestStatsByFile
                .OrderBy(f => Path.GetFileName(f.FileNameOrPath))
                .Select(f => new FileStatisticsViewModel
                {
                    FileName = Path.GetFileName(f.FileNameOrPath),
                    ProcessedExclamations = f.MatchedCount,
                    UniqueEiksFromProcessedExclamations = f.UniqueEiksCount,
                    TotalExclamationsAtPeriodStart = f.TotalRows
                })
                .ToList();

            return View(vm);
        }
    }
}
