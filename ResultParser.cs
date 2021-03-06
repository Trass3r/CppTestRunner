﻿using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using System.Collections.Generic;
using System.Linq;

namespace CppTestRunner
{
	internal static class ResultParser
	{
		static public
		List<TestResult> getResults(IMessageLogger logger, string outputPath, IEnumerable<TestCase> allCases)
		{
			var res = new List<TestResult>(allCases.Count());
			foreach (TestCase tc in allCases)
			{
				var testResult = new TestResult(tc);
				testResult.Outcome = TestOutcome.Failed;
				testResult.ErrorMessage = "Tekekeke";
				res.Add(testResult);
			}
			return res;
		}
	}
}
