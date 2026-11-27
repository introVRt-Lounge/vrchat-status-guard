using StatusGuard.Core.Telemetry;

namespace StatusGuard.Core.Tests;

public class TelemetryClientTests
{
    [Fact]
    public void ScrubProps_DropsIdentityKeys_AndRedactsSecrets()
    {
        var scrubbed = TelemetryClient.ScrubProps(new Dictionary<string, string?>
        {
            ["outcome"] = "ok",
            ["username"] = "should-drop",
            ["world_id"] = "wrld_xxx",
            ["detail"] = "Authorization: Bearer abc.def.ghi and C:\\Users\\HeavyGee\\AppData\\x",
        });

        Assert.Equal("ok", scrubbed["outcome"]);
        Assert.False(scrubbed.ContainsKey("username"));
        Assert.False(scrubbed.ContainsKey("world_id"));
        Assert.Contains("[redacted]", scrubbed["detail"], StringComparison.OrdinalIgnoreCase);
        Assert.Contains(@"C:\Users\[user]\", scrubbed["detail"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DisabledClient_DoesNotEnqueueOrPost()
    {
        var handler = new CountingHandler();
        using var http = new HttpClient(handler);
        using var client = new TelemetryClient(
            installId: "abc",
            appVersion: "9.9.9",
            enabled: false,
            endpoint: "https://example.test/",
            http: http);

        client.Track("app_start");
        await client.FlushAsync();
        Assert.Equal(0, handler.Posts);
    }

    [Fact]
    public async Task EnabledClient_PostsBatch_AndOptOutStopsFurtherPosts()
    {
        var handler = new CountingHandler();
        using var http = new HttpClient(handler);
        using var client = new TelemetryClient(
            installId: "abc",
            appVersion: "9.9.9",
            enabled: true,
            endpoint: "https://example.test/status-guard",
            http: http);

        client.Track("app_start", new Dictionary<string, string?> { ["telemetry"] = "1" });
        await client.FlushAsync();
        Assert.Equal(1, handler.Posts);
        Assert.Contains("status-guard", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("install_id", handler.LastBody, StringComparison.Ordinal);

        client.SetEnabled(false);
        client.Track("should_not_send");
        await client.FlushAsync();
        Assert.Equal(1, handler.Posts);
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Posts;
        public string LastBody = "";

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Posts);
            if (request.Content is not null)
            {
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.Created);
        }
    }
}
