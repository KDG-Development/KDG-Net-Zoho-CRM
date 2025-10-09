namespace KDG.Zoho.CRM.Exceptions
{
  [Serializable]
  class TooManyRetries : Exception
  {
    public TooManyRetries() { }
    public TooManyRetries(string message) : base(message) {
    
    }
  }
}
