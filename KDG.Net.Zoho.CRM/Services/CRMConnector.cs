using Newtonsoft.Json;
using KDG.Zoho.CRM.Models;
using Microsoft.Extensions.Logging;
using NodaTime;
using System.Formats.Tar;
using System.Net;

namespace KDG.Zoho.CRM.Services
{
  public class CRMConnector : ConnectorBase
  {

    private readonly CRMConfig _config;

    public CRMConnector(CRMConfig config, ILogger<ConnectorBase> logger, Newtonsoft.Json.JsonSerializer serializer, JsonSerializerSettings serializerSettings, IClock clock, int maxRetryAttempts = 10)
    : base($"https://www.zohoapis.com/crm/{config.ApiVersion}/", logger, serializer, serializerSettings, maxRetryAttempts)
    {
      _config = config;
      _clock = clock;
    }

    private KDG.Zoho.CRM.Models.ZohoAccessToken? _currentToken;
    private long? _tokenExpiration;
    private IClock _clock;
    private string _tokenUri = "https://accounts.zoho.com/oauth/v2/token";
    private string _userModule = "users";

    /// <summary>
    /// Formats a DateTime for use in If-Modified-Since header according to HTTP RFC 7232 requirements
    /// </summary>
    /// <param name="dateTime">The date and time to format</param>
    /// <returns>RFC 1123 formatted date string</returns>
    private string FormatModifiedSinceDate(DateTime dateTime)
    {
      // Convert to UTC if not already, then format as RFC 1123
      var utcDateTime = dateTime.Kind == DateTimeKind.Utc ? dateTime : dateTime.ToUniversalTime();
      return utcDateTime.ToString("R"); // RFC 1123 format
    }

    /// <summary>
    /// Creates headers dictionary with X-Modified-Since header if date is provided
    /// </summary>
    /// <param name="modifiedSince">Optional date for X-Modified-Since header</param>
    /// <returns>Headers dictionary or null if no date provided</returns>
    private Dictionary<string, string>? CreateModifiedSinceHeaders(DateTime? modifiedSince)
    {
      return modifiedSince.HasValue ?
        new Dictionary<string, string> { ["If-Modified-Since"] = FormatModifiedSinceDate(modifiedSince.Value) } :
        null;
    }

    private AccessToken<KDG.Zoho.CRM.Models.ZohoAccessToken> AccessTokenGenerator()
    {
      var config = _config;
      var token = new AccessToken<KDG.Zoho.CRM.Models.ZohoAccessToken>(
        new Uri(_tokenUri),
        new Dictionary<string, string?>()
        {
          [LabelHelpers.RefreshTokenLabel] = config.RefreshToken,
          [LabelHelpers.ClientId] = config.ClientId,
          [LabelHelpers.ClientSecret] = config.ClientSecret,
          // Assuming Scopes is defined somewhere in the context
          ["scope"] = String.Join(",", config.Scope),

          [LabelHelpers.GrantTypeLabel] = LabelHelpers.RefreshTokenLabel,
        }
      );

      return token;
    }

    protected async Task<string> GetAccessToken()
    {
      var now = _clock.GetCurrentInstant().ToUnixTimeSeconds();
      if(_currentToken == null || _tokenExpiration < now)
      {
        var gen = AccessTokenGenerator();
        var token = await gen.getAccessToken();
        if (!string.IsNullOrEmpty(token?.AccessToken)){
          _tokenExpiration = now + (token.ExpiresIn / 2);
          _currentToken = token;
        }
      }
      return _currentToken?.AccessToken ?? string.Empty;
    }

    protected override async Task<System.Net.Http.Headers.AuthenticationHeaderValue> GetAuthenticationHeaderValue()
    {
      var token = await GetAccessToken();

      return new System.Net.Http.Headers.AuthenticationHeaderValue("Zoho-oauthtoken", token);
    }

    // Max number of results per call is 200. Keeps getting more until we have all records.
    // Uses page_token for pagination to support up to 100k records.
    async Task<IEnumerable<T>> GetAll<T, TResponse>(string path, ApiParams config)
      where TResponse : IApiResponse<T>
    {
      var hasMore = true;
      var results = new List<T>();
      const int PER_PAGE = 200;
      string? pageToken = null;

      if (config.urlParams == null){
        config.urlParams = [];
      }

      // Set per_page parameter
      if (!config.urlParams.ContainsKey("per_page")){
        config.urlParams.Add("per_page", PER_PAGE.ToString());
      }

      while (hasMore){
        // Use page_token for pagination instead of page number
        if (pageToken != null){
          if (!config.urlParams.ContainsKey("page_token")){
            config.urlParams.Add("page_token", pageToken);
          } else {
            config.urlParams["page_token"] = pageToken;
          }
        } else {
          // Remove page_token if it exists for first request
          config.urlParams.Remove("page_token");
        }

        var response = await Send<TResponse>(HttpMethod.Get, path, config);
        if (response.data?.Any() ?? false){
          results.AddRange(response.data);
        }

        // Check if there are more records and get next page token
        // There was an issue where more_records was true, but next_page_token was null, causing an endless loop.
        // Make sure there's a next page token to fetch more records.
        hasMore = response.info.more_records && !string.IsNullOrEmpty(response.info.next_page_token);
        pageToken = response.info.next_page_token;
      }
      return results;
    }

    async Task<RecordCount> GetRecordCount(string module)
    {
      var config = new ApiParams()
      {
        urlParams = new Dictionary<string, string?>()
      };
      var response = await Send<RecordCount>(HttpMethod.Post, $"{module}/actions/count", config);
      return response;
    }

    /// <summary>
    /// Gets the module image for a specific record
    /// </summary>
    /// <param name="module">Module name (e.g., "Contacts", "Leads")</param>
    /// <param name="id">Record ID</param>
    /// <returns>Base64 encoded PNG image string, or null if no image exists</returns>
    public async Task<string?> GetModuleImage(string module, string id)
    {
      using (var client = new HttpClient())
      {
        client.Timeout = TimeSpan.FromMinutes(TimeOutInMinutes);
        var url = GetUrl($"{module}/{id}/photo", null);
        var uri = new Uri(url);

        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = await GetAuthenticationHeaderValue();

        var response = await client.SendAsync(request);
        
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
          return null;
        }

        if (response.IsSuccessStatusCode)
        {
          var bytes = await response.Content.ReadAsByteArrayAsync();
          if (bytes.Length == 0)
          {
            return null;
          }

          // Convert to base64
          var base64String = Convert.ToBase64String(bytes);
          return base64String;
        }

        return null;
      }
    }

    

    async Task<RelatedRecordCountResponse> GetRelatedRecordCount(string module, string id, string relatedListApiName)
    {
      var config = new ApiParams()
      {
        urlParams = new Dictionary<string, string?>(),
        postParams = new Dictionary<string, object?>()
        {
          ["get_related_records_count"] = new List<Dictionary<string, object?>>()
          {
            new Dictionary<string, object?>()
            {
              ["related_list"] = new Dictionary<string, object?>()
              {
                ["api_name"] = relatedListApiName
              }
            }
          }
        }
      };
      var response = await Send<RelatedRecordCountResponse>(HttpMethod.Post, $"{module}/{id}/actions/get_related_records_count", config);
      return response;
    }
    async Task<PaginatedResponse<T>> GetPaginated<T, TResponse>(string path, ApiParams config, int page, int perPage)
      where TResponse : IApiResponse<T>
    {
      var results = new List<T>();
      if (config.urlParams == null){
        config.urlParams = [];
      }
      if (!config.urlParams.ContainsKey("per_page")){
        config.urlParams.Add("per_page", perPage.ToString());
      }

      if (!config.urlParams.ContainsKey("page")){
        config.urlParams.Add("page", page.ToString());
      } else {
        config.urlParams["page"] = page.ToString();
      }

      var response = await Send<TResponse>(HttpMethod.Get, path, config);
      if (response.data?.Any() ?? false){
        results.AddRange(response.data);
      }

      return new PaginatedResponse<T> {
        Results = results,
        Pagination = response.info
      };
    }

    /// <summary>
    /// Gets all records from a module with specified fields
    /// </summary>
    /// <typeparam name="T">Type to deserialize records to</typeparam>
    /// <param name="module">Module name (e.g., "Contacts", "Leads")</param>
    /// <param name="fields">Fields to retrieve</param>
    /// <returns>List of records</returns>
    public async Task<List<T>> GetRecords<T>(string module, IEnumerable<string> fields)
    {
      return await GetRecords<T>(module, fields, null);
    }

    /// <summary>
    /// Gets all records from a module with specified fields, optionally filtering by modification date
    /// </summary>
    /// <typeparam name="T">Type to deserialize records to</typeparam>
    /// <param name="module">Module name (e.g., "Contacts", "Leads")</param>
    /// <param name="fields">Fields to retrieve</param>
    /// <param name="modifiedSince">Optional date to filter records modified since this date</param>
    /// <returns>List of records</returns>
    public async Task<List<T>> GetRecords<T>(string module, IEnumerable<string> fields, DateTime? modifiedSince)
    {
      var config = new ApiParams()
      {
        urlParams = new Dictionary<string, string?>()
        {
          ["fields"] = String.Join(",", fields)
        },
        headers = CreateModifiedSinceHeaders(modifiedSince)
      };

      return (await GetAll<T, ApiResponse<T>>(module, config)).ToList();
    }

    /// <summary>
    /// Gets paginated records from a module with specified fields
    /// </summary>
    /// <typeparam name="T">Type to deserialize records to</typeparam>
    /// <param name="module">Module name (e.g., "Contacts", "Leads")</param>
    /// <param name="fields">Fields to retrieve</param>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="perPage">Number of records per page</param>
    /// <returns>Paginated response with records and total count</returns>
    public async Task<PaginatedApiResponse<T>> GetRecordsPaginated<T>(string module, IEnumerable<string> fields, int page, int perPage)
    {
      return await GetRecordsPaginated<T>(module, fields, page, perPage, null);
    }

    /// <summary>
    /// Gets paginated records from a module with specified fields, optionally filtering by modification date
    /// </summary>
    /// <typeparam name="T">Type to deserialize records to</typeparam>
    /// <param name="module">Module name (e.g., "Contacts", "Leads")</param>
    /// <param name="fields">Fields to retrieve</param>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="perPage">Number of records per page</param>
    /// <param name="modifiedSince">Optional date to filter records modified since this date</param>
    /// <returns>Paginated response with records and total count</returns>
    public async Task<PaginatedApiResponse<T>> GetRecordsPaginated<T>(string module, IEnumerable<string> fields, int page, int perPage, DateTime? modifiedSince)
    {
      var config = new ApiParams()
      {
        urlParams = new Dictionary<string, string?>()
        {
          ["fields"] = String.Join(",", fields)
        },
        headers = CreateModifiedSinceHeaders(modifiedSince)
      };

      var results = await GetPaginated<T, ApiResponse<T>>(module, config, page, perPage);
      var count = await GetRecordCount(module);
      return new PaginatedApiResponse<T>
      {
        Results = results.Results,
        TotalCount = count.Count
      };
    }

    /// <summary>
    /// Gets paginated related records with specified fields
    /// </summary>
    /// <typeparam name="T">Type to deserialize records to</typeparam>
    /// <param name="module">Parent module name</param>
    /// <param name="id">Parent record ID</param>
    /// <param name="relatedListApiName">Related list API name</param>
    /// <param name="fields">Fields to retrieve</param>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="perPage">Number of records per page</param>
    /// <returns>Paginated response with related records and total count</returns>
    public async Task<PaginatedApiResponse<T>> GetRelatedRecordsPaginated<T>(string module, string id, string relatedListApiName, IEnumerable<string> fields, int page, int perPage)
    {
      return await GetRelatedRecordsPaginated<T>(module, id, relatedListApiName, fields, page, perPage, null);
    }

    /// <summary>
    /// Gets paginated related records with specified fields, optionally filtering by modification date
    /// </summary>
    /// <typeparam name="T">Type to deserialize records to</typeparam>
    /// <param name="module">Parent module name</param>
    /// <param name="id">Parent record ID</param>
    /// <param name="relatedListApiName">Related list API name</param>
    /// <param name="fields">Fields to retrieve</param>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="perPage">Number of records per page</param>
    /// <param name="modifiedSince">Optional date to filter records modified since this date</param>
    /// <returns>Paginated response with related records and total count</returns>
    public async Task<PaginatedApiResponse<T>> GetRelatedRecordsPaginated<T>(string module, string id, string relatedListApiName, IEnumerable<string> fields, int page, int perPage, DateTime? modifiedSince)
    {
      var config = new ApiParams()
      {
        urlParams = new Dictionary<string, string?>()
        {
          ["fields"] = String.Join(",", fields)
        },
        headers = CreateModifiedSinceHeaders(modifiedSince)
      };
      var results = await GetPaginated<T, ApiResponse<T>>($"{module}/{id}/{relatedListApiName}", config, page, perPage);
      var count = await GetRelatedRecordCount(module, id, relatedListApiName);
      return new PaginatedApiResponse<T>
      {
        Results = results.Results,
        TotalCount = count.GetRelatedRecordsCount.First().Count
      };
    }

    /// <summary>
    /// Gets a single record by ID with specified fields
    /// </summary>
    /// <typeparam name="T">Type to deserialize record to</typeparam>
    /// <param name="module">Module name (e.g., "Contacts", "Leads")</param>
    /// <param name="id">Record ID</param>
    /// <param name="fields">Fields to retrieve</param>
    /// <returns>Record or null if not found</returns>
    public async Task<T?> GetRecord<T>(string module, string id, IEnumerable<string> fields)
    {
      return await GetRecord<T>(module, id, fields, null);
    }

    /// <summary>
    /// Gets a single record by ID with specified fields, optionally filtering by modification date
    /// </summary>
    /// <typeparam name="T">Type to deserialize record to</typeparam>
    /// <param name="module">Module name (e.g., "Contacts", "Leads")</param>
    /// <param name="id">Record ID</param>
    /// <param name="fields">Fields to retrieve</param>
    /// <param name="modifiedSince">Optional date to filter record modified since this date</param>
    /// <returns>Record or null if not found or not modified since specified date</returns>
    public async Task<T?> GetRecord<T>(string module, string id, IEnumerable<string> fields, DateTime? modifiedSince)
    {
      var config = new ApiParams()
      {
        urlParams = new Dictionary<string, string?>()
        {
          ["fields"] = String.Join(",", fields)
        },
        headers = CreateModifiedSinceHeaders(modifiedSince)
      };
      var response = await Send<ApiResponse<T>>(HttpMethod.Get, $"{module}/{id}", config);

      return response.data.FirstOrDefault();
    }

    /// <summary>
    /// Gets all related records with specified fields
    /// </summary>
    /// <typeparam name="T">Type to deserialize records to</typeparam>
    /// <param name="module">Parent module name</param>
    /// <param name="id">Parent record ID</param>
    /// <param name="relatedListApiName">Related list API name</param>
    /// <param name="fields">Fields to retrieve</param>
    /// <returns>List of related records</returns>
    public async Task<IEnumerable<T>> GetRelatedRecords<T>(string module, string id, string relatedListApiName, IEnumerable<string> fields)
    {
      return await GetRelatedRecords<T>(module, id, relatedListApiName, fields, null);
    }

    /// <summary>
    /// Gets all related records with specified fields, optionally filtering by modification date
    /// </summary>
    /// <typeparam name="T">Type to deserialize records to</typeparam>
    /// <param name="module">Parent module name</param>
    /// <param name="id">Parent record ID</param>
    /// <param name="relatedListApiName">Related list API name</param>
    /// <param name="fields">Fields to retrieve</param>
    /// <param name="modifiedSince">Optional date to filter records modified since this date</param>
    /// <returns>List of related records</returns>
    public async Task<IEnumerable<T>> GetRelatedRecords<T>(string module, string id, string relatedListApiName, IEnumerable<string> fields, DateTime? modifiedSince)
    {
      var config = new ApiParams()
      {
        urlParams = new Dictionary<string, string?>()
        {
          ["fields"] = String.Join(",", fields)
        },
        headers = CreateModifiedSinceHeaders(modifiedSince)
      };
      return await GetAll<T, ApiResponse<T>>($"{module}/{id}/{relatedListApiName}", config);
    }

    public async Task<T?> GetUserRecord<T>(string id, IEnumerable<string> fields)
    {
      var config = new ApiParams()
      {
        urlParams = new Dictionary<string, string?>()
        {
          ["ids"] = id,
          ["fields"] = String.Join(",", fields)
        }
      };
      var response = await Send<UsersApiResponse<T>>(HttpMethod.Get, $"{_userModule}", config);

      return response.Users.FirstOrDefault();
    }

    public async Task<IEnumerable<T>> GetUserRecords<T>(IEnumerable<string> ids, IEnumerable<string> fields)
    {
      Dictionary<string, string?> userParams = new Dictionary<string, string?>(){};
      if(ids.Count() > 0)
      {
        userParams.Add("ids", String.Join(",",ids));
      }
      if(fields.Count() > 0)
      {
        userParams.Add("fields", String.Join(",",fields));
      }
      var config = new ApiParams()
      {
        urlParams = userParams
      };

      return await GetAll<T, UsersApiResponse<T>>($"{_userModule}", config);
    }

    public async Task<List<T>> GetEmailRecords<T>(string module)
    {
      var config = new ApiParams(){};
      return (await GetAll<T, EmailApiResponse<T>>(module, config)).ToList();
    }

    public async Task<CreateResponse<O>> CreateRecord<T,O>(string module, T data, List<Enums.Triggers> triggers)
    {
        var config = new ApiParams()
        {
            postParams = new CreateRequest<T>(data,triggers)
        };
        var response = await Send<Response<CreateResponse<O>>>(HttpMethod.Post, module, config);
        return response.Data.First();
    }

    public async Task<CreateResponse<O>> UpdateRecord<T,O>(string module, T data, List<Enums.Triggers> triggers)
    {
        var config = new ApiParams()
        {
            postParams = new CreateRequest<T>(data,triggers)
        };
        var response = await Send<Response<CreateResponse<O>>>(HttpMethod.Put, module, config);
        return response.Data.First();
    }
    public async Task<IEnumerable<CreateResponse<O>>> UpsertRecords<T,O>(string module, IEnumerable<T> data, IEnumerable<string> duplicateCheckFields, List<Enums.Triggers> triggers)
    {
        var config = new ApiParams()
        {
            postParams = new UpsertRequest<T>(data, duplicateCheckFields, triggers)
        };
        var response = await Send<Response<CreateResponse<O>>>(HttpMethod.Post, module+"/upsert", config);
        return response.Data;
    }
    /// <summary>
    /// Searches for records using criteria and optionally filters by modification date
    /// </summary>
    /// <typeparam name="T">Type to deserialize records to</typeparam>
    /// <param name="search">Search parameters including module, criteria, and optional modified since date</param>
    /// <returns>API response with matching records</returns>
    public Task<ApiResponse<T>> Search<T>(SearchParams search)
    {
        var config = new ApiParams()
        {
            urlParams = new Dictionary<string, string?>()
            {
                ["criteria"] = String.Join("and",search.Criterias.Select((v) => v.GetCriteriaValue())),
            },
            headers = CreateModifiedSinceHeaders(search.ModifiedSince)
        };
        return Send<ApiResponse<T>>(HttpMethod.Get, $"{search.Module}/search", config);
    }
    public async Task<List<CreateResponse<DeletedRecord>>> DeleteRecords(string module, IEnumerable<string> ids){
      if (ids.Count() > 100){
        throw new InvalidOperationException("API can only delete up to 100 records in a single call.");
      }
      var config = new ApiParams()
      {
        urlParams = new Dictionary<string, string?>()
        {
          ["ids"] = string.Join(',', ids),
        }
      };
      var response = await Send<Response<CreateResponse<DeletedRecord>>>(HttpMethod.Delete, module, config);
      return response.Data;
    }
  }
}