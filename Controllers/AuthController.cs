using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using _4U.chat.Models;

namespace _4U.chat.Controllers
{
    public class AuthController : Controller
    {
        private readonly SignInManager<User> _signInManager;
        private readonly UserManager<User> _userManager;

        public AuthController(SignInManager<User> signInManager, UserManager<User> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password, string returnUrl = "/")
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                return Redirect("/login?error=" + Uri.EscapeDataString("Please fill in all fields"));
            }

            var result = await _signInManager.PasswordSignInAsync(username, password, false, false);

            if (result.Succeeded)
            {
                return LocalRedirect(returnUrl);
            }

            return Redirect("/login?error=" + Uri.EscapeDataString("Invalid username or password"));
        }

        [HttpPost]
        public async Task<IActionResult> Register(string name, string username, string password, string confirmPassword)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || password != confirmPassword)
            {
                return Redirect("/register?error=" + Uri.EscapeDataString("Please fill in all fields and ensure passwords match"));
            }

            // Check if username already exists
            var existingUser = await _userManager.FindByNameAsync(username);
            if (existingUser != null)
            {
                return Redirect("/register?error=" + Uri.EscapeDataString("Username already exists. Please choose a different username."));
            }

            var user = new User 
            { 
                UserName = username, 
                Name = name,
                Email = $"{username.Replace(" ", "").ToLower()}@local.app" // Generate email for Identity compatibility
            };
            
            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                return Redirect("/login?message=" + Uri.EscapeDataString("Account created successfully"));
            }

            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return Redirect("/register?error=" + Uri.EscapeDataString(errors));
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return Redirect("/login");
        }
    }
}