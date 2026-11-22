using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Controllers;
using FUD.DTOs.Pricing;
using FUD.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FUD.Tests.Pricing;

public class PricingControllerAuthTests
{
    private readonly Mock<IPricingService> _pricingServiceMock = new();

    private PricingController CreateControllerWithUser(ClaimsPrincipal user)
    {
        var controller = new PricingController(_pricingServiceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };
        return controller;
    }

    [Fact]
    public async Task SavePricing_WhenUserIdClaimMissing_Returns401UnauthorizedAndNeverCallsService()
    {
        // Arrange
        var principalWithoutId = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, "Manager")
        }, "TestAuth"));
        var controller = CreateControllerWithUser(principalWithoutId);

        var request = new SavePricingRequestDto
        {
            TermId = 1
        };

        // Act
        var result = await controller.SavePricing(request);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
        _pricingServiceMock.Verify(
            s => s.SavePricingAsync(It.IsAny<int>(), It.IsAny<SavePricingRequestDto>()),
            Times.Never);
    }

    [Fact]
    public async Task SaveUtilityPrices_WhenUserIdClaimMissing_Returns401UnauthorizedAndNeverCallsService()
    {
        // Arrange
        var principalWithoutId = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, "Manager")
        }, "TestAuth"));
        var controller = CreateControllerWithUser(principalWithoutId);

        var request = new SaveUtilityPricesRequestDto
        {
            TermId = 1
        };

        // Act
        var result = await controller.SaveUtilityPrices(request);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
        _pricingServiceMock.Verify(
            s => s.SaveUtilityPricesAsync(It.IsAny<int>(), It.IsAny<SaveUtilityPricesRequestDto>()),
            Times.Never);
    }
}
