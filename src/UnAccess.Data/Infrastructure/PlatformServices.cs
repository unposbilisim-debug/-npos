using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using UnAccess.Core.Abstractions;
using UnAccess.Data.Services;
using UnAccess.Core.Backup;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;

namespace UnAccess.Data.Infrastructure;

internal sealed class UnAccessEnvironment : IUnAccessEnvironment
{
    public UnAccessEnvironment(IConfiguration configuration)
    {
        var name = configuration["UnAccess:Environment"];
        Name = string.IsNullOrWhiteSpace(name) ? "Development" : name.Trim();
        IsProduction = string.Equals(Name, "Production", StringComparison.OrdinalIgnoreCase);
        AllowsDevelopmentSeed = !IsProduction;
        AllowsAutomaticMigration = !IsProduction;
    }

    public string Name { get; }
    public bool IsProduction { get; }
    public bool AllowsDevelopmentSeed { get; }
    public bool AllowsAutomaticMigration { get; }
}

internal sealed class AppPathProvider : IAppPathProvider
{
    public AppPathProvider()
    {
        Root = Resolve();
        LogDirectory = Path.Combine(Root, "Logs");
        ConfigurationDirectory = Path.Combine(Root, "Configuration");
    }

    public string Root { get; }
    public string LogDirectory { get; }
    public string ConfigurationDirectory { get; }

    private static string Resolve()
    {
        var common = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (CanUse(common, out var commonPath))
            return commonPath;
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (CanUse(local, out var localPath))
            return localPath;
        var fallback = Path.Combine(Path.GetTempPath(), "UnAccess");
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    private static bool CanUse(string root, out string path)
    {
        path = "";
        if (string.IsNullOrWhiteSpace(root))
            return false;
        try
        {
            path = Path.Combine(root, "UnAccess");
            Directory.CreateDirectory(path);
            var probe = Path.Combine(path, ".write-probe");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch (Exception)
        {
            path = "";
            return false;
        }
    }
}

internal sealed class DpapiSecretProtector : ISecretProtector
{
    public byte[] Protect(byte[] plaintext)
    {
        if (!OperatingSystem.IsWindows())
            throw new UnAccessException(BackupMessages.ProtectedUnsupported);
        return ProtectedData.Protect(plaintext, null, DataProtectionScope.LocalMachine);
    }

    public byte[] Unprotect(byte[] ciphertext)
    {
        if (!OperatingSystem.IsWindows())
            throw new UnAccessException(BackupMessages.ProtectedUnsupported);
        return ProtectedData.Unprotect(ciphertext, null, DataProtectionScope.LocalMachine);
    }
}

internal sealed class FileConnectionConfigurationStore : IConnectionConfigurationStore
{
    private readonly IAppPathProvider _paths;
    private readonly ISecretProtector _protector;

    public FileConnectionConfigurationStore(IAppPathProvider paths, ISecretProtector protector)
    {
        _paths = paths;
        _protector = protector;
    }

    public void Save(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new UnAccessException(DataMessages.MissingConnectionString);

        Directory.CreateDirectory(_paths.ConfigurationDirectory);
        var protectedBytes = _protector.Protect(Encoding.UTF8.GetBytes(connectionString));
        var path = Path.Combine(_paths.ConfigurationDirectory, "connection.protected");
        File.WriteAllBytes(path, protectedBytes);
        TryRestrict(path);
    }

    public ConnectionConfigurationRead Read()
    {
        var path = Path.Combine(_paths.ConfigurationDirectory, "connection.protected");
        if (!File.Exists(path))
            return new ConnectionConfigurationRead { Success = false };

        try
        {
            var plain = _protector.Unprotect(File.ReadAllBytes(path));
            var text = Encoding.UTF8.GetString(plain);
            if (string.IsNullOrWhiteSpace(text))
                return new ConnectionConfigurationRead { Success = false, Error = BackupMessages.ProtectedUnreadable };
            return new ConnectionConfigurationRead { Success = true, ConnectionString = text };
        }
        catch (CryptographicException)
        {
            return new ConnectionConfigurationRead { Success = false, Error = BackupMessages.ProtectedUnreadable };
        }
        catch (UnAccessException exception)
        {
            return new ConnectionConfigurationRead { Success = false, Error = exception.UserMessage };
        }
    }

    private static void TryRestrict(string path)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception)
        {
        }
    }
}

internal sealed class ApplicationVersionProvider : IApplicationVersionProvider
{
    public ApplicationVersionProvider(IUnAccessEnvironment environment)
    {
        var assembly = typeof(UnAccess.Core.Backup.BackupMessages).Assembly;
        Version = assembly.GetName().Version?.ToString() ?? "";
        InformationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? Version;
        EnvironmentName = environment.Name;
    }

    public string Version { get; }
    public string InformationalVersion { get; }
    public string EnvironmentName { get; }
}

internal sealed class NotConfiguredUpdateService : IUpdateCheckService
{
    private readonly IApplicationVersionProvider _version;

    public NotConfiguredUpdateService(IApplicationVersionProvider version)
    {
        _version = version;
    }

    public Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new UpdateCheckResult
        {
            CurrentVersion = _version.Version,
            LatestVersion = null,
            IsUpdateAvailable = false,
            ReleaseNotes = null,
            DownloadUri = null,
            Status = "NotConfigured",
            Message = BackupMessages.UpdateNotConfigured
        });
}

internal sealed class ConfigurationAccessor : IConfigurationAccessor
{
    public ConfigurationAccessor(IConfiguration configuration)
    {
        EnvironmentConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__UnAccess");
        _ = configuration;
    }

    public string? EnvironmentConnectionString { get; }
}
