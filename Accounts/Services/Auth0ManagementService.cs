using Accounts.Interfaces;
using Auth0.ManagementApi;
using Newtonsoft.Json;

namespace Accounts.Services
{
    public class Auth0ManagementService : IAuth0ManagementService
    {
        private readonly string _auth0Domain;
        private readonly string _auth0ManagementApiAccessToken;

        public Auth0ManagementService(string auth0Domain, string auth0ManagementApiAccessToken)
        {
            _auth0Domain = auth0Domain;
            _auth0ManagementApiAccessToken = auth0ManagementApiAccessToken;
        }

        private ManagementApiClient CreateManagementApiClient()
        {
            return new ManagementApiClient(_auth0ManagementApiAccessToken, new Uri($"https://{_auth0Domain}/api/v2/"));
        }

        public async Task<string> GetUserProfileAsync(string userId)
        {
            using var client = CreateManagementApiClient();
            var user = await client.Users.GetAsync(userId);
            return JsonConvert.SerializeObject(user);
        }

        public async Task<bool> DeleteUserAsync(string userId)
        {
            using var client = CreateManagementApiClient();
            var user = await GetUserProfileAsync(userId);
            if(user == null)
            {
                return false;
            }
            else
            {
                await client.Users.DeleteAsync(userId);
                return true;
            }
        }
    }
}
