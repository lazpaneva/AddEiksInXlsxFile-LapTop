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
                .Where(s => s.TimestampUtc >= start && s.TimestampUtc <= end);

            vm.From = start;
            vm.To = end;

            vm.TotalRowsChecked = await query.SumAsync(s => (int?)s.TotalRows) ?? 0;

            var hasUniqueEiks = typeof(ProcessingStatistics).GetProperty("UniqueEiksCount") != null;
            if (hasUniqueEiks)
            {
                vm.UniqueEiks = await query.SumAsync(s => (int?)EF.Property<int>(s, "UniqueEiksCount")) ?? 0;
            }
            else
            {
                vm.UniqueEiks = await query.SumAsync(s => (int?)s.MatchedCount) ?? 0;
            }

            return View(vm);
        }
    }
}
