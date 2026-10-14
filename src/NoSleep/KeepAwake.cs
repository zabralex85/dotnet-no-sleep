using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using static NoSleep.Native;

namespace NoSleep;

/// <summary>
/// One power request (the mechanism Chromium/Teams uses), visible to admins in <c>powercfg /requests</c> with its reason.
/// Bound to the handle, not a thread; Windows drops it when the process exits.
/// </summary>
internal sealed class KeepAwake : IDisposable
{
	private readonly SafeFileHandle _request;
	private bool _system;
	private bool _display;

	public KeepAwake(string reason)
	{
		var text = Marshal.StringToHGlobalUni(reason);
		try
		{
			var context = new ReasonContext { Version = PowerRequestContextVersion, Flags = PowerRequestContextSimpleString, SimpleReasonString = text };
			_request = PowerCreateRequest(in context);
		}
		finally
		{
			Marshal.FreeHGlobal(text);
		}

		if (_request.IsInvalid)
		{
			throw new Win32Exception();
		}
	}

	/// <summary>Sets or clears each request; requests are counted by Windows, so only changes are sent.</summary>
	public void Apply(bool system, bool display)
	{
		Set(PowerRequestType.SystemRequired, ref _system, system);
		Set(PowerRequestType.DisplayRequired, ref _display, display);
	}

	public void Dispose()
	{
		// Closing the handle drops both requests.
		_request.Dispose();
	}

	private void Set(PowerRequestType type, ref bool current, bool wanted)
	{
		if (current == wanted)
		{
			return;
		}

		if (!(wanted ? PowerSetRequest(_request, type) : PowerClearRequest(_request, type)))
		{
			throw new Win32Exception();
		}

		current = wanted;
	}
}
