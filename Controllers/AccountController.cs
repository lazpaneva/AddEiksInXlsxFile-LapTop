using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using AddEiksInXlsxFile.Models;

namespace AddEiksInXlsxFile.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<AccountController> _logger;

        public AccountController(SignInManager<IdentityUser> signInManager, UserManager<IdentityUser> userManager, ILogger<AccountController> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(Models.RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            _logger.LogInformation("Register attempt for {Email}", model.Email);

            var user = new IdentityUser { UserName = model.Email, Email = model.Email, EmailConfirmed = true };
            var res = await _userManager.CreateAsync(user, model.Password);
            if (res.Succeeded)
            {
                _logger.LogInformation("Registration succeeded for {Email}", model.Email);
                // add to default role
                await _userManager.AddToRoleAsync(user, "User");
                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToAction("Index", "Upload");
            }
            _logger.LogWarning("Registration failed for {Email}: {Errors}", model.Email, string.Join(";", res.Errors.Select(e => e.Description)));
            foreach (var e in res.Errors)
            {
                ModelState.AddModelError(string.Empty, e.Description);
            }
            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            var vm = new LoginViewModel { ReturnUrl = returnUrl };
            return View(vm);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Login ModelState invalid for {Email}: {Errors}", model?.Email, string.Join(";", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                return View(model);
            }
            _logger.LogInformation("Login attempt for {Email}", model.Email);
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user != null)
            {
                var result = await _signInManager.PasswordSignInAsync(user.UserName!, model.Password, model.RememberMe, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    _logger.LogInformation("Login succeeded for {Email}", model.Email);

                    if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                    {
                        return LocalRedirect(model.ReturnUrl);
                    }

                    return RedirectToAction("Index", "Upload");
                }
                else
                {
                    _logger.LogWarning("Login failed for {Email}: {Result}", model.Email, result.ToString());
                }
            }
            else
            {
                _logger.LogWarning("Login failed: user not found for {Email}", model.Email);
            }

            ModelState.AddModelError(string.Empty, "Невалидни данни за вход.");
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            _logger.LogInformation("Logout requested for {User}", User?.Identity?.Name);
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Upload");
        }
    }
}
