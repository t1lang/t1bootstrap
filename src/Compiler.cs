using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

class Compiler {

	/*
	 * If true, then print out messages and statistics for the various
	 * compilation phases.
	 */
	internal static bool Verbose = false;

	/*
	 * If non-null, then functions are printed out on that object
	 * when they are first encountered during analysis.
	 */
	internal static TextWriter PrintFunctions = null;

	/*
	 * If non-null, then built trees are printed out on that object
	 * upon successful completion.
	 */
	internal static TextWriter PrintTrees = null;

	/*
	 * Set to the base file name for compiled output.
	 */
	internal static string OutputBase = "t1out";

	/*
	 * String for each indentation level.
	 */
	internal static string IndentString = "    ";

	static List<string> entryPoints;

	static Compiler()
	{
		entryPoints = new List<string>();
	}

	/*
	 * Add an entry point (function name).
	 */
	internal static void AddEntryPoint(string name)
	{
		entryPoints.Add(name);
	}

	/*
	 * Encode an arbitrary string into an identifier-like string.
	 * The output contains only letters (lowercase and uppercase),
	 * digits, and underscore characters. The output may contain
	 * only digits. If the source is empty, so is the output.
	 */
	internal static string Encode(string s)
	{
		StringBuilder sb = new StringBuilder();
		foreach (byte b in Encoding.UTF8.GetBytes(s)) {
			if ((b >= '0' && b <= '9')
				|| (b >= 'A' && b <= 'Z')
				|| (b >= 'a' && b <= 'z'))
			{
				sb.Append((char)b);
				continue;
			}
			switch ((int)b) {
			case ':':
				sb.Append("__");
				break;
			default:
				sb.AppendFormat("_{0:X2}", b);
				break;
			}
		}
		return sb.ToString();
	}

	/*
	 * Write some spaces to match the specified indentation level.
	 */
	internal static void Indent(TextWriter tw, int indent)
	{
		while (indent -- > 0) {
			tw.Write(IndentString);
		}
	}

	/*
	 * Get the C type for a restricted value.
	 */
	internal static string RestrictedCType(XType xt)
	{
		if (xt == XType.BOOL) {
			return "uint8_t";
		} else if (xt == XType.U8) {
			return "uint8_t";
		} else if (xt == XType.U16) {
			return "uint16_t";
		} else if (xt == XType.U32) {
			return "uint32_t";
		} else if (xt == XType.U64) {
			return "uint64_t";
		} else if (xt == XType.I8) {
			return "int8_t";
		} else if (xt == XType.I16) {
			return "int16_t";
		} else if (xt == XType.I32) {
			return "int32_t";
		} else if (xt == XType.I64) {
			return "int64_t";
		} else {
			if (xt.IsRestricted) {
				throw new Exception(string.Format("unknown restricted type: {0}", xt.Name));
			} else {
				throw new Exception(string.Format("not a restricted type: {0}", xt.Name));
			}
		}
	}

	/*
	 * Perform compilation.
	 */
	internal static void DoCompile()
	{
		string[] eps;
		if (entryPoints.Count == 0) {
			eps = new string[] { "def::main" };
		} else {
			eps = entryPoints.ToArray();
		}

		if (Verbose) {
			Console.WriteLine("+++++ Type Analysis +++++");
		}
		List<CCNode> roots = new List<CCNode>();
		foreach (string name in eps) {
			if (Verbose) {
				Console.WriteLine("--- Entry point: {0}", name);
			}
			Function f = Function.LookupNoArgs(name);
			if (f == null) {
				throw new Exception(string.Format("no such entry point: {0}", name));
			}
			roots.Add(CCNode.BuildTree(f));
		}
		if (PrintTrees != null) {
			PrintTrees.WriteLine("Call trees:");
			foreach (CCNode node in roots) {
				Indent(PrintTrees, 1);
				PrintTrees.WriteLine("*****");
				node.Print(PrintTrees, 2);
			}
		}

		if (Verbose) {
			Console.WriteLine("+++++ Code Generation +++++");
		}
		foreach (CCNode root in roots) {
			var ne = root as CCNodeEntry;
			if (ne != null) {
				GFunctionInterpreted.Add(ne.cfi);
			}
		}
		if (PrintTrees != null) {
			PrintTrees.WriteLine("Generated functions:");
			GFunctionInterpreted.PrintAll(PrintTrees);
		}

		using (TextWriter tw = File.CreateText(OutputBase + ".c")) {
			CCValues.PrintAll(tw);
		}
	}
}
