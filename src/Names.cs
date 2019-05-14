using System;
using System.Text;

/*
 * Utility methods for handling names, i.e. detecting, removing, and
 * adding namespaces.
 */

class Names {

	/*
	 * Get the namespace from the provided name. Returns null if
	 * the name is unqualified.
	 */
	internal static string GetNamespace(string name)
	{
		int j = name.IndexOf("::");
		if (j < 0) {
			return null;
		}
		return name.Substring(0, j);
	}

	/*
	 * Get the unqualified name by removing the namespace part (if
	 * present).
	 */
	internal static string GetRaw(string name)
	{
		int j = name.IndexOf("::");
		if (j >= 0) {
			name = name.Substring(j + 2);
		}
		return name;
	}

	/*
	 * Test whether a given name is qualified.
	 */
	internal static bool HasNamespace(string name)
	{
		return name.IndexOf("::") >= 0;
	}

	/*
	 * Make a qualified name from a namespace and a raw name.
	 */
	internal static string Make(string nsname, string name)
	{
		return nsname + "::" + name;
	}

	/*
	 * Decorate a name with a prefix and suffix (either or both
	 * may be null). The prefix and suffix are applied to the
	 * raw name; the namespace, if present, is conserved.
	 */
	internal static string Decorate(
		string prefix, string name, string suffix)
	{
		StringBuilder sb = new StringBuilder();
		int j = name.IndexOf("::");
		if (j >= 0) {
			sb.Append(name.Substring(0, j + 2));
			name = name.Substring(j + 2);
		}
		if (prefix != null) {
			sb.Append(prefix);
		}
		sb.Append(name);
		if (suffix != null) {
			sb.Append(suffix);
		}
		return sb.ToString();
	}
}
