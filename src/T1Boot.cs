using System;
using System.Collections.Generic;
using System.IO;

public class T1Boot {

	public static void Main(string[] args)
	{
		try {
			DoCLI(args);
		} catch (Exception e) {
			Console.WriteLine(e.ToString());
			Environment.Exit(1);
		}
	}

	static void Usage()
	{
		Console.WriteLine(
"usage: t1boot.exe [ options ] file...");
		Console.WriteLine(
"options:");
		Console.WriteLine(
"   -h  |  --help");
		Console.WriteLine(
"       Print this message.");
		Console.WriteLine(
"");
		Console.WriteLine(
"   -c  |  --compile");
		Console.WriteLine(
"       Perform compilation once all source code has been interpreted.");
		Console.WriteLine(
"");
		Console.WriteLine(
"   -e name  |  --entry-point name");
		Console.WriteLine(
"       Add 'name' as entry point. If no entry point has been defined");
		Console.WriteLine(
"       when compilation is performed, def::main is used.");
		Console.WriteLine(
"");
		Console.WriteLine(
"   -v  |  --verbose");
		Console.WriteLine(
"       Be verbose about what happens.");
		Console.WriteLine(
"");
		Console.WriteLine(
"   -vv  |  --extra-verbose");
		Console.WriteLine(
"       Be extra verbose about what happens.");

		Environment.Exit(1);
	}

	static void DoCLI(string[] args)
	{
		bool compile = false;
		for (int i = 0; i < args.Length; i ++) {
			string a = args[i];
			if (!a.StartsWith("-")) {
				continue;
			}
			args[i] = null;
			switch (a) {
			case "-h":
			case "--help":
				Usage();
				break;
			case "-c":
			case "--compile":
				compile = true;
				break;
			case "-e":
			case "--entry-point":
				if (++ i >= args.Length) {
					Usage();
				}
				string name = args[i];
				if (!Names.HasNamespace(name)) {
					name = Names.Make("def", name);
				}
				Compiler.AddEntryPoint(name);
				args[i] = null;
				compile = true;
				break;
			case "-v":
			case "--verbose":
				Compiler.Verbose = true;
				break;
			case "-vv":
			case "--extra-verbose":
				Compiler.Verbose = true;
				Compiler.PrintFunctions = Console.Out;
				Compiler.PrintTrees = Console.Out;
				break;
			default:
				Usage();
				break;
			}
		}
		Kernel.Init();
		foreach (string arg in args) {
			if (arg == null) {
				continue;
			}
			using (TextReader r = File.OpenText(arg)) {
				Interpreter ip = new Interpreter(r);
				ip.Run();
			}
		}
		if (compile) {
			Compiler.DoCompile();
		}
	}
}
