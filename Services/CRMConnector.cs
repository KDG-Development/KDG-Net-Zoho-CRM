using Newtonsoft.Json;
using KDG.Zoho.CRM.Models;
using Microsoft.Extensions.Logging;
using NodaTime;
using KDG.Zoho.CRM.Search.Criteria;

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
        _tokenExpiration = now + (token.ExpiresIn / 2);
        _currentToken = token;
      }

      return _currentToken.AccessToken;
    }

    protected override async Task<System.Net.Http.Headers.AuthenticationHeaderValue> GetAuthenticationHeaderValue()
    {
      var token = await GetAccessToken();

      return new System.Net.Http.Headers.AuthenticationHeaderValue("Zoho-oauthtoken", token);
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
      var response = await Send<ApiResponse<T>>(HttpMethod.Get, module, config);

      return response.data.ToList();
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
  }
}