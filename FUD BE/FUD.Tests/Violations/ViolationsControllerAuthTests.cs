using System.Linq;
using System.Reflection;
using FluentAssertions;
using FUD.Controllers.Violations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FUD.Tests.Violations;

public class ViolationsControllerAuthTests
{
    [Theory]
    [InlineData(nameof(ViolationsController.GetStudentsByRoom), "Guard,Manager,Admin")]
    [InlineData(nameof(ViolationsController.SearchStudent), "Guard,Manager,Admin")]
    [InlineData(nameof(ViolationsController.RecordViolation), "Guard")]
    [InlineData(nameof(ViolationsController.GetViolations), "Guard,Manager,Admin")]
    [InlineData(nameof(ViolationsController.GetViolationById), "Student,Guard,Manager,Admin")]
    [InlineData(nameof(ViolationsController.GetStudentNotices), "Student")]
    [InlineData(nameof(ViolationsController.GetStudentHistory), "Student")]
    [InlineData(nameof(ViolationsController.EvaluateViolation), "Manager,Admin")]
    [InlineData(nameof(ViolationsController.UnsuspendStudent), "Manager,Admin")]
    [InlineData(nameof(ViolationsController.GetBlocksByGuardDorm), "Guard")]
    public void Action_HasExpectedAuthorizeRoles_AndNoAllowAnonymous(string actionName, string expectedRoles)
    {
        var method = typeof(ViolationsController).GetMethod(
            actionName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

        method.Should().NotBeNull($"action {actionName} must exist on ViolationsController");
        method!.GetCustomAttribute<AllowAnonymousAttribute>(inherit: false)
            .Should().BeNull($"action {actionName} must not allow anonymous access");

        var authorizeAttributes = method.GetCustomAttributes<AuthorizeAttribute>(inherit: false).ToList();
        authorizeAttributes.Should().ContainSingle(
            $"action {actionName} must declare an explicit [Authorize(Roles = ...)] attribute");
        authorizeAttributes[0].Roles.Should().Be(expectedRoles);
    }

    [Fact]
    public void OnlyGetViolationTypes_MayAllowAnonymous()
    {
        var anonymousActions = typeof(ViolationsController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && m.GetCustomAttribute<AllowAnonymousAttribute>(inherit: false) != null)
            .Select(m => m.Name)
            .ToList();

        anonymousActions.Should().OnlyContain(name => name == nameof(ViolationsController.GetViolationTypes));
    }

    [Fact]
    public void EveryNonTypesAction_DeclaresExplicitRoleBasedAuthorize()
    {
        var publicActions = typeof(ViolationsController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName
                && m.GetCustomAttribute<NonActionAttribute>() == null
                && m.Name != nameof(ViolationsController.GetViolationTypes))
            .ToList();

        publicActions.Should().HaveCount(10);

        foreach (var action in publicActions)
        {
            var authorize = action.GetCustomAttribute<AuthorizeAttribute>(inherit: false);
            authorize.Should().NotBeNull($"action {action.Name} must declare [Authorize(Roles = ...)]");
            authorize!.Roles.Should().NotBeNullOrWhiteSpace($"action {action.Name} must specify Roles");
        }
    }
}
