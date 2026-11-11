using ClassFund.Api.Features.Treasurers;

namespace ClassFund.UnitTests.Api;

/// <summary>Phải chọn học sinh (module-plan.md M4; G-03 "chưa chọn học sinh → lỗi").</summary>
public class TreasurersRequestValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void Missing_student_is_rejected(string? studentId)
    {
        var request = new AssignTreasurerRequest(studentId is null ? null : Guid.Parse(studentId));

        var result = new AssignTreasurerRequestValidator().Validate(request);

        var error = Assert.Single(result.Errors);
        Assert.Equal("StudentId", error.PropertyName);
        Assert.Equal("Chọn học sinh.", error.ErrorMessage);
    }

    [Fact]
    public void Student_id_is_accepted() =>
        Assert.True(new AssignTreasurerRequestValidator().Validate(new AssignTreasurerRequest(Guid.NewGuid())).IsValid);
}
