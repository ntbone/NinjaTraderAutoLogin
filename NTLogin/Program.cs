using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using static System.Net.Mime.MediaTypeNames;

namespace NTLogin
{
	public class Program
	{
		[DllImport("user32.dll")]
		static extern bool SetForegroundWindow(IntPtr hWnd);

		static Process FindNinjaTrader()
		{
			var processlist = Process.GetProcessesByName("NinjaTrader");
			if(processlist.Length > 0)
			{
				return processlist[0];				
			}

			return null;
		}

		[STAThread]
		static void Main(string[] args)
		{
			Process ninjaProcess = null;

			string userName = string.Empty;
			string password = string.Empty;
			string ninjaExe = @"C:\Program Files\NinjaTrader 8\bin\NinjaTrader.exe";

			if(args.Length == 2)
			{
				userName = args[0];
				password = args[1];
			}
			else if(args.Length == 3)
			{
				userName = args[0];
				password = args[1];
				ninjaExe = args[2];
			}

			ninjaProcess = FindNinjaTrader();
			if(ninjaProcess != null)
			{
				Console.WriteLine("Unable to start NinjaTrader. NinjaTrader is already running");
				return;
			}

			Process.Start(@ninjaExe);

			while(ninjaProcess == null)
			{
				ninjaProcess = FindNinjaTrader();
				Thread.Sleep(100);
			}

			// Wait for NinjaTrader login window to show up.
			Thread.Sleep(5000);

			SetForegroundWindow(ninjaProcess.MainWindowHandle);

			// This puts focus into the password box
			SendKeys.SendWait("{TAB}");
			SendKeys.SendWait("{TAB}");
			SendKeys.SendWait("{TAB}");

			Clipboard.SetText(password);
			SendKeys.SendWait("^v");
			SendKeys.SendWait("{ENTER}");
		}
	}
}
