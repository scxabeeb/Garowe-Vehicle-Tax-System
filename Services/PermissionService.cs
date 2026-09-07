using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using VehicleTax.Web.Security;

namespace VehicleTax.Web.Services
{
    /// <summary>
    /// Resolves effective permissions for users and manages role/user permission
    /// assignments with a full audit trail.
    ///
    /// Resolution order for a permission key (first match wins):
    ///   1. Super Admin role          → everything
    ///   2. explicit user Deny        → revoked
    ///   3. explicit user Allow       → granted
    ///   4. role grant                → granted
    ///   5. legacy User.Permissions / permission claim → granted (backward compat)
    ///
    /// Inheritance: an action key is only effective when the user also holds the
    /// corresponding page View permission — revoking View automatically disables
    /// every related action.
    /// </summary>
    public interface IPermissionService
    {
        Task<HashSet<string>> GetEffectiveAsync(ClaimsPrincipal user);
        Task<bool> HasAsync(ClaimsPrincipal user, string permission);
        Task<HashSet<string>> GetRoleGrantsAsync(string role);
        Task<Dictionary<string, PermissionEffect>> GetUserOverridesAsync(int userId);
        Task<HashSet<string>> GetInheritedFromRoleAsync(User user);
        Task SaveRolePermissionsAsync(string role, IEnumerable<string> grantedKeys, ClaimsPrincipal actor);
        Task SaveUserOverridesAsync(User user, Dictionary<string, PermissionEffect> desired, ClaimsPrincipal actor);
        bool IsCritical(string key);
    }

    public class PermissionService : IPermissionService
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private const string VersionKey = "perm:version";
        private static readonly TimeSpan CacheLife = TimeSpan.FromMinutes(10);

        public PermissionService(AppDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        private int Version => _cache.GetOrCreate(VersionKey, e => { e.SlidingExpiration = CacheLife; return 1; });
        private void Bump() => _cache.Set(VersionKey, Version + 1);

        public bool IsCritical(string key) => AppPermissions.IsCritical(key);

        private static bool IsSuperAdmin(ClaimsPrincipal user) =>
            user.IsInRole(AppPermissions.SuperAdminRole);

        // ── Effective resolution ─────────────────────────────────────

        public async Task<HashSet<string>> GetEffectiveAsync(ClaimsPrincipal user)
        {
            if (user.Identity?.IsAuthenticated != true)
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var username = user.Identity!.Name ?? "";
            var cacheKey = $"perm:effective:{Version}:{username}";
            if (_cache.TryGetValue(cacheKey, out HashSet<string>? cached) && cached != null)
                return cached;

            var effective = await ComputeEffectiveAsync(user, username);
            _cache.Set(cacheKey, effective, CacheLife);
            return effective;
        }

        public async Task<bool> HasAsync(ClaimsPrincipal user, string permission) =>
            (await GetEffectiveAsync(user)).Contains(permission);

        private async Task<HashSet<string>> ComputeEffectiveAsync(ClaimsPrincipal principal, string username)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (IsSuperAdmin(principal))
            {
                foreach (var k in AppPermissions.AllKeys) result.Add(k);
                return result;
            }

            var user = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return result;

            var roleGrants = await GetRoleGrantsAsync(user.Role ?? "");

            var legacy = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in (user.Permissions ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
                if (AppPermissions.LegacyMap.TryGetValue(raw.Trim(), out var mapped))
                    foreach (var k in mapped) legacy.Add(k);
            foreach (var claim in principal.Claims.Where(c => c.Type == "permission"))
                if (AppPermissions.LegacyMap.TryGetValue(claim.Value, out var mapped))
                    foreach (var k in mapped) legacy.Add(k);

            var overrides = await GetUserOverridesAsync(user.Id);

            bool Granted(string key)
            {
                if (overrides.TryGetValue(key, out var eff))
                    return eff == PermissionEffect.Allow;
                return roleGrants.Contains(key) || legacy.Contains(key);
            }

            foreach (var module in AppPermissions.Modules)
                foreach (var page in module.Pages)
                {
                    if (!Granted(page.ViewKey)) continue;
                    result.Add(page.ViewKey);
                    foreach (var action in page.Actions)
                        if (Granted(action.Key))
                            result.Add(action.Key);
                }

            // Finance legacy claim keeps working without a DB row.
            if (principal.HasClaim("permission", "finance.manage") ||
                roleGrants.Contains(AppPermissions.Keys.FinanceView) || legacy.Contains(AppPermissions.Keys.FinanceView))
            {
                result.Add(AppPermissions.Keys.FinanceView);
                result.Add(AppPermissions.Keys.FinanceManage);
            }

            return result;
        }

        // ── Raw stores ───────────────────────────────────────────────

        public async Task<HashSet<string>> GetRoleGrantsAsync(string role)
        {
            if (string.IsNullOrWhiteSpace(role))
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var cacheKey = $"perm:role:{Version}:{role}";
            if (_cache.TryGetValue(cacheKey, out HashSet<string>? cached) && cached != null)
                return cached;

            var grants = (await _context.RolePermissions.AsNoTracking()
                    .Where(rp => rp.Role == role)
                    .Select(rp => rp.Permission)
                    .ToListAsync())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _cache.Set(cacheKey, grants, CacheLife);
            return grants;
        }

        public async Task<Dictionary<string, PermissionEffect>> GetUserOverridesAsync(int userId)
        {
            var cacheKey = $"perm:user:{Version}:{userId}";
            if (_cache.TryGetValue(cacheKey, out Dictionary<string, PermissionEffect>? cached) && cached != null)
                return cached;

            var overrides = await _context.UserPermissionOverrides.AsNoTracking()
                .Where(uo => uo.UserId == userId)
                .ToDictionaryAsync(uo => uo.Permission, uo => uo.Effect, StringComparer.OrdinalIgnoreCase);

            _cache.Set(cacheKey, overrides, CacheLife);
            return overrides;
        }

        /// <summary>Keys the user would have purely from their role (for badge display).</summary>
        public async Task<HashSet<string>> GetInheritedFromRoleAsync(User user)
        {
            var roleGrants = await GetRoleGrantsAsync(user.Role ?? "");
            var legacy = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in (user.Permissions ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
                if (AppPermissions.LegacyMap.TryGetValue(raw.Trim(), out var mapped))
                    foreach (var k in mapped) legacy.Add(k);

            var inherited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var module in AppPermissions.Modules)
                foreach (var page in module.Pages)
                {
                    if (roleGrants.Contains(page.ViewKey) || legacy.Contains(page.ViewKey))
                        inherited.Add(page.ViewKey);
                    foreach (var action in page.Actions)
                        if ((roleGrants.Contains(action.Key) || legacy.Contains(action.Key))
                            && inherited.Contains(page.ViewKey))
                            inherited.Add(action.Key);
                }
            return inherited;
        }

        // ── Saving (with audit) ──────────────────────────────────────

        public async Task SaveRolePermissionsAsync(string role, IEnumerable<string> grantedKeys, ClaimsPrincipal actor)
        {
            var desired = grantedKeys
                .Where(k => !string.IsNullOrWhiteSpace(k) && AppPermissions.AllKeys.Contains(k))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var current = (await _context.RolePermissions
                    .Where(rp => rp.Role == role)
                    .ToListAsync())
                .Select(rp => rp.Permission)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var existing = await _context.RolePermissions
                .Where(rp => rp.Role == role)
                .ToListAsync();

            foreach (var rp in existing.Where(rp => !desired.Contains(rp.Permission)))
                _context.RolePermissions.Remove(rp);

            foreach (var key in desired.Where(k => !current.Contains(k)))
                _context.RolePermissions.Add(new RolePermission { Role = role, Permission = key });

            await WriteAuditAsync("Role", role, current, desired, actor,
                $"Role permissions updated for '{role}'.");

            await _context.SaveChangesAsync();
            Bump();
        }

        public async Task SaveUserOverridesAsync(User user, Dictionary<string, PermissionEffect> desired, ClaimsPrincipal actor)
        {
            var clean = desired
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Key) && AppPermissions.AllKeys.Contains(kv.Key))
                .GroupBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Value, StringComparer.OrdinalIgnoreCase);

            var existing = await _context.UserPermissionOverrides
                .Where(uo => uo.UserId == user.Id)
                .ToListAsync();

            var oldState = existing.ToDictionary(uo => uo.Permission, uo => uo.Effect,
                StringComparer.OrdinalIgnoreCase);

            foreach (var uo in existing)
                if (!clean.TryGetValue(uo.Permission, out var effect) || effect == uo.Effect)
                    _context.UserPermissionOverrides.Remove(uo);

            foreach (var kv in clean)
            {
                var row = existing.FirstOrDefault(uo =>
                    uo.Permission.Equals(kv.Key, StringComparison.OrdinalIgnoreCase));
                if (row != null)
                    row.Effect = kv.Value;
                else
                    _context.UserPermissionOverrides.Add(new UserPermissionOverride
                    {
                        UserId = user.Id,
                        Permission = kv.Key,
                        Effect = kv.Value
                    });
            }

            // Keep the legacy User.Permissions claim string in sync with explicit Allow keys.
            var roleGrants = await GetRoleGrantsAsync(user.Role ?? "");
            user.Permissions = string.Join(",",
                clean.Where(kv => kv.Value == PermissionEffect.Allow)
                     .Select(kv => kv.Key)
                     .Where(k => !roleGrants.Contains(k))
                     .Select(k => AppPermissions.ToLegacy.TryGetValue(k, out var l) ? l : k.ToLowerInvariant())
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(x => x));

            await WriteAuditAsync("User", user.Username, oldState, clean, actor,
                $"User permission overrides updated for '{user.Username}'.");

            await _context.SaveChangesAsync();
            Bump();
        }

        private async Task WriteAuditAsync(
            string targetType, string target,
            HashSet<string> oldKeys, HashSet<string> newKeys,
            ClaimsPrincipal actor, string details)
        {
            await WriteAuditAsync(targetType, target,
                oldKeys.ToDictionary(k => k, _ => PermissionEffect.Allow),
                newKeys.ToDictionary(k => k, _ => PermissionEffect.Allow),
                actor, details);
        }

        private async Task WriteAuditAsync(
            string targetType, string target,
            Dictionary<string, PermissionEffect> oldState,
            Dictionary<string, PermissionEffect> newState,
            ClaimsPrincipal actor, string details)
        {
            int? byUserId = null;
            var username = actor.Identity?.Name;
            if (!string.IsNullOrWhiteSpace(username))
            {
                byUserId = await _context.Users.AsNoTracking()
                    .Where(u => u.Username == username)
                    .Select(u => (int?)u.Id)
                    .FirstOrDefaultAsync();
            }

            foreach (var key in oldState.Keys.Union(newState.Keys, StringComparer.OrdinalIgnoreCase))
            {
                var oldLabel = Describe(oldState, key);
                var newLabel = Describe(newState, key);
                if (string.Equals(oldLabel, newLabel, StringComparison.Ordinal)) continue;

                _context.PermissionAuditLogs.Add(new PermissionAuditLog
                {
                    ActionAt = DateTime.UtcNow,
                    ByUserId = byUserId,
                    TargetType = targetType,
                    Target = target,
                    Permission = key,
                    OldValue = oldLabel,
                    NewValue = newLabel,
                    Details = details
                });
            }
        }

        private static string Describe(Dictionary<string, PermissionEffect> state, string key) =>
            !state.TryGetValue(key, out var effect)
                ? "Inherited"
                : effect == PermissionEffect.Allow ? "Granted" : "Revoked";
    }
}
