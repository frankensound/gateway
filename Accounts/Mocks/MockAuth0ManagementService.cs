using Accounts.Interfaces;
using Accounts.Models;
using Newtonsoft.Json;

namespace Accounts.Utils
{
    public class MockAuth0ManagementService : IAuth0ManagementService
    {
        private readonly Dictionary<string, UserDTO> _mockUsers;

        public MockAuth0ManagementService()
        {
            _mockUsers = new Dictionary<string, UserDTO>
            {
            // Initialize with some mock data
            { "auth0|ete66gfgdg346346", new UserDTO { Name = "Mock User 1", Email = "user1@example.com" } },
            { "auth0|fgh87hgfgh567657", new UserDTO { Name = "Mock User 2", Email = "user2@example.com" } }
            };
        }

        public Task<string> GetUserProfileAsync(string userId)
        {
            if (_mockUsers.TryGetValue(userId, out var userDTO))
            {
                var userProfileJson = JsonConvert.SerializeObject(userDTO);
                return Task.FromResult(userProfileJson);
            }

            // Throw an exception to indicate the user was not found
            throw new KeyNotFoundException($"User with ID '{userId}' was not found.");
        }

        public Task<bool> DeleteUserAsync(string userId)
        {
            // Check if the user exists before attempting to delete
            if (_mockUsers.ContainsKey(userId))
            {
                _mockUsers.Remove(userId);
                return Task.FromResult(true);
            }

            // Return false if the user does not exist
            return Task.FromResult(false);
        }
    }
}
