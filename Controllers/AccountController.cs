using Microsoft.AspNetCore.Mvc;
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
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
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
        public IActionResult Login(string? returnUrl = null)
        {
            var vm = new LoginViewModel { ReturnUrl = returnUrl };
            return View(vm);
        }

        [HttpPost]
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
                var check = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: false);
                if (check.Succeeded)
                {
                    _logger.LogInformation("Login succeeded for {Email}", model.Email);
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    // Log Set-Cookie headers emitted by the authentication system for debugging
                    if (Response?.Headers != null && Response.Headers.ContainsKey("Set-Cookie"))
                    {
                        _logger.LogInformation("Response Set-Cookie after SignIn: {SetCookie}", Response.Headers["Set-Cookie"].ToString());
                    }

                    // Return a small HTML fallback page that forces a client-side redirect
                    // and writes document.cookie to console so the user can confirm cookie presence.
                    var redirectUrl = !string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl : Url.Action("Index", "Upload");
                    var html = $"<!doctype html><html><head><meta charset=\"utf-8\"><title>Signing in...</title></head><body>"
                             + "<p>Signing in... Redirecting to the application.</p>"
                             + "<pre id=\"cookies\">Checking cookies...</pre>"
                             + "<script>console.log('document.cookie=', document.cookie); document.getElementById('cookies').textContent = document.cookie || '<no cookies>'; setTimeout(function(){ window.location = '" + redirectUrl + "'; }, 500);</script>"
                             + "</body></html>";
                    return Content(html, "text/html");
                }
                else
                {
                    _logger.LogWarning("Login failed for {Email}: invalid password", model.Email);
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
