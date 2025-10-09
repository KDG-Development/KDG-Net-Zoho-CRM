namespace KDG.Zoho.CRM.Models
{
    public class SearchParams
    {
        public string Module { get; set; }
        public List<KDG.Zoho.CRM.Search.Criteria.Criteria> Criterias { get; set; }

        /// <summary>
        /// Optional date to filter records modified since this date using X-Modified-Since header
        /// </summary>
        public DateTime? ModifiedSince { get; set; }

        public SearchParams(string module, List<KDG.Zoho.CRM.Search.Criteria.Criteria> criterias)
        {
            Module = module;
            Criterias = criterias;
        }

        public SearchParams(string module, List<KDG.Zoho.CRM.Search.Criteria.Criteria> criterias, DateTime? modifiedSince)
        {
            Module = module;
            Criterias = criterias;
            ModifiedSince = modifiedSince;
        }
    }
}
