namespace Application.Common.Exceptions
{
    public class LockedOutException : Exception
    {
        public string Code { get; }

        public LockedOutException(string message = "Account is temporarily locked. Please try again later.", string code = "account_locked")
            : base(message)
        {
            Code = code;
        }
    }
}
