using AutoTrack.Models;
using AutoTrack.Services;
using AutoTrack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace AutoTrack.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly EmailSender _emailSender;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            EmailSender emailSender)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _emailSender = emailSender;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (TempData["EmailVerified"] as bool? != true || model.Email != (TempData["VerifiedEmail"] as string))
            {
                ModelState.AddModelError("", "Email not verified.");
                return View(model);
            }

            if (ModelState.IsValid)
            {
                // Check if email is already registered
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Email", "Email is already registered.");
                    return View(model);
                }

                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName
                };
                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    // Assign default role
                    await _userManager.AddToRoleAsync(user, "Employee");
                    return RedirectToAction("Login");
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }
            return View(model);
        }


        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> SendOtp([FromBody] VerifyOtpViewModel model)
        {
            if (string.IsNullOrEmpty(model.Email) || !new EmailAddressAttribute().IsValid(model.Email))
                return Json(new { success = false, message = "Invalid email." });

            var otp = new Random().Next(100000, 999999).ToString();
            TempData["RegisterOtp"] = otp;
            TempData["RegisterEmail"] = model.Email;

            await _emailSender.SendEmailAsync(model.Email, "Your OTP Code", $"Your OTP code is: {otp}");

            return Json(new { success = true });
        }

        [HttpPost]
        [AllowAnonymous]
        public IActionResult VerifyOtp([FromBody] VerifyOtpViewModel model)
        {
            var otp = TempData["RegisterOtp"] as string;
            var email = TempData["RegisterEmail"] as string;

            if (model.Email == email && model.Otp == otp)
            {
                TempData["EmailVerified"] = true;
                TempData["VerifiedEmail"] = email;
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "Invalid OTP." });
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login()
        {
            return View();
        }
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    // Redirect to dashboard or tasks page after login
                    return RedirectToAction("Dashboard", "Tasks");
                }
                ModelState.AddModelError("", "Invalid login attempt.");
            }
            return View(model);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }

    }
}
