using Application.DTos.Request;
using Application.Validators;
using FluentAssertions;

namespace Application.Tests;

public class MapChartTypeValidatorTests
{
    [Fact]
    public void Prefab_mode_accepts_map_and_rejects_unknown_types()
    {
        var validator = new GenerateChartRequestValidator();

        validator.Validate(new GenerateChartRequest(Guid.NewGuid(), "sales", null, "map", "prefab"))
            .IsValid.Should().BeTrue();

        var unknown = validator.Validate(new GenerateChartRequest(Guid.NewGuid(), "sales", null, "heatmap", "prefab"));
        unknown.IsValid.Should().BeFalse();
        unknown.Errors.Should().Contain(error =>
            error.PropertyName == "PrefabChartType" && error.ErrorMessage.Contains("map"));
    }

    [Fact]
    public void Collection_prefab_mode_rejects_unknown_chart_types()
    {
        var validator = new GenerateCollectionChartRequestValidator();

        validator.Validate(new GenerateCollectionChartRequest(null, "map", "prefab"))
            .IsValid.Should().BeTrue();

        validator.Validate(new GenerateCollectionChartRequest("show sales", null, "auto"))
            .IsValid.Should().BeTrue();

        validator.Validate(new GenerateCollectionChartRequest(null, "heatmap", "prefab"))
            .IsValid.Should().BeFalse();
    }
}
