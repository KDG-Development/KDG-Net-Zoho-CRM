using KDG.Zoho.CRM.Enums;

namespace KDG.Zoho.CRM.Models
{
    public class CreateRequest<T>
    {
        public CreateRequest(T requestData,List<Enums.Triggers> triggers)
        {
            data = new List<T> { requestData };
            trigger = triggers.Select((v) => v.Value).ToList();
        }
        public IEnumerable<T> data;
        public List<string> trigger;
    }
}
