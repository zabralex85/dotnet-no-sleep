using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace NoSleep;

internal static partial class Native
{
	public const uint PowerRequestContextVersion = 0;
	public const uint PowerRequestContextSimpleString = 0x1;

	public enum PowerRequestType
	{
		DisplayRequired = 0,
		SystemRequired = 1,
	}

	[LibraryImport("kernel32.dll", SetLastError = true)]
	public static partial SafeFileHandle PowerCreateRequest(in ReasonContext context);

	[LibraryImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static partial bool PowerSetRequest(SafeFileHandle request, PowerRequestType type);

	[LibraryImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static partial bool PowerClearRequest(SafeFileHandle request, PowerRequestType type);

	/// <summary>REASON_CONTEXT with POWER_REQUEST_CONTEXT_SIMPLE_STRING: <see cref="SimpleReasonString"/> is a native UTF-16 string.</summary>
	[StructLayout(LayoutKind.Sequential)]
	public struct ReasonContext
	{
		public uint Version;
		public uint Flags;
		public nint SimpleReasonString;
	}
}
