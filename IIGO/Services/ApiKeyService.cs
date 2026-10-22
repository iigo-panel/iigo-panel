using IIGO.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace IIGO.Services
{
    public class ApiKeyService(ApplicationDbContext context)
    {
        const string KeyStart = "iigo_";
        const int PrefixLength = 12;

        public Task<List<ApiKey>> GetKeysAsync() =>
            context.ApiKey.AsNoTracking().OrderBy(x => x.Name).ToListAsync();

        /// <summary>
        /// Creates a key and returns its full value. The value can't be retrieved again later.
        /// </summary>
        public async Task<string> CreateKeyAsync(string name, DateTime? expiresAt, string createdBy)
        {
            // 32 random bytes, URL-safe, so the key works in headers and query strings
            var key = KeyStart + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

            context.ApiKey.Add(new ApiKey
            {
                Name = name,
                KeyPrefix = key[..PrefixLength],
                KeyHash = Hash(key),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = createdBy,
                ExpiresAt = expiresAt
            });
            await context.SaveChangesAsync();

            return key;
        }

        public async Task SetEnabledAsync(int id, bool enabled)
        {
            await context.ApiKey.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Enabled, enabled));
        }

        public async Task DeleteKeyAsync(int id)
        {
            await context.ApiKey.Where(x => x.Id == id).ExecuteDeleteAsync();
        }

        /// <summary>
        /// Returns the key if it exists, is enabled, and hasn't expired, and records when it was used.
        /// </summary>
        public async Task<ApiKey?> ValidateAsync(string? providedKey)
        {
            if (string.IsNullOrWhiteSpace(providedKey) || !providedKey.StartsWith(KeyStart, StringComparison.Ordinal))
                return null;

            var hash = Hash(providedKey.Trim());
            var key = await context.ApiKey.FirstOrDefaultAsync(x => x.KeyHash == hash);
            if (key == null || !key.Enabled || key.ExpiresAt <= DateTime.UtcNow)
                return null;

            key.LastUsedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
            return key;
        }

        static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    }
}
