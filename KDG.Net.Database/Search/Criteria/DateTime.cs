using System;

namespace KDG.Zoho.CRM.Search.Criteria
{
    public class DateTimeCriteria : Criteria
    {
        private static string _format { get; } = "yyyy-MM-ddTHH:mm:ss";
        private DateTimeCriteria(string field,string when, string operater)
            : base(field,when,operater) {}
        public static DateTimeCriteria GreaterThan(string field, DateTime when)
        {
            return new DateTimeCriteria(field,$"{when.ToString(_format)}+00:00",GREATER_THAN);
        }
    }
}
