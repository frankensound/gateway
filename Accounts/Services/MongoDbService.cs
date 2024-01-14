using Accounts.Models;
using MongoDB.Driver;

namespace Accounts.Services
{
    public class MongoDbService
    {
        private readonly IMongoCollection<UserProfile> _userProfiles;

        public MongoDbService(IConfiguration configuration)
        {
            var client = new MongoClient(configuration["MongoDB:ConnectionString"]);
            var database = client.GetDatabase(configuration["MongoDB:DatabaseName"]);
            _userProfiles = database.GetCollection<UserProfile>("UserProfiles");
        }

        public async Task AddOrUpdateUserProfile(string userId, UserActivity activity)
        {
            var filter = Builders<UserProfile>.Filter.Eq(up => up.UserId, userId);
            var update = Builders<UserProfile>.Update.Push(up => up.Activities, activity);
            await _userProfiles.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });
        }

        public async Task<UserProfile> GetUserProfile(string userId)
        {
            return await _userProfiles.Find(up => up.UserId == userId).FirstOrDefaultAsync();
        }
    }
}
