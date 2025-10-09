namespace KDG.Zoho.CRM.Models
{
  public class CRMConfig
  {
    // Add your properties here
    public string ApiVersion { get; set; }
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
    public string RefreshToken { get; set; }
    public IEnumerable<string> Scope { get; set; }
    public CRMConfig()
    {
      ApiVersion = string.Empty;
      ClientId = string.Empty;
      ClientSecret = string.Empty;
      RefreshToken = string.Empty;
      Scope = new List<string>();
    }
  }
}
