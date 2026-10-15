using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Desktop.Ui;

namespace UnAccess.Desktop.Forms;

internal sealed class UserListControl : ScreenControl
{
    private readonly DataGridView _grid = Grids.Create();
    private List<UserListItem> _rows = [];

    public UserListControl(IServiceScopeFactory scopes, ILogger<UserListControl> logger)
        : base(scopes, logger)
    {
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40 };
        bar.Controls.Add(Button("Yeni Kullanıcı", async () => await EditAsync(null)));
        bar.Controls.Add(Button("Düzenle", async () => await EditAsync(Selected())));
        bar.Controls.Add(Button("Şifre Sıfırla", ResetAsync));
        bar.Controls.Add(Button("Aktif/Pasif", ToggleAsync));
        bar.Controls.Add(Button("Yenile", ReloadAsync));
        _grid.Columns.Add(Grids.Text(nameof(Row.Username), "Kullanıcı Adı", 120));
        _grid.Columns.Add(Grids.Text(nameof(Row.FullName), "Ad Soyad", 160));
        _grid.Columns.Add(Grids.Text(nameof(Row.Roles), "Roller", 180));
        _grid.Columns.Add(Grids.Text(nameof(Row.Status), "Durum", 70));
        _grid.Columns.Add(Grids.Text(nameof(Row.LastLogin), "Son Giriş", 120));
        Controls.Add(_grid);
        Controls.Add(bar);
        Load += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        IReadOnlyList<UserListItem>? rows = null;
        var ok = await RunAsync(async services =>
        {
            rows = await services.GetRequiredService<IUserAdminService>().ListAsync(Cancellation);
        });
        if (!ok || rows == null || IsDisposed)
            return;

        _rows = rows.ToList();
        _grid.DataSource = _rows.Select(row => new Row
        {
            Id = row.Id,
            Username = row.Username,
            FullName = row.FullName,
            Roles = row.Roles,
            Status = row.IsActive ? "Aktif" : "Pasif",
            LastLogin = row.LastLoginAtUtc?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? ""
        }).ToList();
        SetStatus($"{_rows.Count} kullanıcı");
    }

    private async Task EditAsync(UserListItem? existing)
    {
        IReadOnlyList<RoleChoice>? roles = null;
        var loaded = await RunAsync(async services =>
        {
            roles = await services.GetRequiredService<IUserAdminService>().ListRolesAsync(Cancellation);
        });
        if (!loaded || roles == null)
            return;

        using var form = new UserEditForm(existing, roles);
        if (form.ShowDialog(this) != DialogResult.OK || form.Model == null)
            return;

        var ok = await RunAsync(async services =>
        {
            var admin = services.GetRequiredService<IUserAdminService>();
            if (existing == null)
                await admin.CreateAsync(form.Model, Cancellation);
            else
                await admin.UpdateAsync(existing.Id, form.Model, Cancellation);
        });
        if (ok)
            await ReloadAsync();
    }

    private async Task ResetAsync()
    {
        var user = Selected();
        if (user == null)
            return;

        using var form = new PasswordResetForm();
        if (form.ShowDialog(this) != DialogResult.OK || string.IsNullOrEmpty(form.TemporaryPassword))
            return;

        var ok = await RunAsync(async services =>
        {
            await services.GetRequiredService<IUserAdminService>().ResetPasswordAsync(user.Id, form.TemporaryPassword, Cancellation);
        });
        if (ok)
        {
            SetStatus("Şifre sıfırlandı. Kullanıcı bir sonraki girişte şifresini değiştirmeli.");
            await ReloadAsync();
        }
    }

    private async Task ToggleAsync()
    {
        var user = Selected();
        if (user == null)
            return;

        var ok = await RunAsync(async services =>
        {
            await services.GetRequiredService<IUserAdminService>().SetActiveAsync(user.Id, !user.IsActive, Cancellation);
        });
        if (ok)
            await ReloadAsync();
    }

    private UserListItem? Selected()
    {
        if (_grid.CurrentRow?.DataBoundItem is not Row row)
            return null;
        return _rows.FirstOrDefault(item => item.Id == row.Id);
    }

    private static Button Button(string text, Func<Task> action)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 28, Margin = new Padding(0, 4, 8, 0) };
        button.Click += async (_, _) => await action();
        return button;
    }

    private sealed class Row
    {
        public int Id { get; init; }
        public string Username { get; init; } = "";
        public string FullName { get; init; } = "";
        public string Roles { get; init; } = "";
        public string Status { get; init; } = "";
        public string LastLogin { get; init; } = "";
    }
}

internal sealed class UserEditForm : Form
{
    private readonly TextBox _username = new() { MaxLength = 64 };
    private readonly TextBox _fullName = new() { MaxLength = 150 };
    private readonly TextBox _password = new() { UseSystemPasswordChar = true, MaxLength = 128 };
    private readonly CheckBox _active = new() { Text = "Aktif", Checked = true };
    private readonly CheckedListBox _roles = new() { Height = 120, CheckOnClick = true };

    public UserEditForm(UserListItem? existing, IReadOnlyList<RoleChoice> roles)
    {
        FormChrome.Apply(this, existing == null ? "Yeni Kullanıcı" : "Kullanıcı", new Size(460, 420));
        _username.Text = existing?.Username ?? "";
        _username.ReadOnly = existing != null;
        _fullName.Text = existing?.FullName ?? "";
        _active.Checked = existing?.IsActive ?? true;
        foreach (var role in roles)
        {
            var index = _roles.Items.Add(new RoleItem(role));
            if (existing?.RoleCodes.Contains(role.Code) == true)
                _roles.SetItemChecked(index, true);
        }

        var fields = FormChrome.Fields(4);
        FormChrome.AddField(fields, 0, "Kullanıcı Adı", _username);
        FormChrome.AddField(fields, 1, "Ad Soyad", _fullName);
        if (existing == null)
            FormChrome.AddField(fields, 2, "Geçici Şifre", _password);
        fields.Controls.Add(_active, 1, 3);
        var roleLabel = new Label { Text = "Roller", Dock = DockStyle.Top, Height = 22 };
        _roles.Dock = DockStyle.Fill;
        var roleHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 12, 8) };
        roleHost.Controls.Add(_roles);
        roleHost.Controls.Add(roleLabel);
        var buttons = FormChrome.Buttons(this, out var save, out _);
        save.Click += (_, _) => Save();
        Controls.Add(roleHost);
        Controls.Add(fields);
        Controls.Add(buttons);
        fields.Height = 150;
        fields.Dock = DockStyle.Top;
    }

    public UserWriteModel? Model { get; private set; }

    private void Save()
    {
        var codes = _roles.CheckedItems.Cast<RoleItem>().Select(role => role.Code).ToArray();
        Model = new UserWriteModel
        {
            Username = _username.Text,
            FullName = _fullName.Text,
            Password = _password.Text,
            IsActive = _active.Checked,
            RoleCodes = codes
        };
        DialogResult = DialogResult.OK;
        Close();
    }

    private sealed class RoleItem
    {
        public RoleItem(RoleChoice role) => (Code, Name) = (role.Code, role.Name);
        public string Code { get; }
        public string Name { get; }
        public override string ToString() => Name;
    }
}

internal sealed class PasswordResetForm : Form
{
    private readonly TextBox _password = new() { UseSystemPasswordChar = true, MaxLength = 128 };

    public PasswordResetForm()
    {
        FormChrome.Apply(this, "Şifre Sıfırla", new Size(420, 160));
        var fields = FormChrome.Fields(1);
        FormChrome.AddField(fields, 0, "Geçici Şifre", _password);
        var buttons = FormChrome.Buttons(this, out var save, out _);
        save.Text = "Sıfırla";
        save.Click += (_, _) =>
        {
            TemporaryPassword = _password.Text;
            DialogResult = DialogResult.OK;
            Close();
        };
        Controls.Add(fields);
        Controls.Add(buttons);
    }

    public string? TemporaryPassword { get; private set; }
}
