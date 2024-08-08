namespace KDG.Zoho.CRM.Utilities
{
  public static class QueryHelpers
  {
    public static Uri GenerateUri(string baseUrl, Dictionary<string, string?> queryParams)
    {
      var parameters = queryParams.Select(x => string.Join("=", x.Key, System.Web.HttpUtility.UrlEncode(x.Value)));
      var parameterText = string.Join('&', parameters);
      return new Uri($"{baseUrl}?{parameterText}");
    }
  }
}
