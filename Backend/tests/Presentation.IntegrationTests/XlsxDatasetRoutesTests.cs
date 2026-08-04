using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using FluentAssertions;

namespace Presentation.IntegrationTests;

[Collection("Api")]
public sealed class XlsxDatasetRoutesTests
{
    private readonly ApiFactory _factory;

    public XlsxDatasetRoutesTests(ApiFactory factory) => _factory = factory;

    private HttpClient CreateClient() => _factory.CreateClient(new() { AllowAutoRedirect = false });

    private static byte[] BuildSalesWorkbook()
    {
        using var wb = new XLWorkbook();
        var sheet = wb.AddWorksheet("Sales");
        sheet.Cell(1, 1).Value = "category";
        sheet.Cell(1, 2).Value = "amount";
        sheet.Cell(2, 1).Value = "A";
        sheet.Cell(2, 2).Value = 10;
        sheet.Cell(3, 1).Value = "B";
        sheet.Cell(3, 2).Value = 20;
        sheet.Cell(4, 1).Value = "A";
        sheet.Cell(4, 2).Value = 5;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static MultipartFormDataContent Upload(string fileName, byte[] bytes)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "file", fileName);
        return content;
    }

    [Fact]
    public async Task Xlsx_upload_list_detail_generate_save_execute_delete_happy_path()
    {
        var client = CreateClient();
        var email = $"dsx_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.SeedActiveSubscriptionAsync(email);

        using var upload = await client.PostAsync(
            "/api/datasets/upload",
            Upload("sales.xlsx", BuildSalesWorkbook()));
        upload.StatusCode.Should().Be(HttpStatusCode.OK);

        using var uploadDoc = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
        var datasetId = uploadDoc.RootElement.GetProperty("id").GetGuid();
        uploadDoc.RootElement.GetProperty("name").GetString().Should().Be("sales");
        uploadDoc.RootElement.GetProperty("rowCount").GetInt32().Should().Be(3);

        var detail = await client.GetAsync($"/api/datasets/{datasetId}");
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        using var detailDoc = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        detailDoc.RootElement.GetProperty("columns").GetArrayLength().Should().Be(2);

        var generate = await client.PostAsJsonAsync(
            $"/api/datasets/{datasetId}/generate",
            new { Prompt = "Show sales by category", PrefabChartType = (string?)null, Mode = "auto" });
        generate.StatusCode.Should().Be(HttpStatusCode.OK);
        using var genDoc = JsonDocument.Parse(await generate.Content.ReadAsStringAsync());
        genDoc.RootElement.GetProperty("queryResult").GetArrayLength().Should().Be(2);
        var sql = genDoc.RootElement.GetProperty("sqlQuery").GetString();

        var saveChart = await client.PostAsJsonAsync("/api/charts", new
        {
            Title = "XLSX sales chart",
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

        var execute = await client.PostAsync($"/api/charts/{chartId}/execute", null);
        execute.StatusCode.Should().Be(HttpStatusCode.OK);
        using var executeDoc = JsonDocument.Parse(await execute.Content.ReadAsStringAsync());
        executeDoc.RootElement.GetProperty("queryResult").GetArrayLength().Should().Be(2);

        var delete = await client.DeleteAsync($"/api/datasets/{datasetId}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetAsync($"/api/datasets/{datasetId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}