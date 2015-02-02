using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using System.Diagnostics;
using System.IO;
using System.Xml.Linq;

namespace CppTestRunner
{
	struct util
	{
		static public
		string formatCollection<T>(IEnumerable<T> coll)
		{
			return "[" + String.Join(", ", coll.Select(o => o.ToString())) + "]";
		}
	}

	struct ProcessUtil
	{
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

	/// <summary>
	/// Runs the tests found inside 1 test container
	/// 
	/// is called from a process named vstest.executionengine.x86.exe. This process starts during first discovery pass.
	/// </summary>
	[ExtensionUri(ExecutorUriString)]
	class TestExecutor : ITestExecutor
	{
		public const string ExecutorUriString = "executor://CppTestRunnerExecutor/v1";
		public static readonly Uri ExecutorUri = new Uri(TestExecutor.ExecutorUriString);
		private bool mCancelled;

		private
		void runOnce(IFrameworkHandle framework, IRunContext runContext, IEnumerable<TestCase> tests, string exe, bool runAll)
		{
			string outputPath = System.IO.Path.GetTempPath();
			
			string arguments = "";
			//if (!runAll)
				//arguments = "";//GoogleTestCommandLine(runAll, tests, outputPath).GetCommandLine();

			string wd = System.IO.Path.GetTempPath(); //System.IO.Path.GetDirectoryName(exe);

			List<TestResult> results = new List<TestResult>(tests.Count());

			foreach (TestCase test in tests)
			{
				framework.RecordStart(test);

				// also create TestResult for startTime
				var res = new TestResult(test);
				res.StartTime = DateTime.Now;
				results.Add(res);
			}

			Process proc;
			const int waitTime = 20 * 60 * 1000; // 20 min
			if (runContext.IsBeingDebugged)
			{
				framework.SendMessage(TestMessageLevel.Informational, "Attaching debugger to " + exe);
				proc = System.Diagnostics.Process.GetProcessById(framework.LaunchProcessWithDebuggerAttached(exe, wd, arguments, null));
			}
			else
			{
				framework.SendMessage(TestMessageLevel.Informational, String.Format("[{0}] Running {1} {2}", wd, exe, arguments));
				proc = ProcessUtil.prepCommand(wd, exe, arguments);

				// now we need to setup async reading of stdout/stderr
				// so the called process can't block on full buffers
				StringBuilder output = new StringBuilder();
				StringBuilder error = new StringBuilder();

				var outputWaitHandle = new System.Threading.AutoResetEvent(false);
				var errorWaitHandle  = new System.Threading.AutoResetEvent(false);

				proc.OutputDataReceived += (sender, e) =>
				{
					if (e.Data == null)
					{
						outputWaitHandle.Set();
					}
					else
					{
						output.AppendLine(e.Data);
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
						error.AppendLine(e.Data);
					}
				};
				proc.Start();
				proc.BeginOutputReadLine();
				proc.BeginErrorReadLine();
				
				int errCode;
				if (proc.WaitForExit(waitTime) && outputWaitHandle.WaitOne(waitTime) && errorWaitHandle.WaitOne(waitTime))
				{
					// process completed
					errCode = proc.ExitCode;
				}
				else
				{
					// timed out
					framework.SendMessage(TestMessageLevel.Warning, String.Format("Had to kill {0} after {1}s", exe, waitTime / 1000));
					proc.Kill();
					errCode = 42;
				}

				// now register the results
				foreach (TestResult res in results)
				{
					res.Outcome = errCode != 0 ? TestOutcome.Failed : TestOutcome.Passed;
					string stdout = output.ToString();
					res.Messages.Add(new TestResultMessage(TestResultMessage.StandardOutCategory, stdout));
					res.Messages.Add(new TestResultMessage(TestResultMessage.StandardErrorCategory, error.ToString()));
					// AdditionalInfoCategory and DebugTraceCategory get ignored

					res.ErrorMessage = "Exit code: " + errCode.ToString() + "\n";
					res.EndTime = DateTime.Now;
					res.Duration = res.EndTime - res.StartTime;

					// for now just add failures to message
					int idx = stdout.IndexOf(@"<?xml");
					if (idx >= 0)
					try
					{
						string xmlContent = new string(stdout.Skip(idx).ToArray());
							XElement doc = XElement.Parse(xmlContent); // TODO: LoadOptions.PreserveWhitespace?
						
						var failedTests = from failedTest in doc.Descendants("FailedTest")
						                  select failedTest;

						// TODO: qtest
						foreach (XElement failedTest in failedTests)
						{
							string testName = failedTest.Element("Name").Value;
							XElement locEl = failedTest.Element("Location");
							string loc = locEl.Element("File").Value + ":" + locEl.Element("Line").Value;
							string msg = failedTest.Element("Message").Value;
							string failureType = failedTest.Element("FailureType").Value;

							res.ErrorMessage += testName + ": " + failureType + "\n" + msg;
							res.ErrorStackTrace += loc + "\n";
						}
					}
					catch (Exception /*e*/)
					{
					}

					framework.RecordResult(res);

					// TODO: looks like outcome passed here is just ignored
					// maybe only if no TestResult
//					framework.RecordEnd(res.TestCase, TestOutcome.None);
				}
			}

		//	var results = ResultParser.getResults(framework, outputPath, tests);

		//	foreach (TestResult res in results)
			//	framework.RecordResult(res);

//			foreach (var cas in cases)
//				framework.RecordEnd(cas, TestOutcome.Passed);
		}

		private
		void runTests(IEnumerable<TestCase> tests, IRunContext runContext, IFrameworkHandle framework, bool runAll)
		{
			mCancelled = false;

			// TODO: fix
			if (runContext.IsBeingDebugged)
			{
				framework.SendMessage(TestMessageLevel.Error, "Debugging is not supported yet!");
				return;
			}

			framework.SendMessage(TestMessageLevel.Informational, String.Format("Running {0} tests...", runAll ? "all" : util.formatCollection(tests)));
//			System.Diagnostics.Debugger.Break();

			Parallel.ForEach(tests.GroupBy(c => c.Source), new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
			(testGroup, loopState) =>
			{
				if (mCancelled)
					// no previous iterations need to run so use Stop, not Break
					loopState.Stop();

				string source = testGroup.Key;
				try
				{
					runOnce(framework, runContext, testGroup, source, runAll);
				}
				catch (Exception e)
				{
					framework.SendMessage(TestMessageLevel.Error, e.Message);
					framework.SendMessage(TestMessageLevel.Error, e.StackTrace);
				}
			});
			// group them by test container
/*			foreach (IGrouping<string, TestCase> testGroup in )
			{
				if (mCancelled)
					break;

				string source = testGroup.Key;
				try
				{
					runOnce(framework, runContext, testGroup, source, runAll);
				}
				catch (Exception e)
				{
					framework.SendMessage(TestMessageLevel.Error, e.Message);
					framework.SendMessage(TestMessageLevel.Error, e.StackTrace);
				}
			}
 */
		}

		/// <summary>
		/// cancel the execution of tests
		/// </summary>
		void ITestExecutor.Cancel()
		{
			//framework.SendMessage(TestMessageLevel.Informational, "Cancelling ");
			mCancelled = true;
		}

		/// <summary>
		/// maps to the "RunAll" functionality.
		/// receives a collection of strings which correspond to the sources in the test containers.
		/// list of source file names
		/// </summary>
		/// <param name="sources"></param>
		/// <param name="runContext"></param>
		/// <param name="frameworkHandle"></param>
		void ITestExecutor.RunTests(IEnumerable<string> sources, IRunContext runContext, IFrameworkHandle frameworkHandle)
		{
//			System.Diagnostics.Debugger.Break();

			// just call the discoverer and delegate
			IEnumerable<TestCase> tests = TestDiscoverer.GetTests(sources, null);
			//((ITestExecutor)this).RunTests(tests, runContext, frameworkHandle);

			runTests(tests, runContext, frameworkHandle, true);

			/*
			// TODO:
			mCancelled = false;
			foreach (string executable in sources)
			{
				if (mCancelled)
					break;

				//var list = Utils.getTestsFromExecutable(frameworkHandle, executable);

			}
			*/
		}

		/// <summary>
		/// This maps to "RunSelected" test functionality, where a bunch of test cases are passed to the executor.
		/// </summary>
		/// <param name="tests"></param>
		/// <param name="runContext"></param>
		/// <param name="frameworkHandle"></param>
		void ITestExecutor.RunTests(IEnumerable<TestCase> tests, IRunContext runContext, IFrameworkHandle frameworkHandle)
		{
			runTests(tests, runContext, frameworkHandle, false);
			// TODO: for now no filtering
//			var sources = tests.Select(tc => tc.Source).Distinct();
//			((ITestExecutor)this).RunTests(sources, runContext, frameworkHandle);

/*
			mCancelled = false;

			foreach (TestCase test in tests)
			{
				// TODO: use test.Source
				if (mCancelled)
					break;

				// create a TestResult and pass it to the FrameworkHandle (another component of Unit Test Framework)
				var testResult = new TestResult(test);

				testResult.Outcome = TestOutcome.Passed;// (TestOutcome)test.GetPropertyValue(TestResultProperties.Outcome);
				frameworkHandle.RecordResult(testResult);
			}
 */
		}
	}
}
