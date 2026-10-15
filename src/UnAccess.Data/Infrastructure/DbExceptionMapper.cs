using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Exceptions;

namespace UnAccess.Data.Infrastructure;

internal static class DbExceptionMapper
{
    public static Exception Map(DbUpdateException exception, ILogger logger)
    {
        var sql = FindSqlException(exception);
        if (sql is { Number: 2601 or 2627 })
        {
            logger.LogError(exception, "Benzersiz alan çakışması.");
            return new UnAccessException(UniqueMessage(sql.Message), exception);
        }

        if (sql is { Number: 547 } && sql.Message.Contains("CK_PersonnelAccessOverrides_Target", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogError(exception, "Personel istisnası hedef kuralı ihlal edildi.");
            return new UnAccessException("İstisna için bölge veya kapıdan yalnızca biri seçilmelidir.", exception);
        }

        logger.LogError(exception, "Veritabanı işlemi başarısız.");
        return new UnAccessException("Kayıt veritabanına yazılamadı. Teknik ayrıntı günlük dosyasına yazıldı.", exception);
    }

    private static SqlException? FindSqlException(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current is SqlException sql)
                return sql;
        }

        return null;
    }

    private static string UniqueMessage(string sqlMessage)
    {
        if (sqlMessage.Contains("UX_Personnel_PersonnelNumber", StringComparison.OrdinalIgnoreCase))
            return "Bu sicil numarası zaten kayıtlı.";
        if (sqlMessage.Contains("UX_Cards_CardNumber", StringComparison.OrdinalIgnoreCase))
            return "Bu kart numarası zaten kayıtlı.";
        if (sqlMessage.Contains("UX_Departments_Code", StringComparison.OrdinalIgnoreCase))
            return "Bu departman kodu zaten kayıtlı.";
        if (sqlMessage.Contains("UX_JobTitles_Code", StringComparison.OrdinalIgnoreCase))
            return "Bu görev kodu zaten kayıtlı.";
        if (sqlMessage.Contains("UX_Buildings_Code", StringComparison.OrdinalIgnoreCase))
            return "Bu bina kodu zaten kayıtlı.";
        if (sqlMessage.Contains("UX_Controllers_Code", StringComparison.OrdinalIgnoreCase))
            return "Bu kontrolör kodu zaten kayıtlı.";
        if (sqlMessage.Contains("UX_Floors_Building_Code", StringComparison.OrdinalIgnoreCase))
            return "Bu kat kodu aynı binada zaten kayıtlı.";
        if (sqlMessage.Contains("UX_Floors_Building_Number", StringComparison.OrdinalIgnoreCase))
            return "Bu kat numarası aynı binada zaten kayıtlı.";
        if (sqlMessage.Contains("UX_Zones_Floor_Code", StringComparison.OrdinalIgnoreCase))
            return "Bu bölge kodu aynı katta zaten kayıtlı.";
        if (sqlMessage.Contains("UX_Doors_Code", StringComparison.OrdinalIgnoreCase))
            return "Bu kapı kodu zaten kayıtlı.";
        if (sqlMessage.Contains("UX_Doors_Controller_Channel_Active", StringComparison.OrdinalIgnoreCase))
            return "Bu kanal aynı kontrolörde başka bir aktif kapıya atanmış.";
        if (sqlMessage.Contains("UX_AccessGroups_Code", StringComparison.OrdinalIgnoreCase))
            return "Bu yetki grubu kodu zaten kayıtlı.";
        if (sqlMessage.Contains("UX_TimeSchedules_Code", StringComparison.OrdinalIgnoreCase))
            return "Bu zaman planı kodu zaten kayıtlı.";
        if (sqlMessage.Contains("UX_PersonnelAccessGroups_Person_Group", StringComparison.OrdinalIgnoreCase))
            return "Bu personel seçilen yetki grubuna zaten bağlı.";
        if (sqlMessage.Contains("UX_AccessGroupZones_Group_Zone", StringComparison.OrdinalIgnoreCase))
            return "Bu bölge kuralı grupta zaten kayıtlı.";
        if (sqlMessage.Contains("UX_AccessGroupDoors_Group_Door", StringComparison.OrdinalIgnoreCase))
            return "Bu kapı kuralı grupta zaten kayıtlı.";
        if (sqlMessage.Contains("UX_AccessEvents_EventId", StringComparison.OrdinalIgnoreCase))
            return "Bu geçiş olay numarası zaten kayıtlı.";
        if (sqlMessage.Contains("CK_PersonnelAccessOverrides_Target", StringComparison.OrdinalIgnoreCase))
            return "İstisna için bölge veya kapıdan yalnızca biri seçilmelidir.";

        return "Bu kayıt benzersiz alanlarda mevcut bir kayıtla çakışıyor.";
    }
}
