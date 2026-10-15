namespace UnAccess.Core.Enums;

public static class CardStatusText
{
    public static string ToTurkish(this CardStatus status) => status switch
    {
        CardStatus.Active => "Aktif",
        CardStatus.Lost => "Kayıp",
        CardStatus.Cancelled => "İptal",
        CardStatus.Expired => "Süresi Dolmuş",
        _ => status.ToString()
    };
}
