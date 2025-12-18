using KDG.Zoho.CRM.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.Reflection;

namespace KDG.Net.Zoho.CRM.Tests;

public class AccessTokenTests
{
    [Fact]
    public async Task FetchNew_ShouldSetIsRequstingNewTokenToFalse_WhenExceptionOccurs()
    {
        // Arrange
        // Reset the static flag to ensure clean state
        ResetIsRequstingNewToken<TestToken>();
        
        // Use an invalid URI that will cause an exception (e.g., invalid hostname)
        // This will cause a network exception when trying to make the HTTP request
        var invalidUri = new Uri("http://invalid-hostname-that-does-not-exist-12345.com/token");
        var queryParams = new Dictionary<string, string?>
        {
            { "client_id", "test" },
            { "client_secret", "test" },
            { "grant_type", "refresh_token" },
            { "refresh_token", "test" }
        };
        var mockLogger = new Mock<ILogger<ConnectorBase>>();
        // Use a unique generic type to avoid singleton conflicts
        var accessToken = AccessToken<TestToken>.Instance(invalidUri, queryParams, mockLogger.Object);
        
        // Act & Assert
        // Verify that IsRequstingNewToken starts as false
        Assert.False(AccessToken<TestToken>.IsRequstingNewToken);
        
        // Call GetAccessToken which will call FetchNew internally
        // This should throw an exception due to invalid URI (network error)
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await accessToken.GetAccessToken(token => 3600);
        });
        
        // Verify that IsRequstingNewToken is set back to false even after exception
        // This is the critical assertion - the finally block should ensure this
        Assert.False(AccessToken<TestToken>.IsRequstingNewToken);
    }

    private void ResetIsRequstingNewToken<T>()
    {
        // Use reflection to reset IsRequstingNewToken flag
        var property = typeof(AccessToken<T>).GetProperty("IsRequstingNewToken", 
            BindingFlags.Public | BindingFlags.Static);
        if (property != null)
        {
            var setter = property.GetSetMethod(true);
            setter?.Invoke(null, new object[] { false });
        }
    }

    // Simple test token class
    private class TestToken
    {
        public string AccessToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
    }
}

