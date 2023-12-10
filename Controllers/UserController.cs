using gateway.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace gateway.Controllers
{
    [Route("api/users")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly Auth0ManagementService _auth0ManagementService;

        public UserController(Auth0ManagementService auth0ManagementService)
        {
            _auth0ManagementService = auth0ManagementService;
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetProfile()
        {
            var userId = GetUserIdFromAccessToken();
            var profile = await _auth0ManagementService.GetUserProfileAsync(userId);
            return Ok(profile);
        }

        [HttpDelete("me")]
        [Authorize]
        public async Task<IActionResult> DeleteProfile()
        {
            var userId = GetUserIdFromAccessToken();
            await _auth0ManagementService.DeleteUserAsync(userId);
            return Ok("User deleted successfully.");
        }

        private string GetUserIdFromAccessToken()
        {
            var token = HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            return jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        }
    }
}
