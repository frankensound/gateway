using Microsoft.Extensions.Caching.Distributed;

namespace Gateway.services
{
    public class TokenBlacklistService
    {
        private readonly IDistributedCache _cache;

        public TokenBlacklistService(IDistributedCache cache)
        {
            _cache = cache;
        }

        // Blacklists a token by storing it in cache with a set expiration
        public async Task BlacklistTokenAsync(string token, TimeSpan expiration)
        {
            await _cache.SetStringAsync(token, "blacklisted", new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            });
        }

        // Checks if a token is blacklisted
        public async Task<bool> IsTokenBlacklistedAsync(string token)
        {
            var value = await _cache.GetStringAsync(token);
            return value != null;
        }
    }
}
