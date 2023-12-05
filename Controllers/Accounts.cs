using Gateway.Models;
using Gateway.Models.Dto;
using Gateway.services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Gateway.Controllers
{
    [ApiController]
    [Route("accounts")]
    public class Accounts : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly IConfiguration _configuration;
        private readonly TokenBlacklistService _tokenBlacklistService;

        public Accounts(UserManager<User> userManager, IConfiguration configuration, TokenBlacklistService tokenBlacklistService)
        {
            _userManager = userManager;
            _configuration = configuration;
            _tokenBlacklistService = tokenBlacklistService;
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _userManager.Users.ToListAsync();
            var userDtos = users.Select(u => new UserDataDto(u.Email)).ToList();

            return Ok(userDtos);
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDto model)
        {
            if (!model.Consent)
            {
                return BadRequest("User consent is required.");
            }

            if (await _userManager.FindByNameAsync(model.UserName) != null)
            {
                return BadRequest("Username already exists.");
            }

            if (await _userManager.FindByEmailAsync(model.Email) != null)
            {
                return BadRequest("Email already exists.");
            }

            var user = new User { UserName = model.UserName, Email = model.Email };
            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "User");
                return Ok();
            }
            else
            {
                return BadRequest(result.Errors);
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto model)
        {
            var user = await _userManager.FindByNameAsync(model.UserName);
            if (user != null && await _userManager.CheckPasswordAsync(user, model.Password))
            {
                var token = GenerateJwtToken(user);
                return Ok(new { token });
            }

            return Unauthorized();
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            // Extracts the token from the Authorization header
            var token = this.HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

            // Calls the blacklist service to store the token in Redis
            await _tokenBlacklistService.BlacklistTokenAsync(token, TimeSpan.FromDays(7));

            return Ok("Logged out");
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> ViewMyData()
        {
            // Extract the username from the JWT token's 'sub' claim
            var usernameClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);

            var username = usernameClaim.Value;
            var user = await _userManager.FindByNameAsync(username);
            if (user == null)
            {
                return Unauthorized();
            }

            var userData = new UserDataDto(user.Email);
            return Ok(userData);
        }

        [Authorize]
        [HttpPut("me")]
        public async Task<IActionResult> UpdateMyData(UserDataDto model)
        {
            // Extract the username from the JWT token's 'sub' claim
            var usernameClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);

            var username = usernameClaim.Value;
            var user = await _userManager.FindByNameAsync(username);
            if (user == null)
            {
                return Unauthorized();
            }

            user.Email = model.Email;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                return Ok("Profile updated successfully.");
            }
            else
            {
                return BadRequest(result.Errors);
            }
        }

        [Authorize]
        [HttpDelete("me")]
        public async Task<IActionResult> DeleteMyData()
        {
            // Extract the username from the JWT token's 'sub' claim
            var usernameClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);

            var username = usernameClaim.Value;
            var user = await _userManager.FindByNameAsync(username);
            if (user == null)
            {
                return Unauthorized();
            }

            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                return Ok("Your data has been deleted.");
            }
            else
            {
                return Problem("There was a problem deleting your data.");
            }
        }

        string GenerateJwtToken(User user)
        {
            var secretKey = _configuration.GetValue<string>("JwtSettings:SecretKey");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserName),
            };

            var token = new JwtSecurityToken(
                issuer: null,
                audience: null,
                claims: claims,
                expires: DateTime.Now.AddDays(1),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
