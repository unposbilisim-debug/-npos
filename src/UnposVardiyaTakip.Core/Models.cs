namespace UnposVardiyaTakip.Core;

public sealed class ParsedSale
{
    public int Seq { get; set; }
    public string SoldAt { get; set; } = "";
    public string Pump { get; set; } = "";
    public string Nozzle { get; set; } = "";
    public string Fuel { get; set; } = "";
    public double Volume { get; set; }
    public double Amount { get; set; }
    public double UnitPrice { get; set; }
    public string Attendant { get; set; } = "";
    public string PaymentCode { get; set; } = "";
    public string Plate { get; set; } = "";
    public string Customer { get; set; } = "";
}

public sealed class ParsedMeter
{
    public string Pump { get; set; } = "";
    public string Nozzle { get; set; } = "";
    public string Fuel { get; set; } = "";
    public double Opening { get; set; }
    public double Closing { get; set; }
}

public sealed class ParsedShift
{
    public string StationName { get; set; } = "";
    public string ShiftDate { get; set; } = "";
    public string ShiftNo { get; set; } = "1";
    public string Brand { get; set; } = "turpak";
    public string Status { get; set; } = "closed";
    public string? OpenedAt { get; set; }
    public string? ClosedAt { get; set; }
    public string SourceName { get; set; } = "";
    public List<ParsedSale> Sales { get; } = [];
    public List<ParsedMeter> Meters { get; } = [];
}

public sealed class ShiftRow
{
    public long Id { get; set; }
    public string StationName { get; set; } = "";
    public string ShiftDate { get; set; } = "";
    public string ShiftNo { get; set; } = "";
    public string Brand { get; set; } = "";
    public string Status { get; set; } = "";
    public string SourceFile { get; set; } = "";
    public string SourceHash { get; set; } = "";
    public string? OpenedAt { get; set; }
    public string? ClosedAt { get; set; }
    public int Count { get; set; }
    public double TotalVolume { get; set; }
    public double TotalAmount { get; set; }
    public double CashAmount { get; set; }
}

public sealed class SaleRow
{
    public int Seq { get; set; }
    public string SoldAt { get; set; } = "";
    public string Pump { get; set; } = "";
    public string Nozzle { get; set; } = "";
    public string Fuel { get; set; } = "";
    public double Volume { get; set; }
    public double Amount { get; set; }
    public double UnitPrice { get; set; }
    public string Attendant { get; set; } = "";
    public string PaymentCode { get; set; } = "";
    public string PaymentName { get; set; } = "";
    public string Plate { get; set; } = "";
    public string Customer { get; set; } = "";
}

public sealed class MeterRow
{
    public string Pump { get; set; } = "";
    public string Nozzle { get; set; } = "";
    public string Fuel { get; set; } = "";
    public double Opening { get; set; }
    public double Closing { get; set; }
    public double SalesVolume { get; set; }
    public double Variance { get; set; }
}

public sealed class IngestLogRow
{
    public string Path { get; set; } = "";
    public string Status { get; set; } = "";
    public string Message { get; set; } = "";
    public string CreatedAt { get; set; } = "";
}

public sealed class GroupTotal
{
    public string Name { get; set; } = "";
    public double Volume { get; set; }
    public double Amount { get; set; }
    public int Count { get; set; }
}

public sealed class ShiftSummary
{
    public ShiftRow Shift { get; set; } = new();
    public int Count { get; set; }
    public double TotalVolume { get; set; }
    public double TotalAmount { get; set; }
    public double CashAmount { get; set; }
    public List<GroupTotal> ByFuel { get; set; } = [];
    public List<GroupTotal> ByAttendant { get; set; } = [];
    public List<GroupTotal> ByPayment { get; set; } = [];
    public List<MeterRow> Meters { get; set; } = [];
    public List<SaleRow> Sales { get; set; } = [];
}
