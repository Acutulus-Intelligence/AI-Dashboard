namespace Application.Common.Exceptions
{
    public class NoCompanyException : Exception
    {
        public NoCompanyException(string message = "You are not a member of any company.")
            : base(message)
        {
        }
    }
}
