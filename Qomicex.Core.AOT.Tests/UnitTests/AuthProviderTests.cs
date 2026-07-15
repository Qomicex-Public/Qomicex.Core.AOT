using System.Net;
using System.Text.Json.Nodes;
using Qomicex.Core.AOT.Builder;
using Qomicex.Core.AOT.Core;
using Qomicex.Core.AOT.Interfaces;
using Qomicex.Core.AOT.Services;
using Qomicex.Core.AOT.Utils;
using Xunit.Abstractions;

namespace Qomicex.Core.AOT.Tests.UnitTests;

public class AuthProviderTests
{
    private readonly ITestOutputHelper _output;

    public AuthProviderTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // ── OfflineUuidHelper ────────────────────────────────────────

    [Fact]
    public void OfflineUuid_ShouldGenerateValidUuid()
    {
        var uuid = OfflineUuidHelper.GenerateUuid("Player123");
        uuid.Should().NotBeNullOrEmpty();
        uuid.Length.Should().Be(32);
        uuid.Should().MatchRegex("^[0-9a-f]{32}$");
    }

    [Fact]
    public void OfflineUuid_SameName_ShouldProduceSameUuid()
    {
        var uuid1 = OfflineUuidHelper.GenerateUuid("TestPlayer");
        var uuid2 = OfflineUuidHelper.GenerateUuid("TestPlayer");
        uuid1.Should().Be(uuid2);
    }

    [Fact]
    public void OfflineUuid_EmptyName_ShouldReturnEmpty()
    {
        OfflineUuidHelper.GenerateUuid("").Should().BeEmpty();
    }

    // ── DefaultAuthProvider ──────────────────────────────────────

    [Fact]
    public async Task DefaultAuth_ShouldAuthenticateOffline()
    {
        var provider = new DefaultAuthProvider();
        var result = await provider.AuthenticateAsync(new AuthRequest
        {
            Username = "Steve",
            IsOffline = true
        });

        result.Success.Should().BeTrue();
        result.Username.Should().Be("Steve");
        result.Uuid.Should().NotBeNullOrEmpty();
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.UserType.Should().Be("legacy");
    }

    [Fact]
    public async Task DefaultAuth_ShouldGenerateValidOfflineUuid()
    {
        var provider = new DefaultAuthProvider();
        var result = await provider.AuthenticateAsync(new AuthRequest
        {
            Username = "Notch",
            IsOffline = true
        });

        var expectedUuid = OfflineUuidHelper.GenerateUuid("Notch");
        result.Uuid.Should().Be(expectedUuid);
    }

    [Fact]
    public async Task DefaultAuth_Validate_ShouldAlwaysReturnTrue()
    {
        var provider = new DefaultAuthProvider();
        var valid = await provider.ValidateAsync("any-token");
        valid.Should().BeTrue();
    }

    [Fact]
    public async Task DefaultAuth_Invalidate_ShouldNotThrow()
    {
        var provider = new DefaultAuthProvider();
        await provider.Invoking(p => p.InvalidateAsync("any-token"))
            .Should().NotThrowAsync();
    }

    // ── YggdrasilAuthProvider ────────────────────────────────────

    [Fact]
    public async Task YggdrasilAuth_Authenticate_ShouldReturnSuccess()
    {
        var handler = new MockHttpMessageHandler(async req =>
        {
            var body = await req.Content!.ReadAsStringAsync();
            var authReq = JsonNode.Parse(body)!.AsObject();
            authReq["username"]?.ToString().Should().Be("test@test.com");

            var resp = new JsonObject
            {
                ["accessToken"] = "mock-access-token",
                ["clientToken"] = "mock-client-token",
                ["availableProfiles"] = null,
                ["selectedProfile"] = new JsonObject
                {
                    ["id"] = "abc123def456",
                    ["name"] = "TestPlayer"
                },
                ["user"] = new JsonObject
                {
                    ["id"] = "user123"
                }
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(resp.ToJsonString())
            };
        });

        var http = new HttpClient(handler);
        var provider = new YggdrasilAuthProvider(http, "https://auth.example.com");
        var result = await provider.AuthenticateAsync(new AuthRequest
        {
            Username = "test@test.com",
            Password = "password123"
        });

        result.Success.Should().BeTrue();
        result.Username.Should().Be("TestPlayer");
        result.AccessToken.Should().Be("mock-access-token");
        result.ClientToken.Should().Be("mock-client-token");
        result.Uuid.Should().Be("abc123def456");
    }

    [Fact]
    public async Task YggdrasilAuth_Authenticate_FailedResponse_ShouldReturnError()
    {
        var handler = new MockHttpMessageHandler(_ =>
        {
            var error = new JsonObject
            {
                ["error"] = "ForbiddenOperationException",
                ["errorMessage"] = "Invalid credentials",
                ["cause"] = (string?)null
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent(error.ToJsonString())
            });
        });

        var http = new HttpClient(handler);
        var provider = new YggdrasilAuthProvider(http, "https://auth.example.com");
        var result = await provider.AuthenticateAsync(new AuthRequest
        {
            Username = "bad@user.com",
            Password = "wrong"
        });

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid credentials");
    }

    [Fact]
    public async Task YggdrasilAuth_Validate_NoContent_ShouldReturnTrue()
    {
        var handler = new MockHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)));

        var http = new HttpClient(handler);
        var provider = new YggdrasilAuthProvider(http, "https://auth.example.com");
        var valid = await provider.ValidateAsync("valid-token");

        valid.Should().BeTrue();
    }

    [Fact]
    public async Task YggdrasilAuth_Validate_NonSuccess_ShouldReturnFalse()
    {
        var handler = new MockHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)));

        var http = new HttpClient(handler);
        var provider = new YggdrasilAuthProvider(http, "https://auth.example.com");
        var valid = await provider.ValidateAsync("invalid-token");

        valid.Should().BeFalse();
    }

    [Fact]
    public async Task YggdrasilAuth_Invalidate_ShouldSendRequest()
    {
        var wasCalled = false;
        var handler = new MockHttpMessageHandler(async req =>
        {
            wasCalled = true;
            var body = await req.Content!.ReadAsStringAsync();
            body.Should().Contain("revoke-token");
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        var http = new HttpClient(handler);
        var provider = new YggdrasilAuthProvider(http, "https://auth.example.com");
        await provider.InvalidateAsync("revoke-token");

        wasCalled.Should().BeTrue();
    }

    // ── MicrosoftAuthProvider (basic) ────────────────────────────

    [Fact]
    public async Task MicrosoftAuth_NoAccessToken_ShouldReturnError()
    {
        var http = new HttpClient();
        var provider = new MicrosoftAuthProvider(http, "test-client-id");
        var result = await provider.AuthenticateAsync(new AuthRequest());

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("access_token");
    }

    // ── GameCoreBuilder ──────────────────────────────────────────

    [Fact]
    public void Builder_Default_ShouldUseDefaultAuthProvider()
    {
        using var core = new GameCoreBuilder()
            .UseGameRoot(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()))
            .Build();

        core.Should().NotBeNull();
    }

    [Fact]
    public void Builder_UseMicrosoftAuth_ShouldSetClientId()
    {
        var builder = new GameCoreBuilder();
        builder.UseMicrosoftAuth("my-client-id");

        builder.Invoking(b => b.Build()).Should().NotThrow();
    }

    [Fact]
    public void Builder_UseYggdrasilAuth_ShouldSetServerUrl()
    {
        var builder = new GameCoreBuilder();
        builder.UseYggdrasilAuth("https://custom-auth.example.com");

        builder.Invoking(b => b.Build()).Should().NotThrow();
    }
}

// ponytail: minimal HTTP handler for testing
internal sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

    public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return await _handler(request);
    }
}
