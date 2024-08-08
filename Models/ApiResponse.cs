namespace KDG.Zoho.CRM.Models
{
    public struct ApiResponse<T>
    {
        public IEnumerable<T> data { get; set; }
        public ApiResponseInfo info { get; set; }
    }
}
