using System.Net.Http.Json;
using RaceDay.Contracts;

namespace RaceDay.Api.Tests;

public abstract class ApiTestBase : IClassFixture<RaceDayFactory>
{
    protected const string TestPassword = "Password123!";
    protected readonly RaceDayFactory Factory;

    protected ApiTestBase(RaceDayFactory factory) => Factory = factory;

    protected static string UniqueEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@raceday.test";

    protected static RegisterRequest NewRegisterRequest(string role, string? email = null) => new()
    {
        FirstName = "Test", LastName = role, Email = email ?? UniqueEmail(role.ToLower()),
        Password = TestPassword, Role = role
    };

    //Registers a new user, logs in, and returns a client holding the session cookie.
    protected async Task<(HttpClient Client, AuthResponse User)> CreateLoggedInUserAsync(string role)
    {
        var client = Factory.CreateClient();
        var request = NewRegisterRequest(role);
        (await client.PostAsJsonAsync("/api/auth/register", request)).EnsureSuccessStatusCode();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = request.Email, Password = request.Password });
        login.EnsureSuccessStatusCode();
        return (client, (await login.Content.ReadFromJsonAsync<AuthResponse>())!);
    }

    protected static CreateEventRequest NewEventRequest(string name = "City 10km Run") => new()
    {
        Name = name, Description = "A fun run through the city.",
        EventDate = DateTime.UtcNow.AddDays(30), Location = "Johannesburg",
        DistanceKm = 10, EventType = EventTypes.Run
    };

    protected static async Task<EventDto> CreateEventAsync(HttpClient organiser, string name = "City 10km Run")
    {
        var response = await organiser.PostAsJsonAsync("/api/events", NewEventRequest(name));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EventDto>())!;
    }

    protected static async Task<CategoryDto> CreateCategoryAsync(HttpClient organiser, int eventId, string name = "Senior")
    {
        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories",
            new CreateCategoryRequest { Name = name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CategoryDto>())!;
    }

    protected static async Task<EnrolmentDto> EnrolAsync(HttpClient participant, int eventId, int categoryId)
    {
        var response = await participant.PostAsJsonAsync("/api/enrolments",
            new CreateEnrolmentRequest { EventId = eventId, CategoryId = categoryId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EnrolmentDto>())!;
    }
}
