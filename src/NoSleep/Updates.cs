using System.Reflection;
using Velopack;
using Velopack.Sources;

namespace NoSleep;

/// <summary>
/// GitHub Releases through Velopack. Inert when not installed by Velopack (dotnet run) or the build has no repository
/// (<c>-p:UpdateRepository=</c>, set by the release workflow). Pre-releases are never offered.
/// </summary>
internal sealed class Updates
{
	private readonly UpdateManager? _manager;
	private UpdateInfo? _pending;

	public Updates()
	{
		var repository = typeof(Updates).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "UpdateRepository")?.Value;
		if (!string.IsNullOrWhiteSpace(repository))
		{
			var manager = new UpdateManager(new GithubSource(repository, accessToken: null, prerelease: false));
			_manager = manager.IsInstalled ? manager : null;
		}
	}

	public string CurrentVersion => _manager?.CurrentVersion?.ToString() ?? typeof(Updates).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?";

	public string? PendingVersion => _pending?.TargetFullRelease.Version.ToString();

	/// <summary>Returns false when offline or GitHub is blocked (corporate proxy): the next hourly check retries.</summary>
	public async Task<bool> CheckAsync()
	{
		if (_manager is null)
		{
			return true;
		}

		try
		{
			_pending = await _manager.CheckForUpdatesAsync();
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	/// <summary>Downloads, then exits this process and restarts the new version.</summary>
	public async Task ApplyAsync()
	{
		if (_manager is null || _pending is null)
		{
			return;
		}

		await _manager.DownloadUpdatesAsync(_pending);
		_manager.ApplyUpdatesAndRestart(_pending.TargetFullRelease);
	}
}
