using Application.Settings;
using ClosedXML.Excel;
using FluentAssertions;
using Infrastructure.Datasets;
using Microsoft.Extensions.Options;

namespace Infrastructure.Tests;

public sealed class XlsxFileParserTests
{
    private static XlsxFileParser CreateParser(DatasetSettings settings) =>
        new(Options.Create(settings));

    private static byte[] BuildWorkbook(int dataRows, int columns)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Data");

        for (var column = 1; column <= columns; column++)
            sheet.Cell(1, column).Value = $"col{column}";

        for (var row = 1; row <= dataRows; row++)
        {
            for (var column = 1; column <= columns; column++)
                sheet.Cell(row + 1, column).Value = row * 10 + column;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    [Fact]
    public void Parses_a_normal_workbook()
    {
        var parser = CreateParser(new DatasetSettings
        {
            MaxRows = 10,
            MaxColumns = 5,
            MaxExpandedBytes = 256L * 1024 * 1024,
        });

        using var stream = new MemoryStream(BuildWorkbook(dataRows: 2, columns: 2));

        var (headers, rows) = parser.Parse(stream);

        headers.Should().Equal("col1", "col2");
        rows.Should().HaveCount(2);
        rows[0].Should().Equal("11", "12");
    }

    [Fact]
    public void Rejects_workbook_with_too_many_rows()
    {
        var parser = CreateParser(new DatasetSettings { MaxRows = 2, MaxColumns = 5 });
        using var stream = new MemoryStream(BuildWorkbook(dataRows: 3, columns: 2));

        var act = () => parser.Parse(stream);

        act.Should().Throw<ArgumentException>().WithMessage("*at most 2 rows*");
    }

    [Fact]
    public void Rejects_workbook_with_too_many_columns()
    {
        var parser = CreateParser(new DatasetSettings { MaxRows = 10, MaxColumns = 2 });
        using var stream = new MemoryStream(BuildWorkbook(dataRows: 1, columns: 3));

        var act = () => parser.Parse(stream);

        act.Should().Throw<ArgumentException>().WithMessage("*at most 2 columns*");
    }

    [Fact]
    public void Rejects_archive_that_expands_beyond_the_budget()
    {
        var parser = CreateParser(new DatasetSettings
        {
            MaxRows = 10,
            MaxColumns = 5,
            MaxExpandedBytes = 1,
        });

        using var stream = new MemoryStream(BuildWorkbook(dataRows: 1, columns: 1));

        var act = () => parser.Parse(stream);

        act.Should().Throw<ArgumentException>().WithMessage("*expands*");
    }

    [Fact]
    public void Rejects_invalid_archive()
    {
        var parser = CreateParser(new DatasetSettings());
        using var stream = new MemoryStream([1, 2, 3, 4]);

        var act = () => parser.Parse(stream);

        act.Should().Throw<ArgumentException>().WithMessage("*not a valid XLSX*");
    }
}
