using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;

namespace NTLogin
{
	public static class UIAutomationHelper
	{
		/// <summary>
		/// Finds an AutomationElement in the specified process by AutomationId, or optionally by ControlType and Name.
		/// </summary>
		/// <param name="process">The target Process object.</param>
		/// <param name="automationId">The AutomationId to search for (can be null).</param>
		/// <param name="controlType">Optional fallback ControlType (e.g., ControlType.Edit).</param>
		/// <param name="name">Optional fallback Name property.</param>
		/// <returns>The found AutomationElement, or null if not found.</returns>
		public static AutomationElement FindControlInProcess(Process process, string automationId = null, ControlType controlType = null, string name = null)
		{
			if(process == null || process.MainWindowHandle == IntPtr.Zero)
			{
				return null;
			}

			AutomationElement mainWindow;
			try
			{
				mainWindow = AutomationElement.FromHandle(process.MainWindowHandle);
			}
			catch(ElementNotAvailableException)
			{
				return null;
			}

			if(mainWindow == null)
				return null;

			Condition condition;

			if(!string.IsNullOrEmpty(automationId))
			{
				condition = new PropertyCondition(AutomationElement.AutomationIdProperty, automationId);
			}
			else if(controlType != null && !string.IsNullOrEmpty(name))
			{
				condition = new AndCondition(
					new PropertyCondition(AutomationElement.ControlTypeProperty, controlType),
					new PropertyCondition(AutomationElement.NameProperty, name)
				);
			}
			else if(controlType != null)
			{
				condition = new PropertyCondition(AutomationElement.ControlTypeProperty, controlType);
			}
			else
			{
				return null;
			}

			return mainWindow.FindFirst(TreeScope.Descendants, condition);
		}

		/// <summary>
		/// Sets the text value of a supported AutomationElement (e.g., TextBox) using ValuePattern.
		/// </summary>
		/// <param name="element">The target AutomationElement (must support ValuePattern).</param>
		/// <param name="text">The text to set.</param>
		/// <returns>True if text was set successfully; false otherwise.</returns>
		public static bool SetControlText(AutomationElement element, string text)
		{
			if(element == null || text == null)
			{
				return false;
			}

			if(!element.TryGetCurrentPattern(ValuePattern.Pattern, out object patternObj))
			{
				return false;
			}

			var valuePattern = patternObj as ValuePattern;
			if(valuePattern == null || valuePattern.Current.IsReadOnly)
			{
				return false;
			}

			try
			{
				valuePattern.SetValue(text);
				return true;
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// Finds a button in the specified process by AutomationId.
		/// </summary>
		/// <param name="process">The target Process object.</param>
		/// <param name="automationId">The AutomationId of the button.</param>
		/// <returns>The found AutomationElement or null if not found.</returns>
		public static AutomationElement FindButton(Process process, string automationId)
		{
			if(process == null || string.IsNullOrEmpty(automationId))
			{
				return null;
			}

			if(process.MainWindowHandle == IntPtr.Zero)
			{
				return null;
			}

			AutomationElement mainWindow;
			try
			{
				mainWindow = AutomationElement.FromHandle(process.MainWindowHandle);
			}
			catch
			{
				return null;
			}

			if(mainWindow == null)
			{
				return null;
			}

			return mainWindow.FindFirst(
				TreeScope.Descendants,
				new PropertyCondition(AutomationElement.AutomationIdProperty, automationId)
			);
		}

		/// <summary>
		/// Attempts to invoke (click) a button AutomationElement using the InvokePattern.
		/// </summary>
		/// <param name="buttonElement">The button AutomationElement.</param>
		/// <returns>True if clicked successfully; false otherwise.</returns>
		public static bool TryClickButton(AutomationElement buttonElement)
		{
			if(buttonElement == null)
			{
				return false;
			}

			if(buttonElement.TryGetCurrentPattern(InvokePattern.Pattern, out object patternObj))
			{
				InvokePattern invokePattern = patternObj as InvokePattern;
				try
				{
					invokePattern.Invoke();
					return true;
				}
				catch
				{
					return false;
				}
			}

			return false;
		}
	}


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


		const string passwordEditBoxID = "passwordBox";
		const string usernameEditBoxID = "tbUserName";
		const string loginButtonID = "btnLogin";

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

			var userNameBox = UIAutomationHelper.FindControlInProcess(ninjaProcess, usernameEditBoxID);
			if(userNameBox != null)
			{
				UIAutomationHelper.SetControlText(userNameBox, userName);
			}


			var passwordBox = UIAutomationHelper.FindControlInProcess(ninjaProcess, passwordEditBoxID);
			var loginButton = UIAutomationHelper.FindButton(ninjaProcess, loginButtonID);
			if(passwordBox != null && loginButton != null)
			{
				UIAutomationHelper.SetControlText(passwordBox, password);
				UIAutomationHelper.TryClickButton(loginButton);
			}
			else	
			{
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
}
