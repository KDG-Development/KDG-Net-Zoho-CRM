using KDG.Zoho.CRM.Enums;
using Newtonsoft.Json;

namespace KDG.Zoho.CRM.Models
{
    public class UpsertRequest<T>
    {
        public UpsertRequest(IEnumerable<T> requestData, IEnumerable<string> duplicateCheckFields, List<Enums.Triggers> triggers)
        {
            data = requestData;
            DuplicateCheckFields = duplicateCheckFields;
            trigger = triggers.Select((v) => v.Value).ToList();
        }
        public IEnumerable<T> data;
        [JsonProperty("duplicate_check_fields")]
        public IEnumerable<string> DuplicateCheckFields { get; set; }
        public List<string> trigger;
    }
}
