using System;
using System.Linq;
using System.Threading.Tasks;
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

        public async Task<IActionResult> Index(DateTime? from, DateTime? to)
        {
            var vm = new StatisticsViewModel();

            var start = from?.Date ?? DateTime.UtcNow.Date.AddDays(-7);
            var end = to?.Date.AddDays(1).AddTicks(-1) ?? DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);

            var query = _db.ProcessingStatistics.AsQueryable()
                .Where(s => s.TimestampUtc >= start && s.TimestampUtc <= end)
                .Where(s =>
                    (s.OutputFilePath != null && s.OutputFilePath.EndsWith("-operator-result.xlsx")) ||
                    (s.InputFile2 != null && s.InputFile2.EndsWith("-operator-result.xlsx")));

            vm.From = start;
            vm.To = end;

            vm.ProcessedExclamations = await query.SumAsync(s => (int?)s.MatchedCount) ?? 0;
            vm.UniqueEiksFromProcessedExclamations = await query.SumAsync(s => (int?)s.UniqueEiksCount) ?? 0;
            vm.TotalExclamationsAtPeriodStart = await query.SumAsync(s => (int?)s.TotalRows) ?? 0;

            return View(vm);
        }
    }
}
