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
using System.Security.Principal;

namespace NTLogin
{
	public class Program
	{
		[DllImport("user32.dll")]
		static extern bool SetForegroundWindow(IntPtr hWnd);


		[DllImport("user32.dll")]
		static extern bool IsWindow(IntPtr hWnd);


		[DllImport("advapi32.dll", SetLastError = true)]
		private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);
		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool CloseHandle(IntPtr hObject);


		static WindowsIdentity GetProcessUser(Process process)
		{
			IntPtr processHandle = IntPtr.Zero;
			try
			{
				OpenProcessToken(process.Handle, 8, out processHandle);
				return new WindowsIdentity(processHandle);
			}
			catch
			{
				return null;
			}
			finally
			{
				if(processHandle != IntPtr.Zero)
				{
					CloseHandle(processHandle);
				}
			}
		}

		static string GetUserName(WindowsIdentity identity)
		{
			string user = identity.Name;
			return user.Contains(@"\") ? user.Substring(user.IndexOf(@"\") + 1) : user;
		}

		static Process FindNinjaTrader()
		{
			var processes = Process.GetProcessesByName("NinjaTrader");
			if(processes.Length > 0)
			{
				var currentUser = GetProcessUser(Process.GetCurrentProcess());

				foreach(var process in processes)
				{
					var processUser = GetProcessUser(process);
					if(currentUser.User == processUser.User)
					{
						return process;
					}
				}
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


			if(string.IsNullOrEmpty(password))
			{
				Console.Error.WriteLine("Cannot launch NinjaTrader. No password specified");
				return;
			}

			ninjaProcess = FindNinjaTrader();
			while(ninjaProcess != null)
			{
				Thread.Sleep(1000);
				ninjaProcess = FindNinjaTrader();
			}


			if(ninjaProcess != null)
			{
				Console.Error.WriteLine("Unable to start NinjaTrader. NinjaTrader is already running");
				return;
			}

			Process.Start(@ninjaExe);

			while(ninjaProcess == null)
			{
				ninjaProcess = FindNinjaTrader();
				Thread.Sleep(100);
			}

			while(!IsWindow(ninjaProcess.MainWindowHandle))
			{
				Thread.Sleep(1000);
			}

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
