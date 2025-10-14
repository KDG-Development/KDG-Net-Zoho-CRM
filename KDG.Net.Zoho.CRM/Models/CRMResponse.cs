namespace KDG.Zoho.CRM.Models
{
  public struct ReponseInfo
  {
    public int PerPage { get; set; }
    public string NextPageToken { get; set; }
    public int Count { get; set; }
    public string SortBy { get; set; }
    public int Page { get; set; }
    public string PreviousPageToken { get; set; }
    public string PageTokenExpiry { get; set; }
    public string SortOrder { get; set; }
    public bool MoreRecords { get; set; }
  }
  public struct CRMResponse<T>
  {
    public IEnumerable<T> Data { get; set; }
    public ReponseInfo Info { get; set; }
  }
}

