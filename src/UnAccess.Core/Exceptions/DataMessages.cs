namespace UnAccess.Core.Exceptions;

public static class DataMessages
{
    public const string MissingConnectionString =
        "Veritabanı bağlantısı yapılandırılmamış. Bağlantı dizesini kaynak koda yazmayın. " +
        "ConnectionStrings__UnAccess ortam değişkenini veya AppData\\UnAccess\\appsettings.json dosyasını tanımlayın.";
}
