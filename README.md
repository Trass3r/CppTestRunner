CppTestRunner
=============

This is an experimental Visual Studio test adapter for cppunit and QTest unittest executables.

Currently it simply searches for all executables in the solution with a utest or qtest suffix,
runs them in a temporary working directory and collects the results from stdout/stderr as well as giving timing information.

Useful to run a batch of tests at once and get an overview of the results.

Download
--------
https://visualstudiogallery.msdn.microsoft.com/d87ef483-27a9-408a-9052-df24d79cf3fb