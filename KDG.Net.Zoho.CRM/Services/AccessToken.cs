using Microsoft.Extensions.Logging;

namespace KDG.Zoho.CRM.Services
{
    public class AccessToken<A>
    {
      private readonly ILogger<ConnectorBase> _logger;
      private static AccessToken<A>? _instance;
      public static AccessToken<A> Instance(Uri url, Dictionary<string, string?> queryParams, ILogger<ConnectorBase> logger)
      {
        return _instance ??= new AccessToken<A>(url, queryParams, logger);
      }

      public A? CurrentToken { get; private set; }
      public long? ExpiresAt { get; private set; }

      public static bool IsRequstingNewToken { get; private set; }

      private Uri Uri { get; }
      private Dictionary<string, string?> QueryParams { get; }
      private AccessToken(Uri url, Dictionary<string, string?> queryParams, ILogger<ConnectorBase> logger) // Constructor
      {
        if (_instance != null)
        {
          throw new Exception("AccessToken already initialized");
        }
        Uri = url;
        QueryParams = queryParams;
        _logger = logger;
      }

      private async Task<A> FetchNew()
      {
        if (IsRequstingNewToken)
        {
          throw new Exception("AccessToken is already being requested");
        }
        IsRequstingNewToken = true;
        try{
          using var client = new HttpClient();
          var uri = KDG.Zoho.CRM.Utilities.QueryHelpers.GenerateUri(Uri.ToString(), QueryParams);
          var response = await client.PostAsync(uri, null);
          var contents = await response.Content.ReadAsStringAsync();
          _logger.LogInformation("Content: {contents}", contents);
          var token = System.Text.Json.JsonSerializer.Deserialize<A>(contents);
          if(token == null)
          {
            throw new Exception("Cannot fetch new Access Token");
          }
          IsRequstingNewToken = false;
          return token;
        }
        catch (Exception e) {
          _logger.LogError(e, "Error fetching new Access Token");
          throw;
        } finally {
          IsRequstingNewToken = false;
        }
      }

      public async Task<A> GetAccessToken(Func<A, long> getExpiresAt)
      {
        // If the token is already being requested, wait for it to complete
        if (IsRequstingNewToken)
        {
          var timer = new System.Diagnostics.Stopwatch();
          timer.Start();
          while (IsRequstingNewToken){
            await Task.Delay(100);
            if (timer.Elapsed.TotalSeconds > 10)
            {
              throw new Exception("Timeout waiting for new Access Token");
            }
          }
          timer.Stop();
        } else {
          CurrentToken = await FetchNew();
          ExpiresAt = getExpiresAt(CurrentToken);
        }
        return CurrentToken!;
      }
    }
}