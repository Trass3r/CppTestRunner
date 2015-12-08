using System;
using System.Diagnostics;
using System.Text;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace CppTestRunner
{
	internal static class ProcessUtil
	{
		internal struct ProcessRet
		{
			public Process proc;
			public string  stdout;
			public string  stderr;
		}

		/// run a command and collect stdout/stderr
		static public
		ProcessRet runCommand(string cmd, string args, string cwd, IMessageLogger logger)
		{

			// now we need to setup async reading of stdout/stderr
			// so the called process can't block on full buffers
			StringBuilder output = new StringBuilder();
			StringBuilder error = new StringBuilder();

			var proc = runCommand(cmd, args, cwd, logger,
			                      (string line) => output.AppendLine(line),
			                      (string line) => error.AppendLine(line));

			ProcessRet ret;
			ret.proc = proc;
			ret.stdout = output.ToString();
			ret.stderr = error.ToString();
			return ret;
		}

		public delegate void outputCallback(string line);

		/// run a command and pass stdout/stderr to the given callbacks
		static public
		Process runCommand(string cmd, string args, string cwd, IMessageLogger logger,
		                   outputCallback stdout, outputCallback stderr = default(outputCallback))
		{
			const int waitTime = 20 * 60 * 1000; // 20 min

			Process proc = prepCommand(cwd, cmd, args);

			var outputWaitHandle = new System.Threading.AutoResetEvent(false);
			var errorWaitHandle = new System.Threading.AutoResetEvent(false);

			proc.OutputDataReceived += (sender, e) =>
			{
				if (e.Data == null)
				{
					outputWaitHandle.Set();
				}
				else
				{
					stdout(e.Data);
				}
			};
			proc.ErrorDataReceived += (sender, e) =>
			{
				if (e.Data == null)
				{
					errorWaitHandle.Set();
				}
				else
				{
					stderr(e.Data);
				}
			};
			proc.Start();
			proc.BeginOutputReadLine();
			proc.BeginErrorReadLine();

			if (!proc.WaitForExit(waitTime) || !outputWaitHandle.WaitOne(waitTime) || !errorWaitHandle.WaitOne(waitTime))
			{
				// timed out
				logger.SendMessage(TestMessageLevel.Error, String.Format("Had to kill {0} after {1}s", cmd, waitTime / 1000));
				proc.Kill();
			}

			return proc;
		}

		static public
		Process prepCommand(string cwd, string cmd, string args)
		{
			var si = new ProcessStartInfo(cmd, args)
			{
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
				WorkingDirectory = cwd
			};
			Process proc = new Process();
			proc.StartInfo = si;
			return proc;
		}

	}
}
