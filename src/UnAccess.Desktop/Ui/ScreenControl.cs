using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Exceptions;

namespace UnAccess.Desktop.Ui;

internal class ScreenControl : UserControl
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly ILogger _logger;
    private bool _disposed;

    protected ScreenControl(IServiceScopeFactory scopes, ILogger logger)
    {
        Scopes = scopes;
        _logger = logger;
        Font = SystemFonts.MessageBoxFont;
        Dock = DockStyle.Fill;
    }

    protected IServiceScopeFactory Scopes { get; }

    protected CancellationToken Cancellation => _cancellation.Token;

    public event EventHandler<string>? StatusTextChanged;

    protected void SetStatus(string text) => StatusTextChanged?.Invoke(this, text);

    protected async Task<bool> RunAsync(Func<IServiceProvider, Task> action)
    {
        if (IsDisposed)
            return false;

        try
        {
            UseWaitCursor = true;
            await using var scope = Scopes.CreateAsyncScope();
            await action(scope.ServiceProvider);
            return !IsDisposed;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception exception)
        {
            if (!IsDisposed)
                UiErrors.Show(_logger, this, exception);
            return false;
        }
        finally
        {
            if (!IsDisposed)
                UseWaitCursor = false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _cancellation.Cancel();
            _cancellation.Dispose();
        }

        base.Dispose(disposing);
    }
}
