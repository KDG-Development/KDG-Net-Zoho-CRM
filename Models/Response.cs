namespace KDG.Zoho.CRM.Models
{
    public class Response<T>
    {
        public List<T> Data { get; set; } = new List<T>();
    }
}

