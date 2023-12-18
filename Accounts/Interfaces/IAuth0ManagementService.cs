namespace Accounts.Interfaces
{
    public interface IAuth0ManagementService
    {
        Task<string> GetUserProfileAsync(string userId);
        Task<bool> DeleteUserAsync(string userId);
    }
}
