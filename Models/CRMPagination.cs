using Newtonsoft.Json;

namespace KDG.Zoho.CRM.Models
{
  public class PaginatedResponse<T>
  {
    public IEnumerable<T> Results = new List<T>{};
    public ApiResponseInfo Pagination { get; set; } = new();
  }

  public class PaginatedApiResponse<T>
  {
    public IEnumerable<T> Results = new List<T>{};
    public int TotalCount { get; set; }
  }

  public class RecordCount
  {
    [JsonProperty("count")]
    public int Count { get; set; }
  }
  
  public class RelatedRecordCount : RecordCount
  {
    [JsonProperty("related_list")]
    public RelatedList RelatedList { get; set; } = new();
  }

  public class RelatedList 
  {
    [JsonProperty("api_name")]
    public string ApiName { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
  }

  public class RelatedRecordCountResponse
  {
    [JsonProperty("get_related_records_count")]
    public IEnumerable<RelatedRecordCount> GetRelatedRecordsCount { get; set; } = new List<RelatedRecordCount>();
  }
}