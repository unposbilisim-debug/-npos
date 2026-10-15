namespace UnAccess.Core.Exceptions;

public class UnAccessException : Exception
{
    public UnAccessException(string userMessage)
        : base(userMessage)
    {
        UserMessage = userMessage;
    }

    public UnAccessException(string userMessage, Exception innerException)
        : base(userMessage, innerException)
    {
        UserMessage = userMessage;
    }

    public string UserMessage { get; }
}
