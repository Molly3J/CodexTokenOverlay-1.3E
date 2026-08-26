using System;
using System.Threading;
using System.Windows.Forms;

namespace CodexTokenOverlay;

internal static class Program
{
	[STAThread]
	private static void Main(string[] args)
	{
		Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
		string sessionRoot = SessionPathResolver.Resolve(args);
		if (ProbeRunner.TryRun(args, sessionRoot))
		{
			return;
		}
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(defaultValue: false);
		bool createdNew;
		using Mutex obj = new Mutex(initiallyOwned: true, "Local\\CodexTokenOverlay", out createdNew);
		if (createdNew)
		{
			string settingsPath = OverlaySettings.ResolveSettingsOverride(args);
			Application.Run(new OverlayContext(sessionRoot, settingsPath));
			GC.KeepAlive(obj);
		}
	}
}
