using ApexPost.Application.Interfaces;

namespace ApexPost.Web.Configuration;

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string Name => accessor.HttpContext?.User.FindFirst("StaffId")?.Value ?? "system";
}
