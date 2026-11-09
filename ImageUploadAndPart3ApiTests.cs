using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using RaceDay.Contracts;

namespace RaceDay.Api.Tests;

/// <summary>Part 3 API additions: image uploads (Azure Blob via the API), enrolment counts and finisher totals.</summary>
public class ImageUploadAndPart3ApiTests : ApiTestBase
{
    public ImageUploadAndPart3ApiTests(RaceDayFactory factory) : base(factory) { }

    private static MultipartFormDataContent ImageForm(string fileName = "banner.png", string contentType = "image/png", int size = 128)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(new byte[size]);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        return form;
    }

    // ---------------- Event banner ----------------

    [Fact]
    public async Task Organiser_CanUploadBannerForOwnEvent_UrlIsSavedOnEvent()
    {
        var (org, _) = await CreateLoggedInUserAsync(RoleNames.Organiser);
        var ev = await CreateEventAsync(org);

        var response = await org.PostAsync($"/api/events/{ev.EventId}/banner", ImageForm());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var upload = await response.Content.ReadFromJsonAsync<UploadResponse>();
        Assert.StartsWith("https://", upload!.FileUrl);

        var reloaded = await org.GetFromJsonAsync<EventDto>($"/api/events/{ev.EventId}");
        Assert.Equal(upload.FileUrl, reloaded!.BannerImageUrl);
    }

    [Fact]
    public async Task Participant_CannotUploadBanner_Returns403()
    {
        var (org, _) = await CreateLoggedInUserAsync(RoleNames.Organiser);
        var (part, _) = await CreateLoggedInUserAsync(RoleNames.Participant);
        var ev = await CreateEventAsync(org);

        var response = await part.PostAsync($"/api/events/{ev.EventId}/banner", ImageForm());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OtherOrganiser_CannotUploadBanner_Returns403()
    {
        var (owner, _) = await CreateLoggedInUserAsync(RoleNames.Organiser);
        var (other, _) = await CreateLoggedInUserAsync(RoleNames.Organiser);
        var ev = await CreateEventAsync(owner);

        var response = await other.PostAsync($"/api/events/{ev.EventId}/banner", ImageForm());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_CannotUploadBanner_Returns401()
    {
        var response = await Factory.CreateClient().PostAsync("/api/events/1/banner", ImageForm());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_NonImageFile_Returns400()
    {
        var (org, _) = await CreateLoggedInUserAsync(RoleNames.Organiser);
        var ev = await CreateEventAsync(org);

        var response = await org.PostAsync($"/api/events/{ev.EventId}/banner", ImageForm("notes.txt", "text/plain"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_FileTooLarge_Returns400()
    {
        var (org, _) = await CreateLoggedInUserAsync(RoleNames.Organiser);
        var ev = await CreateEventAsync(org);

        var response = await org.PostAsync($"/api/events/{ev.EventId}/banner", ImageForm(size: 5 * 1024 * 1024 + 1));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_ForMissingEvent_Returns404()
    {
        var (org, _) = await CreateLoggedInUserAsync(RoleNames.Organiser);
        var response = await org.PostAsync("/api/events/999999/banner", ImageForm());
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------------- Profile picture ----------------

    [Theory]
    [InlineData(RoleNames.Organiser)]
    [InlineData(RoleNames.Participant)]
    public async Task LoggedInUser_CanUploadProfilePicture_UrlIsSavedOnProfile(string role)
    {
        var (client, _) = await CreateLoggedInUserAsync(role);

        var response = await client.PostAsync("/api/profile/me/image", ImageForm("me.jpg", "image/jpeg"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var upload = await response.Content.ReadFromJsonAsync<UploadResponse>();

        var profile = await client.GetFromJsonAsync<ProfileDto>("/api/profile/me");
        Assert.Equal(upload!.FileUrl, profile!.ProfileImageUrl);
    }

    [Fact]
    public async Task Unauthenticated_CannotUploadProfilePicture_Returns401()
    {
        var response = await Factory.CreateClient().PostAsync("/api/profile/me/image", ImageForm());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UploadProfilePicture_NonImage_Returns400()
    {
        var (client, _) = await CreateLoggedInUserAsync(RoleNames.Participant);
        var response = await client.PostAsync("/api/profile/me/image", ImageForm("virus.exe", "application/octet-stream"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------------- Dashboard / results data ----------------

    [Fact]
    public async Task OrganiserEvents_IncludeEnrolmentCount_ExcludingCancelled()
    {
        var (org, _) = await CreateLoggedInUserAsync(RoleNames.Organiser);
        var (p1, _) = await CreateLoggedInUserAsync(RoleNames.Participant);
        var (p2, _) = await CreateLoggedInUserAsync(RoleNames.Participant);
        var ev = await CreateEventAsync(org);
        var cat = await CreateCategoryAsync(org, ev.EventId);
        await EnrolAsync(p1, ev.EventId, cat.CategoryId);
        var second = await EnrolAsync(p2, ev.EventId, cat.CategoryId);

        var mine = await org.GetFromJsonAsync<List<EventDto>>("/api/events/mine");
        Assert.Equal(2, mine!.Single(e => e.EventId == ev.EventId).EnrolmentCount);

        await org.PutAsJsonAsync($"/api/enrolments/{second.EnrolmentId}/status",
            new UpdateEnrolmentStatusRequest { Status = "Cancelled" });
        mine = await org.GetFromJsonAsync<List<EventDto>>("/api/events/mine");
        Assert.Equal(1, mine!.Single(e => e.EventId == ev.EventId).EnrolmentCount);
    }

    [Fact]
    public async Task MyResults_IncludeEventDateAndTotalFinishers()
    {
        var (org, _) = await CreateLoggedInUserAsync(RoleNames.Organiser);
        var (p1, _) = await CreateLoggedInUserAsync(RoleNames.Participant);
        var (p2, _) = await CreateLoggedInUserAsync(RoleNames.Participant);
        var ev = await CreateEventAsync(org);
        var cat = await CreateCategoryAsync(org, ev.EventId);
        var e1 = await EnrolAsync(p1, ev.EventId, cat.CategoryId);
        var e2 = await EnrolAsync(p2, ev.EventId, cat.CategoryId);

        await org.PostAsJsonAsync("/api/results", new CreateResultRequest { EnrolmentId = e1.EnrolmentId, FinishTime = new TimeSpan(0, 41, 0), FinishPosition = 1 });
        await org.PostAsJsonAsync("/api/results", new CreateResultRequest { EnrolmentId = e2.EnrolmentId, FinishTime = new TimeSpan(0, 49, 0), FinishPosition = 2 });

        var mine = await p2.GetFromJsonAsync<List<ResultDto>>("/api/results/mine");
        var result = Assert.Single(mine!);
        Assert.Equal(2, result.FinishPosition);
        Assert.Equal(2, result.TotalFinishers);
        Assert.Equal(ev.EventDate.Date, result.EventDate.Date);
        Assert.Equal(cat.Name, result.CategoryName);
    }
}
