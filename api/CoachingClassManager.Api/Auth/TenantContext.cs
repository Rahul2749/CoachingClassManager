using System.Security.Claims;

namespace CoachingClassManager.Api.Auth;

public sealed class TenantContext(IHttpContextAccessor accessor)
{
    public Guid InstituteId => GetId("tenant_id");
    public Guid TeacherId => GetId(ClaimTypes.NameIdentifier);
    public Guid SessionId => GetId("session_id");

    private Guid GetId(string claim) => Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(claim), out var id)
        ? id
        : throw new UnauthorizedAccessException("An authenticated account is required.");
}
