namespace Application.Common.Exceptions
{
    public class EmailDeliveryException : Exception
    {
        public string Code { get; }

        public EmailDeliveryException(string message, string code = "email_delivery_failed")
            : base(message)
        {
            Code = code;
        }
    }
}
