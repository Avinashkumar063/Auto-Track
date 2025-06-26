using AutoTrack.Data;
using AutoTrack.Hubs;
using AutoTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Linq;
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
                task.CreatedBy = User.Identity.Name; // Set the creator's username or email
                _context.Add(task);
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ReceiveTaskUpdate", task.Id.ToString(), task.Status);
                return RedirectToAction(nameof(Index));
            }

            // Log validation errors for debugging
            foreach (var key in ModelState.Keys)
            {
                var errors = ModelState[key].Errors;
                foreach (var error in errors)
                {
                    System.Diagnostics.Debug.WriteLine($"ModelState error for {key}: {error.ErrorMessage}");
                }
            }

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
        public async Task<IActionResult> Edit(int id, TaskItem task)
        {
            if (id != task.Id)
            {
                return BadRequest();
            }
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(task);
                    await _context.SaveChangesAsync();
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
            foreach (var key in ModelState.Keys)
            {
                var errors = ModelState[key].Errors;
                foreach (var error in errors)
                {
                    System.Diagnostics.Debug.WriteLine($"ModelState error for {key}: {error.ErrorMessage}");
                }
            }
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
            var statusCounts = _context.TaskItems
                .GroupBy(t => t.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToList();

            ViewBag.StatusData = statusCounts;
            return View();
        }

        public async Task<IActionResult> Dashboard()
        {
            var userId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var tasks = await _context.TaskItems
                .Include(t => t.AssignedTo)
                .Where(t => t.AssignedToId == userId)
                .ToListAsync();
            return View(tasks);
        }
    }
}
