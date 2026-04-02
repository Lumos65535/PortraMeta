using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace PortraMeta.Tests;

public class ApiKeyMiddlewareTests
{
    private WebApplicationFactory<Program> CreateFactory(string apiKey = "")
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Auth:ApiKey"] = apiKey,
                        ["Database:Path"] = ":memory:",
                    });
                });
            });
    }

    [Fact]
    public async Task Request_WithoutApiKey_WhenDisabled_Returns200()
    {
        using var factory = CreateFactory(apiKey: "");
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/libraries");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Request_WithValidApiKey_Returns200()
    {
        using var factory = CreateFactory(apiKey: "test-secret");
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "test-secret");

        var response = await client.GetAsync("/api/libraries");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Request_WithInvalidApiKey_Returns401()
    {
        using var factory = CreateFactory(apiKey: "test-secret");
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "wrong-key");

        var response = await client.GetAsync("/api/libraries");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_WithoutApiKey_WhenEnabled_Returns401()
    {
        using var factory = CreateFactory(apiKey: "test-secret");
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/libraries");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OptionsRequest_AlwaysAllowed()
    {
        using var factory = CreateFactory(apiKey: "test-secret");
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Options, "/api/libraries");
        var response = await client.SendAsync(request);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
