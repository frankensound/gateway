using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Accounts.Models
{
    public class UserProfile
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public string UserId { get; set; }

        public List<UserActivity> Activities { get; set; } = new List<UserActivity>();
    }

    public class UserActivity
    {
        public DateTime Date { get; set; }
        public string Activity { get; set; }
        public int ObjectId { get; set; }
    }
}
