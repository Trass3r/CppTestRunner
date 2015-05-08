using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CppTestRunner
{
	/// this class handles C++ Catch unittest executables
	internal
	static class CatchCommandLine
	{
		public static bool isSupportedContainer(string container)
		{
			// dirty code incoming..
			byte[] fileContents = System.IO.File.ReadAllBytes(container);

			for (int i = 0; i < fileContents.Length; ++i)
			{
				for (int j = 0; j < 12; ++j)
				{
					if (fileContents[i+j] != "--list-tests"[j])
						break;
					if (j == 11)
						return true;
				}
			}

			return false;
		}

	}
}
