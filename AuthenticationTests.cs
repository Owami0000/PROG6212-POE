using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.Services;
using RaceDay.Contracts;

namespace RaceDay.Api.Tests;

public class AuthenticationTests : ApiTestBase
{
    public AuthenticationTests(RaceDayFactory factory) : base(factory) { }

    [Theory]
    [InlineData(RoleNames.Organiser)]
    [InlineData(RoleNames.Participant)]
    public async Task Register_WithValidDetails_Returns201(string role)
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", NewRegisterRequest(role));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal(role, body!.Role);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var client = Factory.CreateClient();
        var request = NewRegisterRequest(RoleNames.Participant);
        await client.PostAsJsonAsync("/api/auth/register", request);

        var second = await client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidRole_Returns400()
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", NewRegisterRequest("Admin"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_ShortPassword_Returns400()
    {
        var client = Factory.CreateClient();
        var request = NewRegisterRequest(RoleNames.Participant);
        request.Password = "short";
        var response = await client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_StoresHashedPassword_NotPlainText()
    {
        var client = Factory.CreateClient();
        var request = NewRegisterRequest(RoleNames.Participant);
        await client.PostAsJsonAsync("/api/auth/register", request);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        var stored = db.Users.Single(u => u.Email == request.Email.ToLowerInvariant());

        Assert.NotEqual(request.Password, stored.PasswordHash);
        Assert.DoesNotContain(request.Password, stored.PasswordHash);
        Assert.True(PasswordHasher.Verify(request.Password, stored.PasswordHash));
    }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200AndCreatesSession()
    {
        var client = Factory.CreateClient();
        var request = NewRegisterRequest(RoleNames.Organiser);
        await client.PostAsJsonAsync("/api/auth/register", request);

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = request.Email, Password = request.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(RoleNames.Organiser, (await login.Content.ReadFromJsonAsync<AuthResponse>())!.Role);

        // Session works: a protected endpoint is now reachable.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/profile/me")).StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var client = Factory.CreateClient();
        var request = NewRegisterRequest(RoleNames.Participant);
        await client.PostAsJsonAsync("/api/auth/register", request);

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = request.Email, Password = "WrongPassword!" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/profile/me")).StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401()
    {
        var client = Factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = UniqueEmail("nobody"), Password = TestPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Logout_ClearsSession()
    {
        var (client, _) = await CreateLoggedInUserAsync(RoleNames.Participant);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/profile/me")).StatusCode);

        var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/profile/me")).StatusCode);
    }

    [Fact]
    public async Task Profile_WhenUnauthenticated_Returns401()
    {
        var response = await Factory.CreateClient().GetAsync("/api/profile/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(RoleNames.Organiser)]
    [InlineData(RoleNames.Participant)]
    public async Task Profile_BothRoles_CanViewAndUpdateOwnProfile(string role)
    {
        var (client, user) = await CreateLoggedInUserAsync(role);

        var view = await client.GetFromJsonAsync<ProfileDto>("/api/profile/me");
        Assert.Equal(user.UserId, view!.UserId);

        var update = await client.PutAsJsonAsync("/api/profile/me",
            new UpdateProfileRequest { FirstName = "Updated", LastName = "Name", PhoneNumber = "0123456789" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var after = await client.GetFromJsonAsync<ProfileDto>("/api/profile/me");
        Assert.Equal("Updated", after!.FirstName);
        Assert.Equal("0123456789", after.PhoneNumber);
    }

    [Fact]
    public void PasswordHasher_UsesRandomSalt_AndVerifiesCorrectly()
    {
        var h1 = PasswordHasher.Hash("Secret123!");
        var h2 = PasswordHasher.Hash("Secret123!");

        Assert.NotEqual(h1, h2);
        Assert.True(PasswordHasher.Verify("Secret123!", h1));
        Assert.False(PasswordHasher.Verify("Different!", h1));
        Assert.False(PasswordHasher.Verify("Secret123!", "not-a-valid-hash"));
    }
}
