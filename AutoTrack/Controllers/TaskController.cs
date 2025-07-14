using AutoTrack.Data;
using AutoTrack.Hubs;
using AutoTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoTrack.Controllers
{
    //[Authorize]
    public class TasksController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<TaskHub> _hubContext;

        public TasksController(ApplicationDbContext context, IHubContext<TaskHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        public async Task<IActionResult> Index()
        {
            var tasks = await _context.TaskItems.Include(t => t.AssignedTo).ToListAsync();
            return View(tasks);
        }

        [Authorize]
        public IActionResult Create()
        {
            ViewBag.Users = _context.Users.ToList();
            return View();
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create(TaskItem task)
        {
            if (ModelState.IsValid)
            {
                task.Status = "Pending";
                task.CreatedAt = DateTime.UtcNow;
                task.CreatedBy = User.Identity.Name;
                _context.Add(task);
                await _context.SaveChangesAsync();

                // Send email if assigned
                if (!string.IsNullOrEmpty(task.AssignedToId))
                {
                    var assignedUser = await _context.Users.FindAsync(task.AssignedToId);
                    if (assignedUser != null)
                    {
                        var emailService = HttpContext.RequestServices.GetService<AutoTrack.Services.EmailService>();
                        await emailService.SendTaskAssignmentEmailAsync(
                            assignedUser.Email,
                            $"{assignedUser.FirstName} {assignedUser.LastName}",
                            task.Title
                        );
                    }
                }

                await _hubContext.Clients.All.SendAsync("ReceiveTaskUpdate", task.Id.ToString(), task.Status);
                return RedirectToAction(nameof(Index));
            }

            // ... existing error handling ...
            ViewBag.Users = _context.Users.ToList();
            return View(task);
        }

        //[Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Edit(int id)
        {
            var task = await _context.TaskItems.FindAsync(id);
            if (task == null)
            {
                return NotFound();
            }
            ViewBag.Users = _context.Users.ToList();
            ViewBag.Priorities = new SelectList(new[] { "Low", "Medium", "High" }, task.Priority);
            return View(task);
        }

        //[Authorize(Roles = "Admin,Manager")]
        [HttpPost]
        //[Authorize(Roles = "Admin,Manager")]
        [HttpPost]
        //[Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Edit(int id, TaskItem task)
        {
            if (id != task.Id)
            {
                return BadRequest();
            }
            if (ModelState.IsValid)
            {
                var existingTask = await _context.TaskItems.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
                string previousAssignedToId = existingTask?.AssignedToId;

                try
                {
                    _context.Update(task);
                    await _context.SaveChangesAsync();

                    // If assignment changed, send email
                    if (!string.IsNullOrEmpty(task.AssignedToId) && task.AssignedToId != previousAssignedToId)
                    {
                        var assignedUser = await _context.Users.FindAsync(task.AssignedToId);
                        if (assignedUser != null)
                        {
                            var emailService = HttpContext.RequestServices.GetService<AutoTrack.Services.EmailService>();
                            await emailService.SendTaskAssignmentEmailAsync(
                                assignedUser.Email,
                                $"{assignedUser.FirstName} {assignedUser.LastName}",
                                task.Title
                            );
                        }
                    }

                    await _hubContext.Clients.All.SendAsync("ReceiveTaskUpdate", task.Id.ToString(), task.Status);
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.TaskItems.Any(e => e.Id == id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            // ... existing error handling ...
            ViewBag.Users = _context.Users.ToList();
            ViewBag.Priorities = new SelectList(new[] { "Low", "Medium", "High" }, task.Priority);
            return View(task);
        }


        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Delete(int id)
        {
            var task = await _context.TaskItems.FindAsync(id);
            if (task == null)
            {
                return NotFound();
            }

            return View(task);
        }

        [HttpPost, ActionName("Delete")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var task = await _context.TaskItems.FindAsync(id);
            if (task == null)
            {
                return NotFound();
            }

            _context.TaskItems.Remove(task);
            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("ReceiveTaskDeleted", id.ToString());

            return RedirectToAction(nameof(Index));
        }

        //[Authorize(Roles = "Admin,Manager")]
        public IActionResult Analytics()
        {
            // Completed tasks per day
            var completedTasksPerDay = _context.TaskItems
                .Where(t => t.Status == "Completed" && t.CompletedAt != null)
                .GroupBy(t => t.CompletedAt.Value.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(g => g.Date)
                .ToList();

            // Average completion time (in hours) - fetch to memory, then calculate
            var completedTasks = _context.TaskItems
                .Where(t => t.Status == "Completed" && t.CompletedAt != null)
                .Select(t => new { t.CreatedAt, t.CompletedAt })
                .ToList();

            double avgCompletionTime = 0;
            if (completedTasks.Count > 0)
            {
                avgCompletionTime = completedTasks
                    .Average(t => (t.CompletedAt.Value - t.CreatedAt).TotalHours);
            }

            // Backlog (Pending + InProgress)
            var backlogCount = _context.TaskItems
                .Count(t => t.Status == "Pending" || t.Status == "InProgress");

            // Status chart (existing)
            var statusCounts = _context.TaskItems
                .GroupBy(t => t.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToList();

            ViewBag.StatusLabels = statusCounts.Select(x => x.Status).ToList();
            ViewBag.StatusCounts = statusCounts.Select(x => x.Count).ToList();
            ViewBag.CompletedTasksDates = completedTasksPerDay.Select(x => x.Date.ToString("yyyy-MM-dd")).ToList();
            ViewBag.CompletedTasksCounts = completedTasksPerDay.Select(x => x.Count).ToList();
            ViewBag.AvgCompletionTime = Math.Round(avgCompletionTime, 2);
            ViewBag.BacklogCount = backlogCount;

            return View();
        }

        public async Task<IActionResult> Dashboard(
        string period = "",
        string title = "",
        string status = "",
        string priority = "",
        string assignedTo = "",
        DateTime? from = null,
        DateTime? to = null)
        {
            var userId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var user = await _context.Users.FindAsync(userId);

            var tasksQuery = _context.TaskItems
                .Include(t => t.AssignedTo)
                .Where(t => t.AssignedToId == userId);

            // Only apply filters if the field is not empty
            bool anyFilterApplied = false;

            if (!string.IsNullOrWhiteSpace(title))
            {
                tasksQuery = tasksQuery.Where(t => t.Title != null && t.Title.ToLower().Contains(title.ToLower()));
                anyFilterApplied = true;
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                tasksQuery = tasksQuery.Where(t => t.Status == status);
                anyFilterApplied = true;
            }

            if (!string.IsNullOrWhiteSpace(priority))
            {
                tasksQuery = tasksQuery.Where(t => t.Priority == priority);
                anyFilterApplied = true;
            }

            if (!string.IsNullOrWhiteSpace(assignedTo))
            {
                tasksQuery = tasksQuery.Where(t =>
                    t.AssignedTo != null &&
                    (
                        (t.AssignedTo.FirstName + " " + t.AssignedTo.LastName + " " + t.AssignedTo.Email)
                        .ToLower().Contains(assignedTo.ToLower())
                    )
                );
                anyFilterApplied = true;
            }

            // Date range filter (from/to) only if both are provided
            if (from.HasValue && to.HasValue)
            {
                // If status is Completed, filter by CompletedAt; otherwise by CreatedAt
                if (status == "Completed")
                    tasksQuery = tasksQuery.Where(t => t.CompletedAt != null && t.CompletedAt >= from && t.CompletedAt <= to);
                else
                    tasksQuery = tasksQuery.Where(t => t.CreatedAt >= from && t.CreatedAt <= to);
                anyFilterApplied = true;
            }

            // If no filters are applied, show all tasks for the user
            var tasks = await tasksQuery.OrderByDescending(t => t.CreatedAt).ToListAsync();

            ViewBag.UserName = user != null ? user.FirstName : "User";
            ViewBag.Period = period;
            ViewBag.TitleFilter = title;
            ViewBag.StatusFilter = status;
            ViewBag.PriorityFilter = priority;
            ViewBag.AssignedToFilter = assignedTo;
            ViewBag.FromDate = from?.ToString("yyyy-MM-dd") ?? "";
            ViewBag.ToDate = to?.ToString("yyyy-MM-dd") ?? "";

            return View(tasks);
        }

        public async Task<IActionResult> DownloadCalendarEvent(int id)
        {
            var task = await _context.TaskItems.Include(t => t.AssignedTo).FirstOrDefaultAsync(t => t.Id == id);
            if (task == null)
                return NotFound();

            var sb = new StringBuilder();
            sb.AppendLine("BEGIN:VCALENDAR");
            sb.AppendLine("VERSION:2.0");
            sb.AppendLine("PRODID:-//AutoTrack//EN");
            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine($"UID:task-{task.Id}@autotrack");
            sb.AppendLine($"DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}");
            sb.AppendLine($"DTSTART:{task.DueDate:yyyyMMddTHHmmssZ}");
            sb.AppendLine($"DTEND:{task.DueDate.AddHours(1):yyyyMMddTHHmmssZ}");
            sb.AppendLine($"SUMMARY:{task.Title}");
            sb.AppendLine($"DESCRIPTION:{task.Description}");
            sb.AppendLine("END:VEVENT");
            sb.AppendLine("END:VCALENDAR");

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/calendar", $"{task.Title}-deadline.ics");
        }

        [Authorize]
        public async Task<IActionResult> CalendarFeed()
        {
            var userId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var tasks = await _context.TaskItems
                .Where(t => t.AssignedToId == userId)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("BEGIN:VCALENDAR");
            sb.AppendLine("VERSION:2.0");
            sb.AppendLine("PRODID:-//AutoTrack//EN");

            foreach (var task in tasks)
            {
                sb.AppendLine("BEGIN:VEVENT");
                sb.AppendLine($"UID:task-{task.Id}@autotrack");
                sb.AppendLine($"DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}");
                sb.AppendLine($"DTSTART:{task.DueDate:yyyyMMddTHHmmssZ}");
                sb.AppendLine($"DTEND:{task.DueDate.AddHours(1):yyyyMMddTHHmmssZ}");
                sb.AppendLine($"SUMMARY:{task.Title}");
                sb.AppendLine($"DESCRIPTION:{task.Description}");
                sb.AppendLine("END:VEVENT");
            }

            sb.AppendLine("END:VCALENDAR");

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/calendar", "AutoTrack-Tasks.ics");
        }
    }
}
