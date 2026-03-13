using KDG.Zoho.CRM.Services;
using KDG.Zoho.CRM.Models;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Newtonsoft.Json;

namespace KDG.Net.Zoho.CRM.Tests;

public class CRMConnectorTests
{
    private class TestCRMConnector : CRMConnector
    {
        public TestCRMConnector(CRMConfig config, ILogger<ConnectorBase> logger, JsonSerializer serializer, JsonSerializerSettings serializerSettings, IClock clock)
            : base(config, logger, serializer, serializerSettings, clock)
        {
        }

        public long PublicCalculateExpiresAt(int expiresInSeconds, long nowUnix)
        {
            return CalculateExpiresAt(expiresInSeconds, nowUnix);
        }
    }

    [Fact]
    public void CalculateExpiresAt_ShouldSubtract60SecondsBuffer()
    {
        // Arrange
        var config = new CRMConfig
        {
            ApiVersion = "v6",
            ClientId = "test-id",
            ClientSecret = "test-secret",
            RefreshToken = "test-token",
            Scope = new[] { "ZohoCRM.modules.ALL" }
        };
        var mockLogger = new Mock<ILogger<ConnectorBase>>();
        var mockClock = new Mock<IClock>();
        var serializer = new JsonSerializer();
        var serializerSettings = new JsonSerializerSettings();
        
        var connector = new TestCRMConnector(config, mockLogger.Object, serializer, serializerSettings, mockClock.Object);
        
        int expiresIn = 3600; // 1 hour
        long now = 1710240000; // Example unix timestamp
        
        // Act
        var result = connector.PublicCalculateExpiresAt(expiresIn, now);
        
        // Assert
        // Expected: now + expiresIn - 60
        long expected = now + expiresIn - 60;
        Assert.Equal(expected, result);
    }
}
