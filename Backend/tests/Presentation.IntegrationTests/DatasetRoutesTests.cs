using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace Presentation.IntegrationTests;

[Collection("Api")]
public sealed class DatasetRoutesTests
{
    private readonly ApiFactory _factory;

    public DatasetRoutesTests(ApiFactory factory) => _factory = factory;

    private HttpClient CreateClient() => _factory.CreateClient(new() { AllowAutoRedirect = false });

    private static MultipartFormDataContent CsvUpload(string fileName, string csv)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", fileName);
        return content;
    }

    [Fact]
    public async Task Datasets_require_subscription()
    {
        var client = CreateClient();
        var email = $"dsnosub_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        (await client.GetAsync("/api/datasets")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Dataset_upload_list_detail_generate_save_execute_delete_happy_path()
    {
        var client = CreateClient();
        var email = $"ds_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.SeedActiveSubscriptionAsync(email);

        var csv = "category,amount\nA,10\nB,20\nA,5\n";
        using var upload = await client.PostAsync("/api/datasets/upload", CsvUpload("sales.csv", csv));
        upload.StatusCode.Should().Be(HttpStatusCode.OK);

        using var uploadDoc = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
        var datasetId = uploadDoc.RootElement.GetProperty("id").GetGuid();
        uploadDoc.RootElement.GetProperty("name").GetString().Should().Be("sales");
        uploadDoc.RootElement.GetProperty("rowCount").GetInt32().Should().Be(3);

        (await client.GetAsync("/api/datasets")).StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await client.GetAsync($"/api/datasets/{datasetId}");
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        using var detailDoc = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        detailDoc.RootElement.GetProperty("columns").GetArrayLength().Should().Be(2);
        detailDoc.RootElement.GetProperty("previewRows").GetArrayLength().Should().Be(3);

        var generate = await client.PostAsJsonAsync(
            $"/api/datasets/{datasetId}/generate",
            new { Prompt = "Show sales by category", PrefabChartType = (string?)null, Mode = "auto" });
        generate.StatusCode.Should().Be(HttpStatusCode.OK);
        using var genDoc = JsonDocument.Parse(await generate.Content.ReadAsStringAsync());
        genDoc.RootElement.GetProperty("queryResult").GetArrayLength().Should().Be(2);

        var sql = genDoc.RootElement.GetProperty("sqlQuery").GetString();

        var saveChart = await client.PostAsJsonAsync("/api/charts", new
        {
            Title = "Dataset sales chart",
            ChartType = "bar",
            XAxis = "category",
            YAxis = new[] { "amount" },
            Aggregation = "sum",
            GroupBy = "category",
            SqlQuery = sql,
            ConnectionId = (Guid?)null,
            DatasetId = datasetId,
            TableName = "sales"
        });
        saveChart.StatusCode.Should().Be(HttpStatusCode.OK);
        using var chartDoc = JsonDocument.Parse(await saveChart.Content.ReadAsStringAsync());
        var chartId = chartDoc.RootElement.GetProperty("id").GetGuid();

        var chartDetail = await client.GetAsync($"/api/charts/{chartId}");
        chartDetail.StatusCode.Should().Be(HttpStatusCode.OK);
        using var chartDetailDoc = JsonDocument.Parse(await chartDetail.Content.ReadAsStringAsync());
        chartDetailDoc.RootElement.GetProperty("datasetId").GetGuid().Should().Be(datasetId);

        var execute = await client.PostAsync($"/api/charts/{chartId}/execute", null);
        execute.StatusCode.Should().Be(HttpStatusCode.OK);

        var delete = await client.DeleteAsync($"/api/datasets/{datasetId}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetAsync($"/api/datasets/{datasetId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Dataset_upload_rejects_non_csv_and_duplicate_names()
    {
        var client = CreateClient();
        var email = $"dsbad_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.SeedActiveSubscriptionAsync(email);

        using var notCsv = CsvUpload("sales.csx", "a,b\n1,2\n");
        (await client.PostAsync("/api/datasets/upload", notCsv))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var first = CsvUpload("sales.csv", "category,amount\nA,10\n");
        (await client.PostAsync("/api/datasets/upload", first)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var second = CsvUpload("sales.csv", "category,amount\nA,10\n");
        var duplicate = await client.PostAsync("/api/datasets/upload", second);
        duplicate.StatusCode.Should().Be(HttpStatusCode.OK);
        using var duplicateDoc = JsonDocument.Parse(await duplicate.Content.ReadAsStringAsync());
        duplicateDoc.RootElement.GetProperty("name").GetString().Should().Be("sales (2)");
    }
}