using UnposVardiyaTakip.Core;

namespace UnposVardiyaTakip.Win;

public sealed class ShiftDetailForm : Form
{
    private readonly ShiftSummary _summary;

    public ShiftDetailForm(long shiftId)
    {
        var loaded = new IngestService().GetSummary(shiftId);
        _summary = loaded ?? throw new InvalidOperationException("Vardiya bulunamadı.");
        Text = $"Vardiya {_summary.Shift.ShiftNo}  ·  {_summary.Shift.ShiftDate}";
        Width = 1100;
        Height = 740;
        StartPosition = FormStartPosition.CenterParent;
        Theme.Apply(this);

        var top = new Panel { Dock = DockStyle.Top, Height = 92, Padding = new Padding(16) };
        top.Controls.Add(new Label
        {
            Text = $"{_summary.Shift.StationName}  ·  V{_summary.Shift.ShiftNo}  ·  {(_summary.Shift.Status == "open" ? "Açık" : "Kapalı")}",
            Font = new Font("Segoe UI Semibold", 14F),
            ForeColor = Theme.Gold,
            AutoSize = true,
            Location = new Point(16, 8),
        });
        top.Controls.Add(new Label
        {
            Text = $"Satış {_summary.Count}   ·   {FieldHelper.Liter(_summary.TotalVolume)} L   ·   {FieldHelper.Money(_summary.TotalAmount)} ₺   ·   Nakit {FieldHelper.Money(_summary.CashAmount)} ₺",
            ForeColor = Theme.Muted,
            AutoSize = true,
            Location = new Point(16, 42),
        });

        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(16, 0, 16, 8) };
        var excel = Theme.GoldButton("Excel indir");
        var yazdir = Theme.GhostButton("Yazdır");
        excel.Click += (_, _) =>
        {
            var path = ExcelExport.Export(_summary);
            MessageBox.Show(this, $"Kaydedildi:\n{path}", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); } catch { }
        };
        yazdir.Click += (_, _) => Print();
        actions.Controls.Add(excel);
        actions.Controls.Add(yazdir);

        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 8) };
        tabs.TabPages.Add(MakeGroupTab("Yakıt", _summary.ByFuel));
        tabs.TabPages.Add(MakeGroupTab("Ödeme", _summary.ByPayment));
        tabs.TabPages.Add(MakeGroupTab("Pompacı", _summary.ByAttendant));
        tabs.TabPages.Add(MakeMeterTab());
        tabs.TabPages.Add(MakeSalesTab());

        Controls.Add(tabs);
        Controls.Add(actions);
        Controls.Add(top);
    }

    private TabPage MakeGroupTab(string title, IEnumerable<GroupTotal> items)
    {
        var page = new TabPage(title) { BackColor = Theme.Bg };
        var grid = Theme.Grid();
        grid.Columns.Add("Ad", title);
        grid.Columns.Add("Litre", "Litre");
        grid.Columns.Add("Tutar", "Tutar");
        grid.Columns.Add("Adet", "Adet");
        foreach (var item in items)
            grid.Rows.Add(item.Name, FieldHelper.Liter(item.Volume), FieldHelper.Money(item.Amount) + " ₺", item.Count);
        page.Controls.Add(grid);
        return page;
    }

    private TabPage MakeMeterTab()
    {
        var page = new TabPage("Tabanca") { BackColor = Theme.Bg };
        var grid = Theme.Grid();
        grid.Columns.Add("Pompa", "Pompa");
        grid.Columns.Add("Tabanca", "Tabanca");
        grid.Columns.Add("Yakit", "Yakıt");
        grid.Columns.Add("Acilis", "Açılış");
        grid.Columns.Add("Kapanis", "Kapanış");
        grid.Columns.Add("Satis", "Satış");
        grid.Columns.Add("Fark", "Fark");
        foreach (var meter in _summary.Meters)
            grid.Rows.Add(meter.Pump, meter.Nozzle, meter.Fuel, FieldHelper.Liter(meter.Opening), FieldHelper.Liter(meter.Closing), FieldHelper.Liter(meter.SalesVolume), FieldHelper.Liter(meter.Variance));
        page.Controls.Add(grid);
        return page;
    }

    private TabPage MakeSalesTab()
    {
        var page = new TabPage("Satışlar") { BackColor = Theme.Bg };
        var grid = Theme.Grid();
        foreach (var h in new[] { "#", "Saat", "Pompa", "Tabanca", "Yakıt", "Litre", "Tutar", "Pompacı", "Ödeme", "Plaka" })
            grid.Columns.Add(h, h);
        foreach (var sale in _summary.Sales)
            grid.Rows.Add(sale.Seq, sale.SoldAt, sale.Pump, sale.Nozzle, sale.Fuel, FieldHelper.Liter(sale.Volume), FieldHelper.Money(sale.Amount), sale.Attendant, sale.PaymentName, sale.Plate);
        page.Controls.Add(grid);
        return page;
    }

    private void Print()
    {
        var doc = new System.Drawing.Printing.PrintDocument();
        doc.PrintPage += (_, e) =>
        {
            var y = 40;
            using var font = new Font("Segoe UI", 11);
            using var bold = new Font("Segoe UI Semibold", 14);
            e.Graphics!.DrawString($"ünpos Vardiya takip  ·  V{_summary.Shift.ShiftNo}  {_summary.Shift.ShiftDate}", bold, Brushes.Black, 40, y);
            y += 36;
            e.Graphics.DrawString($"{_summary.Shift.StationName}  ·  {FieldHelper.Liter(_summary.TotalVolume)} L  ·  {FieldHelper.Money(_summary.TotalAmount)} ₺", font, Brushes.Black, 40, y);
            y += 28;
            foreach (var item in _summary.ByFuel)
            {
                e.Graphics.DrawString($"{item.Name}: {FieldHelper.Liter(item.Volume)} L / {FieldHelper.Money(item.Amount)} ₺", font, Brushes.Black, 40, y);
                y += 22;
            }
        };
        using var dlg = new PrintPreviewDialog { Document = doc, Width = 800, Height = 600 };
        dlg.ShowDialog(this);
    }
}
