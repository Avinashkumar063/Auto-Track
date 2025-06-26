using AutoTrack.Models;
using AutoTrack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public AdminController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<IActionResult> Users()
    {
        var users = _userManager.Users.ToList();
        var userWithRoles = new List<UserWithRoleViewModel>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userWithRoles.Add(new UserWithRoleViewModel
            {
                UserId = user.Id,
                Email = user.Email,
                CurrentRole = roles.FirstOrDefault() ?? "Employee"
            });
        }

        ViewBag.AllRoles = _roleManager.Roles.Select(r => r.Name).ToList();
        return View(userWithRoles);
    }

    [HttpPost]
    public async Task<IActionResult> AssignRole(string userId, string role)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user != null && await _roleManager.RoleExistsAsync(role))
        {
            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, role);
        }
        return RedirectToAction("Users");
    }
}
