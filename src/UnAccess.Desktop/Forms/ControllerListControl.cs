using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Desktop.Ui;

namespace UnAccess.Desktop.Forms;

internal sealed class ControllerListControl : ScreenControl
{
    private readonly DataGridView _grid = Grids.Create();

    public ControllerListControl(IServiceScopeFactory scopes, ILogger<ControllerListControl> logger)
        : base(scopes, logger)
    {
        _grid.Columns.Add(Grids.Text(nameof(Row.Name), "Ad", 160));
        _grid.Columns.Add(Grids.Text(nameof(Row.Model), "Model", 100));
        _grid.Columns.Add(Grids.Text(nameof(Row.IpAddress), "IP", 110));
        _grid.Columns.Add(Grids.Text(nameof(Row.Port), "Port", 60));
        _grid.Columns.Add(Grids.Text(nameof(Row.SerialNumber), "Seri No", 110));
        _grid.Columns.Add(Grids.Text(nameof(Row.Status), "Durum", 70));
        _grid.Columns.Add(Grids.Text(nameof(Row.LastSeen), "Son Görülme", 120));
        Controls.Add(_grid);
        Load += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        IReadOnlyList<ControllerListItem>? rows = null;
        var ok = await RunAsync(async services =>
        {
            var service = services.GetService<IControllerQueryService>()
                ?? throw new UnAccessException(DataMessages.MissingConnectionString);
            rows = await service.ListAsync(Cancellation);
        });
        if (!ok || rows == null || IsDisposed)
            return;

        _grid.DataSource = rows.Select(row => new Row
        {
            Name = row.Name,
            Model = row.Model,
            IpAddress = row.IpAddress,
            Port = row.Port.ToString(),
            SerialNumber = row.SerialNumber ?? "",
            Status = Catalog.ActiveText(row.IsActive),
            LastSeen = row.LastSeenAtUtc?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? ""
        }).ToList();
        SetStatus($"{rows.Count} kayıt");
    }

    private sealed class Row
    {
        public string Name { get; init; } = "";
        public string Model { get; init; } = "";
        public string IpAddress { get; init; } = "";
        public string Port { get; init; } = "";
        public string SerialNumber { get; init; } = "";
        public string Status { get; init; } = "";
        public string LastSeen { get; init; } = "";
    }
}
