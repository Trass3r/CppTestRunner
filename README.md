CppTestRunner
=============

This is an experimental Visual Studio test adapter for cppunit and QTest unittest executables.

Currently it simply searches for all executable projects in the solution with a typical suffix like UnitTest/utest/qtest,
runs them in a temporary working directory and collects the results from stdout/stderr as well as giving timing information.
The tests thus need to be able to run independently of the working directory, i.e. don't use relative paths to unit test data etc.

Useful to run a batch of tests at once and get an overview of the results.

Download
--------
https://visualstudiogallery.msdn.microsoft.com/d87ef483-27a9-408a-9052-df24d79cf3fb