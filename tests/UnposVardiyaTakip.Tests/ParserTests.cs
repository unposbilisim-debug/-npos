using UnposVardiyaTakip.Core;

namespace UnposVardiyaTakip.Tests;

public class ParserTests
{
    private static string Sample(string name) =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", name));

    [Fact]
    public void Turpak_shift_volumes_and_payments()
    {
        var parsed = TurpakParser.Parse(Sample("turpak_vardiya.xml"));
        Assert.Equal("2026-09-10", parsed.ShiftDate);
        Assert.Equal("2", parsed.ShiftNo);
        Assert.Equal("closed", parsed.Status);
        Assert.Equal(5, parsed.Sales.Count);
        Assert.Equal(189.2, Math.Round(parsed.Sales.Sum(s => s.Volume), 2));
        Assert.Equal(9095.5, Math.Round(parsed.Sales.Sum(s => s.Amount), 2));
        Assert.Equal("100", parsed.Sales[0].PaymentCode);
        Assert.Equal("1", parsed.Meters[0].Nozzle);
    }

    [Fact]
    public void Live_sales_xml_is_open()
    {
        var parsed = TurpakParser.Parse(Sample("Sales.xml"));
        Assert.Equal("open", parsed.Status);
        Assert.Equal(18, parsed.Sales[0].Volume);
    }

    [Fact]
    public void Asis_text_keeps_decimals()
    {
        var parsed = AsisParser.Parse(Sample("asis_vardiya.txt"));
        Assert.Equal("asis", parsed.Brand);
        Assert.Equal("3", parsed.ShiftNo);
        Assert.Equal(40, parsed.Sales[0].Volume);
        Assert.Equal(1237.5, parsed.Sales[1].Amount);
        Assert.Equal("Can", parsed.Sales[2].Attendant);
    }

    [Fact]
    public void Ingest_creates_report()
    {
        AppPaths.Ensure();
        var ingest = new IngestService();
        var result = ingest.Ingest(Sample("turpak_vardiya.xml"), copyProcessed: false);
        Assert.Contains(result.Status, new[] { "ok", "skip" });
        Assert.NotNull(result.Shift);
        var summary = ingest.GetSummary(result.Shift!.Id);
        Assert.NotNull(summary);
        Assert.Equal(5, summary!.Count);
        Assert.Contains(summary.ByPayment, p => p.Name == "Nakit");
        Assert.Contains(summary.ByAttendant, p => p.Name == "Ali");
    }
}
