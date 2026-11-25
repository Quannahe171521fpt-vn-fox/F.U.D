using System;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using FUD.Controllers.Facilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FUD.Tests.Facilities;

public class FacilitiesControllerAuthTests
{
    [Fact]
    public void NewsController_HasClassLevelManagerAuthorize_AndOnlyGetPublishedAllowsAnonymous()
    {
        var classAuth = typeof(NewsController).GetCustomAttribute<AuthorizeAttribute>(inherit: false);
        classAuth.Should().NotBeNull();
        classAuth!.Roles.Should().Be("Manager");

        var anonymousActions = typeof(NewsController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && m.GetCustomAttribute<AllowAnonymousAttribute>(inherit: false) != null)
            .Select(m => m.Name)
            .ToList();

        anonymousActions.Should().Equal(nameof(NewsController.GetPublished));
    }

    [Fact]
    public void EquipmentController_HasClassLevelManagerAuthorize_AndNoAllowAnonymous()
    {
        var classAuth = typeof(EquipmentController).GetCustomAttribute<AuthorizeAttribute>(inherit: false);
        classAuth.Should().NotBeNull();
        classAuth!.Roles.Should().Be("Manager");

        var anonymousActions = typeof(EquipmentController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && m.GetCustomAttribute<AllowAnonymousAttribute>(inherit: false) != null)
            .ToList();

        anonymousActions.Should().BeEmpty();
    }

    [Theory]
    [InlineData(typeof(BlocksController))]
    [InlineData(typeof(RoomsController))]
    public void BlocksAndRoomsControllers_AllMutatingActions_RequireManager(Type controllerType)
    {
        var classAuth = controllerType.GetCustomAttribute<AuthorizeAttribute>(inherit: false);
        classAuth.Should().NotBeNull();
        classAuth!.Roles.Should().Be("Manager,Guard");

        var mutatingActions = controllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && (
                m.GetCustomAttribute<HttpPostAttribute>() != null ||
                m.GetCustomAttribute<HttpPutAttribute>() != null ||
                m.GetCustomAttribute<HttpPatchAttribute>() != null ||
                m.GetCustomAttribute<HttpDeleteAttribute>() != null))
            .ToList();

        mutatingActions.Should().NotBeEmpty();

        foreach (var action in mutatingActions)
        {
            action.GetCustomAttribute<AllowAnonymousAttribute>(inherit: false).Should().BeNull();
            var methodAuth = action.GetCustomAttribute<AuthorizeAttribute>(inherit: false);
            methodAuth.Should().NotBeNull($"{controllerType.Name}.{action.Name} must restrict write access");
            methodAuth!.Roles.Should().Be("Manager");
        }
    }
}
