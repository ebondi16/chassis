using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Chassis.Api.Tests;

/// <summary>
/// Records every <c>Set-Cookie</c> header the server emits across a redirect
/// chain, so a test can assert exactly what the browser is asked to store
/// (auth doc §5.1: a session cookie, and no token).
/// </summary>
internal sealed class CookieRecordingHandler : DelegatingHandler
{
    private readonly ConcurrentQueue<string> _setCookies = new();

    public IReadOnlyList<string> SetCookies => _setCookies.ToArray();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            foreach (var value in values)
            {
                _setCookies.Enqueue(value);
            }
        }

        return response;
    }
}
