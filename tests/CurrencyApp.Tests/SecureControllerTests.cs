using System.Security.Claims;
using CurrencyApp.Api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CurrencyApp.Tests
{
    public class SecureControllerTests
    {
        [Fact]
        public void Ping_ShouldReturnAuthenticatedUserName_WhenIdentityContainsName()
        {
            var controller = new SecureController();

            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.Name, "admin")
                    },
                    authenticationType: "TestAuth"));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            var result = controller.Ping();

            var ok = Assert.IsType<OkObjectResult>(result);
            var payload = ok.Value!;
            var payloadType = payload.GetType();

            Assert.Equal("authenticated", payloadType.GetProperty("status")!.GetValue(payload)?.ToString());
            Assert.Equal("admin", payloadType.GetProperty("user")!.GetValue(payload)?.ToString());
            Assert.Equal("CurrencyApp.Api", payloadType.GetProperty("application")!.GetValue(payload)?.ToString());
            Assert.IsType<DateTime>(payloadType.GetProperty("timestampUtc")!.GetValue(payload)!);
        }

        [Fact]
        public void Ping_ShouldReturnUnknown_WhenIdentityNameIsMissing()
        {
            var controller = new SecureController();

            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity());

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            var result = controller.Ping();

            var ok = Assert.IsType<OkObjectResult>(result);
            var payload = ok.Value!;
            var payloadType = payload.GetType();

            Assert.Equal("authenticated", payloadType.GetProperty("status")!.GetValue(payload)?.ToString());
            Assert.Equal("unknown", payloadType.GetProperty("user")!.GetValue(payload)?.ToString());
            Assert.Equal("CurrencyApp.Api", payloadType.GetProperty("application")!.GetValue(payload)?.ToString());
            Assert.IsType<DateTime>(payloadType.GetProperty("timestampUtc")!.GetValue(payload)!);
        }

        [Fact]
        public void Ping_ShouldReturnUnknown_WhenIdentityIsNull()
        {
            var controller = new SecureController();

            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(); // žádná identity => Identity bude null

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            var result = controller.Ping();

            var ok = Assert.IsType<OkObjectResult>(result);
            var payload = ok.Value!;
            var payloadType = payload.GetType();

            Assert.Equal("authenticated", payloadType.GetProperty("status")!.GetValue(payload)?.ToString());
            Assert.Equal("unknown", payloadType.GetProperty("user")!.GetValue(payload)?.ToString());
            Assert.Equal("CurrencyApp.Api", payloadType.GetProperty("application")!.GetValue(payload)?.ToString());
            Assert.IsType<DateTime>(payloadType.GetProperty("timestampUtc")!.GetValue(payload)!);
        }
    }
}