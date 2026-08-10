using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Application.DTos.Request;
using Application.DTos.Response;
using Domain.Enums;
using FluentAssertions;

namespace Presentation.IntegrationTests;

[Collection("Api")]
public sealed class CollectionRoutesTests
{
    private readonly ApiFactory _factory;

    public CollectionRoutesTests(ApiFactory factory) => _factory = factory;

    private HttpClient CreateClient() => _factory.CreateClient(new() { AllowAutoRedirect = false });

    private static MultipartFormDataContent CsvUpload(string fileName, string csv)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", fileName);
        return content;
    }

    private static async Task<Guid> CreateCollectionAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/collections", new { Name = name });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Collections_require_subscription()
    {
        var client = CreateClient();
        var email = $"colsnosub_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        (await client.GetAsync("/api/collections")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Collection_create_upload_list_detail_generate_save_execute_delete_happy_path()
    {
        var client = CreateClient();
        var email = $"cols_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.SeedActiveSubscriptionAsync(email);

        var collectionId = await CreateCollectionAsync(client, "Sales data");

        var csv = "category,amount\nA,10\nB,20\nA,5\n";
        using var upload = await client.PostAsync(
            $"/api/collections/{collectionId}/files", CsvUpload("sales.csv", csv));
        upload.StatusCode.Should().Be(HttpStatusCode.OK);

        using var uploadDoc = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
        var fileId = uploadDoc.RootElement.GetProperty("id").GetGuid();
        uploadDoc.RootElement.GetProperty("name").GetString().Should().Be("sales");
        uploadDoc.RootElement.GetProperty("rowCount").GetInt32().Should().Be(3);

        var list = await client.GetAsync("/api/collections");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        using var listDoc = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        listDoc.RootElement.GetArrayLength().Should().BeGreaterThan(0);

        var detail = await client.GetAsync($"/api/collections/{collectionId}");
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        using var detailDoc = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        detailDoc.RootElement.GetProperty("files").GetArrayLength().Should().Be(1);

        var fileDetail = await client.GetAsync($"/api/collections/{collectionId}/files/{fileId}");
        fileDetail.StatusCode.Should().Be(HttpStatusCode.OK);
        using var fileDetailDoc = JsonDocument.Parse(await fileDetail.Content.ReadAsStringAsync());
        fileDetailDoc.RootElement.GetProperty("columns").GetArrayLength().Should().Be(2);
        fileDetailDoc.RootElement.GetProperty("previewRows").GetArrayLength().Should().Be(3);

        var generate = await client.PostAsJsonAsync(
            $"/api/collections/{collectionId}/files/{fileId}/generate",
            new { Prompt = "Show sales by category", PrefabChartType = (string?)null, Mode = "auto" });
        generate.StatusCode.Should().Be(HttpStatusCode.OK);
        using var genDoc = JsonDocument.Parse(await generate.Content.ReadAsStringAsync());
        genDoc.RootElement.GetProperty("queryResult").GetArrayLength().Should().Be(2);

        var saveChart = await client.PostAsJsonAsync("/api/charts", new
        {
            Title = "Collection sales chart",
            ChartType = "bar",
            XAxis = "category",
            YAxis = new[] { "amount" },
            Aggregation = "sum",
            GroupBy = "category",
            SqlQuery = "",
            ConnectionId = (Guid?)null,
            DatasetId = fileId,
            TableName = "sales",
            DataModel = new
            {
                Filters = Array.Empty<object>(),
                GroupBy = new[] { "category" },
                Aggregations = new[] { new { Column = "amount", Function = "sum" } },
                OrderBy = new[] { new { Column = "amount", Direction = "desc" } },
                Limit = 10
            }
        });
        saveChart.StatusCode.Should().Be(HttpStatusCode.OK);
        using var chartDoc = JsonDocument.Parse(await saveChart.Content.ReadAsStringAsync());
        var chartId = chartDoc.RootElement.GetProperty("id").GetGuid();

        var chartDetail = await client.GetAsync($"/api/charts/{chartId}");
        chartDetail.StatusCode.Should().Be(HttpStatusCode.OK);
        using var chartDetailDoc = JsonDocument.Parse(await chartDetail.Content.ReadAsStringAsync());
        chartDetailDoc.RootElement.GetProperty("datasetId").GetGuid().Should().Be(fileId);

        var execute = await client.PostAsync($"/api/charts/{chartId}/execute", null);
        execute.StatusCode.Should().Be(HttpStatusCode.OK);

        var deleteFile = await client.DeleteAsync($"/api/collections/{collectionId}/files/{fileId}");
        deleteFile.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetAsync($"/api/collections/{collectionId}/files/{fileId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var delete = await client.DeleteAsync($"/api/collections/{collectionId}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetAsync($"/api/collections/{collectionId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Collection_upload_rejects_non_csv_and_dedupes_names()
    {
        var client = CreateClient();
        var email = $"colbad_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.SeedActiveSubscriptionAsync(email);

        var collectionId = await CreateCollectionAsync(client, "Uploads");

        using var notCsv = CsvUpload("sales.csx", "a,b\n1,2\n");
        (await client.PostAsync($"/api/collections/{collectionId}/files", notCsv))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var first = CsvUpload("sales.csv", "category,amount\nA,10\n");
        (await client.PostAsync($"/api/collections/{collectionId}/files", first))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        using var second = CsvUpload("sales.csv", "category,amount\nA,10\n");
        using var duplicate = await client.PostAsync($"/api/collections/{collectionId}/files", second);
        duplicate.StatusCode.Should().Be(HttpStatusCode.OK);
        using var duplicateDoc = JsonDocument.Parse(await duplicate.Content.ReadAsStringAsync());
        duplicateDoc.RootElement.GetProperty("name").GetString().Should().Be("sales (2)");
    }

    [Fact]
    public async Task Collection_update_renames_and_switches_visibility_with_permission_check()
    {
        var ownerClient = CreateClient();
        var ownerEmail = $"coledit_owner_{Guid.NewGuid():N}@example.com";
        await ownerClient.RegisterAndLoginAsync(ownerEmail);

        var companyResp = await ownerClient.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest($"Co-{Guid.NewGuid():N}"));
        companyResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var company = await companyResp.ReadJsonAsync<CompanyResponse>();
        await _factory.SeedCompanySubscriptionAsync(company.Id);

        var roleResp = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/roles",
            new CreateRoleRequest("Analyst", false, false, false, false, CanManageConnections: false));
        roleResp.EnsureSuccessStatusCode();
        var analystRole = await roleResp.ReadJsonAsync<CompanyRoleResponse>();

        var createResp = await ownerClient.PostAsJsonAsync("/api/collections",
            new CreateCollectionRequest("Q2 report", null, CollectionVisibility.Company));
        createResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await createResp.ReadJsonAsync<CollectionResponse>();

        var updateResp = await ownerClient.PutAsJsonAsync($"/api/collections/{created.Id}",
            new UpdateCollectionRequest("Q2 report (final)", "Renamed and shared",
                CollectionVisibility.Roles, new List<Guid> { analystRole.Id }));
        updateResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResp.ReadJsonAsync<CollectionResponse>();
        updated.Name.Should().Be("Q2 report (final)");
        updated.Description.Should().Be("Renamed and shared");
        updated.Visibility.Should().Be(CollectionVisibility.Roles);
        updated.AllowedRoleIds.Should().ContainSingle(x => x == analystRole.Id);

        var viewerEmail = $"coledit_viewer_{Guid.NewGuid():N}@example.com";
        var inviteResp = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/invite",
            new InviteUserRequest(viewerEmail, analystRole.Id));
        inviteResp.EnsureSuccessStatusCode();
        var invites = await (await ownerClient.GetAsync($"/api/companies/{company.Id}/invites"))
            .ReadJsonAsync<List<CompanyInviteResponse>>();
        var pending = invites.First(i => i.Email == viewerEmail && !i.IsAccepted);

        var viewerClient = CreateClient();
        await viewerClient.RegisterAndLoginAsync(viewerEmail);
        var accept = await viewerClient.PostAsJsonAsync("/api/companies/accept-invite",
            new AcceptInviteRequest(pending.Id));
        accept.EnsureSuccessStatusCode();

        var forbidden = await viewerClient.PutAsJsonAsync($"/api/collections/{created.Id}",
            new UpdateCollectionRequest("Hijacked", null, CollectionVisibility.Company));
        forbidden.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var stillOldName = await ownerClient.GetAsync($"/api/collections/{created.Id}");
        using var stillOldNameDoc = JsonDocument.Parse(await stillOldName.Content.ReadAsStringAsync());
        stillOldNameDoc.RootElement.GetProperty("name").GetString().Should().Be("Q2 report (final)");
    }
}