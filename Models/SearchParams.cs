namespace KDG.Zoho.CRM.Models
{
    public class SearchParams
    {
        public string Module { get; set; }
        public List<KDG.Zoho.CRM.Search.Criteria.Criteria> Criterias { get; set; }

        public SearchParams(string module, List<KDG.Zoho.CRM.Search.Criteria.Criteria> criterias)
        {
            Module = module;
            Criterias = criterias;
        }
    }
}
