namespace FinancialServices.Tests.TestSupport;

/// <summary>
/// Test <see cref="HttpMessageHandler"/> that always faults the send with a supplied exception — no
/// live network. Lets connector tests prove that a transport fault (<see cref="HttpRequestException"/>)
/// or a timeout (<see cref="TaskCanceledException"/>) surfaces as an <c>UpstreamServiceException</c>.
/// </summary>
public sealed class ThrowingHttpMessageHandler(Exception toThrow) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromException<HttpResponseMessage>(toThrow);
}
