using System.Net;
using System.Net.Http.Json;
using RaceDay.Contracts;

namespace RaceDay.Api.Tests;

/// <summary>Proves that wrong roles are rejected (403) and anonymous users are rejected (401) everywhere.</summary>
public class RoleAccessTests : ApiTestBase
{
    public RoleAccessTests(RaceDayFactory factory) : base(factory) { }

    public static TheoryData<string, string> OrganiserOnlyEndpoints => new()
    {
        { "GET", "/api/events/mine" },
        { "POST", "/api/events" },
        { "PUT", "/api/events/1" },
        { "DELETE", "/api/events/1" },
        { "POST", "/api/events/1/categories" },
        { "PUT", "/api/categories/1" },
        { "DELETE", "/api/categories/1" },
        { "GET", "/api/enrolments/event/1" },
        { "PUT", "/api/enrolments/1/status" },
        { "POST", "/api/results" },
        { "GET", "/api/results/event/1" },
    };

    public static TheoryData<string, string> ParticipantOnlyEndpoints => new()
    {
        { "POST", "/api/enrolments" },
        { "GET", "/api/enrolments/mine" },
        { "GET", "/api/results/mine" },
    };

    public static TheoryData<string, string> AllProtectedEndpoints
    {
        get
        {
            var data = new TheoryData<string, string>
            {
                { "GET", "/api/profile/me" },
                { "PUT", "/api/profile/me" },
                { "GET", "/api/events" },
                { "GET", "/api/events/1" },
                { "GET", "/api/events/1/categories" },
            };
            foreach (var row in OrganiserOnlyEndpoints) data.Add((string)row[0], (string)row[1]);
            foreach (var row in ParticipantOnlyEndpoints) data.Add((string)row[0], (string)row[1]);
            return data;
        }
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string url)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method is "POST" or "PUT") request.Content = JsonContent.Create(new { });
        return client.SendAsync(request);
    }

    [Theory]
    [MemberData(nameof(OrganiserOnlyEndpoints))]
    public async Task Participant_IsRejectedFromOrganiserEndpoints_With403(string method, string url)
    {
        var (participant, _) = await CreateLoggedInUserAsync(RoleNames.Participant);
        var response = await SendAsync(participant, method, url);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(ParticipantOnlyEndpoints))]
    public async Task Organiser_IsRejectedFromParticipantEndpoints_With403(string method, string url)
    {
        var (organiser, _) = await CreateLoggedInUserAsync(RoleNames.Organiser);
        var response = await SendAsync(organiser, method, url);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(AllProtectedEndpoints))]
    public async Task Unauthenticated_IsRejectedFromAllProtectedEndpoints_With401(string method, string url)
    {
        var response = await SendAsync(Factory.CreateClient(), method, url);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
