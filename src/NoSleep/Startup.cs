using Microsoft.Win32;

namespace NoSleep;

/// <summary>
/// "Start with Windows": <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c> value pointing at the running exe
/// (Velopack's stable <c>current\NoSleep.exe</c>, so it survives updates). The registry is the source of truth.
/// </summary>
internal static class Startup
{
	private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
	private const string ValueName = "NoSleep";

	/// <summary>On only when the value points at this exe (a moved exe shows off, and turning it on fixes the path).</summary>
	public static bool IsEnabled
	{
		get
		{
			using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
			{
				return string.Equals(key?.GetValue(ValueName) as string, Command(Environment.ProcessPath!), StringComparison.OrdinalIgnoreCase);
			}
		}
	}

	public static string Command(string exePath) => $"\"{exePath}\"";

	public static void Enable()
	{
		using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
		{
			key.SetValue(ValueName, Command(Environment.ProcessPath!));
		}
	}

	public static void Disable()
	{
		using (var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true))
		{
			key?.DeleteValue(ValueName, throwOnMissingValue: false);
		}
	}
}
