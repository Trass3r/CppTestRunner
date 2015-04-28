// Guids.cs
// MUST match guids.h
using System;

namespace CppTestRunner
{
	internal static class GuidList
	{
		public const string guidCppTestRunnerPkgString = "8b49192b-e6cc-4e33-80dc-f44fd4c121c0";
		public const string guidCppTestRunnerCmdSetString = "2d47905a-7df7-4ce8-9203-c8029ed4b5c1";

		public static readonly Guid guidCppTestRunnerCmdSet = new Guid(guidCppTestRunnerCmdSetString);
	};
}