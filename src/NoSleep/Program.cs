using Velopack;

namespace NoSleep;

internal static class Program
{
	[STAThread]
	public static void Main()
	{
		// Must run first: handles Velopack's --veloapp-* hook invocations and exits.
		VelopackApp.Build()
			.OnBeforeUninstallFastCallback(_ => Startup.Disable())
			.Run();

		// One tray per user session (Start with Windows plus a manual launch must not show two icons).
		using (var single = new Mutex(initiallyOwned: true, @"Local\NoSleep", out var isFirst))
		{
			if (isFirst)
			{
				var app = new App();
				app.InitializeComponent();
				app.Run();
			}
		}
	}
}
