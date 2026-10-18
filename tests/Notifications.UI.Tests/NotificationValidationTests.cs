using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Storefront.Web.Models;
using Storefront.Web.Validation;
using Xunit;

namespace Notifications.UI.Tests;

public class NotificationValidationTests
{
    [Theory]
    [InlineData("User", null, false)]
    [InlineData("User", "bad", false)]
    [InlineData("User", "00000000-0000-0000-0000-000000000000", false)]
    [InlineData("User", "11111111-1111-1111-1111-111111111111", true)]
    [InlineData("All", null, true)]
    [InlineData("Learner", "", true)]
    [InlineData("Moderator", "", true)]
    [InlineData("All", "11111111-1111-1111-1111-111111111111", false)]
    [InlineData("Learner", "11111111-1111-1111-1111-111111111111", false)]
    [InlineData("Moderator", "11111111-1111-1111-1111-111111111111", false)]
    public void ConditionalTargetIsValidatedServerSide(string audience, string? target, bool expected)
    {
        var input = new NotificationInput { Title = " Title ", Content = "Content", Audience = audience, TargetUserId = target };
        var errors = new List<ValidationResult>();
        Assert.Equal(expected, Validator.TryValidateObject(input, new ValidationContext(input), errors, true));
        Assert.Equal("Title", input.Title);
        if (!expected) Assert.Contains(errors, e => e.MemberNames.Contains(nameof(input.TargetUserId)));
    }

    [Fact]
    public void CustomAttributeGeneratesUnobtrusiveClientAttributes()
    {
        var metadata = new EmptyModelMetadataProvider();
        var attributes = new Dictionary<string, string>();
        var context = new ClientModelValidationContext(new ActionContext(new DefaultHttpContext(), new RouteData(),
            new ActionDescriptor()), metadata.GetMetadataForType(typeof(string)), metadata, attributes);
        new NotificationAudienceAttribute().AddValidation(context);
        Assert.Equal("true", attributes["data-val"]);
        Assert.Equal(NotificationAudienceAttribute.Message, attributes["data-val-notificationaudience"]);
        Assert.Equal("Audience", attributes["data-val-notificationaudience-other"]);
    }

    [Theory]
    [InlineData("", "Content", "All")]
    [InlineData("   ", "Content", "All")]
    [InlineData("Title", "   ", "All")]
    [InlineData("Title", "Content", "Admin")]
    [InlineData("Title", "Content", "0")]
    public void InvalidBasicFieldsFail(string title, string content, string audience)
    {
        var input = new NotificationInput { Title = title, Content = content, Audience = audience };
        Assert.False(Validator.TryValidateObject(input, new ValidationContext(input), [], true));
    }
}
