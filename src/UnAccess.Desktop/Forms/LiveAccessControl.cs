using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Exceptions;
using UnAccess.Desktop.Ui;

namespace UnAccess.Desktop.Forms;

internal sealed class LiveAccessControl : ScreenControl
{
    private readonly DataGridView _grid = Grids.Create();

    public LiveAccessControl(IServiceScopeFactory scopes, ILogger<LiveAccessControl> logger)
        : base(scopes, logger)
    {
        _grid.Columns.Add(Grids.Text(nameof(Row.Time), "Saat", 140));
        _grid.Columns.Add(Grids.Text(nameof(Row.Personnel), "Personel", 140));
        _grid.Columns.Add(Grids.Text(nameof(Row.Card), "Kart", 110));
        _grid.Columns.Add(Grids.Text(nameof(Row.Door), "Kapı", 140));
        _grid.Columns.Add(Grids.Text(nameof(Row.Zone), "Bölge", 120));
        _grid.Columns.Add(Grids.Text(nameof(Row.Result), "Sonuç", 110));
        _grid.Columns.Add(Grids.Text(nameof(Row.Reason), "Sebep", 260));
        _grid.CellFormatting += (_, args) =>
        {
            if (args.RowIndex < 0 || _grid.Columns[args.ColumnIndex].Name != nameof(Row.Result))
                return;
            if (_grid.Rows[args.RowIndex].DataBoundItem is not Row row)
                return;
            args.CellStyle!.ForeColor = row.IsGranted ? Color.DarkGreen : Color.DarkRed;
        };

        var refresh = ListButtons.Command("Yenile");
        refresh.Click += async (_, _) => await ReloadAsync();
        Controls.Add(_grid);
        Controls.Add(ListButtons.Bar(refresh));
        Load += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        IReadOnlyList<UnAccess.Core.Models.AccessEventListItem>? rows = null;
        var ok = await RunAsync(async services =>
        {
            var service = services.GetService<IAccessEventQueryService>()
                ?? throw new UnAccessException(DataMessages.MissingConnectionString);
            rows = await service.GetRecentAsync(100, Cancellation);
        });
        if (!ok || rows == null || IsDisposed)
            return;

        _grid.DataSource = rows.Select(row => new Row
        {
            Time = row.TimeText,
            Personnel = row.PersonnelName,
            Card = row.CardNumber,
            Door = row.DoorName,
            Zone = row.ZoneName,
            Result = row.ResultText,
            Reason = row.Reason,
            IsGranted = row.IsGranted
        }).ToList();
        SetStatus($"Son {rows.Count} geçiş");
    }

    private sealed class Row
    {
        public string Time { get; init; } = "";
        public string Personnel { get; init; } = "";
        public string Card { get; init; } = "";
        public string Door { get; init; } = "";
        public string Zone { get; init; } = "";
        public string Result { get; init; } = "";
        public string Reason { get; init; } = "";
        public bool IsGranted { get; init; }
    }
}
