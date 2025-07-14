using AutoTrack.Data;
using AutoTrack.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AutoTrack.Services
{
    public class TaskDeadlineAlertService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TaskDeadlineAlertService> _logger;

        public TaskDeadlineAlertService(IServiceProvider serviceProvider, ILogger<TaskDeadlineAlertService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var emailSender = scope.ServiceProvider.GetRequiredService<EmailSender>();

                var now = DateTime.UtcNow;
                var soon = now.AddHours(24);

                var tasksDueSoon = await db.TaskItems
                    .Include(t => t.AssignedTo)
                    .Where(t => t.Status != "Completed" && t.DueDate >= now && t.DueDate <= soon)
                    .ToListAsync(stoppingToken);

                foreach (var task in tasksDueSoon)
                {
                    if (task.AssignedTo != null)
                    {
                        var subject = $"Task Approaching Deadline: {task.Title}";
                        var message = $"Hello {task.AssignedTo.FirstName},\n\n" +
                                      $"The task \"{task.Title}\" is due on {task.DueDate.ToLocalTime():f}.\n" +
                                      $"Please take necessary action.\n\nAutoTrack";
                        await emailSender.SendEmailAsync(task.AssignedTo.Email, subject, message);
                    }
                }

                _logger.LogInformation($"TaskDeadlineAlertService checked {tasksDueSoon.Count} tasks at {DateTime.UtcNow}.");

                // Wait 1 hour before next check
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }
}