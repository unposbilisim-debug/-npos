using ClosedXML.Excel;

namespace UnposVardiyaTakip.Core;

public static class ExcelExport
{
    public static string Export(ShiftSummary summary)
    {
        AppPaths.Ensure();
        var path = Path.Combine(AppPaths.ExportsDir, $"vardiya_{summary.Shift.ShiftDate}_v{summary.Shift.ShiftNo}.xlsx");
        using var wb = new XLWorkbook();
        var icmal = wb.AddWorksheet("İcmal");
        icmal.Cell(1, 1).Value = "ünpos Vardiya takip";
        icmal.Cell(2, 1).Value = "İstasyon"; icmal.Cell(2, 2).Value = summary.Shift.StationName;
        icmal.Cell(3, 1).Value = "Tarih"; icmal.Cell(3, 2).Value = summary.Shift.ShiftDate;
        icmal.Cell(4, 1).Value = "Vardiya"; icmal.Cell(4, 2).Value = summary.Shift.ShiftNo;
        icmal.Cell(5, 1).Value = "Kaynak"; icmal.Cell(5, 2).Value = Path.GetFileName(summary.Shift.SourceFile);
        icmal.Cell(6, 1).Value = "Satış adedi"; icmal.Cell(6, 2).Value = summary.Count;
        icmal.Cell(7, 1).Value = "Toplam litre"; icmal.Cell(7, 2).Value = Math.Round(summary.TotalVolume, 3);
        icmal.Cell(8, 1).Value = "Toplam tutar"; icmal.Cell(8, 2).Value = Math.Round(summary.TotalAmount, 2);
        icmal.Cell(10, 1).Value = "Yakıt"; icmal.Cell(10, 2).Value = "Litre"; icmal.Cell(10, 3).Value = "Tutar"; icmal.Cell(10, 4).Value = "Adet";
        var row = 11;
        foreach (var item in summary.ByFuel)
        {
            icmal.Cell(row, 1).Value = item.Name;
            icmal.Cell(row, 2).Value = Math.Round(item.Volume, 3);
            icmal.Cell(row, 3).Value = Math.Round(item.Amount, 2);
            icmal.Cell(row, 4).Value = item.Count;
            row++;
        }

        WriteGroup(wb.AddWorksheet("Pompacı"), "Pompacı", summary.ByAttendant);
        WriteGroup(wb.AddWorksheet("Ödeme"), "Ödeme tipi", summary.ByPayment);

        var tabanca = wb.AddWorksheet("Tabanca");
        tabanca.Cell(1, 1).Value = "Pompa";
        tabanca.Cell(1, 2).Value = "Tabanca";
        tabanca.Cell(1, 3).Value = "Yakıt";
        tabanca.Cell(1, 4).Value = "Açılış";
        tabanca.Cell(1, 5).Value = "Kapanış";
        tabanca.Cell(1, 6).Value = "Satış litre";
        tabanca.Cell(1, 7).Value = "Fark";
        row = 2;
        foreach (var meter in summary.Meters)
        {
            tabanca.Cell(row, 1).Value = meter.Pump;
            tabanca.Cell(row, 2).Value = meter.Nozzle;
            tabanca.Cell(row, 3).Value = meter.Fuel;
            tabanca.Cell(row, 4).Value = meter.Opening;
            tabanca.Cell(row, 5).Value = meter.Closing;
            tabanca.Cell(row, 6).Value = meter.SalesVolume;
            tabanca.Cell(row, 7).Value = meter.Variance;
            row++;
        }

        var detay = wb.AddWorksheet("Satışlar");
        string[] headers = ["Sıra", "Saat", "Pompa", "Tabanca", "Yakıt", "Litre", "Tutar", "Birim", "Pompacı", "Ödeme", "Plaka", "Cari"];
        for (var i = 0; i < headers.Length; i++)
        {
            detay.Cell(1, i + 1).Value = headers[i];
            detay.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("0F2744");
            detay.Cell(1, i + 1).Style.Font.FontColor = XLColor.FromHtml("F5B942");
            detay.Cell(1, i + 1).Style.Font.Bold = true;
        }
        row = 2;
        foreach (var sale in summary.Sales)
        {
            detay.Cell(row, 1).Value = sale.Seq;
            detay.Cell(row, 2).Value = sale.SoldAt;
            detay.Cell(row, 3).Value = sale.Pump;
            detay.Cell(row, 4).Value = sale.Nozzle;
            detay.Cell(row, 5).Value = sale.Fuel;
            detay.Cell(row, 6).Value = Math.Round(sale.Volume, 3);
            detay.Cell(row, 7).Value = Math.Round(sale.Amount, 2);
            detay.Cell(row, 8).Value = Math.Round(sale.UnitPrice, 4);
            detay.Cell(row, 9).Value = sale.Attendant;
            detay.Cell(row, 10).Value = sale.PaymentName;
            detay.Cell(row, 11).Value = sale.Plate;
            detay.Cell(row, 12).Value = sale.Customer;
            row++;
        }

        wb.SaveAs(path);
        return path;
    }

    private static void WriteGroup(IXLWorksheet sheet, string title, IEnumerable<GroupTotal> items)
    {
        sheet.Cell(1, 1).Value = title;
        sheet.Cell(1, 2).Value = "Litre";
        sheet.Cell(1, 3).Value = "Tutar";
        sheet.Cell(1, 4).Value = "Adet";
        var row = 2;
        foreach (var item in items)
        {
            sheet.Cell(row, 1).Value = item.Name;
            sheet.Cell(row, 2).Value = Math.Round(item.Volume, 3);
            sheet.Cell(row, 3).Value = Math.Round(item.Amount, 2);
            sheet.Cell(row, 4).Value = item.Count;
            row++;
        }
    }
}
