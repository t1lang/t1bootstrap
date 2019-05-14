using System;
using System.IO;
using System.Text;

/*
 * Basic lexer.
 *
 * Tokens are:
 *
 *   word: sequence of printable non-space characters (ASCII 33 to 126),
 *   excluding ()[]{}`'"#
 *
 *   literal string: starts with double-quote, followed by string contents
 *   (escapes have been interpreted, and there is no final double-quote)
 *
 *   literal character: starts with backquote, followed by a single code point
 *   (escapes have been interpreted)
 *
 *   single-letter word: one of ()[]{}'
 *
 * Whitespace separates tokens; whitespace is any sequence of control
 * characters (ASCII 0 to 31) and space (ASCII 32).
 *
 * The lexer skips comments. A comment starts with # and extends to the end
 * of the line, but does not include the end-of-line (thus, a comment counts
 * as whitespace).
 *
 * Code points 127+ may appear only in literal strings and characters.
 */

class Lexer {

	TextReader r;
	int delayedChar;

	internal Lexer(TextReader r)
	{
		this.r = r;
		delayedChar = -1;
	}

	/*
	 * Utility method: append a code point to a StringBuilder. This
	 * handles splitting into surrogate pairs when necessary.
	 */
	static void AppendCP(StringBuilder sb, int cp)
	{
		if (cp >= 0 && cp <= 0xFFFF) {
			sb.Append((char)cp);
		} else if (cp >= 0x10000 && cp <= 0x10FFFF) {
			cp -= 0x10000;
			sb.Append((char)(0xD800 + (cp >> 10)));
			sb.Append((char)(0xDC00 + (cp & 0x3FF)));
		} else {
			throw new Exception("invalid code point: " + cp);
		}
	}

	/*
	 * Get next word/token.
	 * Returns null on end-of-stream.
	 */
	internal string Next()
	{
		int x;
		for (;;) {
			x = NextChar();
			if (x == '#') {
				for (;;) {
					x = NextChar();
					if (x < 0 || x == '\n') {
						break;
					}
				}
			}
			if (x < 0) {
				return null;
			}
			if (!IsWS(x)) {
				break;
			}
		}

		if (IsSingleCharToken(x)) {
			return "" + (char)x;
		}

		if (x == '`') {
			return ReadLiteralChar();
		} else if (x == '"') {
			return ReadLiteralString();
		} else if (IsWordChar(x)) {
			StringBuilder sb = new StringBuilder();
			AppendCP(sb, x);
			for (;;) {
				x = NextChar();
				if (!IsWordChar(x)) {
					break;
				}
				AppendCP(sb, x);
			}
			Unread(x);
			return sb.ToString();
		} else {
			throw new Exception(string.Format(
				"forbidden source character: U+{0:X4}", x));
		}
	}

	/*
	 * Get next code point. Some processing is applied:
	 *
	 *  - Surrogate pairs are reassembled into code points in the
	 *    upper planes (U+10000 to U+10FFFF). Unmatched surrogates
	 *    trigger exceptions.
	 *
	 *  - Lone CR, and CR+LF pairs, are converted to LF.
	 */
	int NextChar()
	{
		int x = delayedChar;
		if (x >= 0) {
			delayedChar = -1;
			return x;
		}
		if (r == null) {
			return -1;
		}
		x = r.Read();
		if (x < 0) {
			r = null;
			return -1;
		}
		if (x == '\r') {
			x = r.Read();
			if (x < 0) {
				r = null;
			} else if (x != '\n') {
				delayedChar = x;
			}
			x = '\n';
		} else if (x >= 0xD800 && x <= 0xDBFF) {
			int y = r.Read();
			if (y < 0) {
				r = null;
			}
			if (y < 0xDC00 || x > 0xDFFF) {
				throw new Exception("unmatched surrogate");
			}
			x = ((x & 0x3FF) << 10) + (y & 0x3FF) + 0x10000;
		}
		return x;
	}

	/*
	 * Unread the provided code point. It will be returned by the
	 * next NextChar() call. Beware that there may be only one
	 * "unread" code point at a time; moreover, when a LF is
	 * returned by NextChar(), there may be an unread code point.
	 */
	void Unread(int x)
	{
		delayedChar = x;
	}

	/*
	 * A backquote was read, for a literal character constant. This
	 * method reads the rest of the constant; returned value is
	 * a string consisting in a backquote character followed by
	 * a single code point (which may use a surrogate pair). This
	 * method interprets escape sequences.
	 */
	string ReadLiteralChar()
	{
		StringBuilder sb = new StringBuilder();
		sb.Append('`');
		int x = NextChar();
		if (x < 0 || IsWS(x)) {
			throw new Exception("invalid literal character");
		}
		if (x != '\\') {
			AppendCP(sb, x);
		} else {
			x = NextChar();
			if (x < 0) {
				throw new Exception("invalid literal character");
			}
			ParseEscape(sb, x);
		}
		return sb.ToString();
	}

	/*
	 * A double-quote was read, for a literal string. This method
	 * reads the rest of the string. Returned value is a string that
	 * starts with a double-quote, followed by the string contents;
	 * escape sequences are interpreted by this method.
	 */
	string ReadLiteralString()
	{
		StringBuilder sb = new StringBuilder();
		sb.Append('"');
		for (;;) {
			int x = NextChar();
			if (x < 0) {
				throw new Exception("unfinished literal string");
			}
			if (x == '"') {
				return sb.ToString();
			}
			if (x != '\\') {
				sb.Append((char)x);
				continue;
			}
			x = NextChar();
			if (x < 0) {
				throw new Exception("unfinished literal string");
			}
			if (x == '\n') {
				for (;;) {
					x = NextChar();
					if (x < 0) {
						throw new Exception("unfinished literal string");
					}
					if (x == '"') {
						break;
					}
					if (x == '\n' || !IsWS(x)) {
						throw new Exception("invalid newline escape");
					}
				}
			} else {
				ParseEscape(sb, x);
			}
		}
	}

	void ParseEscape(StringBuilder sb, int x)
	{
		switch (x) {
		case 's':   x = ' '; break;
		case 't':   x = '\t'; break;
		case 'r':   x = '\r'; break;
		case 'n':   x = '\n'; break;
		case '\'':  break;
		case '`':   break;
		case '"':   break;
		case '\\':  break;
		case 'x':
			x = ReadEscapeHex(2);
			break;
		case 'u':
			x = ReadEscapeHex(4);
			break;
		case 'U':
			x = ReadEscapeHex(4);
			break;
		default:
			throw new Exception(string.Format("invalid escape U+{0:X4}", x));
		}

		AppendCP(sb, x);
	}

	int ReadEscapeHex(int num)
	{
		int v = 0;
		while (num -- > 0) {
			int x = NextChar();
			if (x >= '0' && x <= '9') {
				x -= '0';
			} else if (x >= 'A' && x <= 'F') {
				x -= ('A' - 10);
			} else if (x >= 'a' && x <= 'f') {
				x -= ('a' - 10);
			} else {
				if (x < 0) {
					throw new Exception("end-of-stream in hexadecimal data");
				} else {
					throw new Exception(string.Format("invalid hex digit U+{0:X4}", x));
				}
			}
			v = (v << 4) + x;
		}
		return v;
	}

	/*
	 * Whitespace is any code point between 0 and 32, inclusive
	 * (i.e. all ASCII control characters, and ASCII space).
	 */
	static bool IsWS(int cp)
	{
		return cp <= 32;
	}

	static bool IsSingleCharToken(int cp)
	{
		return cp == '(' || cp == ')' || cp == '[' || cp == ']'
			|| cp == '{' || cp == '}' || cp == '\'';
	}

	static bool IsWordChar(int cp)
	{
		return cp >= 33 && cp <= 126 && !IsSingleCharToken(cp)
			&& cp != '"' && cp != '`' && cp != '#';
	}
}
