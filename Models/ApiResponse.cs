namespace KDG.Zoho.CRM.Models
{
    public interface IApiResponse<T> {
        IEnumerable<T> data { get; }
        ApiResponseInfo info { get; }
    }

    public struct ApiResponse<T> : IApiResponse<T>
    {
        public IEnumerable<T> data { get; set; }
        public ApiResponseInfo info { get; set; }
    }

    public struct EmailApiResponse<T> : IApiResponse<T>
    {
        public readonly IEnumerable<T> data => Emails;
        public IEnumerable<T> Emails { get; set; }
        public ApiResponseInfo info { get; set; }
    }

    public struct UsersApiResponse<T> : IApiResponse<T>
    {
        public readonly IEnumerable<T> data => Users;
        public IEnumerable<T> Users { get; set; }
        public ApiResponseInfo info { get; set; }

    }
}
