namespace KDG.Zoho.CRM.Models
{
    public struct ApiResponseInfo
    {
        public int per_page { get; set; }
        public string? next_page_token { get; set; }
        public int count { get; set; }
        public string sort_by { get; set; }
        public int page { get; set; }
        public string? previous_page_token { get; set; }
        public string? page_token_expiry { get; set; }
        public string sort_order { get; set; }
        public bool more_records { get; set; }
    }
}
