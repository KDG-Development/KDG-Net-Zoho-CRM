namespace KDG.Zoho.CRM.Search.Criteria
{
    public class Criteria
    {
        protected static string GREATER_THAN = "greater_than";
        private string _field { get; }
        private string _seperator { get; } = ":";
        private string _value { get; }
        private string _operator { get; }

        protected Criteria(string field, string value,string operation)
        {
            _field = field;
            _value = value;
            _operator = operation;
        }

        public string GetCriteriaValue()
        {
            return $"{_field}{_seperator}{_operator}{_seperator}{_value}";
        }
    }
}
