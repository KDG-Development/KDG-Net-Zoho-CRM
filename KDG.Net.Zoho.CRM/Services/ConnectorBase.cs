using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using KDG.Zoho.CRM.Models;
using KDG.Common.Extensions;
using KDG.Connector;

namespace KDG.Zoho.CRM.Services
{
  public abstract class ConnectorBase
  {
    public ConnectorBase(string baseUrl,
                         ILogger<ConnectorBase> logger,
                         Newtonsoft.Json.JsonSerializer serializer,
                         JsonSerializerSettings serializerSettings,
                         int maxRetryAttempts = 10)
    {
      BaseUrl = baseUrl;
      BaseUri = new Uri(baseUrl);
      Logger = logger;
      ConnectorFriendlyName = GetType().Name.Wordify();
      Serializer = serializer;
      MaxRetryAttempts = maxRetryAttempts;
      SerializerSettings = serializerSettings;
    }

    protected readonly Uri BaseUri;
    protected readonly string BaseUrl;
    protected readonly ILogger<ConnectorBase> Logger;
    protected readonly Newtonsoft.Json.JsonSerializer Serializer;
    protected readonly JsonSerializerSettings SerializerSettings;
    protected readonly string ConnectorFriendlyName;
    protected string CommentDivider = "=================================================";
    private const int InitialBackoffSeconds = 2;
    private const int MaxBackoffSeconds = 60;
    private const double BackoffMultiplier = 2.0;
    private static readonly HttpStatusCode[] ServerRetryStatusCodes =
    {
      HttpStatusCode.InternalServerError,
      HttpStatusCode.BadGateway,
      HttpStatusCode.ServiceUnavailable,
      HttpStatusCode.GatewayTimeout
    };

    //https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient.timeout?view=net-8.0
    public const int TimeOutInMinutes = 5; //Default value is 100 seconds (1 min 40 seconds)
    public readonly int MaxRetryAttempts;

    #region Helper methods
    protected abstract Task<AuthenticationHeaderValue> GetAuthenticationHeaderValue();
    protected virtual Dictionary<string, string> GetHeaders(ApiParams config)
    {
      var dict = new Dictionary<string, string>();

      if (config.headers != null && config.headers.Any())
      {
        foreach (var header in config.headers)
        {
          dict.Add(header.Key, header.Value);
        }
      }

      return dict;
    }

    private bool TryGetResponse<RESPONSE>(HttpResponseMessage response, string contents, bool logResponseData, out RESPONSE? responseData)
    {
      var success = false;
      responseData = default(RESPONSE);

      if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
      {
        success = true;
      }
      else if (response.IsSuccessStatusCode)
      {
        success = response.IsSuccessStatusCode;

        if (contents != "")
        {
          var divider = string.Join("", Environment.NewLine, CommentDivider, Environment.NewLine);
          var contentsLogData = logResponseData ? contents : "[Omitted]";

          if (logResponseData)
            Logger.LogInformation("{divider}{connectorFriendlyName} Response: {newLine}{contents}{divider}",
                            divider,
                            ConnectorFriendlyName,
                            Environment.NewLine,
                            contentsLogData,
                            divider);

          responseData = JsonConvert.DeserializeObject<RESPONSE>(contents, SerializerSettings);
        }
      }

      return success;
    }

    protected async Task<int> SendWithRetryResponse<RESPONSE>(HttpClient client, bool logResponseData, Func<Task<HttpRequestMessage>> getRequest)
    {
      var sendAttempt = 0;
      RESPONSE? responseData;
      var successful = false;
      var contents = string.Empty;
      HttpRequestMessage? request = null;
      var requestUri = string.Empty;
      var requestDataDisplayed = false;
      HttpStatusCode statusCode = HttpStatusCode.OK;
      HttpResponseMessage? response = null;

      do
      {
        try
        {
          sendAttempt++;
          request = await getRequest();
          requestUri = request.RequestUri == null ? "" : request.RequestUri.ToString();

          if (!requestDataDisplayed)
          {
            if (request.Content != null)
            {
              var json = await request.Content.ReadAsStringAsync();
              var message = string.Join(Environment.NewLine, CommentDivider, json, CommentDivider);
              Logger.LogInformation(message);
            }

            requestDataDisplayed = true;
          }

          Logger.LogInformation("Sending {method} request to {url}", request.Method.Method, requestUri);

          response = await client.SendAsync(request);
          contents = await response.Content.ReadAsStringAsync();
          statusCode = response.StatusCode;

          if (TryGetResponse<RESPONSE>(response, contents, logResponseData, out responseData))
          {
            successful = true;
            break;
          }
        }
        catch (Exception)
        {
          throw;
        }
        finally
        {
          if (request != null)
          {
            request.Dispose();
          }
        }

        if (response == null)
        {
          break;
        }

        if (!TryGetRetryDelay(sendAttempt, statusCode, response, out var retryDelay))
        {
          var divider = string.Join("", Environment.NewLine, CommentDivider, Environment.NewLine);
          Logger.LogWarning("{divider}Send attempt failed to {requestUri} (HTTP status {statusCode} {statusNumber}). " +
                            "Response is not configured for retries.{newLine}" +
                            "Actual response received: {contents}{divider}",
                            divider, requestUri, statusCode, (int)statusCode, Environment.NewLine, contents, divider);

          var message = string.Join(Environment.NewLine,
                                    $"Unable to complete request to '{requestUri}'. HTTP {statusCode} is not retryable.",
                                    $"Response: {contents}");

          throw new Exception(message);
        }

        var retriesRemaining = MaxRetryAttempts - sendAttempt;
        var dividerForRetry = string.Join("", Environment.NewLine, CommentDivider, Environment.NewLine);

        if (retriesRemaining > 0)
        {
          Logger.LogWarning("{divider}Send attempt failed to {requestUri} (HTTP status {statusCode} {statusNumber}). " +
                            "Will retry in {retrySeconds} seconds. " +
                            "Number of retries remaining: {retryCount}{newLine}" +
                            "Actual response received: {contents}{divider}",
                            dividerForRetry, requestUri, statusCode, (int)statusCode, retryDelay.TotalSeconds, retriesRemaining, Environment.NewLine, contents, dividerForRetry);

          await Task.Delay(retryDelay);
        }
        else
        {
          Logger.LogWarning("{divider}Send attempt failed to {requestUri} (HTTP status {statusCode} {statusNumber}). " +
                            "No retries remaining. Actual response received: {contents}{divider}",
                            dividerForRetry, requestUri, statusCode, (int)statusCode, contents, dividerForRetry);
          break;
        }
      }
      while (sendAttempt < MaxRetryAttempts);

      if (!successful)
      {
        var message = string.Join(Environment.NewLine,
                                  "Unable to successfully complete request. " +
                                  $"Max retry attempts reached for request to '{requestUri}'. Error: {contents}");

        throw new Exceptions.TooManyRetries(message);
      }

      if (responseData != null)
      {
        return sendAttempt;
      }

      throw new Exception("null response");
    }

    private async Task<RESPONSE> SendWithRetries<RESPONSE>(HttpClient client, bool logResponseData, Func<Task<HttpRequestMessage>> getRequest)
    {
      var sendAttempt = 0;
      RESPONSE? responseData;
      var successful = false;
      var contents = string.Empty;
      HttpRequestMessage? request = null;
      var requestUri = string.Empty;
      var requestDataDisplayed = false;
      HttpStatusCode statusCode = HttpStatusCode.OK;
      HttpResponseMessage? response = null;

      do
      {
        try
        {
          sendAttempt++;
          request = await getRequest();
          requestUri = request.RequestUri == null ? "" : request.RequestUri.ToString();

          if (!requestDataDisplayed)
          {
            if (request.Content != null)
            {
              var json = await request.Content.ReadAsStringAsync();
              var message = string.Join(Environment.NewLine, CommentDivider, json, CommentDivider);
              Logger.LogInformation(message);
            }

            requestDataDisplayed = true;
          }

          Logger.LogInformation("Sending {method} request to {url}", request.Method.Method, requestUri);

          response = await client.SendAsync(request);
          contents = await response.Content.ReadAsStringAsync();
          statusCode = response.StatusCode;

          if (statusCode == HttpStatusCode.NotModified)
          {
            successful = true;
            return default(RESPONSE)!;
          }

          if (TryGetResponse<RESPONSE>(response, contents, logResponseData, out responseData))
          {
            successful = true;
            break;
          }
        }
        catch (Exception)
        {
          throw;
        }
        finally
        {
          if (request != null)
          {
            request.Dispose();
          }
        }

        if (response == null)
        {
          break;
        }

        if (!TryGetRetryDelay(sendAttempt, statusCode, response, out var retryDelay))
        {
          var divider = string.Join("", Environment.NewLine, CommentDivider, Environment.NewLine);
          Logger.LogWarning("{divider}Send attempt failed to {requestUri} (HTTP status {statusCode} {statusNumber}). " +
                            "Response is not configured for retries.{newLine}" +
                            "Actual response received: {contents}{divider}",
                            divider, requestUri, statusCode, (int)statusCode, Environment.NewLine, contents, divider);

          var message = string.Join(Environment.NewLine,
                                    $"Unable to complete request to '{requestUri}'. HTTP {statusCode} is not retryable.",
                                    $"Response: {contents}");

          throw new Exception(message);
        }

        var retriesRemaining = MaxRetryAttempts - sendAttempt;
        var dividerForRetry = string.Join("", Environment.NewLine, CommentDivider, Environment.NewLine);

        if (retriesRemaining > 0)
        {
          Logger.LogWarning("{divider}Send attempt failed to {requestUri} (HTTP status {statusCode} {statusNumber}). " +
                            "Will retry in {retrySeconds} seconds. " +
                            "Number of retries remaining: {retryCount}{newLine}" +
                            "Actual response received: {contents}{divider}",
                            dividerForRetry, requestUri, statusCode, (int)statusCode, retryDelay.TotalSeconds, retriesRemaining, Environment.NewLine, contents, dividerForRetry);

          await Task.Delay(retryDelay);
        }
        else
        {
          Logger.LogWarning("{divider}Send attempt failed to {requestUri} (HTTP status {statusCode} {statusNumber}). " +
                            "No retries remaining. Actual response received: {contents}{divider}",
                            dividerForRetry, requestUri, statusCode, (int)statusCode, contents, dividerForRetry);
          break;
        }
      }
      while (sendAttempt < MaxRetryAttempts);

      if (!successful)
      {
        var message = string.Join(Environment.NewLine,
                                  "Unable to successfully complete request. " +
                                  $"Max retry attempts reached for request to '{requestUri}'. Error: {contents}");

        throw new Exceptions.TooManyRetries(message);
      }

      if (responseData != null)
      {
        return responseData;
      }

      throw new Exception("null response");
    }

    private static bool IsRetryableServerError(HttpStatusCode statusCode)
    {
      return Array.IndexOf(ServerRetryStatusCodes, statusCode) >= 0;
    }

    private static bool TryGetRetryDelay(int attempt, HttpStatusCode statusCode, HttpResponseMessage response, out TimeSpan delay)
    {
      delay = GetExponentialDelay(attempt);

      if (statusCode == HttpStatusCode.TooManyRequests)
      {
        var retryAfterDelay = GetRetryAfterDelay(response);
        if (retryAfterDelay > delay)
        {
          delay = retryAfterDelay;
        }

        return true;
      }

      if (IsRetryableServerError(statusCode))
      {
        return true;
      }

      delay = TimeSpan.Zero;
      return false;
    }

    private static TimeSpan GetRetryAfterDelay(HttpResponseMessage response)
    {
      var retryAfter = response.Headers.RetryAfter;

      if (retryAfter == null)
      {
        return TimeSpan.Zero;
      }

      if (retryAfter.Delta.HasValue)
      {
        return retryAfter.Delta.Value > TimeSpan.Zero ? retryAfter.Delta.Value : TimeSpan.Zero;
      }

      if (retryAfter.Date.HasValue)
      {
        var delta = retryAfter.Date.Value - DateTimeOffset.UtcNow;
        return delta > TimeSpan.Zero ? delta : TimeSpan.Zero;
      }

      return TimeSpan.Zero;
    }

    private static TimeSpan GetExponentialDelay(int attempt)
    {
      var normalizedAttempt = Math.Max(1, attempt);
      var multiplier = Math.Pow(BackoffMultiplier, normalizedAttempt - 1);
      var delaySeconds = Math.Min(InitialBackoffSeconds * multiplier, MaxBackoffSeconds);
      return TimeSpan.FromSeconds(delaySeconds);
    }

    protected string GetUrl(string pathToAppend, string? baseUrlOverride, bool addSurroundingSlashes = false)
    {
      var url = string.IsNullOrEmpty(baseUrlOverride) ? BaseUrl : baseUrlOverride;
      var uri = new Uri(url);

      if (!string.IsNullOrEmpty(pathToAppend))
      {
        if (addSurroundingSlashes)
        {
          if (!pathToAppend.StartsWith("/"))
          {
            pathToAppend = "/" + pathToAppend;
          }
          if (!pathToAppend.EndsWith("/"))
          {
            pathToAppend = pathToAppend + "/";
          }
        }

        if (!Uri.TryCreate(uri, pathToAppend, out var result))
        {
          throw new Exception($"'{pathToAppend}' is not a valid relative path. Please review the path and try again");
        }

        url = result.ToString();
      }

      return url;
    }

    protected async Task<HttpRequestMessage> GetRequest(HttpMethod method, Uri uri, ApiParams config)
    {
      var request = new HttpRequestMessage(method, uri);
      var headers = GetHeaders(config);
      request.Headers.Authorization = await GetAuthenticationHeaderValue();

      // Apply custom headers
      foreach (var header in headers)
      {
        request.Headers.Add(header.Key, header.Value);
      }

      if ((method == HttpMethod.Post || method == HttpMethod.Patch || method == HttpMethod.Put) &&
          config.postParams != null)
      {
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(config.postParams, SerializerSettings);

        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
      }

      return request;
    }
    #endregion

    public Uri GenerateUri(Uri path, Dictionary<string,string?> QueryParams)
    {
      string parameterText = String.Empty;
      if (QueryParams != null)
      {
        var parameters = QueryParams.Select(x => string.Join("=", x.Key, System.Web.HttpUtility.UrlEncode(x.Value)));
        parameterText = string.Join('&', parameters);
      }
      var uri = path.ToString();
      return new Uri(String.Join('?', uri, parameterText));
    }

    protected virtual async Task<RESPONSE> Send<RESPONSE>(HttpMethod method, string path, ApiParams config, bool logResponseData = true, string? baseUrlOverride = null)
    {
      using (var client = new HttpClient())
      {
        client.Timeout = TimeSpan.FromMinutes(TimeOutInMinutes);
        var url = GetUrl(path, baseUrlOverride);
        var uri = config.urlParams == null ?
                  new Uri(url) :
                  GenerateUri(new Uri(url), config.urlParams);

        return await SendWithRetries<RESPONSE>(client, logResponseData, async () => await GetRequest(method, uri, config));
      }
    }
  }
}
