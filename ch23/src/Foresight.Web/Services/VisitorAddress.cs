using Microsoft.AspNetCore.Components;

namespace Foresight.Web.Services;

/// <summary>
/// The address of the visitor this circuit serves. Only the first request
/// of a visit, the prerender, has it. The circuit that follows has no
/// request, so [PersistentState] carries the value over, the way chapter 2
/// carries the visitor's country.
/// </summary>
public sealed class VisitorAddress
{
    public VisitorAddress(IHttpContextAccessor http)
    {
        // After UseForwardedHeaders, this is the visitor's address, not
        // Cloudflare's (chapter 23).
        Address = http.HttpContext?.Connection.RemoteIpAddress?.ToString();
    }

    [PersistentState]
    public string? Address { get; set; }
}
