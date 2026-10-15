namespace UnAccess.Devices;

public class AccessControllerException : Exception
{
    public AccessControllerException(string message)
        : base(message)
    {
    }

    public AccessControllerException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
