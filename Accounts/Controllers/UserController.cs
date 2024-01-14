using Accounts.Interfaces;
using Accounts.Models;
using Accounts.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Serilog;
using System.Dynamic;

namespace Accounts.Controllers
{
    [Route("accounts")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IAuth0ManagementService _auth0ManagementService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IMessagePublisher _messagePublisher;
        private readonly MongoDbService _mongoDbService;
        private readonly IConfiguration _configuration;

        public UserController(IAuth0ManagementService auth0ManagementService, IHttpContextAccessor httpContextAccessor, IMessagePublisher messagePublisher, MongoDbService mongoDbService, IConfiguration configuration)
        {
            _auth0ManagementService = auth0ManagementService;
            _httpContextAccessor = httpContextAccessor;
            _messagePublisher = messagePublisher;
            _mongoDbService = mongoDbService;
            _configuration = configuration;
        }

        private string GetUserId()
        {
            var userId = _httpContextAccessor.HttpContext.Request.Headers["UserID"].FirstOrDefault();
            Log.Information($"UserID received: {userId}");
            return userId;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    Log.Warning("GetProfile - UserID header is missing");
                    return BadRequest("UserID header is missing");
                }

                // Fetch user profile from MongoDB
                var mongoProfile = await _mongoDbService.GetUserProfile(userId);
                // Fetch user profile from Auth0
                var auth0Profile = await _auth0ManagementService.GetUserProfileAsync(userId);

                // Combine the data into a dynamic object
                dynamic combinedProfile = new ExpandoObject();
                combinedProfile.Auth0Profile = auth0Profile;
                combinedProfile.MongoActivities = mongoProfile?.Activities ?? new List<UserActivity>();

                var json = JsonConvert.SerializeObject(combinedProfile, Formatting.Indented);
                return Ok(json);
            }
            catch (KeyNotFoundException e)
            {
                return NotFound(new { message = $"User with ID was not found." });
            }
            catch (Exception e)
            {
                return StatusCode(500, new { message = $"An error occurred while processing your request: {e.Message}" });
            }
        }

        [HttpDelete("me")]
        public async Task<IActionResult> DeleteProfile()
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return BadRequest("UserID header is missing");
                }

                bool isDeleted = await _auth0ManagementService.DeleteUserAsync(userId);
                if (isDeleted)
                {
                    _messagePublisher.Publish(_configuration["RabbitMQ:QueueName:Deletion"], userId);
                    return Ok("User deleted successfully. We will remove all your personal data shortly.");
                }
                else
                {
                    return StatusCode(500, "Unable to delete user.");
                }
            }
            catch (Exception e)
            {
                Log.Error($"DeleteProfile - Error: {e.Message}", e);
                return StatusCode(500, $"An internal error occurred: {e.Message}");
            }
        }
    }
}
