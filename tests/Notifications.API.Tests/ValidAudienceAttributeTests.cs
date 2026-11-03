using System.ComponentModel.DataAnnotations;
using Notifications.Application.DTOs;
using Notifications.Application.Validation;
using Xunit;

namespace Notifications.API.Tests;

public class ValidAudienceAttributeTests
{
    [Theory]
    [InlineData("User", null, false)]
    [InlineData("User", "00000000-0000-0000-0000-000000000000", false)]
    [InlineData("User", "11111111-1111-1111-1111-111111111111", true)]
    [InlineData("All", null, true)]
    [InlineData("Learner", null, true)]
    [InlineData("Moderator", null, true)]
    [InlineData("All", "11111111-1111-1111-1111-111111111111", false)]
    [InlineData("Learner", "11111111-1111-1111-1111-111111111111", false)]
    [InlineData("Moderator", "11111111-1111-1111-1111-111111111111", false)]
    [InlineData("All", "00000000-0000-0000-0000-000000000000", false)]
    public void ValidatesAudienceTargetCombinationWithFieldError(string audience, string? target, bool valid)
    {
        var request = new CreateAdminNotificationRequest
        {
            Audience = audience, TargetUserId = target is null ? null : Guid.Parse(target)
        };
        var result = new ValidAudienceAttribute().GetValidationResult(request.TargetUserId,
            new ValidationContext(request) { MemberName = nameof(request.TargetUserId) });
        Assert.Equal(valid, result == ValidationResult.Success);
        if (!valid) Assert.Contains(nameof(request.TargetUserId), result!.MemberNames);
    }
}
