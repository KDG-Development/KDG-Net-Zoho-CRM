namespace KDG.Zoho.CRM.Models
{
    public class CreateResponse<T>
    {
        public string Status { get; set; } = string.Empty;
        public T? Details { get; set; } = default;
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}

