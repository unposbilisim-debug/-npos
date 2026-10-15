using UnAccess.Core.Abstractions;
using UnAccess.Core.Exceptions;
using UnAccess.Desktop.Ui;

namespace UnAccess.Desktop.Forms;

internal sealed class LoginForm : Form
{
    private readonly IAccountService _accounts;
    private readonly TextBox _username = new() { MaxLength = 64 };
    private readonly TextBox _password = new() { UseSystemPasswordChar = true, MaxLength = 128 };
    private readonly Label _message = new() { AutoSize = false, Height = 36, ForeColor = Color.DarkRed };

    public LoginForm(IAccountService accounts)
    {
        _accounts = accounts;
        FormChrome.Apply(this, "Giriş", new Size(420, 230));
        var fields = FormChrome.Fields(2);
        FormChrome.AddField(fields, 0, "Kullanıcı Adı", _username);
        FormChrome.AddField(fields, 1, "Şifre", _password);
        var buttons = FormChrome.Buttons(this, out var enter, out _);
        enter.Text = "Giriş Yap";
        enter.Width = 110;
        enter.Click += async (_, _) => await EnterAsync();
        var host = new Panel { Dock = DockStyle.Fill };
        _message.Dock = DockStyle.Bottom;
        fields.Dock = DockStyle.Fill;
        host.Controls.Add(fields);
        host.Controls.Add(_message);
        Controls.Add(host);
        Controls.Add(buttons);
        Shown += (_, _) => _username.Focus();
    }

    private async Task EnterAsync()
    {
        try
        {
            UseWaitCursor = true;
            var result = await _accounts.LoginAsync(_username.Text, _password.Text, CancellationToken.None);
            if (!result.Succeeded)
            {
                _message.Text = result.Message ?? LoginText.Invalid;
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            _message.Text = exception is UnAccessException known ? known.UserMessage : "Giriş tamamlanamadı.";
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private static class LoginText
    {
        public const string Invalid = "Kullanıcı adı veya şifre hatalı.";
    }
}
