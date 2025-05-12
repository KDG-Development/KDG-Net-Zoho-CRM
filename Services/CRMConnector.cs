using Newtonsoft.Json;
using KDG.Zoho.CRM.Models;
using Microsoft.Extensions.Logging;
using NodaTime;
using System.Formats.Tar;

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
    async Task<IEnumerable<T>> GetAll<T, TResponse>(string path, ApiParams config)
      where TResponse : IApiResponse<T>
    {
      var hasMore = true;
      var results = new List<T>();
      const int PER_PAGE = 200;
      var page = 0;
      if (config.urlParams == null){
        config.urlParams = [];
      }
      while (hasMore){
        if (!config.urlParams.ContainsKey("per_page")){
          config.urlParams.Add("per_page", PER_PAGE.ToString());
        }
        page++;
        if (!config.urlParams.ContainsKey("page")){
          config.urlParams.Add("page", page.ToString());
        } else {
          config.urlParams["page"] = page.ToString();
        }
        
        var response = await Send<TResponse>(HttpMethod.Get, path, config);
        if (response.data?.Any() ?? false){
          results.AddRange(response.data);
        }
        hasMore = response.info.more_records;
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

    public async Task<List<T>> GetRecords<T>(string module, IEnumerable<string> fields)
    {
      var config = new ApiParams()
      {
        urlParams = new Dictionary<string, string?>()
        {
          ["fields"] = String.Join(",", fields)
        }
      };

      return (await GetAll<T, ApiResponse<T>>(module, config)).ToList();
    }

    public async Task<PaginatedApiResponse<T>> GetRecordsPaginated<T>(string module, IEnumerable<string> fields, int page, int perPage)
    {
      var config = new ApiParams()
      {
        urlParams = new Dictionary<string, string?>()
        {
          ["fields"] = String.Join(",", fields)
        }
      };

      var results = await GetPaginated<T, ApiResponse<T>>(module, config, page, perPage);
      var count = await GetRecordCount(module);
      return new PaginatedApiResponse<T>
      {
        Results = results.Results,
        TotalCount = count.Count
      };
    }

    public async Task<PaginatedApiResponse<T>> GetRelatedRecordsPaginated<T>(string module, string id, string relatedListApiName, IEnumerable<string> fields, int page, int perPage)
    {
      var config = new ApiParams()
      {
        urlParams = new Dictionary<string, string?>()
        {
          ["fields"] = String.Join(",", fields)
        }
      };
      var results = await GetPaginated<T, ApiResponse<T>>($"{module}/{id}/{relatedListApiName}", config, page, perPage);
      var count = await GetRelatedRecordCount(module, id, relatedListApiName);
      return new PaginatedApiResponse<T>
      {
        Results = results.Results,
        TotalCount = count.GetRelatedRecordsCount.First().Count
      };
    }

    public async Task<T?> GetRecord<T>(string module, string id, IEnumerable<string> fields)
    {
      var config = new ApiParams()
      {
        urlParams = new Dictionary<string, string?>()
        {
          ["fields"] = String.Join(",", fields)
        }
      };
      var response = await Send<ApiResponse<T>>(HttpMethod.Get, $"{module}/{id}", config);

      return response.data.FirstOrDefault();
    }

    public async Task<IEnumerable<T>> GetRelatedRecords<T>(string module, string id, string relatedListApiName, IEnumerable<string> fields)
    {
      var config = new ApiParams()
      {
        urlParams = new Dictionary<string, string?>()
        {
          ["fields"] = String.Join(",", fields)
        }
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
    public Task<ApiResponse<T>> Search<T>(SearchParams search)
    {
        var config = new ApiParams()
        {
            urlParams = new Dictionary<string, string?>()
            {
                ["criteria"] = String.Join("and",search.Criterias.Select((v) => v.GetCriteriaValue())),
            }
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