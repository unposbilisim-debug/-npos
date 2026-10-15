using UnAccess.Core.Abstractions;
using UnAccess.Core.Exceptions;
using UnAccess.Desktop.Ui;

namespace UnAccess.Desktop.Forms;

internal sealed class ChangePasswordForm : Form
{
    private readonly IAccountService _accounts;
    private readonly bool _required;
    private readonly TextBox _current = new() { UseSystemPasswordChar = true, MaxLength = 128 };
    private readonly TextBox _next = new() { UseSystemPasswordChar = true, MaxLength = 128 };
    private readonly TextBox _repeat = new() { UseSystemPasswordChar = true, MaxLength = 128 };
    private readonly Label _message = new() { AutoSize = false, Height = 40, ForeColor = Color.DarkRed };

    public ChangePasswordForm(IAccountService accounts, bool required)
    {
        _accounts = accounts;
        _required = required;
        FormChrome.Apply(this, "Şifre Değiştir", new Size(440, 270));
        var fields = FormChrome.Fields(3);
        FormChrome.AddField(fields, 0, "Mevcut Şifre", _current);
        FormChrome.AddField(fields, 1, "Yeni Şifre", _next);
        FormChrome.AddField(fields, 2, "Yeni Şifre Tekrar", _repeat);
        var buttons = FormChrome.Buttons(this, out var save, out var cancel);
        save.Text = "Kaydet";
        if (required)
        {
            cancel.Visible = false;
            ControlBox = true;
            FormClosing += (_, args) =>
            {
                if (DialogResult != DialogResult.OK)
                    args.Cancel = false;
            };
        }

        save.Click += async (_, _) => await SaveAsync();
        var host = new Panel { Dock = DockStyle.Fill };
        _message.Dock = DockStyle.Bottom;
        fields.Dock = DockStyle.Fill;
        host.Controls.Add(fields);
        host.Controls.Add(_message);
        Controls.Add(host);
        Controls.Add(buttons);
    }

    private async Task SaveAsync()
    {
        if (!string.Equals(_next.Text, _repeat.Text, StringComparison.Ordinal))
        {
            _message.Text = "Yeni şifre tekrarı eşleşmiyor.";
            return;
        }

        try
        {
            UseWaitCursor = true;
            await _accounts.ChangeOwnPasswordAsync(_current.Text, _next.Text, CancellationToken.None);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            _message.Text = exception is UnAccessException known ? known.UserMessage : "Şifre değiştirilemedi.";
        }
        finally
        {
            UseWaitCursor = false;
        }
    }
}
