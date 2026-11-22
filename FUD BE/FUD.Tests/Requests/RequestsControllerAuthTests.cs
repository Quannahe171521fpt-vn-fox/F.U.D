using System.Linq;
using System.Reflection;
using FluentAssertions;
using FUD.Controllers.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FUD.Tests.Requests;

public class RequestsControllerAuthTests
{
    [Theory]
    [InlineData(nameof(RequestsController.GetAll), "Manager,Admin")]
    [InlineData(nameof(RequestsController.GetGuards), "Manager,Admin")]
    [InlineData(nameof(RequestsController.Process), "Manager,Admin")]
    [InlineData(nameof(RequestsController.GetGuardTransferTasks), "Guard")]
    [InlineData(nameof(RequestsController.SubmitTransferInspection), "Guard")]
    [InlineData(nameof(RequestsController.GetMyRequests), "Student")]
    [InlineData(nameof(RequestsController.GetBedTransferContext), "Student")]
    [InlineData(nameof(RequestsController.Create), "Student")]
    public void Action_HasExpectedAuthorizeRoles(string actionName, string expectedRoles)
    {
        var method = typeof(RequestsController).GetMethod(
            actionName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

        method.Should().NotBeNull($"action {actionName} must exist on RequestsController");

        var authorizeAttributes = method!.GetCustomAttributes<AuthorizeAttribute>(inherit: false).ToList();
        authorizeAttributes.Should().ContainSingle(
            $"action {actionName} must declare an explicit [Authorize(Roles = ...)] attribute");
        authorizeAttributes[0].Roles.Should().Be(expectedRoles);
    }

    [Fact]
    public void AllPublicActions_DeclareExplicitRoleBasedAuthorizeAttribute()
    {
        var publicActions = typeof(RequestsController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && m.GetCustomAttributes<NonActionAttribute>().Any() == false)
            .ToList();

        publicActions.Should().HaveCount(8);

        foreach (var action in publicActions)
        {
            var authorize = action.GetCustomAttribute<AuthorizeAttribute>(inherit: false);
            authorize.Should().NotBeNull($"action {action.Name} must not rely on bare class-level [Authorize]");
            authorize!.Roles.Should().NotBeNullOrWhiteSpace($"action {action.Name} must restrict Roles explicitly");
        }
    }
}
