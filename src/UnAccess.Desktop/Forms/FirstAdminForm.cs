using UnAccess.Core.Abstractions;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Desktop.Ui;

namespace UnAccess.Desktop.Forms;

internal sealed class FirstAdminForm : Form
{
    private readonly IAccountService _accounts;
    private readonly TextBox _username = new() { MaxLength = 64 };
    private readonly TextBox _fullName = new() { MaxLength = 150 };
    private readonly TextBox _password = new() { UseSystemPasswordChar = true, MaxLength = 128 };
    private readonly TextBox _repeat = new() { UseSystemPasswordChar = true, MaxLength = 128 };
    private readonly Label _message = new() { AutoSize = false, Height = 40, ForeColor = Color.DarkRed };

    public FirstAdminForm(IAccountService accounts)
    {
        _accounts = accounts;
        FormChrome.Apply(this, "İlk Yönetici Oluştur", new Size(460, 300));
        var fields = FormChrome.Fields(4);
        FormChrome.AddField(fields, 0, "Kullanıcı Adı", _username);
        FormChrome.AddField(fields, 1, "Ad Soyad", _fullName);
        FormChrome.AddField(fields, 2, "Şifre", _password);
        FormChrome.AddField(fields, 3, "Şifre Tekrar", _repeat);
        var buttons = FormChrome.Buttons(this, out var save, out _);
        save.Text = "Oluştur";
        save.Click += async (_, _) => await CreateAsync();
        var host = new Panel { Dock = DockStyle.Fill };
        _message.Dock = DockStyle.Bottom;
        fields.Dock = DockStyle.Fill;
        host.Controls.Add(fields);
        host.Controls.Add(_message);
        Controls.Add(host);
        Controls.Add(buttons);
    }

    private async Task CreateAsync()
    {
        if (!string.Equals(_password.Text, _repeat.Text, StringComparison.Ordinal))
        {
            _message.Text = "Şifre tekrarı eşleşmiyor.";
            return;
        }

        try
        {
            UseWaitCursor = true;
            var result = await _accounts.CreateInitialAdminAsync(new InitialAdminModel
            {
                Username = _username.Text,
                FullName = _fullName.Text,
                Password = _password.Text
            }, CancellationToken.None);
            if (!result.Succeeded)
            {
                _message.Text = result.Message ?? "Yönetici oluşturulamadı.";
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            _message.Text = exception is UnAccessException known ? known.UserMessage : "Yönetici oluşturulamadı.";
        }
        finally
        {
            UseWaitCursor = false;
        }
    }
}
