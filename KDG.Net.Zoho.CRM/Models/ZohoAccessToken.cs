namespace SMSI.Models.Zoho
{
  public class ZohoAccessToken
  {
    [System.Text.Json.Serialization.JsonPropertyName("access_token")]
    public string AccessToken { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("api_domain")]
    public string ApiDomain { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("token_type")]
    public string TokenType { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
    public ZohoAccessToken()
    {
      AccessToken = "";
      ApiDomain = "";
      TokenType = "";
      ExpiresIn = 0;
    }
  }
}
