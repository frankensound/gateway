﻿using Accounts.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Accounts.Controllers
{
    [Route("accounts")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IAuth0ManagementService _auth0ManagementService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IMessagePublisher _messagePublisher;

        public UserController(IAuth0ManagementService auth0ManagementService, IHttpContextAccessor httpContextAccessor, IMessagePublisher messagePublisher)
        {
            _auth0ManagementService = auth0ManagementService;
            _httpContextAccessor = httpContextAccessor;
            _messagePublisher = messagePublisher;
        }

        private string GetUserId()
        {
            return _httpContextAccessor.HttpContext.Request.Headers["X-User-ID"].FirstOrDefault();
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = GetUserId();
            try
            {
                var profile = await _auth0ManagementService.GetUserProfileAsync(userId);
                return Ok(profile);
            }
            catch (KeyNotFoundException e)
            {
                return NotFound(new { message = $"User with ID {userId} was not found." });
            }
            catch (Exception e)
            {
                return StatusCode(500, new { message = $"An error occurred while processing your request: {e.Message}" });
            }
        }

        [HttpDelete("me")]
        public async Task<IActionResult> DeleteProfile()
        {
            var userId = GetUserId();
            bool isDeleted = await _auth0ManagementService.DeleteUserAsync(userId);

            if (isDeleted)
            {
                _messagePublisher.Publish("deleted_users_queue", $"UserDeleted:{userId}");
                return Ok("User deleted successfully.");
            }
            else
            {
                return StatusCode(500, "Unable to delete user.");
            }
        }
    }
}
