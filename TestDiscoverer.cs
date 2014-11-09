using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using System.Text.RegularExpressions;

namespace CppTestRunner
{
	struct Utils
	{
		public static
		bool isTestExecutable (IMessageLogger logger, string e)
		{
			const string executablesAllowed = "[qu][Tt]est[s]{0,1}.exe";
			var matches = Regex.IsMatch(e, executablesAllowed);
			logger.SendMessage(TestMessageLevel.Informational, String.Format("Does {0} match {1}: {2}", e, executablesAllowed, matches));
			return matches;
		}
	}

	/// <summary>
	/// finds all tests within a test container
	/// 
	/// UTE looks for an implementation of ITestDiscoverer that has an DefaultExecutorUri attribute set to the same value as the ExecutorUri property in ITestContainerDiscoverer
	/// 
	/// This is called from a process named vstest.discoveryengine.x86.exe. This process starts when the UTE is first opened.
	/// </summary>
	[FileExtension(".exe")]
	[DefaultExecutorUri(TestExecutor.ExecutorUriString)] // Url of the executor that is tied to the discoverer
	class TestDiscoverer : ITestDiscoverer
	{
		/// <summary>
		/// </summary>
		/// <param name="sources">Refers to the list of test sources that are passed to the test adapter from the client [VS or command line]. It would be a list of .xml files in our case.</param>
		/// <param name="discoveryContext">Refers to the discovery context/runsettings for the current test run. Discoverer shall pull out the tests in a specific way based on the current context.</param>
		/// <param name="logger">This is used to relay the warning and error messages to the registered loggers. Console Logger and TRXLogger are the built-in loggers that ships with Visual Studio 11. User shall register other custom loggers like FileStreamLogger or DBLogger as required.</param>
		/// <param name="discoverySink">Discovery sink is used to send back the test cases to the framework as and when they are being discovered. Also it is responsible for the discovery related events.</param>
		/// <returns></returns>
		void ITestDiscoverer.DiscoverTests(IEnumerable<string> sources, IDiscoveryContext discoveryContext, IMessageLogger logger, ITestCaseDiscoverySink discoverySink)
		{
			logger.SendMessage(TestMessageLevel.Informational, String.Format("My DiscoverTests called in {0} with {1}", Environment.CurrentDirectory, sources));
			//Console.WriteLine("DiscoverTests called in {0} with {1}", Environment.CurrentDirectory, sources);
//			System.Diagnostics.Debugger.Break();
			var testExecutables = sources.Where(s => Utils.isTestExecutable(logger, s));
			int i;
/*			foreach (string exe in testExecutables)
			{	
				var googleTestTests = Utils.getTestsFromExecutable(logger, executable);
				foreach (var test in googleTestTests)
					discoverySink.SendTestCase(test);
			}
*/
			GetTests(testExecutables, discoverySink);
		}

		// called from "Run all" executor with null sink
		public static List<TestCase> GetTests(IEnumerable<string> sources, ITestCaseDiscoverySink discoverySink)
		{
			// create 1 test case for each unittest executable
			List<TestCase> tests = new List<TestCase>();
			foreach (string source in sources)
			{
				string testName = System.IO.Path.GetFileNameWithoutExtension(source);
				var testcase = new TestCase(testName, TestExecutor.ExecutorUri, source)
				{
					CodeFilePath = source, // what's displayed in testexplorer as source, in this case exe
				};

				if (discoverySink != null)
					discoverySink.SendTestCase(testcase);
				else
					tests.Add(testcase);

/*				var doc = new System.Xml.XmlDocument();
				doc.LoadXml(@"<?xml version=""1.0""?>
					<Tests>
			 			<Test name=""blaTest"" outcome=""Passed""/>
					</Tests>
				");
				//doc.Load(source);

				var testNodes = doc.SelectNodes("//Tests/Test");
				foreach (System.Xml.XmlNode testNode in testNodes)
				{
					System.Xml.XmlAttribute nameAttribute = testNode.Attributes["name"];
					if (nameAttribute == null || string.IsNullOrWhiteSpace(nameAttribute.Value))
						continue;

					var testcase = new TestCase(nameAttribute.Value, TestExecutor.ExecutorUri, source)
					{
						CodeFilePath = source,
					};


					if (discoverySink != null)
						discoverySink.SendTestCase(testcase);
					else
					{
						System.Xml.XmlAttribute outcomeAttibute = testNode.Attributes["outcome"];
						TestOutcome outcome;
						Enum.TryParse<TestOutcome>(outcomeAttibute.Value, out outcome);
						testcase.SetPropertyValue(TestResultProperties.Outcome, outcome);
					}
					
				}
*/
			}
			return tests;
		}
	}
}
