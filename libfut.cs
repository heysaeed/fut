// Generated automatically with "fut". Do not edit.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
namespace Fusion
{

	public abstract class FuParserHost
	{

		public abstract void ReportError(string filename, int startLine, int startColumn, int endLine, int endColumn, string message);
	}

	public enum FuToken
	{
		EndOfFile,
		Id,
		LiteralLong,
		LiteralDouble,
		LiteralChar,
		LiteralString,
		InterpolatedString,
		Semicolon,
		Dot,
		Comma,
		LeftParenthesis,
		RightParenthesis,
		LeftBracket,
		RightBracket,
		LeftBrace,
		RightBrace,
		Plus,
		Minus,
		Asterisk,
		Slash,
		Mod,
		And,
		Or,
		Xor,
		Tilde,
		ShiftLeft,
		ShiftRight,
		Equal,
		NotEqual,
		Less,
		LessOrEqual,
		Greater,
		GreaterOrEqual,
		RightAngle,
		CondAnd,
		CondOr,
		ExclamationMark,
		Hash,
		Assign,
		AddAssign,
		SubAssign,
		MulAssign,
		DivAssign,
		ModAssign,
		AndAssign,
		OrAssign,
		XorAssign,
		ShiftLeftAssign,
		ShiftRightAssign,
		Increment,
		Decrement,
		QuestionMark,
		Colon,
		FatArrow,
		Range,
		DocRegular,
		DocBullet,
		DocBlank,
		Abstract,
		Assert,
		Break,
		Case,
		Class,
		Const,
		Continue,
		Default,
		Do,
		Else,
		Enum,
		False,
		For,
		Foreach,
		If,
		In,
		Internal,
		Is,
		Lock_,
		Native,
		New,
		Null,
		Override,
		Protected,
		Public,
		Resource,
		Return,
		Sealed,
		Static,
		Switch,
		Throw,
		Throws,
		True,
		Virtual,
		Void,
		When,
		While,
		EndOfLine,
		PreUnknown,
		PreIf,
		PreElIf,
		PreElse,
		PreEndIf
	}

	enum FuPreState
	{
		NotYet,
		Already,
		AlreadyElse
	}

	public abstract class FuLexer
	{

		protected byte[] Input;

		int InputLength;

		int NextOffset;

		protected int CharOffset;

		int NextChar;

		FuParserHost Host;

		protected string Filename;

		protected int Line;

		protected int Column;

		protected int TokenColumn;

		protected int LexemeOffset;

		protected FuToken CurrentToken;

		protected long LongValue;

		protected string StringValue;

		readonly HashSet<string> PreSymbols = new HashSet<string>();

		bool AtLineStart = true;

		bool LineMode = false;

		bool EnableDocComments = true;

		protected bool ParsingTypeArg = false;

		readonly Stack<bool> PreElseStack = new Stack<bool>();

		public void SetHost(FuParserHost host)
		{
			this.Host = host;
		}

		public void AddPreSymbol(string symbol)
		{
			this.PreSymbols.Add(symbol);
		}

		protected void Open(string filename, byte[] input, int inputLength)
		{
			this.Filename = filename;
			this.Input = input;
			this.InputLength = inputLength;
			this.NextOffset = 0;
			this.Line = 1;
			this.Column = 1;
			FillNextChar();
			if (this.NextChar == 65279)
				FillNextChar();
			NextToken();
		}

		protected void ReportError(string message)
		{
			this.Host.ReportError(this.Filename, this.Line, this.TokenColumn, this.Line, this.Column, message);
		}

		int ReadByte()
		{
			if (this.NextOffset >= this.InputLength)
				return -1;
			return this.Input[this.NextOffset++];
		}

		const int ReplacementChar = 65533;

		int ReadContinuationByte(int hi)
		{
			int b = ReadByte();
			if (hi != 65533) {
				if (b >= 128 && b <= 191)
					return (hi << 6) + b - 128;
				ReportError("Invalid UTF-8");
			}
			return 65533;
		}

		void FillNextChar()
		{
			this.CharOffset = this.NextOffset;
			int b = ReadByte();
			if (b >= 128) {
				if (b < 194 || b > 244) {
					ReportError("Invalid UTF-8");
					b = 65533;
				}
				else if (b < 224)
					b = ReadContinuationByte(b - 192);
				else if (b < 240) {
					b = ReadContinuationByte(b - 224);
					b = ReadContinuationByte(b);
				}
				else {
					b = ReadContinuationByte(b - 240);
					b = ReadContinuationByte(b);
					b = ReadContinuationByte(b);
				}
			}
			this.NextChar = b;
		}

		protected int PeekChar() => this.NextChar;

		internal static bool IsLetterOrDigit(int c)
		{
			if (c >= 'a' && c <= 'z')
				return true;
			if (c >= 'A' && c <= 'Z')
				return true;
			if (c >= '0' && c <= '9')
				return true;
			return c == '_';
		}

		protected int ReadChar()
		{
			int c = this.NextChar;
			switch (c) {
			case '\t':
			case ' ':
				this.Column++;
				break;
			case '\n':
				this.Line++;
				this.Column = 1;
				this.AtLineStart = true;
				break;
			default:
				this.Column++;
				this.AtLineStart = false;
				break;
			}
			FillNextChar();
			return c;
		}

		bool EatChar(int c)
		{
			if (PeekChar() == c) {
				ReadChar();
				return true;
			}
			return false;
		}

		void SkipWhitespace()
		{
			while (PeekChar() == '\t' || PeekChar() == ' ' || PeekChar() == '\r')
				ReadChar();
		}

		FuToken ReadIntegerLiteral(int bits)
		{
			bool invalidDigit = false;
			bool tooBig = false;
			bool needDigit = true;
			for (long i = 0;; ReadChar()) {
				int c = PeekChar();
				if (c >= '0' && c <= '9')
					c -= '0';
				else if (c >= 'A' && c <= 'Z')
					c -= 55;
				else if (c >= 'a' && c <= 'z')
					c -= 87;
				else if (c == '_') {
					needDigit = true;
					continue;
				}
				else {
					this.LongValue = i;
					if (invalidDigit || needDigit)
						ReportError("Invalid integer");
					else if (tooBig)
						ReportError("Integer too big");
					return FuToken.LiteralLong;
				}
				if (c >= 1 << bits)
					invalidDigit = true;
				else if (i >> (64 - bits) != 0)
					tooBig = true;
				else
					i = (i << bits) + c;
				needDigit = false;
			}
		}

		FuToken ReadFloatLiteral(bool needDigit)
		{
			bool underscoreE = false;
			bool exponent = false;
			for (;;) {
				int c = PeekChar();
				switch (c) {
				case '0':
				case '1':
				case '2':
				case '3':
				case '4':
				case '5':
				case '6':
				case '7':
				case '8':
				case '9':
					ReadChar();
					needDigit = false;
					break;
				case 'E':
				case 'e':
					if (exponent) {
						ReportError("Invalid floating-point number");
						return FuToken.LiteralDouble;
					}
					if (needDigit)
						underscoreE = true;
					ReadChar();
					c = PeekChar();
					if (c == '+' || c == '-')
						ReadChar();
					exponent = true;
					needDigit = true;
					break;
				case '_':
					ReadChar();
					needDigit = true;
					break;
				default:
					if (underscoreE || needDigit || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
						ReportError("Invalid floating-point number");
					return FuToken.LiteralDouble;
				}
			}
		}

		FuToken ReadNumberLiteral(long i)
		{
			bool leadingZero = false;
			bool tooBig = false;
			for (bool needDigit = false;; ReadChar()) {
				int c = PeekChar();
				switch (c) {
				case '0':
				case '1':
				case '2':
				case '3':
				case '4':
				case '5':
				case '6':
				case '7':
				case '8':
				case '9':
					c -= '0';
					break;
				case '.':
					ReadChar();
					return ReadFloatLiteral(true);
				case 'e':
				case 'E':
					return ReadFloatLiteral(needDigit);
				case '_':
					needDigit = true;
					continue;
				default:
					this.LongValue = i;
					if (leadingZero)
						ReportError("Leading zeros are not permitted, octal numbers must begin with 0o");
					if (needDigit || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
						ReportError("Invalid integer");
					else if (tooBig)
						ReportError("Integer too big");
					return FuToken.LiteralLong;
				}
				if (i == 0)
					leadingZero = true;
				if (i > (c < 8 ? 922337203685477580 : 922337203685477579))
					tooBig = true;
				else
					i = 10 * i + c;
				needDigit = false;
			}
		}

		internal static int GetEscapedChar(int c)
		{
			switch (c) {
			case '"':
				return '"';
			case '\'':
				return '\'';
			case '\\':
				return '\\';
			case 'n':
				return '\n';
			case 'r':
				return '\r';
			case 't':
				return '\t';
			default:
				return -1;
			}
		}

		int ReadCharLiteral()
		{
			int c = ReadChar();
			if (c < 32) {
				ReportError("Invalid character in literal");
				return 65533;
			}
			if (c != '\\')
				return c;
			c = GetEscapedChar(ReadChar());
			if (c < 0) {
				ReportError("Unknown escape sequence");
				return 65533;
			}
			return c;
		}

		protected FuToken ReadString(bool interpolated)
		{
			for (int offset = this.CharOffset;; ReadCharLiteral()) {
				switch (PeekChar()) {
				case -1:
					ReportError("Unterminated string literal");
					return FuToken.EndOfFile;
				case '\n':
					ReportError("Unterminated string literal");
					this.StringValue = "";
					return FuToken.LiteralString;
				case '"':
					{
						int endOffset = this.CharOffset;
						ReadChar();
						this.StringValue = Encoding.UTF8.GetString(this.Input, offset, endOffset - offset);
					}
					return FuToken.LiteralString;
				case '{':
					if (interpolated) {
						int endOffset = this.CharOffset;
						ReadChar();
						if (PeekChar() != '{') {
							this.StringValue = Encoding.UTF8.GetString(this.Input, offset, endOffset - offset);
							return FuToken.InterpolatedString;
						}
					}
					break;
				default:
					break;
				}
			}
		}

		bool EndWord(int c) => EatChar(c) && !IsLetterOrDigit(PeekChar());

		protected string GetLexeme() => Encoding.UTF8.GetString(this.Input, this.LexemeOffset, this.CharOffset - this.LexemeOffset);

		FuToken ReadPreToken()
		{
			for (;;) {
				bool atLineStart = this.AtLineStart;
				this.TokenColumn = this.Column;
				this.LexemeOffset = this.CharOffset;
				int c = ReadChar();
				switch (c) {
				case -1:
					return FuToken.EndOfFile;
				case '\t':
				case '\r':
				case ' ':
					break;
				case '\n':
					if (this.LineMode)
						return FuToken.EndOfLine;
					break;
				case '#':
					if (!atLineStart)
						return FuToken.Hash;
					switch (PeekChar()) {
					case 'i':
						ReadChar();
						return EndWord('f') ? FuToken.PreIf : FuToken.PreUnknown;
					case 'e':
						ReadChar();
						switch (PeekChar()) {
						case 'l':
							ReadChar();
							switch (PeekChar()) {
							case 'i':
								ReadChar();
								return EndWord('f') ? FuToken.PreElIf : FuToken.PreUnknown;
							case 's':
								ReadChar();
								return EndWord('e') ? FuToken.PreElse : FuToken.PreUnknown;
							default:
								return FuToken.PreUnknown;
							}
						case 'n':
							ReadChar();
							return EatChar('d') && EatChar('i') && EndWord('f') ? FuToken.PreEndIf : FuToken.PreUnknown;
						default:
							return FuToken.PreUnknown;
						}
					default:
						return FuToken.PreUnknown;
					}
				case ';':
					return FuToken.Semicolon;
				case '.':
					if (EatChar('.'))
						return FuToken.Range;
					return FuToken.Dot;
				case ',':
					return FuToken.Comma;
				case '(':
					return FuToken.LeftParenthesis;
				case ')':
					return FuToken.RightParenthesis;
				case '[':
					return FuToken.LeftBracket;
				case ']':
					return FuToken.RightBracket;
				case '{':
					return FuToken.LeftBrace;
				case '}':
					return FuToken.RightBrace;
				case '~':
					return FuToken.Tilde;
				case '?':
					return FuToken.QuestionMark;
				case ':':
					return FuToken.Colon;
				case '+':
					if (EatChar('+'))
						return FuToken.Increment;
					if (EatChar('='))
						return FuToken.AddAssign;
					return FuToken.Plus;
				case '-':
					if (EatChar('-'))
						return FuToken.Decrement;
					if (EatChar('='))
						return FuToken.SubAssign;
					return FuToken.Minus;
				case '*':
					if (EatChar('='))
						return FuToken.MulAssign;
					return FuToken.Asterisk;
				case '/':
					if (EatChar('/')) {
						c = ReadChar();
						if (c == '/' && this.EnableDocComments) {
							SkipWhitespace();
							switch (PeekChar()) {
							case '\n':
								return FuToken.DocBlank;
							case '*':
								ReadChar();
								SkipWhitespace();
								return FuToken.DocBullet;
							default:
								return FuToken.DocRegular;
							}
						}
						while (c != '\n' && c >= 0)
							c = ReadChar();
						if (c == '\n' && this.LineMode)
							return FuToken.EndOfLine;
						break;
					}
					if (EatChar('*')) {
						int startLine = this.Line;
						do {
							c = ReadChar();
							if (c < 0) {
								ReportError($"Unterminated multi-line comment, started in line {startLine}");
								return FuToken.EndOfFile;
							}
						}
						while (c != '*' || PeekChar() != '/');
						ReadChar();
						break;
					}
					if (EatChar('='))
						return FuToken.DivAssign;
					return FuToken.Slash;
				case '%':
					if (EatChar('='))
						return FuToken.ModAssign;
					return FuToken.Mod;
				case '&':
					if (EatChar('&'))
						return FuToken.CondAnd;
					if (EatChar('='))
						return FuToken.AndAssign;
					return FuToken.And;
				case '|':
					if (EatChar('|'))
						return FuToken.CondOr;
					if (EatChar('='))
						return FuToken.OrAssign;
					return FuToken.Or;
				case '^':
					if (EatChar('='))
						return FuToken.XorAssign;
					return FuToken.Xor;
				case '=':
					if (EatChar('='))
						return FuToken.Equal;
					if (EatChar('>'))
						return FuToken.FatArrow;
					return FuToken.Assign;
				case '!':
					if (EatChar('='))
						return FuToken.NotEqual;
					return FuToken.ExclamationMark;
				case '<':
					if (EatChar('<')) {
						if (EatChar('='))
							return FuToken.ShiftLeftAssign;
						return FuToken.ShiftLeft;
					}
					if (EatChar('='))
						return FuToken.LessOrEqual;
					return FuToken.Less;
				case '>':
					if (this.ParsingTypeArg)
						return FuToken.RightAngle;
					if (EatChar('>')) {
						if (EatChar('='))
							return FuToken.ShiftRightAssign;
						return FuToken.ShiftRight;
					}
					if (EatChar('='))
						return FuToken.GreaterOrEqual;
					return FuToken.Greater;
				case '\'':
					if (PeekChar() == '\'') {
						ReportError("Empty character literal");
						this.LongValue = 0;
					}
					else
						this.LongValue = ReadCharLiteral();
					if (!EatChar('\''))
						ReportError("Unterminated character literal");
					return FuToken.LiteralChar;
				case '"':
					return ReadString(false);
				case '$':
					if (EatChar('"'))
						return ReadString(true);
					ReportError("Expected interpolated string");
					break;
				case '0':
					switch (PeekChar()) {
					case 'B':
					case 'b':
						ReadChar();
						return ReadIntegerLiteral(1);
					case 'O':
					case 'o':
						ReadChar();
						return ReadIntegerLiteral(3);
					case 'X':
					case 'x':
						ReadChar();
						return ReadIntegerLiteral(4);
					default:
						return ReadNumberLiteral(0);
					}
				case '1':
				case '2':
				case '3':
				case '4':
				case '5':
				case '6':
				case '7':
				case '8':
				case '9':
					return ReadNumberLiteral(c - '0');
				default:
					if (!IsLetterOrDigit(c)) {
						ReportError("Invalid character");
						continue;
					}
					while (IsLetterOrDigit(PeekChar()))
						ReadChar();
					this.StringValue = GetLexeme();
					switch (this.StringValue) {
					case "abstract":
						return FuToken.Abstract;
					case "assert":
						return FuToken.Assert;
					case "break":
						return FuToken.Break;
					case "case":
						return FuToken.Case;
					case "class":
						return FuToken.Class;
					case "const":
						return FuToken.Const;
					case "continue":
						return FuToken.Continue;
					case "default":
						return FuToken.Default;
					case "do":
						return FuToken.Do;
					case "else":
						return FuToken.Else;
					case "enum":
						return FuToken.Enum;
					case "false":
						return FuToken.False;
					case "for":
						return FuToken.For;
					case "foreach":
						return FuToken.Foreach;
					case "if":
						return FuToken.If;
					case "in":
						return FuToken.In;
					case "internal":
						return FuToken.Internal;
					case "is":
						return FuToken.Is;
					case "lock":
						return FuToken.Lock_;
					case "native":
						return FuToken.Native;
					case "new":
						return FuToken.New;
					case "null":
						return FuToken.Null;
					case "override":
						return FuToken.Override;
					case "protected":
						return FuToken.Protected;
					case "public":
						return FuToken.Public;
					case "resource":
						return FuToken.Resource;
					case "return":
						return FuToken.Return;
					case "sealed":
						return FuToken.Sealed;
					case "static":
						return FuToken.Static;
					case "switch":
						return FuToken.Switch;
					case "throw":
						return FuToken.Throw;
					case "throws":
						return FuToken.Throws;
					case "true":
						return FuToken.True;
					case "virtual":
						return FuToken.Virtual;
					case "void":
						return FuToken.Void;
					case "when":
						return FuToken.When;
					case "while":
						return FuToken.While;
					default:
						return FuToken.Id;
					}
				}
			}
		}

		void NextPreToken()
		{
			this.CurrentToken = ReadPreToken();
		}

		protected bool See(FuToken token) => this.CurrentToken == token;

		internal static string TokenToString(FuToken token)
		{
			switch (token) {
			case FuToken.EndOfFile:
				return "end-of-file";
			case FuToken.Id:
				return "identifier";
			case FuToken.LiteralLong:
				return "integer constant";
			case FuToken.LiteralDouble:
				return "floating-point constant";
			case FuToken.LiteralChar:
				return "character constant";
			case FuToken.LiteralString:
				return "string constant";
			case FuToken.InterpolatedString:
				return "interpolated string";
			case FuToken.Semicolon:
				return "';'";
			case FuToken.Dot:
				return "'.'";
			case FuToken.Comma:
				return "','";
			case FuToken.LeftParenthesis:
				return "'('";
			case FuToken.RightParenthesis:
				return "')'";
			case FuToken.LeftBracket:
				return "'['";
			case FuToken.RightBracket:
				return "']'";
			case FuToken.LeftBrace:
				return "'{'";
			case FuToken.RightBrace:
				return "'}'";
			case FuToken.Plus:
				return "'+'";
			case FuToken.Minus:
				return "'-'";
			case FuToken.Asterisk:
				return "'*'";
			case FuToken.Slash:
				return "'/'";
			case FuToken.Mod:
				return "'%'";
			case FuToken.And:
				return "'&'";
			case FuToken.Or:
				return "'|'";
			case FuToken.Xor:
				return "'^'";
			case FuToken.Tilde:
				return "'~'";
			case FuToken.ShiftLeft:
				return "'<<'";
			case FuToken.ShiftRight:
				return "'>>'";
			case FuToken.Equal:
				return "'=='";
			case FuToken.NotEqual:
				return "'!='";
			case FuToken.Less:
				return "'<'";
			case FuToken.LessOrEqual:
				return "'<='";
			case FuToken.Greater:
				return "'>'";
			case FuToken.GreaterOrEqual:
				return "'>='";
			case FuToken.RightAngle:
				return "'>'";
			case FuToken.CondAnd:
				return "'&&'";
			case FuToken.CondOr:
				return "'||'";
			case FuToken.ExclamationMark:
				return "'!'";
			case FuToken.Hash:
				return "'#'";
			case FuToken.Assign:
				return "'='";
			case FuToken.AddAssign:
				return "'+='";
			case FuToken.SubAssign:
				return "'-='";
			case FuToken.MulAssign:
				return "'*='";
			case FuToken.DivAssign:
				return "'/='";
			case FuToken.ModAssign:
				return "'%='";
			case FuToken.AndAssign:
				return "'&='";
			case FuToken.OrAssign:
				return "'|='";
			case FuToken.XorAssign:
				return "'^='";
			case FuToken.ShiftLeftAssign:
				return "'<<='";
			case FuToken.ShiftRightAssign:
				return "'>>='";
			case FuToken.Increment:
				return "'++'";
			case FuToken.Decrement:
				return "'--'";
			case FuToken.QuestionMark:
				return "'?'";
			case FuToken.Colon:
				return "':'";
			case FuToken.FatArrow:
				return "'=>'";
			case FuToken.Range:
				return "'..'";
			case FuToken.DocRegular:
			case FuToken.DocBullet:
			case FuToken.DocBlank:
				return "'///'";
			case FuToken.Abstract:
				return "'abstract'";
			case FuToken.Assert:
				return "'assert'";
			case FuToken.Break:
				return "'break'";
			case FuToken.Case:
				return "'case'";
			case FuToken.Class:
				return "'class'";
			case FuToken.Const:
				return "'const'";
			case FuToken.Continue:
				return "'continue'";
			case FuToken.Default:
				return "'default'";
			case FuToken.Do:
				return "'do'";
			case FuToken.Else:
				return "'else'";
			case FuToken.Enum:
				return "'enum'";
			case FuToken.False:
				return "'false'";
			case FuToken.For:
				return "'for'";
			case FuToken.Foreach:
				return "'foreach'";
			case FuToken.If:
				return "'if'";
			case FuToken.In:
				return "'in'";
			case FuToken.Internal:
				return "'internal'";
			case FuToken.Is:
				return "'is'";
			case FuToken.Lock_:
				return "'lock'";
			case FuToken.Native:
				return "'native'";
			case FuToken.New:
				return "'new'";
			case FuToken.Null:
				return "'null'";
			case FuToken.Override:
				return "'override'";
			case FuToken.Protected:
				return "'protected'";
			case FuToken.Public:
				return "'public'";
			case FuToken.Resource:
				return "'resource'";
			case FuToken.Return:
				return "'return'";
			case FuToken.Sealed:
				return "'sealed'";
			case FuToken.Static:
				return "'static'";
			case FuToken.Switch:
				return "'switch'";
			case FuToken.Throw:
				return "'throw'";
			case FuToken.Throws:
				return "'throws'";
			case FuToken.True:
				return "'true'";
			case FuToken.Virtual:
				return "'virtual'";
			case FuToken.Void:
				return "'void'";
			case FuToken.When:
				return "'when'";
			case FuToken.While:
				return "'while'";
			case FuToken.EndOfLine:
				return "end-of-line";
			case FuToken.PreUnknown:
				return "unknown preprocessor directive";
			case FuToken.PreIf:
				return "'#if'";
			case FuToken.PreElIf:
				return "'#elif'";
			case FuToken.PreElse:
				return "'#else'";
			case FuToken.PreEndIf:
				return "'#endif'";
			default:
				throw new NotImplementedException();
			}
		}

		protected bool Check(FuToken expected)
		{
			if (See(expected))
				return true;
			ReportError($"Expected {TokenToString(expected)}, got {TokenToString(this.CurrentToken)}");
			return false;
		}

		bool EatPre(FuToken token)
		{
			if (See(token)) {
				NextPreToken();
				return true;
			}
			return false;
		}

		bool ParsePrePrimary()
		{
			if (EatPre(FuToken.ExclamationMark))
				return !ParsePrePrimary();
			if (EatPre(FuToken.LeftParenthesis)) {
				bool result = ParsePreOr();
				Check(FuToken.RightParenthesis);
				NextPreToken();
				return result;
			}
			if (See(FuToken.Id)) {
				bool result = this.PreSymbols.Contains(this.StringValue);
				NextPreToken();
				return result;
			}
			if (EatPre(FuToken.False))
				return false;
			if (EatPre(FuToken.True))
				return true;
			ReportError("Invalid preprocessor expression");
			return false;
		}

		bool ParsePreEquality()
		{
			bool result = ParsePrePrimary();
			for (;;) {
				if (EatPre(FuToken.Equal))
					result = result == ParsePrePrimary();
				else if (EatPre(FuToken.NotEqual))
					result ^= ParsePrePrimary();
				else
					return result;
			}
		}

		bool ParsePreAnd()
		{
			bool result = ParsePreEquality();
			while (EatPre(FuToken.CondAnd))
				result &= ParsePreEquality();
			return result;
		}

		bool ParsePreOr()
		{
			bool result = ParsePreAnd();
			while (EatPre(FuToken.CondOr))
				result |= ParsePreAnd();
			return result;
		}

		bool ParsePreExpr()
		{
			this.LineMode = true;
			NextPreToken();
			bool result = ParsePreOr();
			Check(FuToken.EndOfLine);
			this.LineMode = false;
			return result;
		}

		void ExpectEndOfLine(string directive)
		{
			this.LineMode = true;
			FuToken token = ReadPreToken();
			if (token != FuToken.EndOfLine && token != FuToken.EndOfFile)
				ReportError($"Unexpected characters after '{directive}'");
			this.LineMode = false;
		}

		bool PopPreElse(string directive)
		{
			if (this.PreElseStack.Count == 0) {
				ReportError($"'{directive}' with no matching '#if'");
				return false;
			}
			if (this.PreElseStack.Pop() && directive != "#endif")
				ReportError($"'{directive}' after '#else'");
			return true;
		}

		void SkipUnmet(FuPreState state)
		{
			this.EnableDocComments = false;
			for (;;) {
				switch (ReadPreToken()) {
				case FuToken.EndOfFile:
					ReportError("Expected '#endif', got end-of-file");
					return;
				case FuToken.PreIf:
					ParsePreExpr();
					SkipUnmet(FuPreState.Already);
					break;
				case FuToken.PreElIf:
					if (state == FuPreState.AlreadyElse)
						ReportError("'#elif' after '#else'");
					if (ParsePreExpr() && state == FuPreState.NotYet) {
						this.PreElseStack.Push(false);
						return;
					}
					break;
				case FuToken.PreElse:
					if (state == FuPreState.AlreadyElse)
						ReportError("'#else' after '#else'");
					ExpectEndOfLine("#else");
					if (state == FuPreState.NotYet) {
						this.PreElseStack.Push(true);
						return;
					}
					state = FuPreState.AlreadyElse;
					break;
				case FuToken.PreEndIf:
					ExpectEndOfLine("#endif");
					return;
				default:
					break;
				}
			}
		}

		FuToken ReadToken()
		{
			for (;;) {
				this.EnableDocComments = true;
				FuToken token = ReadPreToken();
				bool matched;
				switch (token) {
				case FuToken.EndOfFile:
					if (this.PreElseStack.Count != 0)
						ReportError("Expected '#endif', got end-of-file");
					return FuToken.EndOfFile;
				case FuToken.PreIf:
					if (ParsePreExpr())
						this.PreElseStack.Push(false);
					else
						SkipUnmet(FuPreState.NotYet);
					break;
				case FuToken.PreElIf:
					matched = PopPreElse("#elif");
					ParsePreExpr();
					if (matched)
						SkipUnmet(FuPreState.Already);
					break;
				case FuToken.PreElse:
					matched = PopPreElse("#else");
					ExpectEndOfLine("#else");
					if (matched)
						SkipUnmet(FuPreState.AlreadyElse);
					break;
				case FuToken.PreEndIf:
					PopPreElse("#endif");
					ExpectEndOfLine("#endif");
					break;
				default:
					return token;
				}
			}
		}

		protected FuToken NextToken()
		{
			FuToken token = this.CurrentToken;
			this.CurrentToken = ReadToken();
			return token;
		}

		protected bool Eat(FuToken token)
		{
			if (See(token)) {
				NextToken();
				return true;
			}
			return false;
		}

		protected bool Expect(FuToken expected)
		{
			bool found = Check(expected);
			NextToken();
			return found;
		}

		protected void ExpectOrSkip(FuToken expected)
		{
			if (Check(expected))
				NextToken();
			else {
				do
					NextToken();
				while (!See(FuToken.EndOfFile) && !Eat(expected));
			}
		}
	}

	public enum FuVisibility
	{
		Private,
		Internal,
		Protected,
		Public,
		NumericElementType,
		FinalValueType
	}

	public enum FuCallType
	{
		Static,
		Normal,
		Abstract,
		Virtual,
		Override,
		Sealed
	}

	public enum FuPriority
	{
		Statement,
		Argument,
		Assign,
		Select,
		SelectCond,
		CondOr,
		CondAnd,
		Or,
		Xor,
		And,
		Equality,
		Rel,
		Shift,
		Add,
		Mul,
		Primary
	}

	public enum FuId
	{
		None,
		VoidType,
		NullType,
		BasePtr,
		TypeParam0,
		TypeParam0NotFinal,
		TypeParam0Predicate,
		SByteRange,
		ByteRange,
		ShortRange,
		UShortRange,
		IntType,
		LongType,
		FloatType,
		DoubleType,
		FloatIntType,
		BoolType,
		StringClass,
		StringPtrType,
		StringStorageType,
		ArrayPtrClass,
		ArrayStorageClass,
		ListClass,
		QueueClass,
		StackClass,
		HashSetClass,
		SortedSetClass,
		DictionaryClass,
		SortedDictionaryClass,
		OrderedDictionaryClass,
		TextWriterClass,
		StringWriterClass,
		RegexOptionsEnum,
		RegexClass,
		MatchClass,
		LockClass,
		StringLength,
		ArrayLength,
		ConsoleError,
		ClassToString,
		MatchStart,
		MatchEnd,
		MatchLength,
		MatchValue,
		MathNaN,
		MathNegativeInfinity,
		MathPositiveInfinity,
		EnumFromInt,
		EnumHasFlag,
		IntTryParse,
		LongTryParse,
		DoubleTryParse,
		StringContains,
		StringEndsWith,
		StringIndexOf,
		StringLastIndexOf,
		StringReplace,
		StringStartsWith,
		StringSubstring,
		ArrayBinarySearchAll,
		ArrayBinarySearchPart,
		ArrayContains,
		ArrayCopyTo,
		ArrayFillAll,
		ArrayFillPart,
		ArraySortAll,
		ArraySortPart,
		ListAdd,
		ListAddRange,
		ListAll,
		ListAny,
		ListClear,
		ListContains,
		ListCopyTo,
		ListCount,
		ListIndexOf,
		ListInsert,
		ListLast,
		ListRemoveAt,
		ListRemoveRange,
		ListSortAll,
		ListSortPart,
		QueueClear,
		QueueCount,
		QueueDequeue,
		QueueEnqueue,
		QueuePeek,
		StackClear,
		StackCount,
		StackPeek,
		StackPush,
		StackPop,
		HashSetAdd,
		HashSetClear,
		HashSetContains,
		HashSetCount,
		HashSetRemove,
		SortedSetAdd,
		SortedSetClear,
		SortedSetContains,
		SortedSetCount,
		SortedSetRemove,
		DictionaryAdd,
		DictionaryClear,
		DictionaryContainsKey,
		DictionaryCount,
		DictionaryRemove,
		SortedDictionaryClear,
		SortedDictionaryContainsKey,
		SortedDictionaryCount,
		SortedDictionaryRemove,
		OrderedDictionaryClear,
		OrderedDictionaryContainsKey,
		OrderedDictionaryCount,
		OrderedDictionaryRemove,
		TextWriterWrite,
		TextWriterWriteChar,
		TextWriterWriteCodePoint,
		TextWriterWriteLine,
		ConsoleWrite,
		ConsoleWriteLine,
		StringWriterClear,
		StringWriterToString,
		UTF8GetByteCount,
		UTF8GetBytes,
		UTF8GetString,
		EnvironmentGetEnvironmentVariable,
		RegexCompile,
		RegexEscape,
		RegexIsMatchStr,
		RegexIsMatchRegex,
		MatchFindStr,
		MatchFindRegex,
		MatchGetCapture,
		MathMethod,
		MathAbs,
		MathCeiling,
		MathClamp,
		MathFusedMultiplyAdd,
		MathIsFinite,
		MathIsInfinity,
		MathIsNaN,
		MathLog2,
		MathMaxInt,
		MathMaxDouble,
		MathMinInt,
		MathMinDouble,
		MathRound,
		MathTruncate
	}

	abstract class FuDocInline
	{
	}

	class FuDocText : FuDocInline
	{

		internal string Text;
	}

	class FuDocCode : FuDocInline
	{

		internal string Text;
	}

	class FuDocLine : FuDocInline
	{
	}

	public abstract class FuDocBlock
	{
	}

	public class FuDocPara : FuDocBlock
	{

		internal readonly List<FuDocInline> Children = new List<FuDocInline>();
	}

	public class FuDocList : FuDocBlock
	{

		internal readonly List<FuDocPara> Items = new List<FuDocPara>();
	}

	public class FuCodeDoc
	{

		internal readonly FuDocPara Summary = new FuDocPara();

		internal readonly List<FuDocBlock> Details = new List<FuDocBlock>();
	}

	public abstract class FuVisitor
	{

		protected void VisitOptionalStatement(FuStatement statement)
		{
			if (statement != null)
				statement.AcceptStatement(this);
		}

		internal abstract void VisitConst(FuConst statement);

		internal abstract void VisitExpr(FuExpr statement);

		internal abstract void VisitBlock(FuBlock statement);

		internal abstract void VisitAssert(FuAssert statement);

		internal abstract void VisitBreak(FuBreak statement);

		internal abstract void VisitContinue(FuContinue statement);

		internal abstract void VisitDoWhile(FuDoWhile statement);

		internal abstract void VisitFor(FuFor statement);

		internal abstract void VisitForeach(FuForeach statement);

		internal abstract void VisitIf(FuIf statement);

		internal abstract void VisitLock(FuLock statement);

		internal abstract void VisitNative(FuNative statement);

		internal abstract void VisitReturn(FuReturn statement);

		internal abstract void VisitSwitch(FuSwitch statement);

		internal abstract void VisitThrow(FuThrow statement);

		internal abstract void VisitWhile(FuWhile statement);

		internal abstract void VisitEnumValue(FuConst konst, FuConst previous);

		internal abstract void VisitLiteralNull();

		internal abstract void VisitLiteralFalse();

		internal abstract void VisitLiteralTrue();

		internal abstract void VisitLiteralLong(long value);

		internal abstract void VisitLiteralChar(int value);

		internal abstract void VisitLiteralDouble(double value);

		internal abstract void VisitLiteralString(string value);

		internal abstract void VisitAggregateInitializer(FuAggregateInitializer expr);

		internal abstract void VisitInterpolatedString(FuInterpolatedString expr, FuPriority parent);

		internal abstract void VisitSymbolReference(FuSymbolReference expr, FuPriority parent);

		internal abstract void VisitPrefixExpr(FuPrefixExpr expr, FuPriority parent);

		internal abstract void VisitPostfixExpr(FuPostfixExpr expr, FuPriority parent);

		internal abstract void VisitBinaryExpr(FuBinaryExpr expr, FuPriority parent);

		internal abstract void VisitSelectExpr(FuSelectExpr expr, FuPriority parent);

		internal abstract void VisitCallExpr(FuCallExpr expr, FuPriority parent);

		internal abstract void VisitLambdaExpr(FuLambdaExpr expr);

		internal abstract void VisitVar(FuVar expr);
	}

	public abstract class FuStatement
	{

		internal int Line;

		public abstract bool CompletesNormally();

		public abstract void AcceptStatement(FuVisitor visitor);
	}

	public abstract class FuExpr : FuStatement
	{

		internal FuType Type;

		public override bool CompletesNormally() => true;

		public override string ToString()
		{
			throw new NotImplementedException();
		}

		public virtual bool IsIndexing() => false;

		public virtual bool IsLiteralZero() => false;

		public virtual bool IsConstEnum() => false;

		public virtual int IntValue()
		{
			throw new NotImplementedException();
		}

		public virtual void Accept(FuVisitor visitor, FuPriority parent)
		{
			throw new NotImplementedException();
		}

		public override void AcceptStatement(FuVisitor visitor)
		{
			visitor.VisitExpr(this);
		}

		public virtual bool IsReferenceTo(FuSymbol symbol) => false;

		public virtual bool IsNewString(bool substringOffset) => false;
	}

	public abstract class FuSymbol : FuExpr
	{

		internal FuId Id = FuId.None;

		internal string Name;

		internal FuSymbol Next;

		internal FuScope Parent;

		internal FuCodeDoc Documentation = null;

		public override string ToString() => this.Name;
	}

	public class FuScope : FuSymbol
	{

		protected readonly Dictionary<string, FuSymbol> Dict = new Dictionary<string, FuSymbol>();

		internal FuSymbol First = null;

		FuSymbol Last;

		public int Count() => this.Dict.Count;

		public FuVar FirstParameter()
		{
			FuVar result = (FuVar) this.First;
			return result;
		}

		public FuContainerType GetContainer()
		{
			for (FuScope scope = this; scope != null; scope = scope.Parent) {
				if (scope is FuContainerType container)
					return container;
			}
			throw new NotImplementedException();
		}

		public bool Contains(FuSymbol symbol) => this.Dict.ContainsKey(symbol.Name);

		public FuSymbol TryLookup(string name, bool global)
		{
			for (FuScope scope = this; scope != null && (global || !(scope is FuProgram || scope is FuSystem)); scope = scope.Parent) {
				if (scope.Dict.ContainsKey(name))
					return scope.Dict[name];
			}
			return null;
		}

		public void Add(FuSymbol symbol)
		{
			this.Dict[symbol.Name] = symbol;
			symbol.Next = null;
			symbol.Parent = this;
			if (this.First == null)
				this.First = symbol;
			else
				this.Last.Next = symbol;
			this.Last = symbol;
		}

		public bool Encloses(FuSymbol symbol)
		{
			for (FuScope scope = symbol.Parent; scope != null; scope = scope.Parent) {
				if (scope == this)
					return true;
			}
			return false;
		}
	}

	public class FuAggregateInitializer : FuExpr
	{

		internal readonly List<FuExpr> Items = new List<FuExpr>();

		public override void Accept(FuVisitor visitor, FuPriority parent)
		{
			visitor.VisitAggregateInitializer(this);
		}
	}

	public abstract class FuLiteral : FuExpr
	{

		public abstract bool IsDefaultValue();

		public virtual string GetLiteralString()
		{
			throw new NotImplementedException();
		}
	}

	class FuLiteralNull : FuLiteral
	{

		public override bool IsDefaultValue() => true;

		public override void Accept(FuVisitor visitor, FuPriority parent)
		{
			visitor.VisitLiteralNull();
		}

		public override string ToString() => "null";
	}

	class FuLiteralFalse : FuLiteral
	{

		public override bool IsDefaultValue() => true;

		public override void Accept(FuVisitor visitor, FuPriority parent)
		{
			visitor.VisitLiteralFalse();
		}

		public override string ToString() => "false";
	}

	class FuLiteralTrue : FuLiteral
	{

		public override bool IsDefaultValue() => false;

		public override void Accept(FuVisitor visitor, FuPriority parent)
		{
			visitor.VisitLiteralTrue();
		}

		public override string ToString() => "true";
	}

	class FuLiteralLong : FuLiteral
	{

		internal long Value;

		public override bool IsLiteralZero() => this.Value == 0;

		public override int IntValue() => (int) this.Value;

		public override bool IsDefaultValue() => this.Value == 0;

		public override void Accept(FuVisitor visitor, FuPriority parent)
		{
			visitor.VisitLiteralLong(this.Value);
		}

		public override string GetLiteralString() => $"{this.Value}";

		public override string ToString() => $"{this.Value}";
	}

	class FuLiteralChar : FuLiteralLong
	{

		public static FuLiteralChar New(int value, int line) => new FuLiteralChar { Line = line, Type = FuRangeType.New(value, value), Value = value };

		public override void Accept(FuVisitor visitor, FuPriority parent)
		{
			visitor.VisitLiteralChar((int) this.Value);
		}
	}

	class FuLiteralDouble : FuLiteral
	{

		internal double Value;

		public override bool IsDefaultValue() => this.Value == 0 && 1.0 / this.Value > 0;

		public override void Accept(FuVisitor visitor, FuPriority parent)
		{
			visitor.VisitLiteralDouble(this.Value);
		}

		public override string GetLiteralString() => $"{this.Value}";

		public override string ToString() => $"{this.Value}";
	}

	class FuLiteralString : FuLiteral
	{

		internal string Value;

		public override bool IsDefaultValue() => false;

		public override void Accept(FuVisitor visitor, FuPriority parent)
		{
			visitor.VisitLiteralString(this.Value);
		}

		public override string GetLiteralString() => this.Value;

		public override string ToString() => $"\"{this.Value}\"";

		public int GetAsciiLength()
		{
			int length = 0;
			bool escaped = false;
			foreach (int c in this.Value) {
				if (c < 0 || c > 127)
					return -1;
				if (!escaped && c == '\\')
					escaped = true;
				else {
					length++;
					escaped = false;
				}
			}
			return length;
		}

		public int GetAsciiAt(int i)
		{
			bool escaped = false;
			foreach (int c in this.Value) {
				if (c < 0 || c > 127)
					return -1;
				if (!escaped && c == '\\')
					escaped = true;
				else if (i == 0)
					return escaped ? FuLexer.GetEscapedChar(c) : c;
				else {
					i--;
					escaped = false;
				}
			}
			return -1;
		}

		public int GetOneAscii()
		{
			switch (this.Value.Length) {
			case 1:
				int c = this.Value[0];
				return c >= 0 && c <= 127 ? c : -1;
			case 2:
				return this.Value[0] == '\\' ? FuLexer.GetEscapedChar(this.Value[1]) : -1;
			default:
				return -1;
			}
		}
	}

	public class FuInterpolatedPart
	{

		internal string Prefix;

		internal FuExpr Argument;

		internal FuExpr WidthExpr;

		internal int Width;

		internal int Format;

		internal int Precision;
	}

	public class FuInterpolatedString : FuExpr
	{

		internal readonly List<FuInterpolatedPart> Parts = new List<FuInterpolatedPart>();

		internal string Suffix;

		public void AddPart(string prefix, FuExpr arg, FuExpr widthExpr = null, int format = ' ', int precision = -1)
		{
			this.Parts.Add(new FuInterpolatedPart());
			FuInterpolatedPart part = this.Parts[^1];
			part.Prefix = prefix;
			part.Argument = arg;
			part.WidthExpr = widthExpr;
			part.Format = format;
			part.Precision = precision;
		}

		public override void Accept(FuVisitor visitor, FuPriority parent)
		{
			visitor.VisitInterpolatedString(this, parent);
		}

		public override bool IsNewString(bool substringOffset) => true;
	}

	class FuImplicitEnumValue : FuExpr
	{

		internal int Value;

		public override int IntValue() => this.Value;
	}

	public class FuSymbolReference : FuExpr
	{

		internal FuExpr Left;

		internal string Name;

		internal FuSymbol Symbol;

		public override bool IsConstEnum() => this.Symbol.Parent is FuEnum;

		public override int IntValue()
		{
			FuConst konst = (FuConst) this.Symbol;
			return konst.Value.IntValue();
		}

		public override void Accept(FuVisitor visitor, FuPriority parent)
		{
			visitor.VisitSymbolReference(this, parent);
		}

		public override bool IsReferenceTo(FuSymbol symbol) => this.Symbol == symbol;

		public override bool IsNewString(bool substringOffset) => this.Symbol.Id == FuId.MatchValue;

		public override string ToString() => this.Left != null ? $"{this.Left}.{this.Name}" : this.Name;
	}

	public abstract class FuUnaryExpr : FuExpr
	{

		internal FuToken Op;

		internal FuExpr Inner;
	}

	public class FuPrefixExpr : FuUnaryExpr
	{

		public override bool IsConstEnum() => this.Type is FuEnumFlags && this.Inner.IsConstEnum();

		public override int IntValue()
		{
			Debug.Assert(this.Op == FuToken.Tilde);
			return ~this.Inner.IntValue();
		}

		public override void Accept(FuVisitor visitor, FuPriority parent)
		{
			visitor.VisitPrefixExpr(this, parent);
		}
	}

	public class FuPostfixExpr : FuUnaryExpr
	{

		public override void Accept(FuVisitor visitor, FuPriority parent)
		{
			visitor.VisitPostfixExpr(this, parent);
		}
	}

	public class FuBinaryExpr : FuExpr
	{

		internal FuExpr Left;

		internal FuToken Op;

		internal FuExpr Right;

		public override bool IsIndexing() => this.Op == FuToken.LeftBracket;

		public override bool IsConstEnum()
		{
			switch (this.Op) {
			case FuToken.And:
			case FuToken.Or:
			case FuToken.Xor:
				return this.Type is FuEnumFlags && this.Left.IsConstEnum() && this.Right.IsConstEnum();
			default:
				return false;
			}
		}

		public override int IntValue()
		{
			switch (this.Op) {
			case FuToken.And:
				return this.Left.IntValue() & this.Right.IntValue();
			case FuToken.Or:
				return this.Left.IntValue() | this.Right.IntValue();
			case FuToken.Xor:
				return this.Left.IntValue() ^ this.Right.IntValue();
			default:
				throw new NotImplementedException();
			}
		}

		public override void Accept(FuVisitor visitor, FuPriority parent)
		{
			visitor.VisitBinaryExpr(this, parent);
		}

		public override bool IsNewString(bool substringOffset) => this.Op == FuToken.Plus && this.Type.Id == FuId.StringStorageType;

		public bool IsRel()
		{
			switch (this.Op) {
			case FuToken.Equal:
			case FuToken.NotEqual:
			case FuToken.Less:
			case FuToken.LessOrEqual:
			case FuToken.Greater:
			case FuToken.GreaterOrEqual:
				return true;
			default:
				return false;
			}
		}

		public bool IsAssign()
		{
			switch (this.Op) {
			case FuToken.Assign:
			case FuToken.AddAssign:
			case FuToken.SubAssign:
			case FuToken.MulAssign:
			case FuToken.DivAssign:
			case FuToken.ModAssign:
			case FuToken.ShiftLeftAssign:
			case FuToken.ShiftRightAssign:
			case FuToken.AndAssign:
			case FuToken.OrAssign:
			case FuToken.XorAssign:
				return true;
			default:
				return false;
			}
		}

		public string GetOpString()
		{
			switch (this.Op) {
			case FuToken.Plus:
				return "+";
			case FuToken.Minus:
				return "-";
			case FuToken.Asterisk:
				return "*";
			case FuToken.Slash:
				return "/";
			case FuToken.Mod:
				return "%";
			case FuToken.ShiftLeft:
				return "<<";
			case FuToken.ShiftRight:
				return ">>";
			case FuToken.Less:
				return "<";
			case FuToken.LessOrEqual:
				return "<=";
			case FuToken.Greater:
				return ">";
			case FuToken.GreaterOrEqual:
				return ">=";
			case FuToken.Equal:
				return "==";
			case FuToken.NotEqual:
				return "!=";
			case FuToken.And:
				return "&";
			case FuToken.Or:
				return "|";
			case FuToken.Xor:
				return "^";
			case FuToken.CondAnd:
				return "&&";
			case FuToken.CondOr:
				return "||";
			case FuToken.Assign:
				return "=";
			case FuToken.AddAssign:
				return "+=";
			case FuToken.SubAssign:
				return "-=";
			case FuToken.MulAssign:
				return "*=";
			case FuToken.DivAssign:
				return "/=";
			case FuToken.ModAssign:
				return "%=";
			case FuToken.ShiftLeftAssign:
				return "<<=";
			case FuToken.ShiftRightAssign:
				return ">>=";
			case FuToken.AndAssign:
				return "&=";
			case FuToken.OrAssign:
				return "|=";
			case FuToken.XorAssign:
				return "^=";
			default:
				throw new NotImplementedException();
			}
		}

		public override string ToString() => this.Op == FuToken.LeftBracket ? $"{this.Left}[{this.Right}]" : $"({this.Left} {GetOpString()} {this.Right})";
	}

	public class FuSelectExpr : FuExpr
	{

		internal FuExpr Cond;

		internal FuExpr OnTrue;

		internal FuExpr OnFalse;

		public override void Accept(FuVisitor visitor, FuPriority parent)
		{
			visitor.VisitSelectExpr(this, parent);
		}

		public override string ToString() => $"({this.Cond} ? {this.OnTrue} : {this.OnFalse})";
	}

	public class FuCallExpr : FuExpr
	{

		internal FuSymbolReference Method;

		internal readonly List<FuExpr> Arguments = new List<FuExpr>();

		public override void Accept(FuVisitor visitor, FuPriority parent)
		{
			visitor.VisitCallExpr(this, parent);
		}

		public override bool IsNewString(bool substringOffset) => this.Type.Id == FuId.StringStorageType && this.Method.Symbol.Id != FuId.ListLast && this.Method.Symbol.Id != FuId.QueuePeek && this.Method.Symbol.Id != FuId.StackPeek && (substringOffset || this.Method.Symbol.Id != FuId.StringSubstring || this.Arguments.Count != 1);
	}

	class FuLambdaExpr : FuScope
	{

		internal FuExpr Body;

		public override void Accept(FuVisitor visitor, FuPriority parent)
		{
			visitor.VisitLambdaExpr(this);
		}
	}

	public abstract class FuCondCompletionStatement : FuScope
	{

		bool CompletesNormallyValue;

		public override bool CompletesNormally() => this.CompletesNormallyValue;

		public void SetCompletesNormally(bool value)
		{
			this.CompletesNormallyValue = value;
		}
	}

	public class FuBlock : FuCondCompletionStatement
	{

		internal readonly List<FuStatement> Statements = new List<FuStatement>();

		public override void AcceptStatement(FuVisitor visitor)
		{
			visitor.VisitBlock(this);
		}
	}

	public class FuAssert : FuStatement
	{

		internal FuExpr Cond;

		internal FuExpr Message = null;

		public override bool CompletesNormally() => !(this.Cond is FuLiteralFalse);

		public override void AcceptStatement(FuVisitor visitor)
		{
			visitor.VisitAssert(this);
		}
	}

	public abstract class FuLoop : FuCondCompletionStatement
	{

		internal FuExpr Cond;

		internal FuStatement Body;

		internal bool HasBreak = false;
	}

	class FuBreak : FuStatement
	{

		internal FuCondCompletionStatement LoopOrSwitch;

		public override bool CompletesNormally() => false;

		public override void AcceptStatement(FuVisitor visitor)
		{
			visitor.VisitBreak(this);
		}
	}

	class FuContinue : FuStatement
	{

		internal FuLoop Loop;

		public override bool CompletesNormally() => false;

		public override void AcceptStatement(FuVisitor visitor)
		{
			visitor.VisitContinue(this);
		}
	}

	class FuDoWhile : FuLoop
	{

		public override void AcceptStatement(FuVisitor visitor)
		{
			visitor.VisitDoWhile(this);
		}
	}

	class FuFor : FuLoop
	{

		internal FuExpr Init;

		internal FuExpr Advance;

		internal bool IsRange = false;

		internal bool IsIteratorUsed;

		internal long RangeStep;

		public override void AcceptStatement(FuVisitor visitor)
		{
			visitor.VisitFor(this);
		}
	}

	class FuForeach : FuLoop
	{

		internal FuExpr Collection;

		public override void AcceptStatement(FuVisitor visitor)
		{
			visitor.VisitForeach(this);
		}

		public FuVar GetVar() => this.FirstParameter();

		public FuVar GetValueVar() => this.FirstParameter().NextParameter();
	}

	class FuIf : FuCondCompletionStatement
	{

		internal FuExpr Cond;

		internal FuStatement OnTrue;

		internal FuStatement OnFalse;

		public override void AcceptStatement(FuVisitor visitor)
		{
			visitor.VisitIf(this);
		}
	}

	class FuLock : FuStatement
	{

		internal FuExpr Lock;

		internal FuStatement Body;

		public override bool CompletesNormally() => this.Body.CompletesNormally();

		public override void AcceptStatement(FuVisitor visitor)
		{
			visitor.VisitLock(this);
		}
	}

	class FuNative : FuStatement
	{

		internal string Content;

		public override bool CompletesNormally() => true;

		public override void AcceptStatement(FuVisitor visitor)
		{
			visitor.VisitNative(this);
		}
	}

	class FuReturn : FuStatement
	{

		internal FuExpr Value;

		public override bool CompletesNormally() => false;

		public override void AcceptStatement(FuVisitor visitor)
		{
			visitor.VisitReturn(this);
		}
	}

	public class FuCase
	{

		internal readonly List<FuExpr> Values = new List<FuExpr>();

		internal readonly List<FuStatement> Body = new List<FuStatement>();
	}

	public class FuSwitch : FuCondCompletionStatement
	{

		internal FuExpr Value;

		internal readonly List<FuCase> Cases = new List<FuCase>();

		internal readonly List<FuStatement> DefaultBody = new List<FuStatement>();

		public override void AcceptStatement(FuVisitor visitor)
		{
			visitor.VisitSwitch(this);
		}

		public bool IsTypeMatching() => this.Value.Type is FuClassType klass && klass.Class.Id != FuId.StringClass;

		public bool HasWhen() => this.Cases.Exists(kase => kase.Values.Exists(value => value is FuBinaryExpr when1 && when1.Op == FuToken.When));

		public static int LengthWithoutTrailingBreak(List<FuStatement> body)
		{
			int length = body.Count;
			if (length > 0 && body[length - 1] is FuBreak)
				length--;
			return length;
		}

		public bool HasDefault() => LengthWithoutTrailingBreak(this.DefaultBody) > 0;

		static bool HasBreak(FuStatement statement)
		{
			switch (statement) {
			case FuBreak _:
				return true;
			case FuIf ifStatement:
				return HasBreak(ifStatement.OnTrue) || (ifStatement.OnFalse != null && HasBreak(ifStatement.OnFalse));
			case FuBlock block:
				return block.Statements.Exists(child => HasBreak(child));
			default:
				return false;
			}
		}

		public static bool HasEarlyBreak(List<FuStatement> body)
		{
			int length = LengthWithoutTrailingBreak(body);
			for (int i = 0; i < length; i++) {
				if (HasBreak(body[i]))
					return true;
			}
			return false;
		}

		static bool ListHasContinue(List<FuStatement> statements) => statements.Exists(statement => HasContinue(statement));

		static bool HasContinue(FuStatement statement)
		{
			switch (statement) {
			case FuContinue _:
				return true;
			case FuIf ifStatement:
				return HasContinue(ifStatement.OnTrue) || (ifStatement.OnFalse != null && HasContinue(ifStatement.OnFalse));
			case FuSwitch switchStatement:
				return switchStatement.Cases.Exists(kase => ListHasContinue(kase.Body)) || ListHasContinue(switchStatement.DefaultBody);
			case FuBlock block:
				return ListHasContinue(block.Statements);
			default:
				return false;
			}
		}

		public static bool HasEarlyBreakAndContinue(List<FuStatement> body) => HasEarlyBreak(body) && ListHasContinue(body);
	}

	class FuThrow : FuStatement
	{

		internal FuExpr Message;

		public override bool CompletesNormally() => false;

		public override void AcceptStatement(FuVisitor visitor)
		{
			visitor.VisitThrow(this);
		}
	}

	class FuWhile : FuLoop
	{

		public override void AcceptStatement(FuVisitor visitor)
		{
			visitor.VisitWhile(this);
		}
	}

	public class FuParameters : FuScope
	{
	}

	public class FuType : FuScope
	{

		internal bool Nullable = false;

		public virtual string GetArraySuffix() => "";

		public virtual bool IsAssignableFrom(FuType right) => this == right;

		public virtual bool EqualsType(FuType right) => this == right;

		public virtual bool IsArray() => false;

		public virtual bool IsFinal() => false;

		public virtual FuType GetBaseType() => this;

		public virtual FuType GetStorageType() => this;

		public FuClassType AsClassType()
		{
			FuClassType klass = (FuClassType) this;
			return klass;
		}
	}

	abstract class FuNumericType : FuType
	{
	}

	class FuIntegerType : FuNumericType
	{

		public override bool IsAssignableFrom(FuType right) => right is FuIntegerType || right.Id == FuId.FloatIntType;
	}

	class FuRangeType : FuIntegerType
	{

		internal int Min;

		internal int Max;

		static void AddMinMaxValue(FuRangeType target, string name, int value)
		{
			FuRangeType type = target.Min == target.Max ? target : new FuRangeType { Min = value, Max = value };
			target.Add(new FuConst { Visibility = FuVisibility.Public, Name = name, Value = new FuLiteralLong { Type = type, Value = value }, VisitStatus = FuVisitStatus.Done });
		}

		public static FuRangeType New(int min, int max)
		{
			Debug.Assert(min <= max);
			FuRangeType result = new FuRangeType { Id = min >= 0 && max <= 255 ? FuId.ByteRange : min >= -128 && max <= 127 ? FuId.SByteRange : min >= -32768 && max <= 32767 ? FuId.ShortRange : min >= 0 && max <= 65535 ? FuId.UShortRange : FuId.IntType, Min = min, Max = max };
			AddMinMaxValue(result, "MinValue", min);
			AddMinMaxValue(result, "MaxValue", max);
			return result;
		}

		public override string ToString() => this.Min == this.Max ? $"{this.Min}" : $"({this.Min} .. {this.Max})";

		public override bool IsAssignableFrom(FuType right)
		{
			switch (right) {
			case FuRangeType range:
				return this.Min <= range.Max && this.Max >= range.Min;
			case FuIntegerType _:
				return true;
			default:
				return right.Id == FuId.FloatIntType;
			}
		}

		public override bool EqualsType(FuType right) => right is FuRangeType that && this.Min == that.Min && this.Max == that.Max;

		public static int GetMask(int v)
		{
			v |= v >> 1;
			v |= v >> 2;
			v |= v >> 4;
			v |= v >> 8;
			v |= v >> 16;
			return v;
		}

		public int GetVariableBits() => GetMask(this.Min ^ this.Max);
	}

	class FuFloatingType : FuNumericType
	{

		public override bool IsAssignableFrom(FuType right) => right is FuNumericType;
	}

	public abstract class FuNamedValue : FuSymbol
	{

		internal FuExpr TypeExpr;

		internal FuExpr Value;

		public bool IsAssignableStorage() => this.Type is FuStorageType && !(this.Type is FuArrayStorageType) && this.Value is FuLiteralNull;
	}

	public abstract class FuMember : FuNamedValue
	{
		protected FuMember()
		{
		}

		internal FuVisibility Visibility;

		public abstract bool IsStatic();
	}

	public class FuVar : FuNamedValue
	{

		internal bool IsAssigned = false;

		public static FuVar New(FuType type, string name, FuExpr defaultValue = null) => new FuVar { Type = type, Name = name, Value = defaultValue };

		public override void Accept(FuVisitor visitor, FuPriority parent)
		{
			visitor.VisitVar(this);
		}

		public FuVar NextParameter()
		{
			FuVar def = (FuVar) this.Next;
			return def;
		}
	}

	enum FuVisitStatus
	{
		NotYet,
		InProgress,
		Done
	}

	public class FuConst : FuMember
	{

		internal FuMethodBase InMethod;

		internal FuVisitStatus VisitStatus;

		public override void AcceptStatement(FuVisitor visitor)
		{
			visitor.VisitConst(this);
		}

		public override bool IsStatic() => true;
	}

	public class FuField : FuMember
	{

		public override bool IsStatic() => false;
	}

	class FuProperty : FuMember
	{

		public override bool IsStatic() => false;

		public static FuProperty New(FuType type, FuId id, string name) => new FuProperty { Visibility = FuVisibility.Public, Type = type, Id = id, Name = name };
	}

	class FuStaticProperty : FuMember
	{

		public override bool IsStatic() => true;

		public static FuStaticProperty New(FuType type, FuId id, string name) => new FuStaticProperty { Visibility = FuVisibility.Public, Type = type, Id = id, Name = name };
	}

	public class FuMethodBase : FuMember
	{

		internal bool IsMutator = false;

		internal bool Throws;

		internal FuStatement Body;

		internal bool IsLive = false;

		internal readonly HashSet<FuMethod> Calls = new HashSet<FuMethod>();

		public override bool IsStatic() => false;
	}

	public class FuMethod : FuMethodBase
	{

		internal FuCallType CallType;

		internal readonly FuParameters Parameters = new FuParameters();

		internal readonly FuScope MethodScope = new FuScope();

		public static FuMethod New(FuVisibility visibility, FuType type, FuId id, string name, FuVar param0 = null, FuVar param1 = null, FuVar param2 = null, FuVar param3 = null)
		{
			FuMethod result = new FuMethod { Visibility = visibility, CallType = FuCallType.Normal, Type = type, Id = id, Name = name };
			if (param0 != null) {
				result.Parameters.Add(param0);
				if (param1 != null) {
					result.Parameters.Add(param1);
					if (param2 != null) {
						result.Parameters.Add(param2);
						if (param3 != null)
							result.Parameters.Add(param3);
					}
				}
			}
			return result;
		}

		public static FuMethod NewStatic(FuType type, FuId id, string name, FuVar param0, FuVar param1 = null, FuVar param2 = null)
		{
			FuMethod result = New(FuVisibility.Public, type, id, name, param0, param1, param2);
			result.CallType = FuCallType.Static;
			return result;
		}

		public static FuMethod NewMutator(FuVisibility visibility, FuType type, FuId id, string name, FuVar param0 = null, FuVar param1 = null, FuVar param2 = null)
		{
			FuMethod result = New(visibility, type, id, name, param0, param1, param2);
			result.IsMutator = true;
			return result;
		}

		public override bool IsStatic() => this.CallType == FuCallType.Static;

		public bool IsAbstractOrVirtual() => this.CallType == FuCallType.Abstract || this.CallType == FuCallType.Virtual;

		public FuMethod GetDeclaringMethod()
		{
			FuMethod method = this;
			while (method.CallType == FuCallType.Override) {
				FuMethod baseMethod = (FuMethod) method.Parent.Parent.TryLookup(method.Name, false);
				method = baseMethod;
			}
			return method;
		}

		public bool IsToString() => this.Name == "ToString" && this.CallType != FuCallType.Static && this.Parameters.Count() == 0;
	}

	class FuMethodGroup : FuMember
	{
		internal FuMethodGroup()
		{
		}

		internal readonly FuMethod[] Methods = new FuMethod[2];

		public override bool IsStatic()
		{
			throw new NotImplementedException();
		}

		public static FuMethodGroup New(FuMethod method0, FuMethod method1)
		{
			FuMethodGroup result = new FuMethodGroup { Visibility = method0.Visibility, Name = method0.Name };
			result.Methods[0] = method0;
			result.Methods[1] = method1;
			return result;
		}
	}

	public abstract class FuContainerType : FuType
	{

		internal bool IsPublic;

		internal string Filename;
	}

	public class FuEnum : FuContainerType
	{

		internal bool HasExplicitValue = false;

		public FuSymbol GetFirstValue()
		{
			FuSymbol symbol = this.First;
			while (!(symbol is FuConst))
				symbol = symbol.Next;
			return symbol;
		}

		public void AcceptValues(FuVisitor visitor)
		{
			FuConst previous = null;
			for (FuSymbol symbol = this.First; symbol != null; symbol = symbol.Next) {
				if (symbol is FuConst konst) {
					visitor.VisitEnumValue(konst, previous);
					previous = konst;
				}
			}
		}
	}

	class FuEnumFlags : FuEnum
	{
	}

	public class FuClass : FuContainerType
	{
		public FuClass()
		{
			Add(FuVar.New(new FuReadWriteClassType { Class = this }, "this"));
		}

		internal FuCallType CallType;

		internal int TypeParameterCount = 0;

		internal bool HasSubclasses = false;

		internal string BaseClassName = "";

		internal FuMethodBase Constructor;

		internal readonly List<FuConst> ConstArrays = new List<FuConst>();

		public bool HasBaseClass() => this.BaseClassName.Length > 0;

		public bool AddsVirtualMethods()
		{
			for (FuSymbol symbol = this.First; symbol != null; symbol = symbol.Next) {
				if (symbol is FuMethod method && method.IsAbstractOrVirtual())
					return true;
			}
			return false;
		}

		public static FuClass New(FuCallType callType, FuId id, string name, int typeParameterCount = 0) => new FuClass { CallType = callType, Id = id, Name = name, TypeParameterCount = typeParameterCount };

		public bool IsSameOrBaseOf(FuClass derived)
		{
			while (derived != this) {
				if (derived.Parent is FuClass parent)
					derived = parent;
				else
					return false;
			}
			return true;
		}

		public bool HasToString() => TryLookup("ToString", false) is FuMethod method && method.IsToString();

		public bool AddsToString() => this.Dict.ContainsKey("ToString") && this.Dict["ToString"] is FuMethod method && method.IsToString() && method.CallType != FuCallType.Override && method.CallType != FuCallType.Sealed;
	}

	public class FuClassType : FuType
	{

		internal FuClass Class;

		internal FuType TypeArg0;

		internal FuType TypeArg1;

		public FuType GetElementType() => this.TypeArg0;

		public FuType GetKeyType() => this.TypeArg0;

		public FuType GetValueType() => this.TypeArg1;

		public override bool IsArray() => this.Class.Id == FuId.ArrayPtrClass;

		public override FuType GetBaseType() => IsArray() ? GetElementType().GetBaseType() : this;

		internal bool EqualTypeArguments(FuClassType right)
		{
			switch (this.Class.TypeParameterCount) {
			case 0:
				return true;
			case 1:
				return this.TypeArg0.EqualsType(right.TypeArg0);
			case 2:
				return this.TypeArg0.EqualsType(right.TypeArg0) && this.TypeArg1.EqualsType(right.TypeArg1);
			default:
				throw new NotImplementedException();
			}
		}

		protected bool IsAssignableFromClass(FuClassType right) => this.Class.IsSameOrBaseOf(right.Class) && EqualTypeArguments(right);

		public override bool IsAssignableFrom(FuType right)
		{
			return (this.Nullable && right.Id == FuId.NullType) || (right is FuClassType rightClass && IsAssignableFromClass(rightClass));
		}

		protected bool EqualsTypeInternal(FuClassType that) => this.Nullable == that.Nullable && this.Class == that.Class && EqualTypeArguments(that);

		public override bool EqualsType(FuType right) => right is FuClassType that && !(right is FuReadWriteClassType) && EqualsTypeInternal(that);

		public override string GetArraySuffix() => IsArray() ? "[]" : "";

		public virtual string GetClassSuffix() => "";

		string GetNullableSuffix() => this.Nullable ? "?" : "";

		public override string ToString()
		{
			if (IsArray())
				return $"{GetElementType().GetBaseType()}{GetArraySuffix()}{GetNullableSuffix()}{GetElementType().GetArraySuffix()}";
			switch (this.Class.TypeParameterCount) {
			case 0:
				return $"{this.Class.Name}{GetClassSuffix()}{GetNullableSuffix()}";
			case 1:
				return $"{this.Class.Name}<{this.TypeArg0}>{GetClassSuffix()}{GetNullableSuffix()}";
			case 2:
				return $"{this.Class.Name}<{this.TypeArg0}, {this.TypeArg1}>{GetClassSuffix()}{GetNullableSuffix()}";
			default:
				throw new NotImplementedException();
			}
		}
	}

	public class FuReadWriteClassType : FuClassType
	{

		public override bool IsAssignableFrom(FuType right)
		{
			return (this.Nullable && right.Id == FuId.NullType) || (right is FuReadWriteClassType rightClass && IsAssignableFromClass(rightClass));
		}

		public override bool EqualsType(FuType right) => right is FuReadWriteClassType that && !(right is FuStorageType) && !(right is FuDynamicPtrType) && EqualsTypeInternal(that);

		public override string GetArraySuffix() => IsArray() ? "[]!" : "";

		public override string GetClassSuffix() => "!";
	}

	public class FuStorageType : FuReadWriteClassType
	{

		public override bool IsFinal() => this.Class.Id != FuId.MatchClass;

		public override bool IsAssignableFrom(FuType right) => right is FuStorageType rightClass && this.Class == rightClass.Class && EqualTypeArguments(rightClass);

		public override bool EqualsType(FuType right) => right is FuStorageType that && EqualsTypeInternal(that);

		public override string GetClassSuffix() => "()";
	}

	class FuDynamicPtrType : FuReadWriteClassType
	{

		public override bool IsAssignableFrom(FuType right)
		{
			return (this.Nullable && right.Id == FuId.NullType) || (right is FuDynamicPtrType rightClass && IsAssignableFromClass(rightClass));
		}

		public override bool EqualsType(FuType right) => right is FuDynamicPtrType that && EqualsTypeInternal(that);

		public override string GetArraySuffix() => IsArray() ? "[]#" : "";

		public override string GetClassSuffix() => "#";
	}

	public class FuArrayStorageType : FuStorageType
	{

		internal FuExpr LengthExpr;

		internal int Length;

		internal bool PtrTaken = false;

		public override FuType GetBaseType() => GetElementType().GetBaseType();

		public override bool IsArray() => true;

		public override string GetArraySuffix() => $"[{this.Length}]";

		public override bool EqualsType(FuType right) => right is FuArrayStorageType that && GetElementType().EqualsType(that.GetElementType()) && this.Length == that.Length;

		public override FuType GetStorageType() => GetElementType().GetStorageType();
	}

	class FuStringType : FuClassType
	{
	}

	class FuStringStorageType : FuStringType
	{

		public override bool IsAssignableFrom(FuType right) => right is FuStringType;

		public override string GetClassSuffix() => "()";
	}

	class FuPrintableType : FuType
	{

		public override bool IsAssignableFrom(FuType right)
		{
			switch (right) {
			case FuNumericType _:
			case FuStringType _:
				return true;
			case FuClassType klass:
				return klass.Class.HasToString();
			default:
				return false;
			}
		}
	}

	public class FuSystem : FuScope
	{
		internal FuSystem()
		{
			this.Parent = null;
			FuSymbol basePtr = FuVar.New(null, "base");
			basePtr.Id = FuId.BasePtr;
			Add(basePtr);
			AddMinMaxValue(this.IntType, -2147483648, 2147483647);
			this.IntType.Add(FuMethod.NewMutator(FuVisibility.Public, this.BoolType, FuId.IntTryParse, "TryParse", FuVar.New(this.StringPtrType, "value"), FuVar.New(this.IntType, "radix", NewLiteralLong(0))));
			Add(this.IntType);
			this.UIntType.Name = "uint";
			Add(this.UIntType);
			AddMinMaxValue(this.LongType, -9223372036854775808, 9223372036854775807);
			this.LongType.Add(FuMethod.NewMutator(FuVisibility.Public, this.BoolType, FuId.LongTryParse, "TryParse", FuVar.New(this.StringPtrType, "value"), FuVar.New(this.IntType, "radix", NewLiteralLong(0))));
			Add(this.LongType);
			this.ByteType.Name = "byte";
			Add(this.ByteType);
			FuRangeType shortType = FuRangeType.New(-32768, 32767);
			shortType.Name = "short";
			Add(shortType);
			FuRangeType ushortType = FuRangeType.New(0, 65535);
			ushortType.Name = "ushort";
			Add(ushortType);
			FuRangeType minus1Type = FuRangeType.New(-1, 2147483647);
			Add(this.FloatType);
			this.DoubleType.Add(FuMethod.NewMutator(FuVisibility.Public, this.BoolType, FuId.DoubleTryParse, "TryParse", FuVar.New(this.StringPtrType, "value")));
			Add(this.DoubleType);
			Add(this.BoolType);
			this.StringClass.Add(FuMethod.New(FuVisibility.Public, this.BoolType, FuId.StringContains, "Contains", FuVar.New(this.StringPtrType, "value")));
			this.StringClass.Add(FuMethod.New(FuVisibility.Public, this.BoolType, FuId.StringEndsWith, "EndsWith", FuVar.New(this.StringPtrType, "value")));
			this.StringClass.Add(FuMethod.New(FuVisibility.Public, minus1Type, FuId.StringIndexOf, "IndexOf", FuVar.New(this.StringPtrType, "value")));
			this.StringClass.Add(FuMethod.New(FuVisibility.Public, minus1Type, FuId.StringLastIndexOf, "LastIndexOf", FuVar.New(this.StringPtrType, "value")));
			this.StringClass.Add(FuProperty.New(this.UIntType, FuId.StringLength, "Length"));
			this.StringClass.Add(FuMethod.New(FuVisibility.Public, this.StringStorageType, FuId.StringReplace, "Replace", FuVar.New(this.StringPtrType, "oldValue"), FuVar.New(this.StringPtrType, "newValue")));
			this.StringClass.Add(FuMethod.New(FuVisibility.Public, this.BoolType, FuId.StringStartsWith, "StartsWith", FuVar.New(this.StringPtrType, "value")));
			this.StringClass.Add(FuMethod.New(FuVisibility.Public, this.StringStorageType, FuId.StringSubstring, "Substring", FuVar.New(this.IntType, "offset"), FuVar.New(this.IntType, "length", NewLiteralLong(-1))));
			this.StringPtrType.Class = this.StringClass;
			Add(this.StringPtrType);
			this.StringNullablePtrType.Class = this.StringClass;
			this.StringStorageType.Class = this.StringClass;
			FuMethod arrayBinarySearchPart = FuMethod.New(FuVisibility.NumericElementType, this.IntType, FuId.ArrayBinarySearchPart, "BinarySearch", FuVar.New(this.TypeParam0, "value"), FuVar.New(this.IntType, "startIndex"), FuVar.New(this.IntType, "count"));
			this.ArrayPtrClass.Add(arrayBinarySearchPart);
			this.ArrayPtrClass.Add(FuMethod.New(FuVisibility.Public, this.VoidType, FuId.ArrayCopyTo, "CopyTo", FuVar.New(this.IntType, "sourceIndex"), FuVar.New(new FuReadWriteClassType { Class = this.ArrayPtrClass, TypeArg0 = this.TypeParam0 }, "destinationArray"), FuVar.New(this.IntType, "destinationIndex"), FuVar.New(this.IntType, "count")));
			FuMethod arrayFillPart = FuMethod.NewMutator(FuVisibility.Public, this.VoidType, FuId.ArrayFillPart, "Fill", FuVar.New(this.TypeParam0, "value"), FuVar.New(this.IntType, "startIndex"), FuVar.New(this.IntType, "count"));
			this.ArrayPtrClass.Add(arrayFillPart);
			FuMethod arraySortPart = FuMethod.NewMutator(FuVisibility.NumericElementType, this.VoidType, FuId.ArraySortPart, "Sort", FuVar.New(this.IntType, "startIndex"), FuVar.New(this.IntType, "count"));
			this.ArrayPtrClass.Add(arraySortPart);
			this.ArrayStorageClass.Parent = this.ArrayPtrClass;
			this.ArrayStorageClass.Add(FuMethodGroup.New(FuMethod.New(FuVisibility.NumericElementType, this.IntType, FuId.ArrayBinarySearchAll, "BinarySearch", FuVar.New(this.TypeParam0, "value")), arrayBinarySearchPart));
			this.ArrayStorageClass.Add(FuMethod.New(FuVisibility.Public, this.BoolType, FuId.ArrayContains, "Contains", FuVar.New(this.TypeParam0, "value")));
			this.ArrayStorageClass.Add(FuMethodGroup.New(FuMethod.NewMutator(FuVisibility.Public, this.VoidType, FuId.ArrayFillAll, "Fill", FuVar.New(this.TypeParam0, "value")), arrayFillPart));
			this.ArrayStorageClass.Add(FuProperty.New(this.UIntType, FuId.ArrayLength, "Length"));
			this.ArrayStorageClass.Add(FuMethodGroup.New(FuMethod.NewMutator(FuVisibility.NumericElementType, this.VoidType, FuId.ArraySortAll, "Sort"), arraySortPart));
			FuType typeParam0NotFinal = new FuType { Id = FuId.TypeParam0NotFinal, Name = "T" };
			FuType typeParam0Predicate = new FuType { Id = FuId.TypeParam0Predicate, Name = "Predicate<T>" };
			FuClass listClass = AddCollection(FuId.ListClass, "List", 1, FuId.ListClear, FuId.ListCount);
			listClass.Add(FuMethod.NewMutator(FuVisibility.Public, this.VoidType, FuId.ListAdd, "Add", FuVar.New(typeParam0NotFinal, "value")));
			listClass.Add(FuMethod.NewMutator(FuVisibility.Public, this.VoidType, FuId.ListAddRange, "AddRange", FuVar.New(new FuClassType { Class = listClass, TypeArg0 = this.TypeParam0 }, "source")));
			listClass.Add(FuMethod.New(FuVisibility.Public, this.BoolType, FuId.ListAll, "All", FuVar.New(typeParam0Predicate, "predicate")));
			listClass.Add(FuMethod.New(FuVisibility.Public, this.BoolType, FuId.ListAny, "Any", FuVar.New(typeParam0Predicate, "predicate")));
			listClass.Add(FuMethod.New(FuVisibility.Public, this.BoolType, FuId.ListContains, "Contains", FuVar.New(this.TypeParam0, "value")));
			listClass.Add(FuMethod.New(FuVisibility.Public, this.VoidType, FuId.ListCopyTo, "CopyTo", FuVar.New(this.IntType, "sourceIndex"), FuVar.New(new FuReadWriteClassType { Class = this.ArrayPtrClass, TypeArg0 = this.TypeParam0 }, "destinationArray"), FuVar.New(this.IntType, "destinationIndex"), FuVar.New(this.IntType, "count")));
			listClass.Add(FuMethod.NewMutator(FuVisibility.Public, this.IntType, FuId.ListIndexOf, "IndexOf", FuVar.New(this.TypeParam0, "value")));
			listClass.Add(FuMethod.NewMutator(FuVisibility.Public, this.VoidType, FuId.ListInsert, "Insert", FuVar.New(this.UIntType, "index"), FuVar.New(typeParam0NotFinal, "value")));
			listClass.Add(FuMethod.NewMutator(FuVisibility.Public, this.TypeParam0, FuId.ListLast, "Last"));
			listClass.Add(FuMethod.NewMutator(FuVisibility.Public, this.VoidType, FuId.ListRemoveAt, "RemoveAt", FuVar.New(this.IntType, "index")));
			listClass.Add(FuMethod.NewMutator(FuVisibility.Public, this.VoidType, FuId.ListRemoveRange, "RemoveRange", FuVar.New(this.IntType, "index"), FuVar.New(this.IntType, "count")));
			listClass.Add(FuMethodGroup.New(FuMethod.NewMutator(FuVisibility.NumericElementType, this.VoidType, FuId.ListSortAll, "Sort"), FuMethod.NewMutator(FuVisibility.NumericElementType, this.VoidType, FuId.ListSortPart, "Sort", FuVar.New(this.IntType, "startIndex"), FuVar.New(this.IntType, "count"))));
			FuClass queueClass = AddCollection(FuId.QueueClass, "Queue", 1, FuId.QueueClear, FuId.QueueCount);
			queueClass.Add(FuMethod.NewMutator(FuVisibility.Public, this.TypeParam0, FuId.QueueDequeue, "Dequeue"));
			queueClass.Add(FuMethod.NewMutator(FuVisibility.Public, this.VoidType, FuId.QueueEnqueue, "Enqueue", FuVar.New(this.TypeParam0, "value")));
			queueClass.Add(FuMethod.New(FuVisibility.Public, this.TypeParam0, FuId.QueuePeek, "Peek"));
			FuClass stackClass = AddCollection(FuId.StackClass, "Stack", 1, FuId.StackClear, FuId.StackCount);
			stackClass.Add(FuMethod.New(FuVisibility.Public, this.TypeParam0, FuId.StackPeek, "Peek"));
			stackClass.Add(FuMethod.NewMutator(FuVisibility.Public, this.VoidType, FuId.StackPush, "Push", FuVar.New(this.TypeParam0, "value")));
			stackClass.Add(FuMethod.NewMutator(FuVisibility.Public, this.TypeParam0, FuId.StackPop, "Pop"));
			AddSet(FuId.HashSetClass, "HashSet", FuId.HashSetAdd, FuId.HashSetClear, FuId.HashSetContains, FuId.HashSetCount, FuId.HashSetRemove);
			AddSet(FuId.SortedSetClass, "SortedSet", FuId.SortedSetAdd, FuId.SortedSetClear, FuId.SortedSetContains, FuId.SortedSetCount, FuId.SortedSetRemove);
			AddDictionary(FuId.DictionaryClass, "Dictionary", FuId.DictionaryClear, FuId.DictionaryContainsKey, FuId.DictionaryCount, FuId.DictionaryRemove);
			AddDictionary(FuId.SortedDictionaryClass, "SortedDictionary", FuId.SortedDictionaryClear, FuId.SortedDictionaryContainsKey, FuId.SortedDictionaryCount, FuId.SortedDictionaryRemove);
			AddDictionary(FuId.OrderedDictionaryClass, "OrderedDictionary", FuId.OrderedDictionaryClear, FuId.OrderedDictionaryContainsKey, FuId.OrderedDictionaryCount, FuId.OrderedDictionaryRemove);
			FuClass textWriterClass = FuClass.New(FuCallType.Normal, FuId.TextWriterClass, "TextWriter");
			textWriterClass.Add(FuMethod.NewMutator(FuVisibility.Public, this.VoidType, FuId.TextWriterWrite, "Write", FuVar.New(this.PrintableType, "value")));
			textWriterClass.Add(FuMethod.NewMutator(FuVisibility.Public, this.VoidType, FuId.TextWriterWriteChar, "WriteChar", FuVar.New(this.IntType, "c")));
			textWriterClass.Add(FuMethod.NewMutator(FuVisibility.Public, this.VoidType, FuId.TextWriterWriteCodePoint, "WriteCodePoint", FuVar.New(this.IntType, "c")));
			textWriterClass.Add(FuMethod.NewMutator(FuVisibility.Public, this.VoidType, FuId.TextWriterWriteLine, "WriteLine", FuVar.New(this.PrintableType, "value", NewLiteralString(""))));
			Add(textWriterClass);
			FuClass consoleClass = FuClass.New(FuCallType.Static, FuId.None, "Console");
			consoleClass.Add(FuMethod.NewStatic(this.VoidType, FuId.ConsoleWrite, "Write", FuVar.New(this.PrintableType, "value")));
			consoleClass.Add(FuMethod.NewStatic(this.VoidType, FuId.ConsoleWriteLine, "WriteLine", FuVar.New(this.PrintableType, "value", NewLiteralString(""))));
			consoleClass.Add(FuStaticProperty.New(new FuStorageType { Class = textWriterClass }, FuId.ConsoleError, "Error"));
			Add(consoleClass);
			FuClass stringWriterClass = FuClass.New(FuCallType.Sealed, FuId.StringWriterClass, "StringWriter");
			stringWriterClass.Add(FuMethod.NewMutator(FuVisibility.Public, this.VoidType, FuId.StringWriterClear, "Clear"));
			stringWriterClass.Add(FuMethod.New(FuVisibility.Public, this.StringPtrType, FuId.StringWriterToString, "ToString"));
			Add(stringWriterClass);
			stringWriterClass.Parent = textWriterClass;
			FuClass utf8EncodingClass = FuClass.New(FuCallType.Sealed, FuId.None, "UTF8Encoding");
			utf8EncodingClass.Add(FuMethod.New(FuVisibility.Public, this.IntType, FuId.UTF8GetByteCount, "GetByteCount", FuVar.New(this.StringPtrType, "str")));
			utf8EncodingClass.Add(FuMethod.New(FuVisibility.Public, this.VoidType, FuId.UTF8GetBytes, "GetBytes", FuVar.New(this.StringPtrType, "str"), FuVar.New(new FuReadWriteClassType { Class = this.ArrayPtrClass, TypeArg0 = this.ByteType }, "bytes"), FuVar.New(this.IntType, "byteIndex")));
			utf8EncodingClass.Add(FuMethod.New(FuVisibility.Public, this.StringStorageType, FuId.UTF8GetString, "GetString", FuVar.New(new FuClassType { Class = this.ArrayPtrClass, TypeArg0 = this.ByteType }, "bytes"), FuVar.New(this.IntType, "offset"), FuVar.New(this.IntType, "length")));
			FuClass encodingClass = FuClass.New(FuCallType.Static, FuId.None, "Encoding");
			encodingClass.Add(FuStaticProperty.New(utf8EncodingClass, FuId.None, "UTF8"));
			Add(encodingClass);
			FuClass environmentClass = FuClass.New(FuCallType.Static, FuId.None, "Environment");
			environmentClass.Add(FuMethod.NewStatic(this.StringNullablePtrType, FuId.EnvironmentGetEnvironmentVariable, "GetEnvironmentVariable", FuVar.New(this.StringPtrType, "name")));
			Add(environmentClass);
			this.RegexOptionsEnum = NewEnum(true);
			this.RegexOptionsEnum.IsPublic = true;
			this.RegexOptionsEnum.Id = FuId.RegexOptionsEnum;
			this.RegexOptionsEnum.Name = "RegexOptions";
			FuConst regexOptionsNone = NewConstLong("None", 0);
			AddEnumValue(this.RegexOptionsEnum, regexOptionsNone);
			AddEnumValue(this.RegexOptionsEnum, NewConstLong("IgnoreCase", 1));
			AddEnumValue(this.RegexOptionsEnum, NewConstLong("Multiline", 2));
			AddEnumValue(this.RegexOptionsEnum, NewConstLong("Singleline", 16));
			Add(this.RegexOptionsEnum);
			FuClass regexClass = FuClass.New(FuCallType.Sealed, FuId.RegexClass, "Regex");
			regexClass.Add(FuMethod.NewStatic(this.StringStorageType, FuId.RegexEscape, "Escape", FuVar.New(this.StringPtrType, "str")));
			regexClass.Add(FuMethodGroup.New(FuMethod.NewStatic(this.BoolType, FuId.RegexIsMatchStr, "IsMatch", FuVar.New(this.StringPtrType, "input"), FuVar.New(this.StringPtrType, "pattern"), FuVar.New(this.RegexOptionsEnum, "options", regexOptionsNone)), FuMethod.New(FuVisibility.Public, this.BoolType, FuId.RegexIsMatchRegex, "IsMatch", FuVar.New(this.StringPtrType, "input"))));
			regexClass.Add(FuMethod.NewStatic(new FuDynamicPtrType { Class = regexClass }, FuId.RegexCompile, "Compile", FuVar.New(this.StringPtrType, "pattern"), FuVar.New(this.RegexOptionsEnum, "options", regexOptionsNone)));
			Add(regexClass);
			FuClass matchClass = FuClass.New(FuCallType.Sealed, FuId.MatchClass, "Match");
			matchClass.Add(FuMethodGroup.New(FuMethod.NewMutator(FuVisibility.Public, this.BoolType, FuId.MatchFindStr, "Find", FuVar.New(this.StringPtrType, "input"), FuVar.New(this.StringPtrType, "pattern"), FuVar.New(this.RegexOptionsEnum, "options", regexOptionsNone)), FuMethod.NewMutator(FuVisibility.Public, this.BoolType, FuId.MatchFindRegex, "Find", FuVar.New(this.StringPtrType, "input"), FuVar.New(new FuClassType { Class = regexClass }, "pattern"))));
			matchClass.Add(FuProperty.New(this.IntType, FuId.MatchStart, "Start"));
			matchClass.Add(FuProperty.New(this.IntType, FuId.MatchEnd, "End"));
			matchClass.Add(FuMethod.New(FuVisibility.Public, this.StringStorageType, FuId.MatchGetCapture, "GetCapture", FuVar.New(this.UIntType, "group")));
			matchClass.Add(FuProperty.New(this.UIntType, FuId.MatchLength, "Length"));
			matchClass.Add(FuProperty.New(this.StringStorageType, FuId.MatchValue, "Value"));
			Add(matchClass);
			FuFloatingType floatIntType = new FuFloatingType { Id = FuId.FloatIntType, Name = "float" };
			FuClass mathClass = FuClass.New(FuCallType.Static, FuId.None, "Math");
			mathClass.Add(FuMethodGroup.New(FuMethod.NewStatic(this.IntType, FuId.MathAbs, "Abs", FuVar.New(this.LongType, "a")), FuMethod.NewStatic(this.FloatType, FuId.MathAbs, "Abs", FuVar.New(this.DoubleType, "a"))));
			mathClass.Add(FuMethod.NewStatic(this.FloatType, FuId.MathMethod, "Acos", FuVar.New(this.DoubleType, "a")));
			mathClass.Add(FuMethod.NewStatic(this.FloatType, FuId.MathMethod, "Asin", FuVar.New(this.DoubleType, "a")));
			mathClass.Add(FuMethod.NewStatic(this.FloatType, FuId.MathMethod, "Atan", FuVar.New(this.DoubleType, "a")));
			mathClass.Add(FuMethod.NewStatic(this.FloatType, FuId.MathMethod, "Atan2", FuVar.New(this.DoubleType, "y"), FuVar.New(this.DoubleType, "x")));
			mathClass.Add(FuMethod.NewStatic(this.FloatType, FuId.MathMethod, "Cbrt", FuVar.New(this.DoubleType, "a")));
			mathClass.Add(FuMethod.NewStatic(floatIntType, FuId.MathCeiling, "Ceiling", FuVar.New(this.DoubleType, "a")));
			mathClass.Add(FuMethodGroup.New(FuMethod.NewStatic(this.IntType, FuId.MathClamp, "Clamp", FuVar.New(this.LongType, "value"), FuVar.New(this.LongType, "min"), FuVar.New(this.LongType, "max")), FuMethod.NewStatic(this.FloatType, FuId.MathClamp, "Clamp", FuVar.New(this.DoubleType, "value"), FuVar.New(this.DoubleType, "min"), FuVar.New(this.DoubleType, "max"))));
			mathClass.Add(FuMethod.NewStatic(this.FloatType, FuId.MathMethod, "Cos", FuVar.New(this.DoubleType, "a")));
			mathClass.Add(FuMethod.NewStatic(this.FloatType, FuId.MathMethod, "Cosh", FuVar.New(this.DoubleType, "a")));
			mathClass.Add(NewConstDouble("E", 2.718281828459045));
			mathClass.Add(FuMethod.NewStatic(this.FloatType, FuId.MathMethod, "Exp", FuVar.New(this.DoubleType, "a")));
			mathClass.Add(FuMethod.NewStatic(floatIntType, FuId.MathMethod, "Floor", FuVar.New(this.DoubleType, "a")));
			mathClass.Add(FuMethod.NewStatic(this.FloatType, FuId.MathFusedMultiplyAdd, "FusedMultiplyAdd", FuVar.New(this.DoubleType, "x"), FuVar.New(this.DoubleType, "y"), FuVar.New(this.DoubleType, "z")));
			mathClass.Add(FuMethod.NewStatic(this.BoolType, FuId.MathIsFinite, "IsFinite", FuVar.New(this.DoubleType, "a")));
			mathClass.Add(FuMethod.NewStatic(this.BoolType, FuId.MathIsInfinity, "IsInfinity", FuVar.New(this.DoubleType, "a")));
			mathClass.Add(FuMethod.NewStatic(this.BoolType, FuId.MathIsNaN, "IsNaN", FuVar.New(this.DoubleType, "a")));
			mathClass.Add(FuMethod.NewStatic(this.FloatType, FuId.MathMethod, "Log", FuVar.New(this.DoubleType, "a")));
			mathClass.Add(FuMethod.NewStatic(this.FloatType, FuId.MathLog2, "Log2", FuVar.New(this.DoubleType, "a")));
			mathClass.Add(FuMethod.NewStatic(this.FloatType, FuId.MathMethod, "Log10", FuVar.New(this.DoubleType, "a")));
			mathClass.Add(FuMethodGroup.New(FuMethod.NewStatic(this.IntType, FuId.MathMaxInt, "Max", FuVar.New(this.LongType, "a"), FuVar.New(this.LongType, "b")), FuMethod.NewStatic(this.FloatType, FuId.MathMaxDouble, "Max", FuVar.New(this.DoubleType, "a"), FuVar.New(this.DoubleType, "b"))));
			mathClass.Add(FuMethodGroup.New(FuMethod.NewStatic(this.IntType, FuId.MathMinInt, "Min", FuVar.New(this.LongType, "a"), FuVar.New(this.LongType, "b")), FuMethod.NewStatic(this.FloatType, FuId.MathMinDouble, "Min", FuVar.New(this.DoubleType, "a"), FuVar.New(this.DoubleType, "b"))));
			mathClass.Add(FuStaticProperty.New(this.FloatType, FuId.MathNaN, "NaN"));
			mathClass.Add(FuStaticProperty.New(this.FloatType, FuId.MathNegativeInfinity, "NegativeInfinity"));
			mathClass.Add(NewConstDouble("PI", 3.141592653589793));
			mathClass.Add(FuStaticProperty.New(this.FloatType, FuId.MathPositiveInfinity, "PositiveInfinity"));
			mathClass.Add(FuMethod.NewStatic(this.FloatType, FuId.MathMethod, "Pow", FuVar.New(this.DoubleType, "x"), FuVar.New(this.DoubleType, "y")));
			mathClass.Add(FuMethod.NewStatic(floatIntType, FuId.MathRound, "Round", FuVar.New(this.DoubleType, "a")));
			mathClass.Add(FuMethod.NewStatic(this.FloatType, FuId.MathMethod, "Sin", FuVar.New(this.DoubleType, "a")));
			mathClass.Add(FuMethod.NewStatic(this.FloatType, FuId.MathMethod, "Sinh", FuVar.New(this.DoubleType, "a")));
			mathClass.Add(FuMethod.NewStatic(this.FloatType, FuId.MathMethod, "Sqrt", FuVar.New(this.DoubleType, "a")));
			mathClass.Add(FuMethod.NewStatic(this.FloatType, FuId.MathMethod, "Tan", FuVar.New(this.DoubleType, "a")));
			mathClass.Add(FuMethod.NewStatic(this.FloatType, FuId.MathMethod, "Tanh", FuVar.New(this.DoubleType, "a")));
			mathClass.Add(FuMethod.NewStatic(floatIntType, FuId.MathTruncate, "Truncate", FuVar.New(this.DoubleType, "a")));
			Add(mathClass);
			FuClass lockClass = FuClass.New(FuCallType.Sealed, FuId.LockClass, "Lock");
			Add(lockClass);
			this.LockPtrType.Class = lockClass;
		}

		internal FuType VoidType = new FuType { Id = FuId.VoidType, Name = "void" };

		internal FuType NullType = new FuType { Id = FuId.NullType, Name = "null", Nullable = true };

		FuType TypeParam0 = new FuType { Id = FuId.TypeParam0, Name = "T" };

		internal FuIntegerType IntType = new FuIntegerType { Id = FuId.IntType, Name = "int" };

		FuRangeType UIntType = FuRangeType.New(0, 2147483647);

		internal FuIntegerType LongType = new FuIntegerType { Id = FuId.LongType, Name = "long" };

		internal FuRangeType ByteType = FuRangeType.New(0, 255);

		FuFloatingType FloatType = new FuFloatingType { Id = FuId.FloatType, Name = "float" };

		internal FuFloatingType DoubleType = new FuFloatingType { Id = FuId.DoubleType, Name = "double" };

		internal FuRangeType CharType = FuRangeType.New(-128, 65535);

		internal FuEnum BoolType = new FuEnum { Id = FuId.BoolType, Name = "bool" };

		FuClass StringClass = FuClass.New(FuCallType.Normal, FuId.StringClass, "string");

		internal FuStringType StringPtrType = new FuStringType { Id = FuId.StringPtrType, Name = "string" };

		internal FuStringType StringNullablePtrType = new FuStringType { Id = FuId.StringPtrType, Name = "string", Nullable = true };

		internal FuStringStorageType StringStorageType = new FuStringStorageType { Id = FuId.StringStorageType };

		internal FuType PrintableType = new FuPrintableType { Name = "printable" };

		internal FuClass ArrayPtrClass = FuClass.New(FuCallType.Normal, FuId.ArrayPtrClass, "ArrayPtr", 1);

		internal FuClass ArrayStorageClass = FuClass.New(FuCallType.Normal, FuId.ArrayStorageClass, "ArrayStorage", 1);

		internal FuEnum RegexOptionsEnum;

		internal FuReadWriteClassType LockPtrType = new FuReadWriteClassType();

		internal FuLiteralLong NewLiteralLong(long value, int line = 0)
		{
			FuType type = value >= -2147483648 && value <= 2147483647 ? FuRangeType.New((int) value, (int) value) : this.LongType;
			return new FuLiteralLong { Line = line, Type = type, Value = value };
		}

		internal FuLiteralString NewLiteralString(string value, int line = 0) => new FuLiteralString { Line = line, Type = this.StringPtrType, Value = value };

		internal FuType PromoteIntegerTypes(FuType left, FuType right)
		{
			return left == this.LongType || right == this.LongType ? this.LongType : this.IntType;
		}

		internal FuType PromoteFloatingTypes(FuType left, FuType right)
		{
			if (left.Id == FuId.DoubleType || right.Id == FuId.DoubleType)
				return this.DoubleType;
			if (left.Id == FuId.FloatType || right.Id == FuId.FloatType || left.Id == FuId.FloatIntType || right.Id == FuId.FloatIntType)
				return this.FloatType;
			return null;
		}

		internal FuType PromoteNumericTypes(FuType left, FuType right)
		{
			FuType result = PromoteFloatingTypes(left, right);
			return result != null ? result : PromoteIntegerTypes(left, right);
		}

		internal FuEnum NewEnum(bool flags)
		{
			FuEnum enu = flags ? new FuEnumFlags() : new FuEnum();
			enu.Add(FuMethod.NewStatic(enu, FuId.EnumFromInt, "FromInt", FuVar.New(this.IntType, "value")));
			if (flags)
				enu.Add(FuMethod.New(FuVisibility.Public, this.BoolType, FuId.EnumHasFlag, "HasFlag", FuVar.New(enu, "flag")));
			return enu;
		}

		FuClass AddCollection(FuId id, string name, int typeParameterCount, FuId clearId, FuId countId)
		{
			FuClass result = FuClass.New(FuCallType.Normal, id, name, typeParameterCount);
			result.Add(FuMethod.NewMutator(FuVisibility.Public, this.VoidType, clearId, "Clear"));
			result.Add(FuProperty.New(this.UIntType, countId, "Count"));
			Add(result);
			return result;
		}

		void AddSet(FuId id, string name, FuId addId, FuId clearId, FuId containsId, FuId countId, FuId removeId)
		{
			FuClass set = AddCollection(id, name, 1, clearId, countId);
			set.Add(FuMethod.NewMutator(FuVisibility.Public, this.VoidType, addId, "Add", FuVar.New(this.TypeParam0, "value")));
			set.Add(FuMethod.New(FuVisibility.Public, this.BoolType, containsId, "Contains", FuVar.New(this.TypeParam0, "value")));
			set.Add(FuMethod.NewMutator(FuVisibility.Public, this.VoidType, removeId, "Remove", FuVar.New(this.TypeParam0, "value")));
		}

		void AddDictionary(FuId id, string name, FuId clearId, FuId containsKeyId, FuId countId, FuId removeId)
		{
			FuClass dict = AddCollection(id, name, 2, clearId, countId);
			dict.Add(FuMethod.NewMutator(FuVisibility.FinalValueType, this.VoidType, FuId.DictionaryAdd, "Add", FuVar.New(this.TypeParam0, "key")));
			dict.Add(FuMethod.New(FuVisibility.Public, this.BoolType, containsKeyId, "ContainsKey", FuVar.New(this.TypeParam0, "key")));
			dict.Add(FuMethod.NewMutator(FuVisibility.Public, this.VoidType, removeId, "Remove", FuVar.New(this.TypeParam0, "key")));
		}

		static void AddEnumValue(FuEnum enu, FuConst value)
		{
			value.Type = enu;
			enu.Add(value);
		}

		FuConst NewConstLong(string name, long value)
		{
			FuConst result = new FuConst { Visibility = FuVisibility.Public, Name = name, Value = NewLiteralLong(value), VisitStatus = FuVisitStatus.Done };
			result.Type = result.Value.Type;
			return result;
		}

		FuConst NewConstDouble(string name, double value) => new FuConst { Visibility = FuVisibility.Public, Name = name, Value = new FuLiteralDouble { Value = value, Type = this.DoubleType }, Type = this.DoubleType, VisitStatus = FuVisitStatus.Done };

		void AddMinMaxValue(FuIntegerType target, long min, long max)
		{
			target.Add(NewConstLong("MinValue", min));
			target.Add(NewConstLong("MaxValue", max));
		}

		internal static FuSystem New() => new FuSystem();
	}

	public class FuProgram : FuScope
	{

		internal FuSystem System;

		internal readonly List<string> TopLevelNatives = new List<string>();

		internal readonly List<FuClass> Classes = new List<FuClass>();

		internal readonly SortedDictionary<string, List<byte>> Resources = new SortedDictionary<string, List<byte>>();

		internal bool RegexOptionsEnum = false;
	}

	public class FuParser : FuLexer
	{

		internal FuProgram Program;

		string XcrementParent = null;

		FuLoop CurrentLoop = null;

		FuCondCompletionStatement CurrentLoopOrSwitch = null;

		bool DocParseLine(FuDocPara para)
		{
			if (para.Children.Count > 0)
				para.Children.Add(new FuDocLine());
			this.LexemeOffset = this.CharOffset;
			for (int lastNonWhitespace = 0;;) {
				switch (PeekChar()) {
				case -1:
				case '\n':
				case '\r':
					para.Children.Add(new FuDocText { Text = GetLexeme() });
					return lastNonWhitespace == '.';
				case '\t':
				case ' ':
					ReadChar();
					break;
				case '`':
					if (this.CharOffset > this.LexemeOffset)
						para.Children.Add(new FuDocText { Text = GetLexeme() });
					ReadChar();
					this.LexemeOffset = this.CharOffset;
					for (;;) {
						int c = PeekChar();
						if (c == '`') {
							para.Children.Add(new FuDocCode { Text = GetLexeme() });
							ReadChar();
							break;
						}
						if (c < 0 || c == '\n') {
							ReportError("Unterminated code in documentation comment");
							break;
						}
						ReadChar();
					}
					this.LexemeOffset = this.CharOffset;
					lastNonWhitespace = '`';
					break;
				default:
					lastNonWhitespace = ReadChar();
					break;
				}
			}
		}

		void DocParsePara(FuDocPara para)
		{
			do {
				DocParseLine(para);
				NextToken();
			}
			while (See(FuToken.DocRegular));
		}

		FuCodeDoc ParseDoc()
		{
			if (!See(FuToken.DocRegular))
				return null;
			FuCodeDoc doc = new FuCodeDoc();
			bool period;
			do {
				period = DocParseLine(doc.Summary);
				NextToken();
			}
			while (!period && See(FuToken.DocRegular));
			for (;;) {
				switch (this.CurrentToken) {
				case FuToken.DocRegular:
					FuDocPara para = new FuDocPara();
					DocParsePara(para);
					doc.Details.Add(para);
					break;
				case FuToken.DocBullet:
					FuDocList list = new FuDocList();
					do {
						list.Items.Add(new FuDocPara());
						DocParsePara(list.Items[^1]);
					}
					while (See(FuToken.DocBullet));
					doc.Details.Add(list);
					break;
				case FuToken.DocBlank:
					NextToken();
					break;
				default:
					return doc;
				}
			}
		}

		void CheckXcrementParent()
		{
			if (this.XcrementParent != null) {
				string op = See(FuToken.Increment) ? "++" : "--";
				ReportError($"{op} not allowed on the right side of {this.XcrementParent}");
			}
		}

		FuLiteralDouble ParseDouble()
		{
			double d;
			if (!double.TryParse(GetLexeme().Replace("_", ""), out d))
				ReportError("Invalid floating-point number");
			FuLiteralDouble result = new FuLiteralDouble { Line = this.Line, Type = this.Program.System.DoubleType, Value = d };
			NextToken();
			return result;
		}

		bool SeeDigit()
		{
			int c = PeekChar();
			return c >= '0' && c <= '9';
		}

		FuInterpolatedString ParseInterpolatedString()
		{
			FuInterpolatedString result = new FuInterpolatedString { Line = this.Line };
			do {
				string prefix = this.StringValue.Replace("{{", "{");
				NextToken();
				FuExpr arg = ParseExpr();
				FuExpr width = Eat(FuToken.Comma) ? ParseExpr() : null;
				int format = ' ';
				int precision = -1;
				if (See(FuToken.Colon)) {
					format = ReadChar();
					if (SeeDigit()) {
						precision = ReadChar() - '0';
						if (SeeDigit())
							precision = precision * 10 + ReadChar() - '0';
					}
					NextToken();
				}
				result.AddPart(prefix, arg, width, format, precision);
				Check(FuToken.RightBrace);
			}
			while (ReadString(true) == FuToken.InterpolatedString);
			result.Suffix = this.StringValue.Replace("{{", "{");
			NextToken();
			return result;
		}

		FuExpr ParseParenthesized()
		{
			Expect(FuToken.LeftParenthesis);
			FuExpr result = ParseExpr();
			Expect(FuToken.RightParenthesis);
			return result;
		}

		FuSymbolReference ParseSymbolReference(FuExpr left)
		{
			Check(FuToken.Id);
			FuSymbolReference result = new FuSymbolReference { Line = this.Line, Left = left, Name = this.StringValue };
			NextToken();
			return result;
		}

		void ParseCollection(List<FuExpr> result, FuToken closing)
		{
			if (!See(closing)) {
				do
					result.Add(ParseExpr());
				while (Eat(FuToken.Comma));
			}
			ExpectOrSkip(closing);
		}

		FuExpr ParsePrimaryExpr(bool type)
		{
			FuExpr result;
			switch (this.CurrentToken) {
			case FuToken.Increment:
			case FuToken.Decrement:
				CheckXcrementParent();
				return new FuPrefixExpr { Line = this.Line, Op = NextToken(), Inner = ParsePrimaryExpr(false) };
			case FuToken.Minus:
			case FuToken.Tilde:
			case FuToken.ExclamationMark:
				return new FuPrefixExpr { Line = this.Line, Op = NextToken(), Inner = ParsePrimaryExpr(false) };
			case FuToken.New:
				FuPrefixExpr newResult = new FuPrefixExpr { Line = this.Line, Op = NextToken() };
				result = ParseType();
				if (Eat(FuToken.LeftBrace))
					result = new FuBinaryExpr { Line = this.Line, Left = result, Op = FuToken.LeftBrace, Right = ParseObjectLiteral() };
				newResult.Inner = result;
				return newResult;
			case FuToken.LiteralLong:
				result = this.Program.System.NewLiteralLong(this.LongValue, this.Line);
				NextToken();
				break;
			case FuToken.LiteralDouble:
				result = ParseDouble();
				break;
			case FuToken.LiteralChar:
				result = FuLiteralChar.New((int) this.LongValue, this.Line);
				NextToken();
				break;
			case FuToken.LiteralString:
				result = this.Program.System.NewLiteralString(this.StringValue, this.Line);
				NextToken();
				break;
			case FuToken.False:
				result = new FuLiteralFalse { Line = this.Line, Type = this.Program.System.BoolType };
				NextToken();
				break;
			case FuToken.True:
				result = new FuLiteralTrue { Line = this.Line, Type = this.Program.System.BoolType };
				NextToken();
				break;
			case FuToken.Null:
				result = new FuLiteralNull { Line = this.Line, Type = this.Program.System.NullType };
				NextToken();
				break;
			case FuToken.InterpolatedString:
				result = ParseInterpolatedString();
				break;
			case FuToken.LeftParenthesis:
				result = ParseParenthesized();
				break;
			case FuToken.Id:
				FuSymbolReference symbol = ParseSymbolReference(null);
				if (Eat(FuToken.FatArrow)) {
					FuLambdaExpr lambda = new FuLambdaExpr { Line = symbol.Line };
					lambda.Add(FuVar.New(null, symbol.Name));
					lambda.Body = ParseExpr();
					return lambda;
				}
				if (type && Eat(FuToken.Less)) {
					FuAggregateInitializer typeArgs = new FuAggregateInitializer();
					bool saveTypeArg = this.ParsingTypeArg;
					this.ParsingTypeArg = true;
					do
						typeArgs.Items.Add(ParseType());
					while (Eat(FuToken.Comma));
					Expect(FuToken.RightAngle);
					this.ParsingTypeArg = saveTypeArg;
					symbol.Left = typeArgs;
				}
				result = symbol;
				break;
			case FuToken.Resource:
				NextToken();
				if (Eat(FuToken.Less) && this.StringValue == "byte" && Eat(FuToken.Id) && Eat(FuToken.LeftBracket) && Eat(FuToken.RightBracket) && Eat(FuToken.Greater))
					result = new FuPrefixExpr { Line = this.Line, Op = FuToken.Resource, Inner = ParseParenthesized() };
				else {
					ReportError("Expected 'resource<byte[]>'");
					result = null;
				}
				break;
			default:
				ReportError("Invalid expression");
				result = null;
				break;
			}
			for (;;) {
				switch (this.CurrentToken) {
				case FuToken.Dot:
					NextToken();
					result = ParseSymbolReference(result);
					break;
				case FuToken.LeftParenthesis:
					NextToken();
					if (result is FuSymbolReference method) {
						FuCallExpr call = new FuCallExpr { Line = this.Line, Method = method };
						ParseCollection(call.Arguments, FuToken.RightParenthesis);
						result = call;
					}
					else
						ReportError("Expected a method");
					break;
				case FuToken.LeftBracket:
					result = new FuBinaryExpr { Line = this.Line, Left = result, Op = NextToken(), Right = See(FuToken.RightBracket) ? null : ParseExpr() };
					Expect(FuToken.RightBracket);
					break;
				case FuToken.Increment:
				case FuToken.Decrement:
					CheckXcrementParent();
					result = new FuPostfixExpr { Line = this.Line, Inner = result, Op = NextToken() };
					break;
				case FuToken.ExclamationMark:
				case FuToken.Hash:
					result = new FuPostfixExpr { Line = this.Line, Inner = result, Op = NextToken() };
					break;
				case FuToken.QuestionMark:
					if (!type)
						return result;
					result = new FuPostfixExpr { Line = this.Line, Inner = result, Op = NextToken() };
					break;
				default:
					return result;
				}
			}
		}

		FuExpr ParseMulExpr()
		{
			FuExpr left = ParsePrimaryExpr(false);
			for (;;) {
				switch (this.CurrentToken) {
				case FuToken.Asterisk:
				case FuToken.Slash:
				case FuToken.Mod:
					left = new FuBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParsePrimaryExpr(false) };
					break;
				default:
					return left;
				}
			}
		}

		FuExpr ParseAddExpr()
		{
			FuExpr left = ParseMulExpr();
			while (See(FuToken.Plus) || See(FuToken.Minus))
				left = new FuBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParseMulExpr() };
			return left;
		}

		FuExpr ParseShiftExpr()
		{
			FuExpr left = ParseAddExpr();
			while (See(FuToken.ShiftLeft) || See(FuToken.ShiftRight))
				left = new FuBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParseAddExpr() };
			return left;
		}

		FuExpr ParseRelExpr()
		{
			FuExpr left = ParseShiftExpr();
			for (;;) {
				switch (this.CurrentToken) {
				case FuToken.Less:
				case FuToken.LessOrEqual:
				case FuToken.Greater:
				case FuToken.GreaterOrEqual:
					left = new FuBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParseShiftExpr() };
					break;
				case FuToken.Is:
					FuBinaryExpr isExpr = new FuBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParsePrimaryExpr(false) };
					if (See(FuToken.Id)) {
						isExpr.Right = new FuVar { Line = this.Line, TypeExpr = isExpr.Right, Name = this.StringValue };
						NextToken();
					}
					return isExpr;
				default:
					return left;
				}
			}
		}

		FuExpr ParseEqualityExpr()
		{
			FuExpr left = ParseRelExpr();
			while (See(FuToken.Equal) || See(FuToken.NotEqual))
				left = new FuBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParseRelExpr() };
			return left;
		}

		FuExpr ParseAndExpr()
		{
			FuExpr left = ParseEqualityExpr();
			while (See(FuToken.And))
				left = new FuBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParseEqualityExpr() };
			return left;
		}

		FuExpr ParseXorExpr()
		{
			FuExpr left = ParseAndExpr();
			while (See(FuToken.Xor))
				left = new FuBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParseAndExpr() };
			return left;
		}

		FuExpr ParseOrExpr()
		{
			FuExpr left = ParseXorExpr();
			while (See(FuToken.Or))
				left = new FuBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParseXorExpr() };
			return left;
		}

		FuExpr ParseCondAndExpr()
		{
			FuExpr left = ParseOrExpr();
			while (See(FuToken.CondAnd)) {
				string saveXcrementParent = this.XcrementParent;
				this.XcrementParent = "&&";
				left = new FuBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParseOrExpr() };
				this.XcrementParent = saveXcrementParent;
			}
			return left;
		}

		FuExpr ParseCondOrExpr()
		{
			FuExpr left = ParseCondAndExpr();
			while (See(FuToken.CondOr)) {
				string saveXcrementParent = this.XcrementParent;
				this.XcrementParent = "||";
				left = new FuBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParseCondAndExpr() };
				this.XcrementParent = saveXcrementParent;
			}
			return left;
		}

		FuExpr ParseExpr()
		{
			FuExpr left = ParseCondOrExpr();
			if (See(FuToken.QuestionMark)) {
				FuSelectExpr result = new FuSelectExpr { Line = this.Line, Cond = left };
				NextToken();
				string saveXcrementParent = this.XcrementParent;
				this.XcrementParent = "?";
				result.OnTrue = ParseExpr();
				Expect(FuToken.Colon);
				result.OnFalse = ParseExpr();
				this.XcrementParent = saveXcrementParent;
				return result;
			}
			return left;
		}

		FuExpr ParseType()
		{
			FuExpr left = ParsePrimaryExpr(true);
			if (Eat(FuToken.Range))
				return new FuBinaryExpr { Line = this.Line, Left = left, Op = FuToken.Range, Right = ParsePrimaryExpr(true) };
			return left;
		}

		FuExpr ParseConstInitializer()
		{
			if (Eat(FuToken.LeftBrace)) {
				FuAggregateInitializer result = new FuAggregateInitializer { Line = this.Line };
				ParseCollection(result.Items, FuToken.RightBrace);
				return result;
			}
			return ParseExpr();
		}

		FuAggregateInitializer ParseObjectLiteral()
		{
			FuAggregateInitializer result = new FuAggregateInitializer { Line = this.Line };
			do {
				int line = this.Line;
				FuExpr field = ParseSymbolReference(null);
				Expect(FuToken.Assign);
				result.Items.Add(new FuBinaryExpr { Line = line, Left = field, Op = FuToken.Assign, Right = ParseExpr() });
			}
			while (Eat(FuToken.Comma));
			Expect(FuToken.RightBrace);
			return result;
		}

		FuExpr ParseInitializer()
		{
			if (!Eat(FuToken.Assign))
				return null;
			if (Eat(FuToken.LeftBrace))
				return ParseObjectLiteral();
			return ParseExpr();
		}

		void AddSymbol(FuScope scope, FuSymbol symbol)
		{
			if (scope.Contains(symbol))
				ReportError("Duplicate symbol");
			else
				scope.Add(symbol);
		}

		FuVar ParseVar(FuExpr type)
		{
			FuVar result = new FuVar { Line = this.Line, TypeExpr = type, Name = this.StringValue };
			NextToken();
			result.Value = ParseInitializer();
			return result;
		}

		FuConst ParseConst(FuVisibility visibility)
		{
			Expect(FuToken.Const);
			FuConst konst = new FuConst { Line = this.Line, Visibility = visibility, TypeExpr = ParseType(), Name = this.StringValue, VisitStatus = FuVisitStatus.NotYet };
			NextToken();
			Expect(FuToken.Assign);
			konst.Value = ParseConstInitializer();
			Expect(FuToken.Semicolon);
			return konst;
		}

		FuExpr ParseAssign(bool allowVar)
		{
			FuExpr left = allowVar ? ParseType() : ParseExpr();
			switch (this.CurrentToken) {
			case FuToken.Assign:
			case FuToken.AddAssign:
			case FuToken.SubAssign:
			case FuToken.MulAssign:
			case FuToken.DivAssign:
			case FuToken.ModAssign:
			case FuToken.AndAssign:
			case FuToken.OrAssign:
			case FuToken.XorAssign:
			case FuToken.ShiftLeftAssign:
			case FuToken.ShiftRightAssign:
				return new FuBinaryExpr { Line = this.Line, Left = left, Op = NextToken(), Right = ParseAssign(false) };
			case FuToken.Id:
				if (allowVar)
					return ParseVar(left);
				return left;
			default:
				return left;
			}
		}

		FuBlock ParseBlock()
		{
			FuBlock result = new FuBlock { Line = this.Line };
			Expect(FuToken.LeftBrace);
			while (!See(FuToken.RightBrace) && !See(FuToken.EndOfFile))
				result.Statements.Add(ParseStatement());
			Expect(FuToken.RightBrace);
			return result;
		}

		FuAssert ParseAssert()
		{
			FuAssert result = new FuAssert { Line = this.Line };
			Expect(FuToken.Assert);
			result.Cond = ParseExpr();
			if (Eat(FuToken.Comma))
				result.Message = ParseExpr();
			Expect(FuToken.Semicolon);
			return result;
		}

		FuBreak ParseBreak()
		{
			if (this.CurrentLoopOrSwitch == null)
				ReportError("break outside loop or switch");
			FuBreak result = new FuBreak { Line = this.Line, LoopOrSwitch = this.CurrentLoopOrSwitch };
			Expect(FuToken.Break);
			Expect(FuToken.Semicolon);
			if (this.CurrentLoopOrSwitch is FuLoop loop)
				loop.HasBreak = true;
			return result;
		}

		FuContinue ParseContinue()
		{
			if (this.CurrentLoop == null)
				ReportError("continue outside loop");
			FuContinue result = new FuContinue { Line = this.Line, Loop = this.CurrentLoop };
			Expect(FuToken.Continue);
			Expect(FuToken.Semicolon);
			return result;
		}

		void ParseLoopBody(FuLoop loop)
		{
			FuLoop outerLoop = this.CurrentLoop;
			FuCondCompletionStatement outerLoopOrSwitch = this.CurrentLoopOrSwitch;
			this.CurrentLoop = loop;
			this.CurrentLoopOrSwitch = loop;
			loop.Body = ParseStatement();
			this.CurrentLoopOrSwitch = outerLoopOrSwitch;
			this.CurrentLoop = outerLoop;
		}

		FuDoWhile ParseDoWhile()
		{
			FuDoWhile result = new FuDoWhile { Line = this.Line };
			Expect(FuToken.Do);
			ParseLoopBody(result);
			Expect(FuToken.While);
			result.Cond = ParseParenthesized();
			Expect(FuToken.Semicolon);
			return result;
		}

		FuFor ParseFor()
		{
			FuFor result = new FuFor { Line = this.Line };
			Expect(FuToken.For);
			Expect(FuToken.LeftParenthesis);
			if (!See(FuToken.Semicolon))
				result.Init = ParseAssign(true);
			Expect(FuToken.Semicolon);
			if (!See(FuToken.Semicolon))
				result.Cond = ParseExpr();
			Expect(FuToken.Semicolon);
			if (!See(FuToken.RightParenthesis))
				result.Advance = ParseAssign(false);
			Expect(FuToken.RightParenthesis);
			ParseLoopBody(result);
			return result;
		}

		void ParseForeachIterator(FuForeach result)
		{
			AddSymbol(result, new FuVar { Line = this.Line, TypeExpr = ParseType(), Name = this.StringValue });
			NextToken();
		}

		FuForeach ParseForeach()
		{
			FuForeach result = new FuForeach { Line = this.Line };
			Expect(FuToken.Foreach);
			Expect(FuToken.LeftParenthesis);
			if (Eat(FuToken.LeftParenthesis)) {
				ParseForeachIterator(result);
				Expect(FuToken.Comma);
				ParseForeachIterator(result);
				Expect(FuToken.RightParenthesis);
			}
			else
				ParseForeachIterator(result);
			Expect(FuToken.In);
			result.Collection = ParseExpr();
			Expect(FuToken.RightParenthesis);
			ParseLoopBody(result);
			return result;
		}

		FuIf ParseIf()
		{
			FuIf result = new FuIf { Line = this.Line };
			Expect(FuToken.If);
			result.Cond = ParseParenthesized();
			result.OnTrue = ParseStatement();
			if (Eat(FuToken.Else))
				result.OnFalse = ParseStatement();
			return result;
		}

		FuLock ParseLock()
		{
			FuLock result = new FuLock { Line = this.Line };
			Expect(FuToken.Lock_);
			result.Lock = ParseParenthesized();
			result.Body = ParseStatement();
			return result;
		}

		FuNative ParseNative()
		{
			FuNative result = new FuNative { Line = this.Line };
			Expect(FuToken.Native);
			if (See(FuToken.LiteralString))
				result.Content = this.StringValue;
			else {
				int offset = this.CharOffset;
				Expect(FuToken.LeftBrace);
				int nesting = 1;
				for (;;) {
					if (See(FuToken.EndOfFile)) {
						Expect(FuToken.RightBrace);
						return result;
					}
					if (See(FuToken.LeftBrace))
						nesting++;
					else if (See(FuToken.RightBrace)) {
						if (--nesting == 0)
							break;
					}
					NextToken();
				}
				Debug.Assert(this.Input[this.CharOffset - 1] == '}');
				result.Content = Encoding.UTF8.GetString(this.Input, offset, this.CharOffset - 1 - offset);
			}
			NextToken();
			return result;
		}

		FuReturn ParseReturn()
		{
			FuReturn result = new FuReturn { Line = this.Line };
			NextToken();
			if (!See(FuToken.Semicolon))
				result.Value = ParseExpr();
			Expect(FuToken.Semicolon);
			return result;
		}

		FuSwitch ParseSwitch()
		{
			FuSwitch result = new FuSwitch { Line = this.Line };
			Expect(FuToken.Switch);
			result.Value = ParseParenthesized();
			Expect(FuToken.LeftBrace);
			FuCondCompletionStatement outerLoopOrSwitch = this.CurrentLoopOrSwitch;
			this.CurrentLoopOrSwitch = result;
			while (Eat(FuToken.Case)) {
				result.Cases.Add(new FuCase());
				FuCase kase = result.Cases[^1];
				do {
					FuExpr expr = ParseExpr();
					if (See(FuToken.Id))
						expr = ParseVar(expr);
					if (Eat(FuToken.When))
						expr = new FuBinaryExpr { Line = this.Line, Left = expr, Op = FuToken.When, Right = ParseExpr() };
					kase.Values.Add(expr);
					Expect(FuToken.Colon);
				}
				while (Eat(FuToken.Case));
				if (See(FuToken.Default)) {
					ReportError("Please remove 'case' before 'default'");
					break;
				}
				while (!See(FuToken.EndOfFile)) {
					kase.Body.Add(ParseStatement());
					switch (this.CurrentToken) {
					case FuToken.Case:
					case FuToken.Default:
					case FuToken.RightBrace:
						break;
					default:
						continue;
					}
					break;
				}
			}
			if (result.Cases.Count == 0)
				ReportError("Switch with no cases");
			if (Eat(FuToken.Default)) {
				Expect(FuToken.Colon);
				do {
					if (See(FuToken.EndOfFile))
						break;
					result.DefaultBody.Add(ParseStatement());
				}
				while (!See(FuToken.RightBrace));
			}
			Expect(FuToken.RightBrace);
			this.CurrentLoopOrSwitch = outerLoopOrSwitch;
			return result;
		}

		FuThrow ParseThrow()
		{
			FuThrow result = new FuThrow { Line = this.Line };
			Expect(FuToken.Throw);
			result.Message = ParseExpr();
			Expect(FuToken.Semicolon);
			return result;
		}

		FuWhile ParseWhile()
		{
			FuWhile result = new FuWhile { Line = this.Line };
			Expect(FuToken.While);
			result.Cond = ParseParenthesized();
			ParseLoopBody(result);
			return result;
		}

		FuStatement ParseStatement()
		{
			switch (this.CurrentToken) {
			case FuToken.LeftBrace:
				return ParseBlock();
			case FuToken.Assert:
				return ParseAssert();
			case FuToken.Break:
				return ParseBreak();
			case FuToken.Const:
				return ParseConst(FuVisibility.Private);
			case FuToken.Continue:
				return ParseContinue();
			case FuToken.Do:
				return ParseDoWhile();
			case FuToken.For:
				return ParseFor();
			case FuToken.Foreach:
				return ParseForeach();
			case FuToken.If:
				return ParseIf();
			case FuToken.Lock_:
				return ParseLock();
			case FuToken.Native:
				return ParseNative();
			case FuToken.Return:
				return ParseReturn();
			case FuToken.Switch:
				return ParseSwitch();
			case FuToken.Throw:
				return ParseThrow();
			case FuToken.While:
				return ParseWhile();
			default:
				FuExpr expr = ParseAssign(true);
				Expect(FuToken.Semicolon);
				return expr;
			}
		}

		FuCallType ParseCallType()
		{
			switch (this.CurrentToken) {
			case FuToken.Static:
				NextToken();
				return FuCallType.Static;
			case FuToken.Abstract:
				NextToken();
				return FuCallType.Abstract;
			case FuToken.Virtual:
				NextToken();
				return FuCallType.Virtual;
			case FuToken.Override:
				NextToken();
				return FuCallType.Override;
			case FuToken.Sealed:
				NextToken();
				return FuCallType.Sealed;
			default:
				return FuCallType.Normal;
			}
		}

		void ParseMethod(FuMethod method)
		{
			method.IsMutator = Eat(FuToken.ExclamationMark);
			Expect(FuToken.LeftParenthesis);
			if (!See(FuToken.RightParenthesis)) {
				do {
					FuCodeDoc doc = ParseDoc();
					FuVar param = ParseVar(ParseType());
					param.Documentation = doc;
					AddSymbol(method.Parameters, param);
				}
				while (Eat(FuToken.Comma));
			}
			Expect(FuToken.RightParenthesis);
			method.Throws = Eat(FuToken.Throws);
			if (method.CallType == FuCallType.Abstract)
				Expect(FuToken.Semicolon);
			else if (See(FuToken.FatArrow))
				method.Body = ParseReturn();
			else if (Check(FuToken.LeftBrace))
				method.Body = ParseBlock();
		}

		static string CallTypeToString(FuCallType callType)
		{
			switch (callType) {
			case FuCallType.Static:
				return "static";
			case FuCallType.Normal:
				return "normal";
			case FuCallType.Abstract:
				return "abstract";
			case FuCallType.Virtual:
				return "virtual";
			case FuCallType.Override:
				return "override";
			case FuCallType.Sealed:
				return "sealed";
			default:
				throw new NotImplementedException();
			}
		}

		void ParseClass(FuCodeDoc doc, bool isPublic, FuCallType callType)
		{
			Expect(FuToken.Class);
			FuClass klass = new FuClass { Filename = this.Filename, Line = this.Line, Documentation = doc, IsPublic = isPublic, CallType = callType, Name = this.StringValue };
			if (Expect(FuToken.Id))
				AddSymbol(this.Program, klass);
			if (Eat(FuToken.Colon)) {
				klass.BaseClassName = this.StringValue;
				Expect(FuToken.Id);
			}
			Expect(FuToken.LeftBrace);
			while (!See(FuToken.RightBrace) && !See(FuToken.EndOfFile)) {
				doc = ParseDoc();
				FuVisibility visibility;
				switch (this.CurrentToken) {
				case FuToken.Internal:
					visibility = FuVisibility.Internal;
					NextToken();
					break;
				case FuToken.Protected:
					visibility = FuVisibility.Protected;
					NextToken();
					break;
				case FuToken.Public:
					visibility = FuVisibility.Public;
					NextToken();
					break;
				default:
					visibility = FuVisibility.Private;
					break;
				}
				if (See(FuToken.Const)) {
					FuConst konst = ParseConst(visibility);
					konst.Documentation = doc;
					AddSymbol(klass, konst);
					continue;
				}
				callType = ParseCallType();
				FuExpr type = Eat(FuToken.Void) ? this.Program.System.VoidType : ParseType();
				if (See(FuToken.LeftBrace) && type is FuCallExpr call) {
					if (call.Method.Name != klass.Name)
						ReportError("Method with no return type");
					else {
						if (klass.CallType == FuCallType.Static)
							ReportError("Constructor in a static class");
						if (callType != FuCallType.Normal)
							ReportError($"Constructor cannot be {CallTypeToString(callType)}");
						if (call.Arguments.Count != 0)
							ReportError("Constructor parameters not supported");
						if (klass.Constructor != null)
							ReportError($"Duplicate constructor, already defined in line {klass.Constructor.Line}");
					}
					if (visibility == FuVisibility.Private)
						visibility = FuVisibility.Internal;
					klass.Constructor = new FuMethodBase { Line = call.Line, Documentation = doc, Visibility = visibility, Parent = klass, Type = this.Program.System.VoidType, Name = klass.Name, IsMutator = true, Body = ParseBlock() };
					continue;
				}
				int line = this.Line;
				string name = this.StringValue;
				if (!Expect(FuToken.Id))
					continue;
				if (See(FuToken.LeftParenthesis) || See(FuToken.ExclamationMark)) {
					if (callType == FuCallType.Static || klass.CallType == FuCallType.Abstract) {
					}
					else if (klass.CallType == FuCallType.Static)
						ReportError("Only static methods allowed in a static class");
					else if (callType == FuCallType.Abstract)
						ReportError("Abstract methods allowed only in an abstract class");
					else if (klass.CallType == FuCallType.Sealed && callType == FuCallType.Virtual)
						ReportError("Virtual methods disallowed in a sealed class");
					if (visibility == FuVisibility.Private && callType != FuCallType.Static && callType != FuCallType.Normal)
						ReportError($"{CallTypeToString(callType)} method cannot be private");
					FuMethod method = new FuMethod { Line = line, Documentation = doc, Visibility = visibility, CallType = callType, TypeExpr = type, Name = name };
					AddSymbol(klass, method);
					method.Parameters.Parent = klass;
					ParseMethod(method);
					continue;
				}
				if (visibility == FuVisibility.Public)
					ReportError("Field cannot be public");
				if (callType != FuCallType.Normal)
					ReportError($"Field cannot be {CallTypeToString(callType)}");
				if (type == this.Program.System.VoidType)
					ReportError("Field cannot be void");
				FuField field = new FuField { Line = line, Documentation = doc, Visibility = visibility, TypeExpr = type, Name = name, Value = ParseInitializer() };
				AddSymbol(klass, field);
				Expect(FuToken.Semicolon);
			}
			Expect(FuToken.RightBrace);
		}

		void ParseEnum(FuCodeDoc doc, bool isPublic)
		{
			Expect(FuToken.Enum);
			bool flags = Eat(FuToken.Asterisk);
			FuEnum enu = this.Program.System.NewEnum(flags);
			enu.Filename = this.Filename;
			enu.Line = this.Line;
			enu.Documentation = doc;
			enu.IsPublic = isPublic;
			enu.Name = this.StringValue;
			if (Expect(FuToken.Id))
				AddSymbol(this.Program, enu);
			Expect(FuToken.LeftBrace);
			do {
				FuConst konst = new FuConst { Visibility = FuVisibility.Public, Documentation = ParseDoc(), Line = this.Line, Name = this.StringValue, Type = enu, VisitStatus = FuVisitStatus.NotYet };
				Expect(FuToken.Id);
				if (Eat(FuToken.Assign))
					konst.Value = ParseExpr();
				else if (flags)
					ReportError("enum* symbol must be assigned a value");
				AddSymbol(enu, konst);
			}
			while (Eat(FuToken.Comma));
			Expect(FuToken.RightBrace);
		}

		public void Parse(string filename, byte[] input, int inputLength)
		{
			Open(filename, input, inputLength);
			while (!See(FuToken.EndOfFile)) {
				FuCodeDoc doc = ParseDoc();
				bool isPublic = Eat(FuToken.Public);
				switch (this.CurrentToken) {
				case FuToken.Class:
					ParseClass(doc, isPublic, FuCallType.Normal);
					break;
				case FuToken.Static:
				case FuToken.Abstract:
				case FuToken.Sealed:
					ParseClass(doc, isPublic, ParseCallType());
					break;
				case FuToken.Enum:
					ParseEnum(doc, isPublic);
					break;
				case FuToken.Native:
					this.Program.TopLevelNatives.Add(ParseNative().Content);
					break;
				default:
					ReportError("Expected class or enum");
					NextToken();
					break;
				}
			}
		}
	}

	public abstract class FuConsoleHost : GenHost
	{

		internal bool HasErrors = false;

		public override void ReportError(string filename, int startLine, int startColumn, int endLine, int endColumn, string message)
		{
			this.HasErrors = true;
			Console.Error.WriteLine($"{filename}({startLine}): ERROR: {message}");
		}
	}

	public abstract class FuSemaHost : FuParserHost
	{
	}

	public class FuSema
	{

		protected FuProgram Program;

		FuSemaHost Host;

		FuMethodBase CurrentMethod = null;

		FuScope CurrentScope;

		readonly HashSet<FuMethod> CurrentPureMethods = new HashSet<FuMethod>();

		readonly Dictionary<FuVar, FuExpr> CurrentPureArguments = new Dictionary<FuVar, FuExpr>();

		FuType Poison = new FuType { Name = "poison" };

		public void SetHost(FuSemaHost host)
		{
			this.Host = host;
		}

		FuContainerType GetCurrentContainer() => this.CurrentScope.GetContainer();

		protected void ReportError(FuStatement statement, string message)
		{
			this.Host.ReportError(GetCurrentContainer().Filename, statement.Line, 1, statement.Line, 1, message);
		}

		FuType PoisonError(FuStatement statement, string message)
		{
			ReportError(statement, message);
			return this.Poison;
		}

		void ResolveBase(FuClass klass)
		{
			if (klass.HasBaseClass()) {
				this.CurrentScope = klass;
				if (this.Program.TryLookup(klass.BaseClassName, true) is FuClass baseClass) {
					if (klass.IsPublic && !baseClass.IsPublic)
						ReportError(klass, "Public class cannot derive from an internal class");
					baseClass.HasSubclasses = true;
					klass.Parent = baseClass;
				}
				else
					ReportError(klass, $"Base class {klass.BaseClassName} not found");
			}
			this.Program.Classes.Add(klass);
		}

		void CheckBaseCycle(FuClass klass)
		{
			FuSymbol hare = klass;
			FuSymbol tortoise = klass;
			do {
				hare = hare.Parent;
				if (hare == null)
					return;
				hare = hare.Parent;
				if (hare == null)
					return;
				tortoise = tortoise.Parent;
			}
			while (tortoise != hare);
			this.CurrentScope = klass;
			ReportError(klass, $"Circular inheritance for class {klass.Name}");
		}

		static void TakePtr(FuExpr expr)
		{
			if (expr.Type is FuArrayStorageType arrayStg)
				arrayStg.PtrTaken = true;
		}

		bool Coerce(FuExpr expr, FuType type)
		{
			if (expr == this.Poison)
				return false;
			if (!type.IsAssignableFrom(expr.Type)) {
				ReportError(expr, $"Cannot coerce {expr.Type} to {type}");
				return false;
			}
			if (expr is FuPrefixExpr prefix && prefix.Op == FuToken.New && !(type is FuDynamicPtrType)) {
				FuDynamicPtrType newType = (FuDynamicPtrType) expr.Type;
				string kind = newType.Class.Id == FuId.ArrayPtrClass ? "array" : "object";
				ReportError(expr, $"Dynamically allocated {kind} must be assigned to a {expr.Type} reference");
				return false;
			}
			TakePtr(expr);
			return true;
		}

		bool CoercePermanent(FuExpr expr, FuType type)
		{
			bool ok = Coerce(expr, type);
			if (ok && type.Id == FuId.StringPtrType && expr.IsNewString(true)) {
				ReportError(expr, "New string must be assigned to string()");
				return false;
			}
			return ok;
		}

		FuExpr VisitInterpolatedString(FuInterpolatedString expr)
		{
			int partsCount = 0;
			string s = "";
			for (int partsIndex = 0; partsIndex < expr.Parts.Count; partsIndex++) {
				FuInterpolatedPart part = expr.Parts[partsIndex];
				s += part.Prefix;
				FuExpr arg = VisitExpr(part.Argument);
				if (Coerce(arg, this.Program.System.PrintableType)) {
					switch (arg.Type) {
					case FuIntegerType _:
						switch (part.Format) {
						case ' ':
							if (arg is FuLiteralLong literalLong && part.WidthExpr == null) {
								s += $"{literalLong.Value}";
								continue;
							}
							break;
						case 'D':
						case 'd':
						case 'X':
						case 'x':
							if (part.WidthExpr != null && part.Precision >= 0)
								ReportError(part.WidthExpr, "Cannot format an integer with both width and precision");
							break;
						default:
							ReportError(arg, "Invalid format");
							break;
						}
						break;
					case FuFloatingType _:
						switch (part.Format) {
						case ' ':
						case 'F':
						case 'f':
						case 'E':
						case 'e':
							break;
						default:
							ReportError(arg, "Invalid format");
							break;
						}
						break;
					default:
						if (part.Format != ' ')
							ReportError(arg, "Invalid format");
						else if (arg is FuLiteralString literalString && part.WidthExpr == null) {
							s += literalString.Value;
							continue;
						}
						break;
					}
				}
				FuInterpolatedPart targetPart = expr.Parts[partsCount++];
				targetPart.Prefix = s;
				targetPart.Argument = arg;
				targetPart.WidthExpr = part.WidthExpr;
				targetPart.Width = part.WidthExpr != null ? FoldConstInt(part.WidthExpr) : 0;
				targetPart.Format = part.Format;
				targetPart.Precision = part.Precision;
				s = "";
			}
			s += expr.Suffix;
			if (partsCount == 0)
				return this.Program.System.NewLiteralString(s, expr.Line);
			expr.Type = this.Program.System.StringStorageType;
			expr.Parts.RemoveRange(partsCount, expr.Parts.Count - partsCount);
			expr.Suffix = s;
			return expr;
		}

		FuExpr Lookup(FuSymbolReference expr, FuScope scope)
		{
			if (expr.Symbol == null) {
				expr.Symbol = scope.TryLookup(expr.Name, expr.Left == null);
				if (expr.Symbol == null)
					return PoisonError(expr, $"{expr.Name} not found");
				expr.Type = expr.Symbol.Type;
			}
			if (!(scope is FuEnum) && expr.Symbol is FuConst konst) {
				ResolveConst(konst);
				if (konst.Value is FuLiteral || konst.Value is FuSymbolReference) {
					if (konst.Type is FuFloatingType && konst.Value is FuLiteralLong intValue)
						return ToLiteralDouble(expr, intValue.Value);
					return konst.Value;
				}
			}
			return expr;
		}

		FuExpr VisitSymbolReference(FuSymbolReference expr)
		{
			if (expr.Left == null) {
				FuExpr resolved = Lookup(expr, this.CurrentScope);
				if (expr.Symbol is FuMember nearMember) {
					if (nearMember.Visibility == FuVisibility.Private && nearMember.Parent is FuClass memberClass && memberClass != GetCurrentContainer())
						ReportError(expr, $"Cannot access private member {expr.Name}");
					if (!nearMember.IsStatic() && (this.CurrentMethod == null || this.CurrentMethod.IsStatic()))
						ReportError(expr, $"Cannot use instance member {expr.Name} from static context");
				}
				if (resolved is FuSymbolReference symbol) {
					if (symbol.Symbol is FuVar v) {
						if (v.Parent is FuFor loop)
							loop.IsIteratorUsed = true;
						else if (this.CurrentPureArguments.ContainsKey(v))
							return this.CurrentPureArguments[v];
					}
					else if (symbol.Symbol.Id == FuId.RegexOptionsEnum)
						this.Program.RegexOptionsEnum = true;
				}
				return resolved;
			}
			FuExpr left = VisitExpr(expr.Left);
			if (left == this.Poison)
				return left;
			FuScope scope;
			bool isBase = left is FuSymbolReference baseSymbol && baseSymbol.Symbol.Id == FuId.BasePtr;
			if (isBase) {
				if (this.CurrentMethod == null || !(this.CurrentMethod.Parent.Parent is FuClass baseClass))
					return PoisonError(expr, "No base class");
				scope = baseClass;
			}
			else if (left is FuSymbolReference leftSymbol && leftSymbol.Symbol is FuScope obj)
				scope = obj;
			else {
				scope = left.Type;
				if (scope is FuClassType klass)
					scope = klass.Class;
			}
			FuExpr result = Lookup(expr, scope);
			if (result != expr)
				return result;
			if (expr.Symbol is FuMember member) {
				switch (member.Visibility) {
				case FuVisibility.Private:
					if (member.Parent != this.CurrentMethod.Parent || this.CurrentMethod.Parent != scope)
						ReportError(expr, $"Cannot access private member {expr.Name}");
					break;
				case FuVisibility.Protected:
					if (isBase)
						break;
					FuClass currentClass = (FuClass) this.CurrentMethod.Parent;
					FuClass scopeClass = (FuClass) scope;
					if (!currentClass.IsSameOrBaseOf(scopeClass))
						ReportError(expr, $"Cannot access protected member {expr.Name}");
					break;
				case FuVisibility.NumericElementType:
					if (left.Type is FuClassType klass && !(klass.GetElementType() is FuNumericType))
						ReportError(expr, "Method restricted to collections of numbers");
					break;
				case FuVisibility.FinalValueType:
					if (!left.Type.AsClassType().GetValueType().IsFinal())
						ReportError(expr, "Method restricted to dictionaries with storage values");
					break;
				default:
					switch (expr.Symbol.Id) {
					case FuId.ArrayLength:
						FuArrayStorageType arrayStorage = (FuArrayStorageType) left.Type;
						return ToLiteralLong(expr, arrayStorage.Length);
					case FuId.StringLength:
						if (left is FuLiteralString leftLiteral) {
							int length = leftLiteral.GetAsciiLength();
							if (length >= 0)
								return ToLiteralLong(expr, length);
						}
						break;
					default:
						break;
					}
					break;
				}
				if (!(member is FuMethodGroup)) {
					if (left is FuSymbolReference leftContainer && leftContainer.Symbol is FuContainerType) {
						if (!member.IsStatic())
							ReportError(expr, $"Cannot use instance member {expr.Name} without an object");
					}
					else if (member.IsStatic())
						ReportError(expr, $"{expr.Name} is static");
				}
			}
			return new FuSymbolReference { Line = expr.Line, Left = left, Name = expr.Name, Symbol = expr.Symbol, Type = expr.Type };
		}

		static FuRangeType Union(FuRangeType left, FuRangeType right)
		{
			if (right == null)
				return left;
			if (right.Min < left.Min) {
				if (right.Max >= left.Max)
					return right;
				return FuRangeType.New(right.Min, left.Max);
			}
			if (right.Max > left.Max)
				return FuRangeType.New(left.Min, right.Max);
			return left;
		}

		FuType GetIntegerType(FuExpr left, FuExpr right)
		{
			FuType type = this.Program.System.PromoteIntegerTypes(left.Type, right.Type);
			Coerce(left, type);
			Coerce(right, type);
			return type;
		}

		FuIntegerType GetShiftType(FuExpr left, FuExpr right)
		{
			FuIntegerType intType = this.Program.System.IntType;
			Coerce(right, intType);
			if (left.Type.Id == FuId.LongType) {
				FuIntegerType longType = (FuIntegerType) left.Type;
				return longType;
			}
			Coerce(left, intType);
			return intType;
		}

		FuType GetNumericType(FuExpr left, FuExpr right)
		{
			FuType type = this.Program.System.PromoteNumericTypes(left.Type, right.Type);
			Coerce(left, type);
			Coerce(right, type);
			return type;
		}

		static int SaturatedNeg(int a)
		{
			if (a == -2147483648)
				return 2147483647;
			return -a;
		}

		static int SaturatedAdd(int a, int b)
		{
			int c = a + b;
			if (c >= 0) {
				if (a < 0 && b < 0)
					return -2147483648;
			}
			else if (a > 0 && b > 0)
				return 2147483647;
			return c;
		}

		static int SaturatedSub(int a, int b)
		{
			if (b == -2147483648)
				return a < 0 ? a ^ b : 2147483647;
			return SaturatedAdd(a, -b);
		}

		static int SaturatedMul(int a, int b)
		{
			if (a == 0 || b == 0)
				return 0;
			if (a == -2147483648)
				return b >> 31 ^ a;
			if (b == -2147483648)
				return a >> 31 ^ b;
			if (2147483647 / Math.Abs(a) < Math.Abs(b))
				return (a ^ b) >> 31 ^ 2147483647;
			return a * b;
		}

		static int SaturatedDiv(int a, int b)
		{
			if (a == -2147483648 && b == -1)
				return 2147483647;
			return a / b;
		}

		static int SaturatedShiftRight(int a, int b) => a >> (b >= 31 || b < 0 ? 31 : b);

		static FuRangeType BitwiseUnsignedOp(FuRangeType left, FuToken op, FuRangeType right)
		{
			int leftVariableBits = left.GetVariableBits();
			int rightVariableBits = right.GetVariableBits();
			int min;
			int max;
			switch (op) {
			case FuToken.And:
				min = left.Min & right.Min & ~FuRangeType.GetMask(~left.Min & ~right.Min & (leftVariableBits | rightVariableBits));
				max = (left.Max | leftVariableBits) & (right.Max | rightVariableBits);
				if (max > left.Max)
					max = left.Max;
				if (max > right.Max)
					max = right.Max;
				break;
			case FuToken.Or:
				min = (left.Min & ~leftVariableBits) | (right.Min & ~rightVariableBits);
				max = left.Max | right.Max | FuRangeType.GetMask(left.Max & right.Max & FuRangeType.GetMask(leftVariableBits | rightVariableBits));
				if (min < left.Min)
					min = left.Min;
				if (min < right.Min)
					min = right.Min;
				break;
			case FuToken.Xor:
				int variableBits = leftVariableBits | rightVariableBits;
				min = (left.Min ^ right.Min) & ~variableBits;
				max = (left.Max ^ right.Max) | variableBits;
				break;
			default:
				throw new NotImplementedException();
			}
			if (min > max)
				return FuRangeType.New(max, min);
			return FuRangeType.New(min, max);
		}

		bool IsEnumOp(FuExpr left, FuExpr right)
		{
			if (left.Type is FuEnum) {
				if (left.Type.Id != FuId.BoolType && !(left.Type is FuEnumFlags))
					ReportError(left, $"Define flags enumeration as: enum* {left.Type}");
				Coerce(right, left.Type);
				return true;
			}
			return false;
		}

		FuType BitwiseOp(FuExpr left, FuToken op, FuExpr right)
		{
			if (left.Type is FuRangeType leftRange && right.Type is FuRangeType rightRange) {
				FuRangeType range = null;
				FuRangeType rightNegative;
				FuRangeType rightPositive;
				if (rightRange.Min >= 0) {
					rightNegative = null;
					rightPositive = rightRange;
				}
				else if (rightRange.Max < 0) {
					rightNegative = rightRange;
					rightPositive = null;
				}
				else {
					rightNegative = FuRangeType.New(rightRange.Min, -1);
					rightPositive = FuRangeType.New(0, rightRange.Max);
				}
				if (leftRange.Min < 0) {
					FuRangeType leftNegative = leftRange.Max < 0 ? leftRange : FuRangeType.New(leftRange.Min, -1);
					if (rightNegative != null)
						range = BitwiseUnsignedOp(leftNegative, op, rightNegative);
					if (rightPositive != null)
						range = Union(BitwiseUnsignedOp(leftNegative, op, rightPositive), range);
				}
				if (leftRange.Max >= 0) {
					FuRangeType leftPositive = leftRange.Min >= 0 ? leftRange : FuRangeType.New(0, leftRange.Max);
					if (rightNegative != null)
						range = Union(BitwiseUnsignedOp(leftPositive, op, rightNegative), range);
					if (rightPositive != null)
						range = Union(BitwiseUnsignedOp(leftPositive, op, rightPositive), range);
				}
				return range;
			}
			if (IsEnumOp(left, right))
				return left.Type;
			return GetIntegerType(left, right);
		}

		static FuRangeType NewRangeType(int a, int b, int c, int d)
		{
			if (a > b) {
				int t = a;
				a = b;
				b = t;
			}
			if (c > d) {
				int t = c;
				c = d;
				d = t;
			}
			return FuRangeType.New(a <= c ? a : c, b >= d ? b : d);
		}

		FuLiteral ToLiteralBool(FuExpr expr, bool value)
		{
			FuLiteral result = value ? new FuLiteralTrue() : new FuLiteralFalse();
			result.Line = expr.Line;
			result.Type = this.Program.System.BoolType;
			return result;
		}

		FuLiteralLong ToLiteralLong(FuExpr expr, long value) => this.Program.System.NewLiteralLong(value, expr.Line);

		FuLiteralDouble ToLiteralDouble(FuExpr expr, double value) => new FuLiteralDouble { Line = expr.Line, Type = this.Program.System.DoubleType, Value = value };

		void CheckLValue(FuExpr expr)
		{
			switch (expr) {
			case FuSymbolReference symbol:
				switch (symbol.Symbol) {
				case FuVar def:
					def.IsAssigned = true;
					switch (symbol.Symbol.Parent) {
					case FuFor forLoop:
						forLoop.IsRange = false;
						break;
					case FuForeach _:
						ReportError(expr, "Cannot assign a foreach iteration variable");
						break;
					default:
						break;
					}
					for (FuScope scope = this.CurrentScope; !(scope is FuClass); scope = scope.Parent) {
						if (scope is FuFor forLoop && forLoop.IsRange && forLoop.Cond is FuBinaryExpr binaryCond && binaryCond.Right.IsReferenceTo(symbol.Symbol))
							forLoop.IsRange = false;
					}
					break;
				case FuField _:
					if (symbol.Left == null) {
						if (!this.CurrentMethod.IsMutator)
							ReportError(expr, "Cannot modify field in a non-mutating method");
					}
					else {
						switch (symbol.Left.Type) {
						case FuStorageType _:
							break;
						case FuReadWriteClassType _:
							break;
						case FuClassType _:
							ReportError(expr, "Cannot modify field through a read-only reference");
							break;
						default:
							throw new NotImplementedException();
						}
					}
					break;
				default:
					ReportError(expr, "Cannot modify this");
					break;
				}
				break;
			case FuBinaryExpr indexing when indexing.Op == FuToken.LeftBracket:
				switch (indexing.Left.Type) {
				case FuStorageType _:
					break;
				case FuReadWriteClassType _:
					break;
				case FuClassType _:
					ReportError(expr, "Cannot modify array through a read-only reference");
					break;
				default:
					throw new NotImplementedException();
				}
				break;
			default:
				ReportError(expr, "Cannot modify this");
				break;
			}
		}

		FuInterpolatedString Concatenate(FuInterpolatedString left, FuInterpolatedString right)
		{
			FuInterpolatedString result = new FuInterpolatedString { Line = left.Line, Type = this.Program.System.StringStorageType };
			result.Parts.AddRange(left.Parts);
			if (right.Parts.Count == 0)
				result.Suffix = left.Suffix + right.Suffix;
			else {
				result.Parts.AddRange(right.Parts);
				FuInterpolatedPart middle = result.Parts[left.Parts.Count];
				middle.Prefix = left.Suffix + middle.Prefix;
				result.Suffix = right.Suffix;
			}
			return result;
		}

		FuInterpolatedString ToInterpolatedString(FuExpr expr)
		{
			if (expr is FuInterpolatedString interpolated)
				return interpolated;
			FuInterpolatedString result = new FuInterpolatedString { Line = expr.Line, Type = this.Program.System.StringStorageType };
			if (expr is FuLiteral literal)
				result.Suffix = literal.GetLiteralString();
			else {
				result.AddPart("", expr);
				result.Suffix = "";
			}
			return result;
		}

		void CheckComparison(FuExpr left, FuExpr right)
		{
			if (left.Type is FuEnum)
				Coerce(right, left.Type);
			else {
				FuType doubleType = this.Program.System.DoubleType;
				Coerce(left, doubleType);
				Coerce(right, doubleType);
			}
		}

		void OpenScope(FuScope scope)
		{
			scope.Parent = this.CurrentScope;
			this.CurrentScope = scope;
		}

		void CloseScope()
		{
			this.CurrentScope = this.CurrentScope.Parent;
		}

		FuExpr ResolveNew(FuPrefixExpr expr)
		{
			if (expr.Type != null)
				return expr;
			FuType type;
			if (expr.Inner is FuBinaryExpr binaryNew && binaryNew.Op == FuToken.LeftBrace) {
				type = ToType(binaryNew.Left, true);
				if (!(type is FuClassType klass) || klass is FuReadWriteClassType)
					return PoisonError(expr, "Invalid argument to new");
				FuAggregateInitializer init = (FuAggregateInitializer) binaryNew.Right;
				ResolveObjectLiteral(klass, init);
				expr.Type = new FuDynamicPtrType { Line = expr.Line, Class = klass.Class };
				expr.Inner = init;
				return expr;
			}
			type = ToType(expr.Inner, true);
			switch (type) {
			case FuArrayStorageType array:
				expr.Type = new FuDynamicPtrType { Line = expr.Line, Class = this.Program.System.ArrayPtrClass, TypeArg0 = array.GetElementType() };
				expr.Inner = array.LengthExpr;
				return expr;
			case FuStorageType klass:
				expr.Type = new FuDynamicPtrType { Line = expr.Line, Class = klass.Class, TypeArg0 = klass.TypeArg0, TypeArg1 = klass.TypeArg1 };
				expr.Inner = null;
				return expr;
			default:
				return PoisonError(expr, "Invalid argument to new");
			}
		}

		protected virtual int GetResourceLength(string name, FuPrefixExpr expr) => 0;

		FuExpr VisitPrefixExpr(FuPrefixExpr expr)
		{
			FuExpr inner;
			FuType type;
			switch (expr.Op) {
			case FuToken.Increment:
			case FuToken.Decrement:
				inner = VisitExpr(expr.Inner);
				CheckLValue(inner);
				Coerce(inner, this.Program.System.DoubleType);
				if (inner.Type is FuRangeType xcrementRange) {
					int delta = expr.Op == FuToken.Increment ? 1 : -1;
					type = FuRangeType.New(xcrementRange.Min + delta, xcrementRange.Max + delta);
				}
				else
					type = inner.Type;
				expr.Inner = inner;
				expr.Type = type;
				return expr;
			case FuToken.Minus:
				inner = VisitExpr(expr.Inner);
				Coerce(inner, this.Program.System.DoubleType);
				if (inner.Type is FuRangeType negRange) {
					if (negRange.Min == negRange.Max)
						return ToLiteralLong(expr, -negRange.Min);
					type = FuRangeType.New(SaturatedNeg(negRange.Max), SaturatedNeg(negRange.Min));
				}
				else if (inner is FuLiteralDouble d)
					return ToLiteralDouble(expr, -d.Value);
				else if (inner is FuLiteralLong l)
					return ToLiteralLong(expr, -l.Value);
				else
					type = inner.Type;
				break;
			case FuToken.Tilde:
				inner = VisitExpr(expr.Inner);
				if (inner.Type is FuEnumFlags)
					type = inner.Type;
				else {
					Coerce(inner, this.Program.System.IntType);
					if (inner.Type is FuRangeType notRange)
						type = FuRangeType.New(~notRange.Max, ~notRange.Min);
					else
						type = inner.Type;
				}
				break;
			case FuToken.ExclamationMark:
				inner = ResolveBool(expr.Inner);
				return new FuPrefixExpr { Line = expr.Line, Op = FuToken.ExclamationMark, Inner = inner, Type = this.Program.System.BoolType };
			case FuToken.New:
				return ResolveNew(expr);
			case FuToken.Resource:
				if (!(FoldConst(expr.Inner) is FuLiteralString resourceName))
					return PoisonError(expr, "Resource name must be a string");
				inner = resourceName;
				type = new FuArrayStorageType { Class = this.Program.System.ArrayStorageClass, TypeArg0 = this.Program.System.ByteType, Length = GetResourceLength(resourceName.Value, expr) };
				break;
			default:
				throw new NotImplementedException();
			}
			return new FuPrefixExpr { Line = expr.Line, Op = expr.Op, Inner = inner, Type = type };
		}

		FuExpr VisitPostfixExpr(FuPostfixExpr expr)
		{
			expr.Inner = VisitExpr(expr.Inner);
			switch (expr.Op) {
			case FuToken.Increment:
			case FuToken.Decrement:
				CheckLValue(expr.Inner);
				Coerce(expr.Inner, this.Program.System.DoubleType);
				expr.Type = expr.Inner.Type;
				return expr;
			default:
				return PoisonError(expr, $"Unexpected {FuLexer.TokenToString(expr.Op)}");
			}
		}

		static bool CanCompareEqual(FuType left, FuType right)
		{
			switch (left) {
			case FuNumericType _:
				return right is FuNumericType;
			case FuEnum _:
				return left == right;
			case FuClassType leftClass:
				if (left.Nullable && right.Id == FuId.NullType)
					return true;
				if ((left is FuStorageType && (right is FuStorageType || right is FuDynamicPtrType)) || (left is FuDynamicPtrType && right is FuStorageType))
					return false;
				return right is FuClassType rightClass && (leftClass.Class.IsSameOrBaseOf(rightClass.Class) || rightClass.Class.IsSameOrBaseOf(leftClass.Class)) && leftClass.EqualTypeArguments(rightClass);
			default:
				return left.Id == FuId.NullType && right.Nullable;
			}
		}

		FuExpr ResolveEquality(FuBinaryExpr expr, FuExpr left, FuExpr right)
		{
			if (!CanCompareEqual(left.Type, right.Type))
				return PoisonError(expr, $"Cannot compare {left.Type} with {right.Type}");
			if (left.Type is FuRangeType leftRange && right.Type is FuRangeType rightRange) {
				if (leftRange.Min == leftRange.Max && leftRange.Min == rightRange.Min && leftRange.Min == rightRange.Max)
					return ToLiteralBool(expr, expr.Op == FuToken.Equal);
				if (leftRange.Max < rightRange.Min || leftRange.Min > rightRange.Max)
					return ToLiteralBool(expr, expr.Op == FuToken.NotEqual);
			}
			else {
				switch (left) {
				case FuLiteralLong leftLong when right is FuLiteralLong rightLong:
					return ToLiteralBool(expr, (expr.Op == FuToken.NotEqual) ^ (leftLong.Value == rightLong.Value));
				case FuLiteralDouble leftDouble when right is FuLiteralDouble rightDouble:
					return ToLiteralBool(expr, (expr.Op == FuToken.NotEqual) ^ (leftDouble.Value == rightDouble.Value));
				case FuLiteralString leftString when right is FuLiteralString rightString:
					return ToLiteralBool(expr, (expr.Op == FuToken.NotEqual) ^ (leftString.Value == rightString.Value));
				case FuLiteralNull _ when right is FuLiteralNull:
				case FuLiteralFalse _ when right is FuLiteralFalse:
				case FuLiteralTrue _ when right is FuLiteralTrue:
					return ToLiteralBool(expr, expr.Op == FuToken.Equal);
				case FuLiteralFalse _ when right is FuLiteralTrue:
				case FuLiteralTrue _ when right is FuLiteralFalse:
					return ToLiteralBool(expr, expr.Op == FuToken.NotEqual);
				default:
					break;
				}
				if (left.IsConstEnum() && right.IsConstEnum())
					return ToLiteralBool(expr, (expr.Op == FuToken.NotEqual) ^ (left.IntValue() == right.IntValue()));
			}
			TakePtr(left);
			TakePtr(right);
			return new FuBinaryExpr { Line = expr.Line, Left = left, Op = expr.Op, Right = right, Type = this.Program.System.BoolType };
		}

		FuExpr ResolveIs(FuBinaryExpr expr, FuExpr left, FuExpr right)
		{
			if (!(left.Type is FuClassType leftPtr) || left.Type is FuStorageType)
				return PoisonError(expr, "Left hand side of the 'is' operator must be an object reference");
			FuClass klass;
			switch (right) {
			case FuSymbolReference symbol:
				if (symbol.Symbol is FuClass klass2)
					klass = klass2;
				else
					return PoisonError(expr, "Right hand side of the 'is' operator must be a class name");
				break;
			case FuVar def:
				if (!(def.Type is FuClassType rightPtr))
					return PoisonError(expr, "Right hand side of the 'is' operator must be an object reference definition");
				if (rightPtr is FuReadWriteClassType && !(leftPtr is FuDynamicPtrType) && (rightPtr is FuDynamicPtrType || !(leftPtr is FuReadWriteClassType)))
					return PoisonError(expr, $"{leftPtr} cannot be casted to {rightPtr}");
				klass = rightPtr.Class;
				break;
			default:
				return PoisonError(expr, "Right hand side of the 'is' operator must be a class name");
			}
			if (klass.IsSameOrBaseOf(leftPtr.Class))
				return PoisonError(expr, $"{leftPtr} is {klass.Name}, the 'is' operator would always return 'true'");
			if (!leftPtr.Class.IsSameOrBaseOf(klass))
				return PoisonError(expr, $"{leftPtr} is not base class of {klass.Name}, the 'is' operator would always return 'false'");
			expr.Left = left;
			expr.Type = this.Program.System.BoolType;
			return expr;
		}

		FuExpr VisitBinaryExpr(FuBinaryExpr expr)
		{
			FuExpr left = VisitExpr(expr.Left);
			FuExpr right = VisitExpr(expr.Right);
			if (left == this.Poison || right == this.Poison)
				return this.Poison;
			FuType type;
			switch (expr.Op) {
			case FuToken.LeftBracket:
				if (!(left.Type is FuClassType klass))
					return PoisonError(expr, "Cannot index this object");
				switch (klass.Class.Id) {
				case FuId.StringClass:
					Coerce(right, this.Program.System.IntType);
					if (right.Type is FuRangeType stringIndexRange && stringIndexRange.Max < 0)
						ReportError(expr, "Negative index");
					else if (left is FuLiteralString stringLiteral && right is FuLiteralLong indexLiteral) {
						long i = indexLiteral.Value;
						if (i >= 0 && i <= 2147483647) {
							int c = stringLiteral.GetAsciiAt((int) i);
							if (c >= 0)
								return FuLiteralChar.New(c, expr.Line);
						}
					}
					type = this.Program.System.CharType;
					break;
				case FuId.ArrayPtrClass:
				case FuId.ArrayStorageClass:
				case FuId.ListClass:
					Coerce(right, this.Program.System.IntType);
					if (right.Type is FuRangeType indexRange) {
						if (indexRange.Max < 0)
							ReportError(expr, "Negative index");
						else if (klass is FuArrayStorageType array && indexRange.Min >= array.Length)
							ReportError(expr, "Array index out of bounds");
					}
					type = klass.GetElementType();
					break;
				case FuId.DictionaryClass:
				case FuId.SortedDictionaryClass:
				case FuId.OrderedDictionaryClass:
					Coerce(right, klass.GetKeyType());
					type = klass.GetValueType();
					break;
				default:
					return PoisonError(expr, "Cannot index this object");
				}
				break;
			case FuToken.Plus:
				if (left.Type is FuRangeType leftAdd && right.Type is FuRangeType rightAdd) {
					type = FuRangeType.New(SaturatedAdd(leftAdd.Min, rightAdd.Min), SaturatedAdd(leftAdd.Max, rightAdd.Max));
				}
				else if (left.Type is FuStringType) {
					Coerce(right, this.Program.System.StringPtrType);
					if (left is FuLiteral leftLiteral && right is FuLiteral rightLiteral)
						return this.Program.System.NewLiteralString(leftLiteral.GetLiteralString() + rightLiteral.GetLiteralString(), expr.Line);
					if (left is FuInterpolatedString || right is FuInterpolatedString)
						return Concatenate(ToInterpolatedString(left), ToInterpolatedString(right));
					type = this.Program.System.StringStorageType;
				}
				else
					type = GetNumericType(left, right);
				break;
			case FuToken.Minus:
				if (left.Type is FuRangeType leftSub && right.Type is FuRangeType rightSub) {
					type = FuRangeType.New(SaturatedSub(leftSub.Min, rightSub.Max), SaturatedSub(leftSub.Max, rightSub.Min));
				}
				else
					type = GetNumericType(left, right);
				break;
			case FuToken.Asterisk:
				if (left.Type is FuRangeType leftMul && right.Type is FuRangeType rightMul) {
					type = NewRangeType(SaturatedMul(leftMul.Min, rightMul.Min), SaturatedMul(leftMul.Min, rightMul.Max), SaturatedMul(leftMul.Max, rightMul.Min), SaturatedMul(leftMul.Max, rightMul.Max));
				}
				else
					type = GetNumericType(left, right);
				break;
			case FuToken.Slash:
				if (left.Type is FuRangeType leftDiv && right.Type is FuRangeType rightDiv) {
					int denMin = rightDiv.Min;
					if (denMin == 0)
						denMin = 1;
					int denMax = rightDiv.Max;
					if (denMax == 0)
						denMax = -1;
					type = NewRangeType(SaturatedDiv(leftDiv.Min, denMin), SaturatedDiv(leftDiv.Min, denMax), SaturatedDiv(leftDiv.Max, denMin), SaturatedDiv(leftDiv.Max, denMax));
				}
				else
					type = GetNumericType(left, right);
				break;
			case FuToken.Mod:
				if (left.Type is FuRangeType leftMod && right.Type is FuRangeType rightMod) {
					int den = ~Math.Min(rightMod.Min, -rightMod.Max);
					if (den < 0)
						return PoisonError(expr, "Mod zero");
					type = FuRangeType.New(leftMod.Min >= 0 ? 0 : Math.Max(leftMod.Min, -den), leftMod.Max < 0 ? 0 : Math.Min(leftMod.Max, den));
				}
				else
					type = GetIntegerType(left, right);
				break;
			case FuToken.And:
			case FuToken.Or:
			case FuToken.Xor:
				type = BitwiseOp(left, expr.Op, right);
				break;
			case FuToken.ShiftLeft:
				if (left.Type is FuRangeType leftShl && right.Type is FuRangeType rightShl && leftShl.Min == leftShl.Max && rightShl.Min == rightShl.Max) {
					int result = leftShl.Min << rightShl.Min;
					type = FuRangeType.New(result, result);
				}
				else
					type = GetShiftType(left, right);
				break;
			case FuToken.ShiftRight:
				if (left.Type is FuRangeType leftShr && right.Type is FuRangeType rightShr) {
					if (rightShr.Min < 0)
						rightShr = FuRangeType.New(0, 32);
					type = FuRangeType.New(SaturatedShiftRight(leftShr.Min, leftShr.Min < 0 ? rightShr.Min : rightShr.Max), SaturatedShiftRight(leftShr.Max, leftShr.Max < 0 ? rightShr.Max : rightShr.Min));
				}
				else
					type = GetShiftType(left, right);
				break;
			case FuToken.Equal:
			case FuToken.NotEqual:
				return ResolveEquality(expr, left, right);
			case FuToken.Less:
				if (left.Type is FuRangeType leftLess && right.Type is FuRangeType rightLess) {
					if (leftLess.Max < rightLess.Min)
						return ToLiteralBool(expr, true);
					if (leftLess.Min >= rightLess.Max)
						return ToLiteralBool(expr, false);
				}
				else
					CheckComparison(left, right);
				type = this.Program.System.BoolType;
				break;
			case FuToken.LessOrEqual:
				if (left.Type is FuRangeType leftLeq && right.Type is FuRangeType rightLeq) {
					if (leftLeq.Max <= rightLeq.Min)
						return ToLiteralBool(expr, true);
					if (leftLeq.Min > rightLeq.Max)
						return ToLiteralBool(expr, false);
				}
				else
					CheckComparison(left, right);
				type = this.Program.System.BoolType;
				break;
			case FuToken.Greater:
				if (left.Type is FuRangeType leftGreater && right.Type is FuRangeType rightGreater) {
					if (leftGreater.Min > rightGreater.Max)
						return ToLiteralBool(expr, true);
					if (leftGreater.Max <= rightGreater.Min)
						return ToLiteralBool(expr, false);
				}
				else
					CheckComparison(left, right);
				type = this.Program.System.BoolType;
				break;
			case FuToken.GreaterOrEqual:
				if (left.Type is FuRangeType leftGeq && right.Type is FuRangeType rightGeq) {
					if (leftGeq.Min >= rightGeq.Max)
						return ToLiteralBool(expr, true);
					if (leftGeq.Max < rightGeq.Min)
						return ToLiteralBool(expr, false);
				}
				else
					CheckComparison(left, right);
				type = this.Program.System.BoolType;
				break;
			case FuToken.CondAnd:
				Coerce(left, this.Program.System.BoolType);
				Coerce(right, this.Program.System.BoolType);
				if (left is FuLiteralTrue)
					return right;
				if (left is FuLiteralFalse || right is FuLiteralTrue)
					return left;
				type = this.Program.System.BoolType;
				break;
			case FuToken.CondOr:
				Coerce(left, this.Program.System.BoolType);
				Coerce(right, this.Program.System.BoolType);
				if (left is FuLiteralTrue || right is FuLiteralFalse)
					return left;
				if (left is FuLiteralFalse)
					return right;
				type = this.Program.System.BoolType;
				break;
			case FuToken.Assign:
				CheckLValue(left);
				CoercePermanent(right, left.Type);
				expr.Left = left;
				expr.Right = right;
				expr.Type = left.Type;
				return expr;
			case FuToken.AddAssign:
				CheckLValue(left);
				if (left.Type.Id == FuId.StringStorageType)
					Coerce(right, this.Program.System.StringPtrType);
				else {
					Coerce(left, this.Program.System.DoubleType);
					Coerce(right, left.Type);
				}
				expr.Left = left;
				expr.Right = right;
				expr.Type = left.Type;
				return expr;
			case FuToken.SubAssign:
			case FuToken.MulAssign:
			case FuToken.DivAssign:
				CheckLValue(left);
				Coerce(left, this.Program.System.DoubleType);
				Coerce(right, left.Type);
				expr.Left = left;
				expr.Right = right;
				expr.Type = left.Type;
				return expr;
			case FuToken.ModAssign:
			case FuToken.ShiftLeftAssign:
			case FuToken.ShiftRightAssign:
				CheckLValue(left);
				Coerce(left, this.Program.System.IntType);
				Coerce(right, this.Program.System.IntType);
				expr.Left = left;
				expr.Right = right;
				expr.Type = left.Type;
				return expr;
			case FuToken.AndAssign:
			case FuToken.OrAssign:
			case FuToken.XorAssign:
				CheckLValue(left);
				if (!IsEnumOp(left, right)) {
					Coerce(left, this.Program.System.IntType);
					Coerce(right, this.Program.System.IntType);
				}
				expr.Left = left;
				expr.Right = right;
				expr.Type = left.Type;
				return expr;
			case FuToken.Is:
				return ResolveIs(expr, left, right);
			case FuToken.Range:
				return PoisonError(expr, "Range within an expression");
			default:
				throw new NotImplementedException();
			}
			if (type is FuRangeType range && range.Min == range.Max)
				return ToLiteralLong(expr, range.Min);
			return new FuBinaryExpr { Line = expr.Line, Left = left, Op = expr.Op, Right = right, Type = type };
		}

		FuType TryGetPtr(FuType type, bool nullable)
		{
			if (type.Id == FuId.StringStorageType)
				return nullable ? this.Program.System.StringNullablePtrType : this.Program.System.StringPtrType;
			if (type is FuStorageType storage)
				return new FuReadWriteClassType { Class = storage.Class.Id == FuId.ArrayStorageClass ? this.Program.System.ArrayPtrClass : storage.Class, Nullable = nullable, TypeArg0 = storage.TypeArg0, TypeArg1 = storage.TypeArg1 };
			if (nullable && type is FuClassType ptr && !ptr.Nullable) {
				FuClassType result;
				if (type is FuDynamicPtrType)
					result = new FuDynamicPtrType();
				else if (type is FuReadWriteClassType)
					result = new FuReadWriteClassType();
				else
					result = new FuClassType();
				result.Class = ptr.Class;
				result.Nullable = true;
				result.TypeArg0 = ptr.TypeArg0;
				result.TypeArg1 = ptr.TypeArg1;
				return result;
			}
			return type;
		}

		static FuClass GetLowestCommonAncestor(FuClass left, FuClass right)
		{
			for (;;) {
				if (left.IsSameOrBaseOf(right))
					return left;
				if (left.Parent is FuClass parent)
					left = parent;
				else
					return null;
			}
		}

		FuType GetCommonType(FuExpr left, FuExpr right)
		{
			if (left.Type is FuRangeType leftRange && right.Type is FuRangeType rightRange)
				return Union(leftRange, rightRange);
			bool nullable = left.Type.Nullable || right.Type.Nullable;
			FuType ptr = TryGetPtr(left.Type, nullable);
			if (ptr.IsAssignableFrom(right.Type))
				return ptr;
			ptr = TryGetPtr(right.Type, nullable);
			if (ptr.IsAssignableFrom(left.Type))
				return ptr;
			if (left.Type is FuClassType leftClass && right.Type is FuClassType rightClass && leftClass.EqualTypeArguments(rightClass)) {
				FuClass klass = GetLowestCommonAncestor(leftClass.Class, rightClass.Class);
				if (klass != null) {
					FuClassType result;
					if (!(leftClass is FuReadWriteClassType) || !(rightClass is FuReadWriteClassType))
						result = new FuClassType();
					else if (leftClass is FuDynamicPtrType && rightClass is FuDynamicPtrType)
						result = new FuDynamicPtrType();
					else
						result = new FuReadWriteClassType();
					result.Class = klass;
					result.Nullable = nullable;
					result.TypeArg0 = leftClass.TypeArg0;
					result.TypeArg1 = leftClass.TypeArg1;
					return result;
				}
			}
			return PoisonError(left, $"Incompatible types: {left.Type} and {right.Type}");
		}

		FuExpr VisitSelectExpr(FuSelectExpr expr)
		{
			FuExpr cond = ResolveBool(expr.Cond);
			FuExpr onTrue = VisitExpr(expr.OnTrue);
			FuExpr onFalse = VisitExpr(expr.OnFalse);
			if (onTrue == this.Poison || onFalse == this.Poison)
				return this.Poison;
			FuType type = GetCommonType(onTrue, onFalse);
			Coerce(onTrue, type);
			Coerce(onFalse, type);
			if (cond is FuLiteralTrue)
				return onTrue;
			if (cond is FuLiteralFalse)
				return onFalse;
			return new FuSelectExpr { Line = expr.Line, Cond = cond, OnTrue = onTrue, OnFalse = onFalse, Type = type };
		}

		FuType EvalType(FuClassType generic, FuType type)
		{
			if (type.Id == FuId.TypeParam0)
				return generic.TypeArg0;
			if (type.Id == FuId.TypeParam0NotFinal)
				return generic.TypeArg0.IsFinal() ? null : generic.TypeArg0;
			if (type is FuClassType collection && collection.Class.TypeParameterCount == 1 && collection.TypeArg0.Id == FuId.TypeParam0) {
				FuClassType result = type is FuReadWriteClassType ? new FuReadWriteClassType() : new FuClassType();
				result.Class = collection.Class;
				result.TypeArg0 = generic.TypeArg0;
				return result;
			}
			return type;
		}

		bool CanCall(FuExpr obj, FuMethod method, List<FuExpr> arguments)
		{
			FuVar param = method.Parameters.FirstParameter();
			foreach (FuExpr arg in arguments) {
				if (param == null)
					return false;
				FuType type = param.Type;
				if (obj != null && obj.Type is FuClassType generic)
					type = EvalType(generic, type);
				if (!type.IsAssignableFrom(arg.Type))
					return false;
				param = param.NextParameter();
			}
			return param == null || param.Value != null;
		}

		FuExpr ResolveCallWithArguments(FuCallExpr expr, List<FuExpr> arguments)
		{
			if (!(VisitExpr(expr.Method) is FuSymbolReference symbol))
				return this.Poison;
			FuMethod method;
			switch (symbol.Symbol) {
			case null:
				return this.Poison;
			case FuMethod m:
				method = m;
				break;
			case FuMethodGroup group:
				method = group.Methods[0];
				if (!CanCall(symbol.Left, method, arguments))
					method = group.Methods[1];
				break;
			default:
				return PoisonError(symbol, "Expected a method");
			}
			int i = 0;
			for (FuVar param = method.Parameters.FirstParameter(); param != null; param = param.NextParameter()) {
				FuType type = param.Type;
				if (symbol.Left != null && symbol.Left.Type is FuClassType generic) {
					type = EvalType(generic, type);
					if (type == null)
						continue;
				}
				if (i >= arguments.Count) {
					if (param.Value != null)
						break;
					return PoisonError(expr, $"Too few arguments for '{method.Name}'");
				}
				FuExpr arg = arguments[i++];
				if (type.Id == FuId.TypeParam0Predicate && arg is FuLambdaExpr lambda) {
					lambda.First.Type = symbol.Left.Type.AsClassType().TypeArg0;
					OpenScope(lambda);
					lambda.Body = VisitExpr(lambda.Body);
					CloseScope();
					Coerce(lambda.Body, this.Program.System.BoolType);
				}
				else
					Coerce(arg, type);
			}
			if (i < arguments.Count)
				return PoisonError(arguments[i], $"Too many arguments for '{method.Name}'");
			if (method.Throws) {
				if (this.CurrentMethod == null)
					return PoisonError(expr, $"Cannot call method '{method.Name}' here because it is marked 'throws'");
				if (!this.CurrentMethod.Throws)
					return PoisonError(expr, "Method marked 'throws' called from a method not marked 'throws'");
			}
			symbol.Symbol = method;
			if (method.CallType == FuCallType.Static && method.Body is FuReturn ret && arguments.TrueForAll(arg => arg is FuLiteral) && !this.CurrentPureMethods.Contains(method)) {
				this.CurrentPureMethods.Add(method);
				i = 0;
				for (FuVar param = method.Parameters.FirstParameter(); param != null; param = param.NextParameter()) {
					if (i < arguments.Count)
						this.CurrentPureArguments[param] = arguments[i++];
					else
						this.CurrentPureArguments[param] = param.Value;
				}
				FuExpr result = VisitExpr(ret.Value);
				for (FuVar param = method.Parameters.FirstParameter(); param != null; param = param.NextParameter())
					this.CurrentPureArguments.Remove(param);
				this.CurrentPureMethods.Remove(method);
				if (result is FuLiteral)
					return result;
			}
			if (this.CurrentMethod != null)
				this.CurrentMethod.Calls.Add(method);
			if (this.CurrentPureArguments.Count == 0) {
				expr.Method = symbol;
				FuType type = method.Type;
				if (symbol.Left != null && symbol.Left.Type is FuClassType generic)
					type = EvalType(generic, type);
				expr.Type = type;
			}
			return expr;
		}

		FuExpr VisitCallExpr(FuCallExpr expr)
		{
			if (this.CurrentPureArguments.Count == 0) {
				List<FuExpr> arguments = expr.Arguments;
				for (int i = 0; i < arguments.Count; i++) {
					if (!(arguments[i] is FuLambdaExpr))
						arguments[i] = VisitExpr(arguments[i]);
				}
				return ResolveCallWithArguments(expr, arguments);
			}
			else {
				List<FuExpr> arguments = new List<FuExpr>();
				foreach (FuExpr arg in expr.Arguments)
					arguments.Add(VisitExpr(arg));
				return ResolveCallWithArguments(expr, arguments);
			}
		}

		void ResolveObjectLiteral(FuClassType klass, FuAggregateInitializer init)
		{
			foreach (FuExpr item in init.Items) {
				FuBinaryExpr field = (FuBinaryExpr) item;
				Debug.Assert(field.Op == FuToken.Assign);
				FuSymbolReference symbol = (FuSymbolReference) field.Left;
				Lookup(symbol, klass.Class);
				if (symbol.Symbol is FuField) {
					field.Right = VisitExpr(field.Right);
					Coerce(field.Right, symbol.Type);
				}
				else
					ReportError(field, "Expected a field");
			}
		}

		void VisitVar(FuVar expr)
		{
			FuType type = ResolveType(expr);
			if (expr.Value != null) {
				if (type is FuStorageType storage && expr.Value is FuAggregateInitializer init)
					ResolveObjectLiteral(storage, init);
				else {
					expr.Value = VisitExpr(expr.Value);
					if (!expr.IsAssignableStorage()) {
						if (type is FuArrayStorageType array) {
							type = array.GetElementType();
							if (!(expr.Value is FuLiteral literal) || !literal.IsDefaultValue())
								ReportError(expr.Value, "Only null, zero and false supported as an array initializer");
						}
						CoercePermanent(expr.Value, type);
					}
				}
			}
			this.CurrentScope.Add(expr);
		}

		FuExpr VisitExpr(FuExpr expr)
		{
			switch (expr) {
			case FuAggregateInitializer aggregate:
				List<FuExpr> items = aggregate.Items;
				for (int i = 0; i < items.Count; i++)
					items[i] = VisitExpr(items[i]);
				return expr;
			case FuLiteral _:
				return expr;
			case FuInterpolatedString interpolated:
				return VisitInterpolatedString(interpolated);
			case FuSymbolReference symbol:
				return VisitSymbolReference(symbol);
			case FuPrefixExpr prefix:
				return VisitPrefixExpr(prefix);
			case FuPostfixExpr postfix:
				return VisitPostfixExpr(postfix);
			case FuBinaryExpr binary:
				return VisitBinaryExpr(binary);
			case FuSelectExpr select:
				return VisitSelectExpr(select);
			case FuCallExpr call:
				return VisitCallExpr(call);
			case FuLambdaExpr _:
				ReportError(expr, "Unexpected lambda expression");
				return expr;
			case FuVar def:
				VisitVar(def);
				return expr;
			default:
				if (expr == this.Poison)
					return expr;
				throw new NotImplementedException();
			}
		}

		FuExpr ResolveBool(FuExpr expr)
		{
			expr = VisitExpr(expr);
			Coerce(expr, this.Program.System.BoolType);
			return expr;
		}

		static FuClassType CreateClassPtr(FuClass klass, FuToken ptrModifier, bool nullable)
		{
			FuClassType ptr;
			switch (ptrModifier) {
			case FuToken.EndOfFile:
				ptr = new FuClassType();
				break;
			case FuToken.ExclamationMark:
				ptr = new FuReadWriteClassType();
				break;
			case FuToken.Hash:
				ptr = new FuDynamicPtrType();
				break;
			default:
				throw new NotImplementedException();
			}
			ptr.Class = klass;
			ptr.Nullable = nullable;
			return ptr;
		}

		void FillGenericClass(FuClassType result, FuClass klass, FuAggregateInitializer typeArgExprs)
		{
			List<FuType> typeArgs = new List<FuType>();
			foreach (FuExpr typeArgExpr in typeArgExprs.Items)
				typeArgs.Add(ToType(typeArgExpr, false));
			if (typeArgs.Count != klass.TypeParameterCount) {
				ReportError(result, $"Expected {klass.TypeParameterCount} type arguments for {klass.Name}, got {typeArgs.Count}");
				return;
			}
			result.Class = klass;
			result.TypeArg0 = typeArgs[0];
			if (typeArgs.Count == 2)
				result.TypeArg1 = typeArgs[1];
		}

		void ExpectNoPtrModifier(FuExpr expr, FuToken ptrModifier, bool nullable)
		{
			if (ptrModifier != FuToken.EndOfFile)
				ReportError(expr, $"Unexpected {FuLexer.TokenToString(ptrModifier)} on a non-reference type");
			if (nullable)
				ReportError(expr, "Nullable value types not supported");
		}

		FuType ToBaseType(FuExpr expr, FuToken ptrModifier, bool nullable)
		{
			switch (expr) {
			case FuSymbolReference symbol:
				if (this.Program.TryLookup(symbol.Name, true) is FuType type) {
					if (type is FuClass klass) {
						if (klass.Id == FuId.MatchClass && ptrModifier != FuToken.EndOfFile)
							ReportError(expr, "Read-write references to the built-in class Match are not supported");
						FuClassType ptr = CreateClassPtr(klass, ptrModifier, nullable);
						if (symbol.Left is FuAggregateInitializer typeArgExprs)
							FillGenericClass(ptr, klass, typeArgExprs);
						else if (symbol.Left != null)
							return PoisonError(expr, "Invalid type");
						else
							ptr.Name = klass.Name;
						return ptr;
					}
					else if (symbol.Left != null)
						return PoisonError(expr, "Invalid type");
					if (type.Id == FuId.StringPtrType && nullable) {
						type = this.Program.System.StringNullablePtrType;
						nullable = false;
					}
					ExpectNoPtrModifier(expr, ptrModifier, nullable);
					return type;
				}
				return PoisonError(expr, $"Type {symbol.Name} not found");
			case FuCallExpr call:
				ExpectNoPtrModifier(expr, ptrModifier, nullable);
				if (call.Arguments.Count != 0)
					ReportError(call, "Expected empty parentheses for storage type");
				if (call.Method.Left is FuAggregateInitializer typeArgExprs2) {
					FuStorageType storage = new FuStorageType { Line = call.Line };
					if (this.Program.TryLookup(call.Method.Name, true) is FuClass klass) {
						FillGenericClass(storage, klass, typeArgExprs2);
						return storage;
					}
					return PoisonError(typeArgExprs2, $"{call.Method.Name} is not a class");
				}
				else if (call.Method.Left != null)
					return PoisonError(expr, "Invalid type");
				if (call.Method.Name == "string")
					return this.Program.System.StringStorageType;
				if (this.Program.TryLookup(call.Method.Name, true) is FuClass klass2)
					return new FuStorageType { Class = klass2 };
				return PoisonError(expr, $"Class {call.Method.Name} not found");
			default:
				return PoisonError(expr, "Invalid type");
			}
		}

		FuType ToType(FuExpr expr, bool dynamic)
		{
			FuExpr minExpr = null;
			if (expr is FuBinaryExpr range && range.Op == FuToken.Range) {
				minExpr = range.Left;
				expr = range.Right;
			}
			bool nullable;
			FuToken ptrModifier;
			FuClassType outerArray = null;
			FuClassType innerArray = null;
			for (;;) {
				if (expr is FuPostfixExpr question && question.Op == FuToken.QuestionMark) {
					expr = question.Inner;
					nullable = true;
				}
				else
					nullable = false;
				if (expr is FuPostfixExpr postfix && (postfix.Op == FuToken.ExclamationMark || postfix.Op == FuToken.Hash)) {
					expr = postfix.Inner;
					ptrModifier = postfix.Op;
				}
				else
					ptrModifier = FuToken.EndOfFile;
				if (expr is FuBinaryExpr binary && binary.Op == FuToken.LeftBracket) {
					if (binary.Right != null) {
						ExpectNoPtrModifier(expr, ptrModifier, nullable);
						FuExpr lengthExpr = VisitExpr(binary.Right);
						FuArrayStorageType arrayStorage = new FuArrayStorageType { Class = this.Program.System.ArrayStorageClass, TypeArg0 = outerArray, LengthExpr = lengthExpr, Length = 0 };
						if (Coerce(lengthExpr, this.Program.System.IntType) && (!dynamic || binary.Left.IsIndexing())) {
							if (lengthExpr is FuLiteralLong literal) {
								long length = literal.Value;
								if (length < 0)
									ReportError(expr, "Expected non-negative integer");
								else if (length > 2147483647)
									ReportError(expr, "Integer too big");
								else
									arrayStorage.Length = (int) length;
							}
							else
								ReportError(lengthExpr, "Expected constant value");
						}
						outerArray = arrayStorage;
					}
					else {
						FuType elementType = outerArray;
						outerArray = CreateClassPtr(this.Program.System.ArrayPtrClass, ptrModifier, nullable);
						outerArray.TypeArg0 = elementType;
					}
					if (innerArray == null)
						innerArray = outerArray;
					expr = binary.Left;
				}
				else
					break;
			}
			FuType baseType;
			if (minExpr != null) {
				ExpectNoPtrModifier(expr, ptrModifier, nullable);
				int min = FoldConstInt(minExpr);
				int max = FoldConstInt(expr);
				if (min > max)
					return PoisonError(expr, "Range min greater than max");
				baseType = FuRangeType.New(min, max);
			}
			else
				baseType = ToBaseType(expr, ptrModifier, nullable);
			baseType.Line = expr.Line;
			if (outerArray == null)
				return baseType;
			innerArray.TypeArg0 = baseType;
			return outerArray;
		}

		FuType ResolveType(FuNamedValue def)
		{
			def.Type = ToType(def.TypeExpr, false);
			return def.Type;
		}

		void VisitAssert(FuAssert statement)
		{
			statement.Cond = ResolveBool(statement.Cond);
			if (statement.Message != null) {
				statement.Message = VisitExpr(statement.Message);
				if (!(statement.Message.Type is FuStringType))
					ReportError(statement, "The second argument of 'assert' must be a string");
			}
		}

		bool ResolveStatements(List<FuStatement> statements)
		{
			bool reachable = true;
			foreach (FuStatement statement in statements) {
				if (statement is FuConst konst) {
					ResolveConst(konst);
					this.CurrentScope.Add(konst);
					if (konst.Type is FuArrayStorageType) {
						FuClass klass = (FuClass) this.CurrentScope.GetContainer();
						klass.ConstArrays.Add(konst);
					}
				}
				else
					VisitStatement(statement);
				if (!reachable) {
					ReportError(statement, "Unreachable statement");
					return false;
				}
				reachable = statement.CompletesNormally();
			}
			return reachable;
		}

		void VisitBlock(FuBlock statement)
		{
			OpenScope(statement);
			statement.SetCompletesNormally(ResolveStatements(statement.Statements));
			CloseScope();
		}

		void ResolveLoopCond(FuLoop statement)
		{
			if (statement.Cond != null) {
				statement.Cond = ResolveBool(statement.Cond);
				statement.SetCompletesNormally(!(statement.Cond is FuLiteralTrue));
			}
			else
				statement.SetCompletesNormally(false);
		}

		void VisitDoWhile(FuDoWhile statement)
		{
			OpenScope(statement);
			ResolveLoopCond(statement);
			VisitStatement(statement.Body);
			CloseScope();
		}

		void VisitFor(FuFor statement)
		{
			OpenScope(statement);
			if (statement.Init != null)
				VisitStatement(statement.Init);
			ResolveLoopCond(statement);
			if (statement.Advance != null)
				VisitStatement(statement.Advance);
			if (statement.Init is FuVar iter && iter.Type is FuIntegerType && iter.Value != null && statement.Cond is FuBinaryExpr cond && cond.Left.IsReferenceTo(iter) && (cond.Right is FuLiteral || (cond.Right is FuSymbolReference limitSymbol && limitSymbol.Symbol is FuVar)) && statement.Advance != null) {
				long step = 0;
				switch (statement.Advance) {
				case FuUnaryExpr unary when unary.Inner != null && unary.Inner.IsReferenceTo(iter):
					switch (unary.Op) {
					case FuToken.Increment:
						step = 1;
						break;
					case FuToken.Decrement:
						step = -1;
						break;
					default:
						break;
					}
					break;
				case FuBinaryExpr binary when binary.Left.IsReferenceTo(iter) && binary.Right is FuLiteralLong literalStep:
					switch (binary.Op) {
					case FuToken.AddAssign:
						step = literalStep.Value;
						break;
					case FuToken.SubAssign:
						step = -literalStep.Value;
						break;
					default:
						break;
					}
					break;
				default:
					break;
				}
				if ((step > 0 && (cond.Op == FuToken.Less || cond.Op == FuToken.LessOrEqual)) || (step < 0 && (cond.Op == FuToken.Greater || cond.Op == FuToken.GreaterOrEqual))) {
					statement.IsRange = true;
					statement.RangeStep = step;
					statement.IsIteratorUsed = false;
				}
			}
			VisitStatement(statement.Body);
			CloseScope();
		}

		void VisitForeach(FuForeach statement)
		{
			OpenScope(statement);
			FuVar element = statement.GetVar();
			ResolveType(element);
			VisitExpr(statement.Collection);
			if (statement.Collection.Type is FuClassType klass) {
				switch (klass.Class.Id) {
				case FuId.StringClass:
					if (statement.Count() != 1 || !element.Type.IsAssignableFrom(this.Program.System.IntType))
						ReportError(statement, "Expected int iterator variable");
					break;
				case FuId.ArrayStorageClass:
				case FuId.ListClass:
				case FuId.HashSetClass:
				case FuId.SortedSetClass:
					if (statement.Count() != 1)
						ReportError(statement, "Expected one iterator variable");
					else if (!element.Type.IsAssignableFrom(klass.GetElementType()))
						ReportError(statement, $"Cannot coerce {klass.GetElementType()} to {element.Type}");
					break;
				case FuId.DictionaryClass:
				case FuId.SortedDictionaryClass:
				case FuId.OrderedDictionaryClass:
					if (statement.Count() != 2)
						ReportError(statement, "Expected (TKey key, TValue value) iterator");
					else {
						FuVar value = statement.GetValueVar();
						ResolveType(value);
						if (!element.Type.IsAssignableFrom(klass.GetKeyType()))
							ReportError(statement, $"Cannot coerce {klass.GetKeyType()} to {element.Type}");
						else if (!value.Type.IsAssignableFrom(klass.GetValueType()))
							ReportError(statement, $"Cannot coerce {klass.GetValueType()} to {value.Type}");
					}
					break;
				default:
					ReportError(statement, $"'foreach' invalid on {klass.Class.Name}");
					break;
				}
			}
			else
				ReportError(statement, $"'foreach' invalid on {statement.Collection.Type}");
			statement.SetCompletesNormally(true);
			VisitStatement(statement.Body);
			CloseScope();
		}

		void VisitIf(FuIf statement)
		{
			statement.Cond = ResolveBool(statement.Cond);
			VisitStatement(statement.OnTrue);
			if (statement.OnFalse != null) {
				VisitStatement(statement.OnFalse);
				statement.SetCompletesNormally(statement.OnTrue.CompletesNormally() || statement.OnFalse.CompletesNormally());
			}
			else
				statement.SetCompletesNormally(true);
		}

		void VisitLock(FuLock statement)
		{
			statement.Lock = VisitExpr(statement.Lock);
			Coerce(statement.Lock, this.Program.System.LockPtrType);
			VisitStatement(statement.Body);
		}

		void VisitReturn(FuReturn statement)
		{
			if (this.CurrentMethod.Type.Id == FuId.VoidType) {
				if (statement.Value != null)
					ReportError(statement, "Void method cannot return a value");
			}
			else if (statement.Value == null)
				ReportError(statement, "Missing return value");
			else {
				statement.Value = VisitExpr(statement.Value);
				CoercePermanent(statement.Value, this.CurrentMethod.Type);
				if (statement.Value is FuSymbolReference symbol && symbol.Symbol is FuVar local && ((local.Type.IsFinal() && !(this.CurrentMethod.Type is FuStorageType)) || (local.Type.Id == FuId.StringStorageType && this.CurrentMethod.Type.Id != FuId.StringStorageType)))
					ReportError(statement, "Returning dangling reference to local storage");
			}
		}

		void VisitSwitch(FuSwitch statement)
		{
			OpenScope(statement);
			statement.Value = VisitExpr(statement.Value);
			switch (statement.Value.Type) {
			case FuIntegerType i when i.Id != FuId.LongType:
			case FuEnum _:
				break;
			case FuClassType klass when !(klass is FuStorageType):
				break;
			default:
				ReportError(statement.Value, $"Switch on type {statement.Value.Type} - expected int, enum, string or object reference");
				return;
			}
			statement.SetCompletesNormally(false);
			foreach (FuCase kase in statement.Cases) {
				for (int i = 0; i < kase.Values.Count; i++) {
					if (statement.Value.Type is FuClassType switchPtr && switchPtr.Class.Id != FuId.StringClass) {
						FuExpr value = kase.Values[i];
						if (value is FuBinaryExpr when1 && when1.Op == FuToken.When)
							value = when1.Left;
						if (value is FuLiteralNull) {
						}
						else if (!(value is FuVar def) || def.Value != null)
							ReportError(kase.Values[i], "Expected 'case Type name'");
						else if (!(ResolveType(def) is FuClassType casePtr) || casePtr is FuStorageType)
							ReportError(def, "'case' with non-reference type");
						else if (casePtr is FuReadWriteClassType && !(switchPtr is FuDynamicPtrType) && (casePtr is FuDynamicPtrType || !(switchPtr is FuReadWriteClassType)))
							ReportError(def, $"{switchPtr} cannot be casted to {casePtr}");
						else if (casePtr.Class.IsSameOrBaseOf(switchPtr.Class))
							ReportError(def, $"{statement.Value} is {switchPtr}, 'case {casePtr}' would always match");
						else if (!switchPtr.Class.IsSameOrBaseOf(casePtr.Class))
							ReportError(def, $"{switchPtr} is not base class of {casePtr.Class.Name}, 'case {casePtr}' would never match");
						else {
							statement.Add(def);
							if (kase.Values[i] is FuBinaryExpr when2 && when2.Op == FuToken.When)
								when2.Right = ResolveBool(when2.Right);
						}
					}
					else if (kase.Values[i] is FuBinaryExpr when1 && when1.Op == FuToken.When) {
						when1.Left = FoldConst(when1.Left);
						Coerce(when1.Left, statement.Value.Type);
						when1.Right = ResolveBool(when1.Right);
					}
					else {
						kase.Values[i] = FoldConst(kase.Values[i]);
						Coerce(kase.Values[i], statement.Value.Type);
					}
				}
				if (ResolveStatements(kase.Body))
					ReportError(kase.Body[^1], "Case must end with break, continue, return or throw");
			}
			if (statement.DefaultBody.Count > 0) {
				bool reachable = ResolveStatements(statement.DefaultBody);
				if (reachable)
					ReportError(statement.DefaultBody[^1], "Default must end with break, continue, return or throw");
			}
			CloseScope();
		}

		void VisitThrow(FuThrow statement)
		{
			if (!this.CurrentMethod.Throws)
				ReportError(statement, "'throw' in a method not marked 'throws'");
			statement.Message = VisitExpr(statement.Message);
			if (!(statement.Message.Type is FuStringType))
				ReportError(statement, "The argument of 'throw' must be a string");
		}

		void VisitWhile(FuWhile statement)
		{
			OpenScope(statement);
			ResolveLoopCond(statement);
			VisitStatement(statement.Body);
			CloseScope();
		}

		void VisitStatement(FuStatement statement)
		{
			switch (statement) {
			case FuAssert asrt:
				VisitAssert(asrt);
				break;
			case FuBlock block:
				VisitBlock(block);
				break;
			case FuBreak brk:
				brk.LoopOrSwitch.SetCompletesNormally(true);
				break;
			case FuContinue _:
			case FuNative _:
				break;
			case FuDoWhile doWhile:
				VisitDoWhile(doWhile);
				break;
			case FuFor forLoop:
				VisitFor(forLoop);
				break;
			case FuForeach foreachLoop:
				VisitForeach(foreachLoop);
				break;
			case FuIf ifStatement:
				VisitIf(ifStatement);
				break;
			case FuLock lockStatement:
				VisitLock(lockStatement);
				break;
			case FuReturn ret:
				VisitReturn(ret);
				break;
			case FuSwitch switchStatement:
				VisitSwitch(switchStatement);
				break;
			case FuThrow throwStatement:
				VisitThrow(throwStatement);
				break;
			case FuWhile whileStatement:
				VisitWhile(whileStatement);
				break;
			case FuExpr expr:
				VisitExpr(expr);
				break;
			default:
				throw new NotImplementedException();
			}
		}

		FuExpr FoldConst(FuExpr expr)
		{
			expr = VisitExpr(expr);
			if (expr is FuLiteral || expr.IsConstEnum())
				return expr;
			ReportError(expr, "Expected constant value");
			return expr;
		}

		int FoldConstInt(FuExpr expr)
		{
			if (FoldConst(expr) is FuLiteralLong literal) {
				long l = literal.Value;
				if (l < -2147483648 || l > 2147483647) {
					ReportError(expr, "Only 32-bit ranges supported");
					return 0;
				}
				return (int) l;
			}
			ReportError(expr, "Expected integer");
			return 0;
		}

		void ResolveConst(FuConst konst)
		{
			switch (konst.VisitStatus) {
			case FuVisitStatus.NotYet:
				break;
			case FuVisitStatus.InProgress:
				konst.Value = PoisonError(konst, $"Circular dependency in value of constant {konst.Name}");
				konst.VisitStatus = FuVisitStatus.Done;
				return;
			case FuVisitStatus.Done:
				return;
			}
			konst.VisitStatus = FuVisitStatus.InProgress;
			if (!(this.CurrentScope is FuEnum))
				ResolveType(konst);
			konst.Value = VisitExpr(konst.Value);
			if (konst.Value is FuAggregateInitializer coll) {
				if (konst.Type is FuClassType array) {
					FuType elementType = array.GetElementType();
					if (array is FuArrayStorageType arrayStg) {
						if (arrayStg.Length != coll.Items.Count)
							ReportError(konst, $"Declared {arrayStg.Length} elements, initialized {coll.Items.Count}");
					}
					else if (array is FuReadWriteClassType)
						ReportError(konst, "Invalid constant type");
					else
						konst.Type = new FuArrayStorageType { Class = this.Program.System.ArrayStorageClass, TypeArg0 = elementType, Length = coll.Items.Count };
					coll.Type = konst.Type;
					foreach (FuExpr item in coll.Items)
						Coerce(item, elementType);
				}
				else
					ReportError(konst, $"Array initializer for scalar constant {konst.Name}");
			}
			else if (this.CurrentScope is FuEnum && konst.Value.Type is FuRangeType && konst.Value is FuLiteral) {
			}
			else if (konst.Value is FuLiteral || konst.Value.IsConstEnum())
				Coerce(konst.Value, konst.Type);
			else if (konst.Value != this.Poison)
				ReportError(konst.Value, $"Value for constant {konst.Name} is not constant");
			konst.InMethod = this.CurrentMethod;
			konst.VisitStatus = FuVisitStatus.Done;
		}

		void ResolveConsts(FuContainerType container)
		{
			this.CurrentScope = container;
			switch (container) {
			case FuClass klass:
				for (FuSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
					if (symbol is FuConst konst)
						ResolveConst(konst);
				}
				break;
			case FuEnum enu:
				FuConst previous = null;
				for (FuSymbol symbol = enu.First; symbol != null; symbol = symbol.Next) {
					if (symbol is FuConst konst) {
						if (konst.Value != null) {
							ResolveConst(konst);
							enu.HasExplicitValue = true;
						}
						else
							konst.Value = new FuImplicitEnumValue { Value = previous == null ? 0 : previous.Value.IntValue() + 1 };
						previous = konst;
					}
				}
				break;
			default:
				throw new NotImplementedException();
			}
		}

		void ResolveTypes(FuClass klass)
		{
			this.CurrentScope = klass;
			for (FuSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
				switch (symbol) {
				case FuField field:
					FuType type = ResolveType(field);
					if (field.Value != null) {
						field.Value = VisitExpr(field.Value);
						if (!field.IsAssignableStorage())
							Coerce(field.Value, type is FuArrayStorageType array ? array.GetElementType() : type);
					}
					break;
				case FuMethod method:
					if (method.TypeExpr == this.Program.System.VoidType)
						method.Type = this.Program.System.VoidType;
					else
						ResolveType(method);
					for (FuVar param = method.Parameters.FirstParameter(); param != null; param = param.NextParameter()) {
						ResolveType(param);
						if (param.Value != null) {
							param.Value = FoldConst(param.Value);
							Coerce(param.Value, param.Type);
						}
					}
					break;
				default:
					break;
				}
			}
		}

		void ResolveCode(FuClass klass)
		{
			if (klass.Constructor != null) {
				this.CurrentScope = klass;
				this.CurrentMethod = klass.Constructor;
				VisitStatement(klass.Constructor.Body);
				this.CurrentMethod = null;
			}
			for (FuSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
				if (symbol is FuMethod method) {
					if (method.Name == "ToString" && method.CallType != FuCallType.Static && method.Parameters.Count() == 0)
						method.Id = FuId.ClassToString;
					if (method.Body != null) {
						if (method.CallType == FuCallType.Override || method.CallType == FuCallType.Sealed) {
							if (klass.Parent.TryLookup(method.Name, false) is FuMethod baseMethod) {
								switch (baseMethod.CallType) {
								case FuCallType.Abstract:
								case FuCallType.Virtual:
								case FuCallType.Override:
									break;
								default:
									ReportError(method, "Base method is not abstract or virtual");
									break;
								}
								if (!method.Type.EqualsType(baseMethod.Type))
									ReportError(method, "Base method has a different return type");
								if (method.IsMutator != baseMethod.IsMutator) {
									if (method.IsMutator)
										ReportError(method, "Mutating method cannot override a non-mutating method");
									else
										ReportError(method, "Non-mutating method cannot override a mutating method");
								}
								FuVar baseParam = baseMethod.Parameters.FirstParameter();
								for (FuVar param = method.Parameters.FirstParameter();; param = param.NextParameter()) {
									if (param == null) {
										if (baseParam != null)
											ReportError(method, "Fewer parameters than the overridden method");
										break;
									}
									if (baseParam == null) {
										ReportError(method, "More parameters than the overridden method");
										break;
									}
									if (!param.Type.EqualsType(baseParam.Type)) {
										ReportError(method, "Base method has a different parameter type");
										break;
									}
									baseParam = baseParam.NextParameter();
								}
								baseMethod.Calls.Add(method);
							}
							else
								ReportError(method, "No method to override");
						}
						this.CurrentScope = method.Parameters;
						this.CurrentMethod = method;
						if (!(method.Body is FuScope))
							OpenScope(method.MethodScope);
						VisitStatement(method.Body);
						if (method.Type.Id != FuId.VoidType && method.Body.CompletesNormally())
							ReportError(method.Body, "Method can complete without a return value");
						this.CurrentMethod = null;
					}
				}
			}
		}

		static void MarkMethodLive(FuMethodBase method)
		{
			if (method.IsLive)
				return;
			method.IsLive = true;
			foreach (FuMethod called in method.Calls)
				MarkMethodLive(called);
		}

		static void MarkClassLive(FuClass klass)
		{
			if (klass.IsPublic) {
				for (FuSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
					if (symbol is FuMethod method && (method.Visibility == FuVisibility.Public || method.Visibility == FuVisibility.Protected))
						MarkMethodLive(method);
				}
			}
			if (klass.Constructor != null)
				MarkMethodLive(klass.Constructor);
		}

		public void Process(FuProgram program)
		{
			this.Program = program;
			for (FuSymbol type = program.First; type != null; type = type.Next) {
				if (type is FuClass klass)
					ResolveBase(klass);
			}
			foreach (FuClass klass in program.Classes)
				CheckBaseCycle(klass);
			for (FuSymbol type = program.First; type != null; type = type.Next) {
				FuContainerType container = (FuContainerType) type;
				ResolveConsts(container);
			}
			foreach (FuClass klass in program.Classes)
				ResolveTypes(klass);
			foreach (FuClass klass in program.Classes)
				ResolveCode(klass);
			foreach (FuClass klass in program.Classes)
				MarkClassLive(klass);
		}
	}

	public abstract class GenHost : FuSemaHost
	{

		public abstract TextWriter CreateFile(string directory, string filename);

		public abstract void CloseFile();
	}

	public abstract class GenBase : FuVisitor
	{

		internal string Namespace;

		internal string OutputFile;

		GenHost Host;

		TextWriter Writer;

		readonly StringWriter StringWriter = new StringWriter();

		protected int Indent = 0;

		protected bool AtLineStart = true;

		bool AtChildStart = false;

		bool InChildBlock = false;

		protected bool InHeaderFile = false;

		readonly SortedDictionary<string, bool> Includes = new SortedDictionary<string, bool>();

		protected FuMethodBase CurrentMethod = null;

		protected readonly HashSet<FuClass> WrittenClasses = new HashSet<FuClass>();

		protected readonly List<FuSwitch> SwitchesWithGoto = new List<FuSwitch>();

		protected readonly List<FuExpr> CurrentTemporaries = new List<FuExpr>();

		public void SetHost(GenHost host)
		{
			this.Host = host;
		}

		protected virtual FuContainerType GetCurrentContainer()
		{
			FuClass klass = (FuClass) this.CurrentMethod.Parent;
			return klass;
		}

		protected abstract string GetTargetName();

		void ReportError(FuStatement statement, string message)
		{
			this.Host.ReportError(GetCurrentContainer().Filename, statement.Line, 1, statement.Line, 1, message);
		}

		protected void NotSupported(FuStatement statement, string feature)
		{
			ReportError(statement, $"{feature} not supported when targeting {GetTargetName()}");
		}

		protected void NotYet(FuStatement statement, string feature)
		{
			ReportError(statement, $"{feature} not supported yet when targeting {GetTargetName()}");
		}

		protected virtual void StartLine()
		{
			if (this.AtLineStart) {
				if (this.AtChildStart) {
					this.AtChildStart = false;
					this.Writer.Write('\n');
					this.Indent++;
				}
				for (int i = 0; i < this.Indent; i++)
					this.Writer.Write('\t');
				this.AtLineStart = false;
			}
		}

		protected void WriteChar(int c)
		{
			StartLine();
			this.Writer.Write(new Rune(c));
		}

		protected void Write(string s)
		{
			StartLine();
			this.Writer.Write(s);
		}

		internal override void VisitLiteralNull()
		{
			Write("null");
		}

		internal override void VisitLiteralFalse()
		{
			Write("false");
		}

		internal override void VisitLiteralTrue()
		{
			Write("true");
		}

		internal override void VisitLiteralLong(long i)
		{
			this.Writer.Write(i);
		}

		protected virtual int GetLiteralChars() => 0;

		internal override void VisitLiteralChar(int c)
		{
			if (c < GetLiteralChars()) {
				WriteChar('\'');
				switch (c) {
				case '\n':
					Write("\\n");
					break;
				case '\r':
					Write("\\r");
					break;
				case '\t':
					Write("\\t");
					break;
				case '\'':
					Write("\\'");
					break;
				case '\\':
					Write("\\\\");
					break;
				default:
					WriteChar(c);
					break;
				}
				WriteChar('\'');
			}
			else
				this.Writer.Write(c);
		}

		internal override void VisitLiteralDouble(double value)
		{
			string s = $"{value}";
			Write(s);
			foreach (int c in s) {
				switch (c) {
				case '-':
				case '0':
				case '1':
				case '2':
				case '3':
				case '4':
				case '5':
				case '6':
				case '7':
				case '8':
				case '9':
					break;
				default:
					return;
				}
			}
			Write(".0");
		}

		internal override void VisitLiteralString(string value)
		{
			WriteChar('"');
			Write(value);
			WriteChar('"');
		}

		void WriteLowercaseChar(int c)
		{
			if (c >= 'A' && c <= 'Z')
				c += 32;
			this.Writer.Write((char) c);
		}

		void WriteUppercaseChar(int c)
		{
			if (c >= 'a' && c <= 'z')
				c -= 32;
			this.Writer.Write((char) c);
		}

		protected void WriteLowercase(string s)
		{
			StartLine();
			foreach (int c in s)
				WriteLowercaseChar(c);
		}

		protected void WriteCamelCase(string s)
		{
			StartLine();
			WriteLowercaseChar(s[0]);
			this.Writer.Write(s.Substring(1));
		}

		protected void WritePascalCase(string s)
		{
			StartLine();
			WriteUppercaseChar(s[0]);
			this.Writer.Write(s.Substring(1));
		}

		protected void WriteUppercaseWithUnderscores(string s)
		{
			StartLine();
			bool first = true;
			foreach (int c in s) {
				if (!first && c >= 'A' && c <= 'Z') {
					this.Writer.Write('_');
					this.Writer.Write((char) c);
				}
				else
					WriteUppercaseChar(c);
				first = false;
			}
		}

		protected void WriteLowercaseWithUnderscores(string s)
		{
			StartLine();
			bool first = true;
			foreach (int c in s) {
				if (c >= 'A' && c <= 'Z') {
					if (!first)
						this.Writer.Write('_');
					WriteLowercaseChar(c);
				}
				else
					this.Writer.Write((char) c);
				first = false;
			}
		}

		protected void WriteNewLine()
		{
			this.Writer.Write('\n');
			this.AtLineStart = true;
		}

		protected void WriteCharLine(int c)
		{
			WriteChar(c);
			WriteNewLine();
		}

		protected void WriteLine(string s)
		{
			Write(s);
			WriteNewLine();
		}

		protected abstract void WriteName(FuSymbol symbol);

		protected virtual void WriteBanner()
		{
			WriteLine("// Generated automatically with \"fut\". Do not edit.");
		}

		protected void CreateFile(string directory, string filename)
		{
			this.Writer = this.Host.CreateFile(directory, filename);
			WriteBanner();
		}

		protected void CreateOutputFile()
		{
			CreateFile(null, this.OutputFile);
		}

		protected void CloseFile()
		{
			this.Host.CloseFile();
		}

		protected void OpenStringWriter()
		{
			this.Writer = this.StringWriter;
		}

		protected void CloseStringWriter()
		{
			this.Writer.Write(this.StringWriter.ToString());
			this.StringWriter.GetStringBuilder().Clear();
		}

		protected void Include(string name)
		{
			if (!this.Includes.ContainsKey(name))
				this.Includes[name] = this.InHeaderFile;
		}

		protected void WriteIncludes(string prefix, string suffix)
		{
			foreach ((string name, bool inHeaderFile) in this.Includes) {
				if (inHeaderFile == this.InHeaderFile) {
					Write(prefix);
					Write(name);
					WriteLine(suffix);
				}
			}
			if (!this.InHeaderFile)
				this.Includes.Clear();
		}

		protected virtual void StartDocLine()
		{
			Write(" * ");
		}

		protected void WriteXmlDoc(string text)
		{
			foreach (int c in text) {
				switch (c) {
				case '&':
					Write("&amp;");
					break;
				case '<':
					Write("&lt;");
					break;
				case '>':
					Write("&gt;");
					break;
				default:
					WriteChar(c);
					break;
				}
			}
		}

		protected virtual void WriteDocPara(FuDocPara para, bool many)
		{
			if (many) {
				WriteNewLine();
				Write(" * <p>");
			}
			foreach (FuDocInline inline in para.Children) {
				switch (inline) {
				case FuDocText text:
					WriteXmlDoc(text.Text);
					break;
				case FuDocCode code:
					Write("<code>");
					WriteXmlDoc(code.Text);
					Write("</code>");
					break;
				case FuDocLine _:
					WriteNewLine();
					StartDocLine();
					break;
				default:
					throw new NotImplementedException();
				}
			}
		}

		protected virtual void WriteDocList(FuDocList list)
		{
			WriteNewLine();
			WriteLine(" * <ul>");
			foreach (FuDocPara item in list.Items) {
				Write(" * <li>");
				WriteDocPara(item, false);
				WriteLine("</li>");
			}
			Write(" * </ul>");
		}

		protected void WriteDocBlock(FuDocBlock block, bool many)
		{
			switch (block) {
			case FuDocPara para:
				WriteDocPara(para, many);
				break;
			case FuDocList list:
				WriteDocList(list);
				break;
			default:
				throw new NotImplementedException();
			}
		}

		protected void WriteContent(FuCodeDoc doc)
		{
			StartDocLine();
			WriteDocPara(doc.Summary, false);
			WriteNewLine();
			if (doc.Details.Count > 0) {
				StartDocLine();
				if (doc.Details.Count == 1)
					WriteDocBlock(doc.Details[0], false);
				else {
					foreach (FuDocBlock block in doc.Details)
						WriteDocBlock(block, true);
				}
				WriteNewLine();
			}
		}

		protected virtual void WriteDoc(FuCodeDoc doc)
		{
			if (doc != null) {
				WriteLine("/**");
				WriteContent(doc);
				WriteLine(" */");
			}
		}

		protected virtual void WriteSelfDoc(FuMethod method)
		{
		}

		protected virtual void WriteParameterDoc(FuVar param, bool first)
		{
			Write(" * @param ");
			WriteName(param);
			WriteChar(' ');
			WriteDocPara(param.Documentation.Summary, false);
			WriteNewLine();
		}

		protected void WriteParametersDoc(FuMethod method)
		{
			bool first = true;
			for (FuVar param = method.Parameters.FirstParameter(); param != null; param = param.NextParameter()) {
				if (param.Documentation != null) {
					WriteParameterDoc(param, first);
					first = false;
				}
			}
		}

		protected void WriteMethodDoc(FuMethod method)
		{
			if (method.Documentation == null)
				return;
			WriteLine("/**");
			WriteContent(method.Documentation);
			WriteSelfDoc(method);
			WriteParametersDoc(method);
			WriteLine(" */");
		}

		protected void WriteTopLevelNatives(FuProgram program)
		{
			foreach (string content in program.TopLevelNatives)
				Write(content);
		}

		protected void OpenBlock()
		{
			WriteCharLine('{');
			this.Indent++;
		}

		protected void CloseBlock()
		{
			this.Indent--;
			WriteCharLine('}');
		}

		protected virtual void EndStatement()
		{
			WriteCharLine(';');
		}

		protected void WriteComma(int i)
		{
			if (i > 0) {
				if ((i & 15) == 0) {
					WriteCharLine(',');
					WriteChar('\t');
				}
				else
					Write(", ");
			}
		}

		protected void WriteBytes(List<byte> content)
		{
			int i = 0;
			foreach (int b in content) {
				WriteComma(i++);
				VisitLiteralLong(b);
			}
		}

		protected virtual FuId GetTypeId(FuType type, bool promote) => promote && type is FuRangeType ? FuId.IntType : type.Id;

		protected abstract void WriteTypeAndName(FuNamedValue value);

		protected virtual void WriteLocalName(FuSymbol symbol, FuPriority parent)
		{
			if (symbol is FuField)
				Write("this.");
			WriteName(symbol);
		}

		protected void WriteDoubling(string s, int doubled)
		{
			foreach (int c in s) {
				if (c == doubled)
					WriteChar(c);
				WriteChar(c);
			}
		}

		protected virtual void WritePrintfWidth(FuInterpolatedPart part)
		{
			if (part.WidthExpr != null)
				VisitLiteralLong(part.Width);
			if (part.Precision >= 0) {
				WriteChar('.');
				VisitLiteralLong(part.Precision);
			}
		}

		static int GetPrintfFormat(FuType type, int format)
		{
			switch (type) {
			case FuIntegerType _:
				return format == 'x' || format == 'X' ? format : 'd';
			case FuNumericType _:
				switch (format) {
				case 'E':
				case 'e':
				case 'f':
				case 'G':
				case 'g':
					return format;
				case 'F':
					return 'f';
				default:
					return 'g';
				}
			case FuClassType _:
				return 's';
			default:
				throw new NotImplementedException();
			}
		}

		protected void WritePrintfFormat(FuInterpolatedString expr)
		{
			foreach (FuInterpolatedPart part in expr.Parts) {
				WriteDoubling(part.Prefix, '%');
				WriteChar('%');
				WritePrintfWidth(part);
				WriteChar(GetPrintfFormat(part.Argument.Type, part.Format));
			}
			WriteDoubling(expr.Suffix, '%');
		}

		protected void WritePyFormat(FuInterpolatedPart part)
		{
			if (part.WidthExpr != null || part.Precision >= 0 || (part.Format != ' ' && part.Format != 'D'))
				WriteChar(':');
			if (part.WidthExpr != null) {
				if (part.Width >= 0) {
					if (!(part.Argument.Type is FuNumericType))
						WriteChar('>');
					VisitLiteralLong(part.Width);
				}
				else {
					WriteChar('<');
					VisitLiteralLong(-part.Width);
				}
			}
			if (part.Precision >= 0) {
				WriteChar(part.Argument.Type is FuIntegerType ? '0' : '.');
				VisitLiteralLong(part.Precision);
			}
			if (part.Format != ' ' && part.Format != 'D')
				WriteChar(part.Format);
			WriteChar('}');
		}

		protected virtual void WriteInterpolatedStringArg(FuExpr expr)
		{
			expr.Accept(this, FuPriority.Argument);
		}

		protected void WriteInterpolatedStringArgs(FuInterpolatedString expr)
		{
			foreach (FuInterpolatedPart part in expr.Parts) {
				Write(", ");
				WriteInterpolatedStringArg(part.Argument);
			}
		}

		protected void WritePrintf(FuInterpolatedString expr, bool newLine)
		{
			WriteChar('"');
			WritePrintfFormat(expr);
			if (newLine)
				Write("\\n");
			WriteChar('"');
			WriteInterpolatedStringArgs(expr);
			WriteChar(')');
		}

		protected void WritePostfix(FuExpr obj, string s)
		{
			obj.Accept(this, FuPriority.Primary);
			Write(s);
		}

		protected void WriteCall(string function, FuExpr arg0, FuExpr arg1 = null, FuExpr arg2 = null)
		{
			Write(function);
			WriteChar('(');
			arg0.Accept(this, FuPriority.Argument);
			if (arg1 != null) {
				Write(", ");
				arg1.Accept(this, FuPriority.Argument);
				if (arg2 != null) {
					Write(", ");
					arg2.Accept(this, FuPriority.Argument);
				}
			}
			WriteChar(')');
		}

		protected virtual void WriteMemberOp(FuExpr left, FuSymbolReference symbol)
		{
			WriteChar('.');
		}

		protected void WriteMethodCall(FuExpr obj, string method, FuExpr arg0, FuExpr arg1 = null)
		{
			obj.Accept(this, FuPriority.Primary);
			WriteMemberOp(obj, null);
			WriteCall(method, arg0, arg1);
		}

		protected virtual void WriteSelectValues(FuType type, FuSelectExpr expr)
		{
			WriteCoerced(type, expr.OnTrue, FuPriority.Select);
			Write(" : ");
			WriteCoerced(type, expr.OnFalse, FuPriority.Select);
		}

		protected virtual void WriteCoercedSelect(FuType type, FuSelectExpr expr, FuPriority parent)
		{
			if (parent > FuPriority.Select)
				WriteChar('(');
			expr.Cond.Accept(this, FuPriority.SelectCond);
			Write(" ? ");
			WriteSelectValues(type, expr);
			if (parent > FuPriority.Select)
				WriteChar(')');
		}

		protected virtual void WriteCoercedInternal(FuType type, FuExpr expr, FuPriority parent)
		{
			expr.Accept(this, parent);
		}

		protected void WriteCoerced(FuType type, FuExpr expr, FuPriority parent)
		{
			if (expr is FuSelectExpr select)
				WriteCoercedSelect(type, select, parent);
			else
				WriteCoercedInternal(type, expr, parent);
		}

		protected virtual void WriteCoercedExpr(FuType type, FuExpr expr)
		{
			WriteCoerced(type, expr, FuPriority.Argument);
		}

		protected virtual void WriteStronglyCoerced(FuType type, FuExpr expr)
		{
			WriteCoerced(type, expr, FuPriority.Argument);
		}

		protected virtual void WriteCoercedLiteral(FuType type, FuExpr expr)
		{
			expr.Accept(this, FuPriority.Argument);
		}

		protected void WriteCoercedLiterals(FuType type, List<FuExpr> exprs)
		{
			for (int i = 0; i < exprs.Count; i++) {
				WriteComma(i);
				WriteCoercedLiteral(type, exprs[i]);
			}
		}

		protected void WriteArgs(FuMethod method, List<FuExpr> args)
		{
			FuVar param = method.Parameters.FirstParameter();
			bool first = true;
			foreach (FuExpr arg in args) {
				if (!first)
					Write(", ");
				first = false;
				WriteStronglyCoerced(param.Type, arg);
				param = param.NextParameter();
			}
		}

		protected void WriteArgsInParentheses(FuMethod method, List<FuExpr> args)
		{
			WriteChar('(');
			WriteArgs(method, args);
			WriteChar(')');
		}

		protected abstract void WriteNewArray(FuType elementType, FuExpr lengthExpr, FuPriority parent);

		protected virtual void WriteNewArrayStorage(FuArrayStorageType array)
		{
			WriteNewArray(array.GetElementType(), array.LengthExpr, FuPriority.Argument);
		}

		protected abstract void WriteNew(FuReadWriteClassType klass, FuPriority parent);

		protected void WriteNewStorage(FuType type)
		{
			switch (type) {
			case FuArrayStorageType array:
				WriteNewArrayStorage(array);
				break;
			case FuStorageType storage:
				WriteNew(storage, FuPriority.Argument);
				break;
			default:
				throw new NotImplementedException();
			}
		}

		protected virtual void WriteArrayStorageInit(FuArrayStorageType array, FuExpr value)
		{
			Write(" = ");
			WriteNewArrayStorage(array);
		}

		protected virtual void WriteNewWithFields(FuReadWriteClassType type, FuAggregateInitializer init)
		{
			WriteNew(type, FuPriority.Argument);
		}

		protected virtual void WriteStorageInit(FuNamedValue def)
		{
			Write(" = ");
			if (def.Value is FuAggregateInitializer init) {
				FuReadWriteClassType klass = (FuReadWriteClassType) def.Type;
				WriteNewWithFields(klass, init);
			}
			else
				WriteNewStorage(def.Type);
		}

		protected virtual void WriteVarInit(FuNamedValue def)
		{
			if (def.IsAssignableStorage()) {
			}
			else if (def.Type is FuArrayStorageType array)
				WriteArrayStorageInit(array, def.Value);
			else if (def.Value != null && !(def.Value is FuAggregateInitializer)) {
				Write(" = ");
				WriteCoercedExpr(def.Type, def.Value);
			}
			else if (def.Type.IsFinal() && !(def.Parent is FuParameters))
				WriteStorageInit(def);
		}

		protected virtual void WriteVar(FuNamedValue def)
		{
			WriteTypeAndName(def);
			WriteVarInit(def);
		}

		internal override void VisitVar(FuVar expr)
		{
			WriteVar(expr);
		}

		protected void WriteObjectLiteral(FuAggregateInitializer init, string separator)
		{
			string prefix = " { ";
			foreach (FuExpr item in init.Items) {
				Write(prefix);
				FuBinaryExpr assign = (FuBinaryExpr) item;
				FuSymbolReference field = (FuSymbolReference) assign.Left;
				WriteName(field.Symbol);
				Write(separator);
				WriteCoerced(assign.Left.Type, assign.Right, FuPriority.Argument);
				prefix = ", ";
			}
			Write(" }");
		}

		static FuAggregateInitializer GetAggregateInitializer(FuNamedValue def)
		{
			FuExpr expr = def.Value;
			if (expr is FuPrefixExpr unary)
				expr = unary.Inner;
			return expr is FuAggregateInitializer init ? init : null;
		}

		void WriteAggregateInitField(FuExpr obj, FuExpr item)
		{
			FuBinaryExpr assign = (FuBinaryExpr) item;
			FuSymbolReference field = (FuSymbolReference) assign.Left;
			WriteMemberOp(obj, field);
			WriteName(field.Symbol);
			Write(" = ");
			WriteCoerced(field.Type, assign.Right, FuPriority.Argument);
			EndStatement();
		}

		protected virtual void WriteInitCode(FuNamedValue def)
		{
			FuAggregateInitializer init = GetAggregateInitializer(def);
			if (init != null) {
				foreach (FuExpr item in init.Items) {
					WriteLocalName(def, FuPriority.Primary);
					WriteAggregateInitField(def, item);
				}
			}
		}

		protected virtual void DefineIsVar(FuBinaryExpr binary)
		{
			if (binary.Right is FuVar def) {
				EnsureChildBlock();
				WriteVar(def);
				EndStatement();
			}
		}

		protected void WriteArrayElement(FuNamedValue def, int nesting)
		{
			WriteLocalName(def, FuPriority.Primary);
			for (int i = 0; i < nesting; i++) {
				Write("[_i");
				VisitLiteralLong(i);
				WriteChar(']');
			}
		}

		protected void OpenLoop(string intString, int nesting, int count)
		{
			Write("for (");
			Write(intString);
			Write(" _i");
			VisitLiteralLong(nesting);
			Write(" = 0; _i");
			VisitLiteralLong(nesting);
			Write(" < ");
			VisitLiteralLong(count);
			Write("; _i");
			VisitLiteralLong(nesting);
			Write("++) ");
			OpenBlock();
		}

		protected void WriteResourceName(string name)
		{
			foreach (int c in name)
				WriteChar(FuLexer.IsLetterOrDigit(c) ? c : '_');
		}

		protected abstract void WriteResource(string name, int length);

		internal override void VisitPrefixExpr(FuPrefixExpr expr, FuPriority parent)
		{
			switch (expr.Op) {
			case FuToken.Increment:
				Write("++");
				break;
			case FuToken.Decrement:
				Write("--");
				break;
			case FuToken.Minus:
				WriteChar('-');
				if (expr.Inner is FuPrefixExpr inner && (inner.Op == FuToken.Minus || inner.Op == FuToken.Decrement))
					WriteChar(' ');
				break;
			case FuToken.Tilde:
				WriteChar('~');
				break;
			case FuToken.ExclamationMark:
				WriteChar('!');
				break;
			case FuToken.New:
				FuDynamicPtrType dynamic = (FuDynamicPtrType) expr.Type;
				if (dynamic.Class.Id == FuId.ArrayPtrClass)
					WriteNewArray(dynamic.GetElementType(), expr.Inner, parent);
				else if (expr.Inner is FuAggregateInitializer init) {
					int tempId = this.CurrentTemporaries.IndexOf(expr);
					if (tempId >= 0) {
						Write("futemp");
						VisitLiteralLong(tempId);
					}
					else
						WriteNewWithFields(dynamic, init);
				}
				else
					WriteNew(dynamic, parent);
				return;
			case FuToken.Resource:
				FuLiteralString name = (FuLiteralString) expr.Inner;
				FuArrayStorageType array = (FuArrayStorageType) expr.Type;
				WriteResource(name.Value, array.Length);
				return;
			default:
				throw new NotImplementedException();
			}
			expr.Inner.Accept(this, FuPriority.Primary);
		}

		internal override void VisitPostfixExpr(FuPostfixExpr expr, FuPriority parent)
		{
			expr.Inner.Accept(this, FuPriority.Primary);
			switch (expr.Op) {
			case FuToken.Increment:
				Write("++");
				break;
			case FuToken.Decrement:
				Write("--");
				break;
			default:
				throw new NotImplementedException();
			}
		}

		protected void StartAdd(FuExpr expr)
		{
			if (!expr.IsLiteralZero()) {
				expr.Accept(this, FuPriority.Add);
				Write(" + ");
			}
		}

		protected void WriteAdd(FuExpr left, FuExpr right)
		{
			if (left is FuLiteralLong leftLiteral) {
				long leftValue = leftLiteral.Value;
				if (leftValue == 0) {
					right.Accept(this, FuPriority.Argument);
					return;
				}
				if (right is FuLiteralLong rightLiteral) {
					VisitLiteralLong(leftValue + rightLiteral.Value);
					return;
				}
			}
			else if (right.IsLiteralZero()) {
				left.Accept(this, FuPriority.Argument);
				return;
			}
			left.Accept(this, FuPriority.Add);
			Write(" + ");
			right.Accept(this, FuPriority.Add);
		}

		protected void WriteStartEnd(FuExpr startIndex, FuExpr length)
		{
			startIndex.Accept(this, FuPriority.Argument);
			Write(", ");
			WriteAdd(startIndex, length);
		}

		static bool IsBitOp(FuPriority parent)
		{
			switch (parent) {
			case FuPriority.Or:
			case FuPriority.Xor:
			case FuPriority.And:
			case FuPriority.Shift:
				return true;
			default:
				return false;
			}
		}

		protected virtual void WriteBinaryOperand(FuExpr expr, FuPriority parent, FuBinaryExpr binary)
		{
			expr.Accept(this, parent);
		}

		protected void WriteBinaryExpr(FuBinaryExpr expr, bool parentheses, FuPriority left, string op, FuPriority right)
		{
			if (parentheses)
				WriteChar('(');
			WriteBinaryOperand(expr.Left, left, expr);
			Write(op);
			WriteBinaryOperand(expr.Right, right, expr);
			if (parentheses)
				WriteChar(')');
		}

		protected void WriteBinaryExpr2(FuBinaryExpr expr, FuPriority parent, FuPriority child, string op)
		{
			WriteBinaryExpr(expr, parent > child, child, op, child);
		}

		protected static string GetEqOp(bool not) => not ? " != " : " == ";

		protected virtual void WriteEqualOperand(FuExpr expr, FuExpr other)
		{
			expr.Accept(this, FuPriority.Equality);
		}

		protected void WriteEqualExpr(FuExpr left, FuExpr right, FuPriority parent, string op)
		{
			if (parent > FuPriority.CondAnd)
				WriteChar('(');
			WriteEqualOperand(left, right);
			Write(op);
			WriteEqualOperand(right, left);
			if (parent > FuPriority.CondAnd)
				WriteChar(')');
		}

		protected virtual void WriteEqual(FuExpr left, FuExpr right, FuPriority parent, bool not)
		{
			WriteEqualExpr(left, right, parent, GetEqOp(not));
		}

		protected virtual void WriteRel(FuBinaryExpr expr, FuPriority parent, string op)
		{
			WriteBinaryExpr(expr, parent > FuPriority.CondAnd, FuPriority.Rel, op, FuPriority.Rel);
		}

		protected virtual void WriteAnd(FuBinaryExpr expr, FuPriority parent)
		{
			WriteBinaryExpr(expr, parent > FuPriority.CondAnd && parent != FuPriority.And, FuPriority.And, " & ", FuPriority.And);
		}

		protected virtual void WriteAssignRight(FuBinaryExpr expr)
		{
			WriteCoerced(expr.Left.Type, expr.Right, FuPriority.Argument);
		}

		protected virtual void WriteAssign(FuBinaryExpr expr, FuPriority parent)
		{
			if (parent > FuPriority.Assign)
				WriteChar('(');
			expr.Left.Accept(this, FuPriority.Assign);
			Write(" = ");
			WriteAssignRight(expr);
			if (parent > FuPriority.Assign)
				WriteChar(')');
		}

		protected void WriteIndexing(FuExpr collection, FuExpr index)
		{
			collection.Accept(this, FuPriority.Primary);
			WriteChar('[');
			index.Accept(this, FuPriority.Argument);
			WriteChar(']');
		}

		protected virtual void WriteIndexingExpr(FuBinaryExpr expr, FuPriority parent)
		{
			WriteIndexing(expr.Left, expr.Right);
		}

		protected virtual string GetIsOperator() => " is ";

		internal override void VisitBinaryExpr(FuBinaryExpr expr, FuPriority parent)
		{
			switch (expr.Op) {
			case FuToken.Plus:
				WriteBinaryExpr(expr, parent > FuPriority.Add || IsBitOp(parent), FuPriority.Add, " + ", FuPriority.Add);
				break;
			case FuToken.Minus:
				WriteBinaryExpr(expr, parent > FuPriority.Add || IsBitOp(parent), FuPriority.Add, " - ", FuPriority.Mul);
				break;
			case FuToken.Asterisk:
				WriteBinaryExpr(expr, parent > FuPriority.Mul, FuPriority.Mul, " * ", FuPriority.Primary);
				break;
			case FuToken.Slash:
				WriteBinaryExpr(expr, parent > FuPriority.Mul, FuPriority.Mul, " / ", FuPriority.Primary);
				break;
			case FuToken.Mod:
				WriteBinaryExpr(expr, parent > FuPriority.Mul, FuPriority.Mul, " % ", FuPriority.Primary);
				break;
			case FuToken.ShiftLeft:
				WriteBinaryExpr(expr, parent > FuPriority.Shift, FuPriority.Shift, " << ", FuPriority.Mul);
				break;
			case FuToken.ShiftRight:
				WriteBinaryExpr(expr, parent > FuPriority.Shift, FuPriority.Shift, " >> ", FuPriority.Mul);
				break;
			case FuToken.Equal:
				WriteEqual(expr.Left, expr.Right, parent, false);
				break;
			case FuToken.NotEqual:
				WriteEqual(expr.Left, expr.Right, parent, true);
				break;
			case FuToken.Less:
				WriteRel(expr, parent, " < ");
				break;
			case FuToken.LessOrEqual:
				WriteRel(expr, parent, " <= ");
				break;
			case FuToken.Greater:
				WriteRel(expr, parent, " > ");
				break;
			case FuToken.GreaterOrEqual:
				WriteRel(expr, parent, " >= ");
				break;
			case FuToken.And:
				WriteAnd(expr, parent);
				break;
			case FuToken.Or:
				WriteBinaryExpr2(expr, parent, FuPriority.Or, " | ");
				break;
			case FuToken.Xor:
				WriteBinaryExpr(expr, parent > FuPriority.Xor || parent == FuPriority.Or, FuPriority.Xor, " ^ ", FuPriority.Xor);
				break;
			case FuToken.CondAnd:
				WriteBinaryExpr(expr, parent > FuPriority.CondAnd || parent == FuPriority.CondOr, FuPriority.CondAnd, " && ", FuPriority.CondAnd);
				break;
			case FuToken.CondOr:
				WriteBinaryExpr2(expr, parent, FuPriority.CondOr, " || ");
				break;
			case FuToken.Assign:
				WriteAssign(expr, parent);
				break;
			case FuToken.AddAssign:
			case FuToken.SubAssign:
			case FuToken.MulAssign:
			case FuToken.DivAssign:
			case FuToken.ModAssign:
			case FuToken.ShiftLeftAssign:
			case FuToken.ShiftRightAssign:
			case FuToken.AndAssign:
			case FuToken.OrAssign:
			case FuToken.XorAssign:
				if (parent > FuPriority.Assign)
					WriteChar('(');
				expr.Left.Accept(this, FuPriority.Assign);
				WriteChar(' ');
				Write(expr.GetOpString());
				WriteChar(' ');
				expr.Right.Accept(this, FuPriority.Argument);
				if (parent > FuPriority.Assign)
					WriteChar(')');
				break;
			case FuToken.LeftBracket:
				if (expr.Left.Type is FuStringType)
					WriteCharAt(expr);
				else
					WriteIndexingExpr(expr, parent);
				break;
			case FuToken.Is:
				if (parent > FuPriority.Rel)
					WriteChar('(');
				expr.Left.Accept(this, FuPriority.Rel);
				Write(GetIsOperator());
				switch (expr.Right) {
				case FuSymbolReference symbol:
					WriteName(symbol.Symbol);
					break;
				case FuVar def:
					WriteTypeAndName(def);
					break;
				default:
					throw new NotImplementedException();
				}
				if (parent > FuPriority.Rel)
					WriteChar(')');
				break;
			case FuToken.When:
				expr.Left.Accept(this, FuPriority.Argument);
				Write(" when ");
				expr.Right.Accept(this, FuPriority.Argument);
				break;
			default:
				throw new NotImplementedException();
			}
		}

		protected abstract void WriteStringLength(FuExpr expr);

		protected static bool IsReferenceTo(FuExpr expr, FuId id) => expr is FuSymbolReference symbol && symbol.Symbol.Id == id;

		protected bool WriteJavaMatchProperty(FuSymbolReference expr, FuPriority parent)
		{
			switch (expr.Symbol.Id) {
			case FuId.MatchStart:
				WritePostfix(expr.Left, ".start()");
				return true;
			case FuId.MatchEnd:
				WritePostfix(expr.Left, ".end()");
				return true;
			case FuId.MatchLength:
				if (parent > FuPriority.Add)
					WriteChar('(');
				WritePostfix(expr.Left, ".end() - ");
				WritePostfix(expr.Left, ".start()");
				if (parent > FuPriority.Add)
					WriteChar(')');
				return true;
			case FuId.MatchValue:
				WritePostfix(expr.Left, ".group()");
				return true;
			default:
				return false;
			}
		}

		internal override void VisitSymbolReference(FuSymbolReference expr, FuPriority parent)
		{
			if (expr.Left == null)
				WriteLocalName(expr.Symbol, parent);
			else if (expr.Symbol.Id == FuId.StringLength)
				WriteStringLength(expr.Left);
			else {
				expr.Left.Accept(this, FuPriority.Primary);
				WriteMemberOp(expr.Left, expr);
				WriteName(expr.Symbol);
			}
		}

		protected abstract void WriteCharAt(FuBinaryExpr expr);

		protected virtual void WriteNotPromoted(FuType type, FuExpr expr)
		{
			expr.Accept(this, FuPriority.Argument);
		}

		protected virtual void WriteEnumAsInt(FuExpr expr, FuPriority parent)
		{
			expr.Accept(this, parent);
		}

		protected void WriteEnumHasFlag(FuExpr obj, List<FuExpr> args, FuPriority parent)
		{
			if (parent > FuPriority.Equality)
				WriteChar('(');
			int i = args[0].IntValue();
			if ((i & (i - 1)) == 0 && i != 0) {
				WriteChar('(');
				WriteEnumAsInt(obj, FuPriority.And);
				Write(" & ");
				WriteEnumAsInt(args[0], FuPriority.And);
				Write(") != 0");
			}
			else {
				Write("(~");
				WriteEnumAsInt(obj, FuPriority.Primary);
				Write(" & ");
				WriteEnumAsInt(args[0], FuPriority.And);
				Write(") == 0");
			}
			if (parent > FuPriority.Equality)
				WriteChar(')');
		}

		protected void WriteTryParseRadix(List<FuExpr> args)
		{
			Write(", ");
			if (args.Count == 2)
				args[1].Accept(this, FuPriority.Argument);
			else
				Write("10");
		}

		protected void WriteListAdd(FuExpr obj, string method, List<FuExpr> args)
		{
			obj.Accept(this, FuPriority.Primary);
			WriteChar('.');
			Write(method);
			WriteChar('(');
			FuType elementType = obj.Type.AsClassType().GetElementType();
			if (args.Count == 0)
				WriteNewStorage(elementType);
			else
				WriteNotPromoted(elementType, args[0]);
			WriteChar(')');
		}

		protected void WriteListInsert(FuExpr obj, string method, List<FuExpr> args, string separator = ", ")
		{
			obj.Accept(this, FuPriority.Primary);
			WriteChar('.');
			Write(method);
			WriteChar('(');
			args[0].Accept(this, FuPriority.Argument);
			Write(separator);
			FuType elementType = obj.Type.AsClassType().GetElementType();
			if (args.Count == 1)
				WriteNewStorage(elementType);
			else
				WriteNotPromoted(elementType, args[1]);
			WriteChar(')');
		}

		protected void WriteDictionaryAdd(FuExpr obj, List<FuExpr> args)
		{
			WriteIndexing(obj, args[0]);
			Write(" = ");
			WriteNewStorage(obj.Type.AsClassType().GetValueType());
		}

		protected void WriteClampAsMinMax(List<FuExpr> args)
		{
			args[0].Accept(this, FuPriority.Argument);
			Write(", ");
			args[1].Accept(this, FuPriority.Argument);
			Write("), ");
			args[2].Accept(this, FuPriority.Argument);
			WriteChar(')');
		}

		protected RegexOptions GetRegexOptions(List<FuExpr> args)
		{
			FuExpr expr = args[^1];
			if (expr.Type is FuEnum)
				return (RegexOptions) expr.IntValue();
			return RegexOptions.None;
		}

		protected bool WriteRegexOptions(List<FuExpr> args, string prefix, string separator, string suffix, string i, string m, string s)
		{
			RegexOptions options = GetRegexOptions(args);
			if (options == RegexOptions.None)
				return false;
			Write(prefix);
			if (options.HasFlag(RegexOptions.IgnoreCase))
				Write(i);
			if (options.HasFlag(RegexOptions.Multiline)) {
				if (options.HasFlag(RegexOptions.IgnoreCase))
					Write(separator);
				Write(m);
			}
			if (options.HasFlag(RegexOptions.Singleline)) {
				if (options != RegexOptions.Singleline)
					Write(separator);
				Write(s);
			}
			Write(suffix);
			return true;
		}

		protected abstract void WriteCallExpr(FuExpr obj, FuMethod method, List<FuExpr> args, FuPriority parent);

		internal override void VisitCallExpr(FuCallExpr expr, FuPriority parent)
		{
			FuMethod method = (FuMethod) expr.Method.Symbol;
			WriteCallExpr(expr.Method.Left, method, expr.Arguments, parent);
		}

		internal override void VisitSelectExpr(FuSelectExpr expr, FuPriority parent)
		{
			WriteCoercedSelect(expr.Type, expr, parent);
		}

		protected void EnsureChildBlock()
		{
			if (this.AtChildStart) {
				this.AtLineStart = false;
				this.AtChildStart = false;
				WriteChar(' ');
				OpenBlock();
				this.InChildBlock = true;
			}
		}

		protected static bool HasTemporaries(FuExpr expr)
		{
			switch (expr) {
			case FuAggregateInitializer init:
				return init.Items.Exists(item => HasTemporaries(item));
			case FuLiteral _:
			case FuLambdaExpr _:
				return false;
			case FuInterpolatedString interp:
				return interp.Parts.Exists(part => HasTemporaries(part.Argument));
			case FuSymbolReference symbol:
				return symbol.Left != null && HasTemporaries(symbol.Left);
			case FuUnaryExpr unary:
				return unary.Inner != null && (HasTemporaries(unary.Inner) || unary.Inner is FuAggregateInitializer);
			case FuBinaryExpr binary:
				return HasTemporaries(binary.Left) || (binary.Op == FuToken.Is ? binary.Right is FuVar : HasTemporaries(binary.Right));
			case FuSelectExpr select:
				return HasTemporaries(select.Cond) || HasTemporaries(select.OnTrue) || HasTemporaries(select.OnFalse);
			case FuCallExpr call:
				return HasTemporaries(call.Method) || call.Arguments.Exists(arg => HasTemporaries(arg));
			default:
				throw new NotImplementedException();
			}
		}

		protected abstract void StartTemporaryVar(FuType type);

		protected virtual void DefineObjectLiteralTemporary(FuUnaryExpr expr)
		{
			if (expr.Inner is FuAggregateInitializer init) {
				EnsureChildBlock();
				int id = this.CurrentTemporaries.IndexOf(expr.Type);
				if (id < 0) {
					id = this.CurrentTemporaries.Count;
					StartTemporaryVar(expr.Type);
					this.CurrentTemporaries.Add(expr);
				}
				else
					this.CurrentTemporaries[id] = expr;
				Write("futemp");
				VisitLiteralLong(id);
				Write(" = ");
				FuDynamicPtrType dynamic = (FuDynamicPtrType) expr.Type;
				WriteNew(dynamic, FuPriority.Argument);
				EndStatement();
				foreach (FuExpr item in init.Items) {
					Write("futemp");
					VisitLiteralLong(id);
					WriteAggregateInitField(expr, item);
				}
			}
		}

		protected void WriteTemporaries(FuExpr expr)
		{
			switch (expr) {
			case FuVar def:
				if (def.Value != null) {
					if (def.Value is FuUnaryExpr unary && unary.Inner is FuAggregateInitializer)
						WriteTemporaries(unary.Inner);
					else
						WriteTemporaries(def.Value);
				}
				break;
			case FuAggregateInitializer init:
				foreach (FuExpr item in init.Items) {
					FuBinaryExpr assign = (FuBinaryExpr) item;
					WriteTemporaries(assign.Right);
				}
				break;
			case FuLiteral _:
			case FuLambdaExpr _:
				break;
			case FuInterpolatedString interp:
				foreach (FuInterpolatedPart part in interp.Parts)
					WriteTemporaries(part.Argument);
				break;
			case FuSymbolReference symbol:
				if (symbol.Left != null)
					WriteTemporaries(symbol.Left);
				break;
			case FuUnaryExpr unary:
				if (unary.Inner != null) {
					WriteTemporaries(unary.Inner);
					DefineObjectLiteralTemporary(unary);
				}
				break;
			case FuBinaryExpr binary:
				WriteTemporaries(binary.Left);
				if (binary.Op == FuToken.Is)
					DefineIsVar(binary);
				else
					WriteTemporaries(binary.Right);
				break;
			case FuSelectExpr select:
				WriteTemporaries(select.Cond);
				WriteTemporaries(select.OnTrue);
				WriteTemporaries(select.OnFalse);
				break;
			case FuCallExpr call:
				WriteTemporaries(call.Method);
				foreach (FuExpr arg in call.Arguments)
					WriteTemporaries(arg);
				break;
			default:
				throw new NotImplementedException();
			}
		}

		protected virtual void CleanupTemporary(int i, FuExpr temp)
		{
		}

		protected void CleanupTemporaries()
		{
			for (int i = this.CurrentTemporaries.Count; --i >= 0;) {
				FuExpr temp = this.CurrentTemporaries[i];
				if (!(temp is FuType)) {
					CleanupTemporary(i, temp);
					this.CurrentTemporaries[i] = temp.Type;
				}
			}
		}

		internal override void VisitExpr(FuExpr statement)
		{
			WriteTemporaries(statement);
			statement.Accept(this, FuPriority.Statement);
			WriteCharLine(';');
			if (statement is FuVar def)
				WriteInitCode(def);
			CleanupTemporaries();
		}

		internal override void VisitConst(FuConst statement)
		{
		}

		protected abstract void WriteAssertCast(FuBinaryExpr expr);

		protected abstract void WriteAssert(FuAssert statement);

		internal override void VisitAssert(FuAssert statement)
		{
			if (statement.Cond is FuBinaryExpr binary && binary.Op == FuToken.Is && binary.Right is FuVar)
				WriteAssertCast(binary);
			else
				WriteAssert(statement);
		}

		protected void WriteFirstStatements(List<FuStatement> statements, int count)
		{
			for (int i = 0; i < count; i++)
				statements[i].AcceptStatement(this);
		}

		protected virtual void WriteStatements(List<FuStatement> statements)
		{
			WriteFirstStatements(statements, statements.Count);
		}

		protected virtual void CleanupBlock(FuBlock statement)
		{
		}

		internal override void VisitBlock(FuBlock statement)
		{
			if (this.AtChildStart) {
				this.AtLineStart = false;
				this.AtChildStart = false;
				WriteChar(' ');
			}
			OpenBlock();
			int temporariesCount = this.CurrentTemporaries.Count;
			WriteStatements(statement.Statements);
			CleanupBlock(statement);
			this.CurrentTemporaries.RemoveRange(temporariesCount, this.CurrentTemporaries.Count - temporariesCount);
			CloseBlock();
		}

		protected virtual void WriteChild(FuStatement statement)
		{
			bool wasInChildBlock = this.InChildBlock;
			this.AtLineStart = true;
			this.AtChildStart = true;
			this.InChildBlock = false;
			statement.AcceptStatement(this);
			if (this.InChildBlock)
				CloseBlock();
			else if (!(statement is FuBlock))
				this.Indent--;
			this.InChildBlock = wasInChildBlock;
		}

		protected virtual void StartBreakGoto()
		{
			Write("goto fuafterswitch");
		}

		internal override void VisitBreak(FuBreak statement)
		{
			if (statement.LoopOrSwitch is FuSwitch switchStatement) {
				int gotoId = this.SwitchesWithGoto.IndexOf(switchStatement);
				if (gotoId >= 0) {
					StartBreakGoto();
					VisitLiteralLong(gotoId);
					WriteCharLine(';');
					return;
				}
			}
			WriteLine("break;");
		}

		internal override void VisitContinue(FuContinue statement)
		{
			WriteLine("continue;");
		}

		internal override void VisitDoWhile(FuDoWhile statement)
		{
			Write("do");
			WriteChild(statement.Body);
			Write("while (");
			statement.Cond.Accept(this, FuPriority.Argument);
			WriteLine(");");
		}

		internal override void VisitFor(FuFor statement)
		{
			if (statement.Cond != null)
				WriteTemporaries(statement.Cond);
			Write("for (");
			if (statement.Init != null)
				statement.Init.Accept(this, FuPriority.Statement);
			WriteChar(';');
			if (statement.Cond != null) {
				WriteChar(' ');
				statement.Cond.Accept(this, FuPriority.Argument);
			}
			WriteChar(';');
			if (statement.Advance != null) {
				WriteChar(' ');
				statement.Advance.Accept(this, FuPriority.Statement);
			}
			WriteChar(')');
			WriteChild(statement.Body);
		}

		protected virtual bool EmbedIfWhileIsVar(FuExpr expr, bool write) => false;

		void StartIfWhile(FuExpr expr)
		{
			EmbedIfWhileIsVar(expr, true);
			expr.Accept(this, FuPriority.Argument);
			WriteChar(')');
		}

		void WriteIf(FuIf statement)
		{
			Write("if (");
			StartIfWhile(statement.Cond);
			WriteChild(statement.OnTrue);
			if (statement.OnFalse != null) {
				Write("else");
				if (statement.OnFalse is FuIf elseIf) {
					bool wasInChildBlock = this.InChildBlock;
					this.AtLineStart = true;
					this.AtChildStart = true;
					this.InChildBlock = false;
					if (!EmbedIfWhileIsVar(elseIf.Cond, false))
						WriteTemporaries(elseIf.Cond);
					if (this.InChildBlock) {
						WriteIf(elseIf);
						CloseBlock();
					}
					else {
						this.AtLineStart = false;
						this.AtChildStart = false;
						WriteChar(' ');
						WriteIf(elseIf);
					}
					this.InChildBlock = wasInChildBlock;
				}
				else
					WriteChild(statement.OnFalse);
			}
		}

		internal override void VisitIf(FuIf statement)
		{
			if (!EmbedIfWhileIsVar(statement.Cond, false))
				WriteTemporaries(statement.Cond);
			WriteIf(statement);
		}

		internal override void VisitNative(FuNative statement)
		{
			Write(statement.Content);
		}

		internal override void VisitReturn(FuReturn statement)
		{
			if (statement.Value == null)
				WriteLine("return;");
			else {
				WriteTemporaries(statement.Value);
				Write("return ");
				WriteStronglyCoerced(this.CurrentMethod.Type, statement.Value);
				WriteCharLine(';');
				CleanupTemporaries();
			}
		}

		protected void DefineVar(FuExpr value)
		{
			if (value is FuVar def && def.Name != "_") {
				WriteVar(def);
				EndStatement();
			}
		}

		protected virtual void WriteSwitchCaseTypeVar(FuExpr value)
		{
		}

		protected virtual void WriteSwitchValue(FuExpr expr)
		{
			expr.Accept(this, FuPriority.Argument);
		}

		protected virtual void WriteSwitchCaseValue(FuSwitch statement, FuExpr value)
		{
			WriteCoercedLiteral(statement.Value.Type, value);
		}

		protected virtual void WriteSwitchCaseBody(List<FuStatement> statements)
		{
			WriteStatements(statements);
		}

		protected virtual void WriteSwitchCase(FuSwitch statement, FuCase kase)
		{
			foreach (FuExpr value in kase.Values) {
				Write("case ");
				WriteSwitchCaseValue(statement, value);
				WriteCharLine(':');
			}
			this.Indent++;
			WriteSwitchCaseBody(kase.Body);
			this.Indent--;
		}

		protected void StartSwitch(FuSwitch statement)
		{
			Write("switch (");
			WriteSwitchValue(statement.Value);
			WriteLine(") {");
			foreach (FuCase kase in statement.Cases)
				WriteSwitchCase(statement, kase);
		}

		protected virtual void WriteSwitchCaseCond(FuSwitch statement, FuExpr value, FuPriority parent)
		{
			if (value is FuBinaryExpr when1 && when1.Op == FuToken.When) {
				if (parent > FuPriority.SelectCond)
					WriteChar('(');
				WriteSwitchCaseCond(statement, when1.Left, FuPriority.CondAnd);
				Write(" && ");
				when1.Right.Accept(this, FuPriority.CondAnd);
				if (parent > FuPriority.SelectCond)
					WriteChar(')');
			}
			else
				WriteEqual(statement.Value, value, parent, false);
		}

		protected virtual void WriteIfCaseBody(List<FuStatement> body, bool doWhile, FuSwitch statement, FuCase kase)
		{
			int length = FuSwitch.LengthWithoutTrailingBreak(body);
			if (doWhile && FuSwitch.HasEarlyBreak(body)) {
				this.Indent++;
				WriteNewLine();
				Write("do ");
				OpenBlock();
				WriteFirstStatements(body, length);
				CloseBlock();
				WriteLine("while (0);");
				this.Indent--;
			}
			else if (length != 1 || body[0] is FuIf || body[0] is FuSwitch) {
				WriteChar(' ');
				OpenBlock();
				WriteFirstStatements(body, length);
				CloseBlock();
			}
			else
				WriteChild(body[0]);
		}

		protected void WriteSwitchAsIfs(FuSwitch statement, bool doWhile)
		{
			foreach (FuCase kase in statement.Cases) {
				foreach (FuExpr value in kase.Values) {
					if (value is FuBinaryExpr when1 && when1.Op == FuToken.When) {
						DefineVar(when1.Left);
						WriteTemporaries(when1);
					}
					else
						WriteSwitchCaseTypeVar(value);
				}
			}
			string op = "if (";
			foreach (FuCase kase in statement.Cases) {
				FuPriority parent = kase.Values.Count == 1 ? FuPriority.Argument : FuPriority.CondOr;
				foreach (FuExpr value in kase.Values) {
					Write(op);
					WriteSwitchCaseCond(statement, value, parent);
					op = " || ";
				}
				WriteChar(')');
				WriteIfCaseBody(kase.Body, doWhile, statement, kase);
				op = "else if (";
			}
			if (statement.HasDefault()) {
				Write("else");
				WriteIfCaseBody(statement.DefaultBody, doWhile, statement, null);
			}
		}

		internal override void VisitSwitch(FuSwitch statement)
		{
			WriteTemporaries(statement.Value);
			StartSwitch(statement);
			if (statement.DefaultBody.Count > 0) {
				WriteLine("default:");
				this.Indent++;
				WriteSwitchCaseBody(statement.DefaultBody);
				this.Indent--;
			}
			WriteCharLine('}');
		}

		internal override void VisitWhile(FuWhile statement)
		{
			if (!EmbedIfWhileIsVar(statement.Cond, false))
				WriteTemporaries(statement.Cond);
			Write("while (");
			StartIfWhile(statement.Cond);
			WriteChild(statement.Body);
		}

		protected void FlattenBlock(FuStatement statement)
		{
			if (statement is FuBlock block)
				WriteStatements(block.Statements);
			else
				statement.AcceptStatement(this);
		}

		protected virtual bool HasInitCode(FuNamedValue def) => GetAggregateInitializer(def) != null;

		protected virtual bool NeedsConstructor(FuClass klass)
		{
			for (FuSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
				if (symbol is FuField field && HasInitCode(field))
					return true;
			}
			return klass.Constructor != null;
		}

		protected virtual void WriteInitField(FuField field)
		{
			WriteInitCode(field);
		}

		protected void WriteConstructorBody(FuClass klass)
		{
			for (FuSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
				if (symbol is FuField field)
					WriteInitField(field);
			}
			if (klass.Constructor != null) {
				this.CurrentMethod = klass.Constructor;
				FuBlock block = (FuBlock) klass.Constructor.Body;
				WriteStatements(block.Statements);
				this.CurrentMethod = null;
			}
			this.SwitchesWithGoto.Clear();
			this.CurrentTemporaries.Clear();
		}

		protected virtual void WriteParameter(FuVar param)
		{
			WriteTypeAndName(param);
		}

		protected void WriteRemainingParameters(FuMethod method, bool first, bool defaultArguments)
		{
			for (FuVar param = method.Parameters.FirstParameter(); param != null; param = param.NextParameter()) {
				if (!first)
					Write(", ");
				first = false;
				WriteParameter(param);
				if (defaultArguments)
					WriteVarInit(param);
			}
			WriteChar(')');
		}

		protected void WriteParameters(FuMethod method, bool defaultArguments)
		{
			WriteChar('(');
			WriteRemainingParameters(method, true, defaultArguments);
		}

		protected virtual bool IsShortMethod(FuMethod method) => false;

		protected void WriteBody(FuMethod method)
		{
			if (method.CallType == FuCallType.Abstract)
				WriteCharLine(';');
			else {
				this.CurrentMethod = method;
				if (IsShortMethod(method)) {
					Write(" => ");
					FuReturn ret = (FuReturn) method.Body;
					WriteCoerced(method.Type, ret.Value, FuPriority.Argument);
					WriteCharLine(';');
				}
				else {
					WriteNewLine();
					OpenBlock();
					FlattenBlock(method.Body);
					CloseBlock();
				}
				this.CurrentMethod = null;
			}
		}

		protected void WritePublic(FuContainerType container)
		{
			if (container.IsPublic)
				Write("public ");
		}

		protected void WriteEnumValue(FuConst konst)
		{
			WriteDoc(konst.Documentation);
			WriteName(konst);
			if (!(konst.Value is FuImplicitEnumValue)) {
				Write(" = ");
				konst.Value.Accept(this, FuPriority.Argument);
			}
		}

		internal override void VisitEnumValue(FuConst konst, FuConst previous)
		{
			if (previous != null)
				WriteCharLine(',');
			WriteEnumValue(konst);
		}

		protected abstract void WriteEnum(FuEnum enu);

		protected virtual void WriteRegexOptionsEnum(FuProgram program)
		{
			if (program.RegexOptionsEnum)
				WriteEnum(program.System.RegexOptionsEnum);
		}

		protected void StartClass(FuClass klass, string suffix, string extendsClause)
		{
			Write("class ");
			Write(klass.Name);
			Write(suffix);
			if (klass.HasBaseClass()) {
				Write(extendsClause);
				Write(klass.BaseClassName);
			}
		}

		protected void OpenClass(FuClass klass, string suffix, string extendsClause)
		{
			StartClass(klass, suffix, extendsClause);
			WriteNewLine();
			OpenBlock();
		}

		protected abstract void WriteConst(FuConst konst);

		protected abstract void WriteField(FuField field);

		protected abstract void WriteMethod(FuMethod method);

		protected void WriteMembers(FuClass klass, bool constArrays)
		{
			for (FuSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
				switch (symbol) {
				case FuConst konst:
					WriteConst(konst);
					break;
				case FuField field:
					WriteField(field);
					break;
				case FuMethod method:
					WriteMethod(method);
					this.SwitchesWithGoto.Clear();
					this.CurrentTemporaries.Clear();
					break;
				case FuVar _:
					break;
				default:
					throw new NotImplementedException();
				}
			}
			if (constArrays) {
				foreach (FuConst konst in klass.ConstArrays)
					WriteConst(konst);
			}
		}

		protected bool WriteBaseClass(FuClass klass, FuProgram program)
		{
			if (this.WrittenClasses.Contains(klass))
				return false;
			this.WrittenClasses.Add(klass);
			if (klass.Parent is FuClass baseClass)
				WriteClass(baseClass, program);
			return true;
		}

		protected abstract void WriteClass(FuClass klass, FuProgram program);

		protected void WriteTypes(FuProgram program)
		{
			WriteRegexOptionsEnum(program);
			for (FuSymbol type = program.First; type != null; type = type.Next) {
				switch (type) {
				case FuClass klass:
					WriteClass(klass, program);
					break;
				case FuEnum enu:
					WriteEnum(enu);
					break;
				default:
					throw new NotImplementedException();
				}
			}
		}

		public abstract void WriteProgram(FuProgram program);
	}

	public abstract class GenTyped : GenBase
	{

		protected abstract void WriteType(FuType type, bool promote);

		protected override void WriteCoercedLiteral(FuType type, FuExpr expr)
		{
			expr.Accept(this, FuPriority.Argument);
			if (type != null && type.Id == FuId.FloatType && expr is FuLiteralDouble)
				WriteChar('f');
		}

		protected override void WriteTypeAndName(FuNamedValue value)
		{
			WriteType(value.Type, true);
			WriteChar(' ');
			WriteName(value);
		}

		internal override void VisitAggregateInitializer(FuAggregateInitializer expr)
		{
			Write("{ ");
			WriteCoercedLiterals(expr.Type.AsClassType().GetElementType(), expr.Items);
			Write(" }");
		}

		protected void WriteArrayStorageLength(FuExpr expr)
		{
			FuArrayStorageType array = (FuArrayStorageType) expr.Type;
			VisitLiteralLong(array.Length);
		}

		protected override void WriteNewArray(FuType elementType, FuExpr lengthExpr, FuPriority parent)
		{
			Write("new ");
			WriteType(elementType.GetBaseType(), false);
			WriteChar('[');
			lengthExpr.Accept(this, FuPriority.Argument);
			WriteChar(']');
			while (elementType.IsArray()) {
				WriteChar('[');
				if (elementType is FuArrayStorageType arrayStorage)
					arrayStorage.LengthExpr.Accept(this, FuPriority.Argument);
				WriteChar(']');
				elementType = elementType.AsClassType().GetElementType();
			}
		}

		protected int GetOneAscii(FuExpr expr) => expr is FuLiteralString literal ? literal.GetOneAscii() : -1;

		protected void WriteCharMethodCall(FuExpr obj, string method, FuExpr arg)
		{
			obj.Accept(this, FuPriority.Primary);
			WriteChar('.');
			Write(method);
			WriteChar('(');
			if (!(arg is FuLiteralChar))
				Write("(char) ");
			arg.Accept(this, FuPriority.Primary);
			WriteChar(')');
		}

		protected static bool IsNarrower(FuId left, FuId right)
		{
			switch (left) {
			case FuId.SByteRange:
				switch (right) {
				case FuId.ByteRange:
				case FuId.ShortRange:
				case FuId.UShortRange:
				case FuId.IntType:
				case FuId.LongType:
					return true;
				default:
					return false;
				}
			case FuId.ByteRange:
				switch (right) {
				case FuId.SByteRange:
				case FuId.ShortRange:
				case FuId.UShortRange:
				case FuId.IntType:
				case FuId.LongType:
					return true;
				default:
					return false;
				}
			case FuId.ShortRange:
				switch (right) {
				case FuId.UShortRange:
				case FuId.IntType:
				case FuId.LongType:
					return true;
				default:
					return false;
				}
			case FuId.UShortRange:
				switch (right) {
				case FuId.ShortRange:
				case FuId.IntType:
				case FuId.LongType:
					return true;
				default:
					return false;
				}
			case FuId.IntType:
				return right == FuId.LongType;
			default:
				return false;
			}
		}

		protected FuExpr GetStaticCastInner(FuType type, FuExpr expr)
		{
			if (expr is FuBinaryExpr binary && binary.Op == FuToken.And && binary.Right is FuLiteralLong rightMask && type is FuIntegerType) {
				long mask;
				switch (type.Id) {
				case FuId.ByteRange:
				case FuId.SByteRange:
					mask = 255;
					break;
				case FuId.ShortRange:
				case FuId.UShortRange:
					mask = 65535;
					break;
				case FuId.IntType:
					mask = 4294967295;
					break;
				default:
					return expr;
				}
				if ((rightMask.Value & mask) == mask)
					return binary.Left;
			}
			return expr;
		}

		protected void WriteStaticCastType(FuType type)
		{
			WriteChar('(');
			WriteType(type, false);
			Write(") ");
		}

		protected virtual void WriteStaticCast(FuType type, FuExpr expr)
		{
			WriteStaticCastType(type);
			GetStaticCastInner(type, expr).Accept(this, FuPriority.Primary);
		}

		protected override void WriteNotPromoted(FuType type, FuExpr expr)
		{
			if (type is FuIntegerType && IsNarrower(type.Id, GetTypeId(expr.Type, true)))
				WriteStaticCast(type, expr);
			else
				WriteCoercedLiteral(type, expr);
		}

		protected virtual bool IsPromoted(FuExpr expr) => !(expr is FuBinaryExpr binary && (binary.Op == FuToken.LeftBracket || binary.IsAssign()));

		protected override void WriteAssignRight(FuBinaryExpr expr)
		{
			if (expr.Left.IsIndexing()) {
				if (expr.Right is FuLiteralLong) {
					WriteCoercedLiteral(expr.Left.Type, expr.Right);
					return;
				}
				FuId leftTypeId = expr.Left.Type.Id;
				FuId rightTypeId = GetTypeId(expr.Right.Type, IsPromoted(expr.Right));
				if (leftTypeId == FuId.SByteRange && rightTypeId == FuId.SByteRange) {
					expr.Right.Accept(this, FuPriority.Assign);
					return;
				}
				if (IsNarrower(leftTypeId, rightTypeId)) {
					WriteStaticCast(expr.Left.Type, expr.Right);
					return;
				}
			}
			base.WriteAssignRight(expr);
		}

		protected override void WriteCoercedInternal(FuType type, FuExpr expr, FuPriority parent)
		{
			if (type is FuIntegerType && type.Id != FuId.LongType && expr.Type.Id == FuId.LongType)
				WriteStaticCast(type, expr);
			else if (type.Id == FuId.FloatType && expr.Type.Id == FuId.DoubleType) {
				if (expr is FuLiteralDouble literal) {
					VisitLiteralDouble(literal.Value);
					WriteChar('f');
				}
				else
					WriteStaticCast(type, expr);
			}
			else if (type is FuIntegerType && expr.Type.Id == FuId.FloatIntType) {
				if (expr is FuCallExpr call && call.Method.Symbol.Id == FuId.MathTruncate) {
					expr = call.Arguments[0];
					if (expr is FuLiteralDouble literal) {
						VisitLiteralLong((long) literal.Value);
						return;
					}
				}
				WriteStaticCast(type, expr);
			}
			else
				base.WriteCoercedInternal(type, expr, parent);
		}

		protected override void WriteCharAt(FuBinaryExpr expr)
		{
			WriteIndexing(expr.Left, expr.Right);
		}

		protected override void StartTemporaryVar(FuType type)
		{
			WriteType(type, true);
			WriteChar(' ');
		}

		protected override void WriteAssertCast(FuBinaryExpr expr)
		{
			FuVar def = (FuVar) expr.Right;
			WriteTypeAndName(def);
			Write(" = ");
			WriteStaticCast(def.Type, expr.Left);
			WriteCharLine(';');
		}
	}

	public abstract class GenCCppD : GenTyped
	{

		internal override void VisitLiteralLong(long i)
		{
			if (i == -9223372036854775808)
				Write("(-9223372036854775807 - 1)");
			else
				base.VisitLiteralLong(i);
		}

		static bool IsPtrTo(FuExpr ptr, FuExpr other) => ptr.Type is FuClassType klass && klass.Class.Id != FuId.StringClass && klass.IsAssignableFrom(other.Type);

		protected override void WriteEqual(FuExpr left, FuExpr right, FuPriority parent, bool not)
		{
			FuType coercedType;
			if (IsPtrTo(left, right))
				coercedType = left.Type;
			else if (IsPtrTo(right, left))
				coercedType = right.Type;
			else {
				base.WriteEqual(left, right, parent, not);
				return;
			}
			if (parent > FuPriority.Equality)
				WriteChar('(');
			WriteCoerced(coercedType, left, FuPriority.Equality);
			Write(GetEqOp(not));
			WriteCoerced(coercedType, right, FuPriority.Equality);
			if (parent > FuPriority.Equality)
				WriteChar(')');
		}

		internal override void VisitConst(FuConst statement)
		{
			if (statement.Type is FuArrayStorageType)
				WriteConst(statement);
		}

		protected void WriteSwitchAsIfsWithGoto(FuSwitch statement)
		{
			if (statement.Cases.Exists(kase => FuSwitch.HasEarlyBreakAndContinue(kase.Body)) || FuSwitch.HasEarlyBreakAndContinue(statement.DefaultBody)) {
				int gotoId = this.SwitchesWithGoto.Count;
				this.SwitchesWithGoto.Add(statement);
				WriteSwitchAsIfs(statement, false);
				Write("fuafterswitch");
				VisitLiteralLong(gotoId);
				WriteLine(": ;");
			}
			else
				WriteSwitchAsIfs(statement, true);
		}
	}

	public abstract class GenCCpp : GenCCppD
	{

		protected abstract void IncludeStdInt();

		protected abstract void IncludeAssert();

		protected abstract void IncludeMath();

		void WriteCIncludes()
		{
			WriteIncludes("#include <", ">");
		}

		protected override int GetLiteralChars() => 127;

		protected virtual void WriteNumericType(FuId id)
		{
			switch (id) {
			case FuId.SByteRange:
				IncludeStdInt();
				Write("int8_t");
				break;
			case FuId.ByteRange:
				IncludeStdInt();
				Write("uint8_t");
				break;
			case FuId.ShortRange:
				IncludeStdInt();
				Write("int16_t");
				break;
			case FuId.UShortRange:
				IncludeStdInt();
				Write("uint16_t");
				break;
			case FuId.IntType:
				Write("int");
				break;
			case FuId.LongType:
				IncludeStdInt();
				Write("int64_t");
				break;
			case FuId.FloatType:
				Write("float");
				break;
			case FuId.DoubleType:
				Write("double");
				break;
			default:
				throw new NotImplementedException();
			}
		}

		internal override void VisitSymbolReference(FuSymbolReference expr, FuPriority parent)
		{
			switch (expr.Symbol.Id) {
			case FuId.MathNaN:
				IncludeMath();
				Write("NAN");
				break;
			case FuId.MathNegativeInfinity:
				IncludeMath();
				Write("-INFINITY");
				break;
			case FuId.MathPositiveInfinity:
				IncludeMath();
				Write("INFINITY");
				break;
			default:
				base.VisitSymbolReference(expr, parent);
				break;
			}
		}

		protected static FuExpr IsStringEmpty(FuBinaryExpr expr)
		{
			if (expr.Left is FuSymbolReference symbol && symbol.Symbol.Id == FuId.StringLength && expr.Right.IsLiteralZero())
				return symbol.Left;
			return null;
		}

		protected abstract void WriteArrayPtr(FuExpr expr, FuPriority parent);

		protected void WriteArrayPtrAdd(FuExpr array, FuExpr index)
		{
			if (index.IsLiteralZero())
				WriteArrayPtr(array, FuPriority.Argument);
			else {
				WriteArrayPtr(array, FuPriority.Add);
				Write(" + ");
				index.Accept(this, FuPriority.Mul);
			}
		}

		protected static FuCallExpr IsStringSubstring(FuExpr expr)
		{
			if (expr is FuCallExpr call) {
				FuId id = call.Method.Symbol.Id;
				if ((id == FuId.StringSubstring && call.Arguments.Count == 2) || id == FuId.UTF8GetString)
					return call;
			}
			return null;
		}

		protected static bool IsUTF8GetString(FuCallExpr call) => call.Method.Symbol.Id == FuId.UTF8GetString;

		protected static FuExpr GetStringSubstringPtr(FuCallExpr call) => IsUTF8GetString(call) ? call.Arguments[0] : call.Method.Left;

		protected static FuExpr GetStringSubstringOffset(FuCallExpr call) => call.Arguments[IsUTF8GetString(call) ? 1 : 0];

		protected static FuExpr GetStringSubstringLength(FuCallExpr call) => call.Arguments[IsUTF8GetString(call) ? 2 : 1];

		protected void WriteStringPtrAdd(FuCallExpr call)
		{
			WriteArrayPtrAdd(GetStringSubstringPtr(call), GetStringSubstringOffset(call));
		}

		protected static FuExpr IsTrimSubstring(FuBinaryExpr expr)
		{
			FuCallExpr call = IsStringSubstring(expr.Right);
			if (call != null && !IsUTF8GetString(call) && expr.Left is FuSymbolReference leftSymbol && GetStringSubstringPtr(call).IsReferenceTo(leftSymbol.Symbol) && GetStringSubstringOffset(call).IsLiteralZero())
				return GetStringSubstringLength(call);
			return null;
		}

		protected void WriteStringLiteralWithNewLine(string s)
		{
			WriteChar('"');
			Write(s);
			Write("\\n\"");
		}

		protected virtual void WriteUnreachable(FuAssert statement)
		{
			Write("abort();");
			if (statement.Message != null) {
				Write(" // ");
				statement.Message.Accept(this, FuPriority.Argument);
			}
			WriteNewLine();
		}

		protected override void WriteAssert(FuAssert statement)
		{
			if (statement.CompletesNormally()) {
				WriteTemporaries(statement.Cond);
				IncludeAssert();
				Write("assert(");
				if (statement.Message == null)
					statement.Cond.Accept(this, FuPriority.Argument);
				else {
					statement.Cond.Accept(this, FuPriority.CondAnd);
					Write(" && ");
					statement.Message.Accept(this, FuPriority.Argument);
				}
				WriteLine(");");
			}
			else
				WriteUnreachable(statement);
		}

		internal override void VisitSwitch(FuSwitch statement)
		{
			if (statement.Value.Type is FuStringType || statement.HasWhen())
				WriteSwitchAsIfsWithGoto(statement);
			else
				base.VisitSwitch(statement);
		}

		protected void WriteMethods(FuClass klass)
		{
			for (FuSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
				if (symbol is FuMethod method) {
					WriteMethod(method);
					this.CurrentTemporaries.Clear();
				}
			}
		}

		protected abstract void WriteClassInternal(FuClass klass);

		protected override void WriteClass(FuClass klass, FuProgram program)
		{
			if (!WriteBaseClass(klass, program))
				return;
			for (FuSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
				if (symbol is FuField field && field.Type.GetBaseType() is FuStorageType storage && storage.Class.Id == FuId.None)
					WriteClass(storage.Class, program);
			}
			WriteClassInternal(klass);
		}

		static string ChangeExtension(string path, string ext)
		{
			int extIndex = path.Length;
			for (int i = extIndex; --i >= 0 && path[i] != '/' && path[i] != '\\';) {
				if (path[i] == '.') {
					extIndex = i;
					break;
				}
			}
			return path.Substring(0, extIndex) + ext;
		}

		protected void CreateHeaderFile(string headerExt)
		{
			CreateFile(null, ChangeExtension(this.OutputFile, headerExt));
			WriteLine("#pragma once");
			WriteCIncludes();
		}

		static string GetFilenameWithoutExtension(string path)
		{
			int pathLength = path.Length;
			int extIndex = pathLength;
			int i = pathLength;
			while (--i >= 0 && path[i] != '/' && path[i] != '\\') {
				if (path[i] == '.' && extIndex == pathLength)
					extIndex = i;
			}
			i++;
			return path.Substring(i, extIndex - i);
		}

		protected void CreateImplementationFile(FuProgram program, string headerExt)
		{
			CreateOutputFile();
			WriteTopLevelNatives(program);
			WriteCIncludes();
			Write("#include \"");
			Write(GetFilenameWithoutExtension(this.OutputFile));
			Write(headerExt);
			WriteCharLine('"');
		}
	}

	public class GenC : GenCCpp
	{

		bool IntTryParse;

		bool LongTryParse;

		bool DoubleTryParse;

		bool StringAssign;

		bool StringSubstring;

		bool StringAppend;

		bool StringIndexOf;

		bool StringLastIndexOf;

		bool StringEndsWith;

		bool StringReplace;

		bool StringFormat;

		bool MatchFind;

		bool MatchPos;

		bool PtrConstruct;

		bool SharedMake;

		bool SharedAddRef;

		bool SharedRelease;

		bool SharedAssign;

		readonly SortedDictionary<string, string> ListFrees = new SortedDictionary<string, string>();

		bool TreeCompareInteger;

		bool TreeCompareString;

		readonly SortedSet<FuId> Compares = new SortedSet<FuId>();

		readonly SortedSet<FuId> Contains = new SortedSet<FuId>();

		readonly List<FuVar> VarsToDestruct = new List<FuVar>();

		protected FuClass CurrentClass;

		protected override FuContainerType GetCurrentContainer() => this.CurrentClass;

		protected override string GetTargetName() => "C";

		protected override void WriteSelfDoc(FuMethod method)
		{
			if (method.CallType == FuCallType.Static)
				return;
			Write(" * @param self This <code>");
			WriteName(method.Parent);
			WriteLine("</code>.");
		}

		protected override void IncludeStdInt()
		{
			Include("stdint.h");
		}

		protected override void IncludeAssert()
		{
			Include("assert.h");
		}

		protected override void IncludeMath()
		{
			Include("math.h");
		}

		protected virtual void IncludeStdBool()
		{
			Include("stdbool.h");
		}

		internal override void VisitLiteralNull()
		{
			Write("NULL");
		}

		protected virtual void WritePrintfLongPrefix()
		{
			Write("ll");
		}

		protected override void WritePrintfWidth(FuInterpolatedPart part)
		{
			base.WritePrintfWidth(part);
			if (IsStringSubstring(part.Argument) != null) {
				Debug.Assert(part.Precision < 0);
				Write(".*");
			}
			if (part.Argument.Type.Id == FuId.LongType)
				WritePrintfLongPrefix();
		}

		protected virtual void WriteInterpolatedStringArgBase(FuExpr expr)
		{
			if (expr.Type.Id == FuId.LongType) {
				Write("(long long) ");
				expr.Accept(this, FuPriority.Primary);
			}
			else
				WriteTemporaryOrExpr(expr, FuPriority.Argument);
		}

		void WriteStringPtrAddCast(FuCallExpr call)
		{
			if (IsUTF8GetString(call))
				Write("(const char *) ");
			WriteStringPtrAdd(call);
		}

		static bool IsDictionaryClassStgIndexing(FuExpr expr)
		{
			return expr is FuBinaryExpr indexing && indexing.Op == FuToken.LeftBracket && indexing.Left.Type is FuClassType dict && dict.Class.TypeParameterCount == 2 && dict.GetValueType() is FuStorageType;
		}

		void WriteTemporaryOrExpr(FuExpr expr, FuPriority parent)
		{
			int tempId = this.CurrentTemporaries.IndexOf(expr);
			if (tempId >= 0) {
				Write("futemp");
				VisitLiteralLong(tempId);
			}
			else
				expr.Accept(this, parent);
		}

		void WriteUpcast(FuClass resultClass, FuSymbol klass)
		{
			for (; klass != resultClass; klass = klass.Parent)
				Write(".base");
		}

		void WriteClassPtr(FuClass resultClass, FuExpr expr, FuPriority parent)
		{
			switch (expr.Type) {
			case FuStorageType storage when storage.Class.Id == FuId.None && !IsDictionaryClassStgIndexing(expr):
				WriteChar('&');
				WriteTemporaryOrExpr(expr, FuPriority.Primary);
				WriteUpcast(resultClass, storage.Class);
				break;
			case FuClassType ptr when ptr.Class != resultClass:
				WriteChar('&');
				WritePostfix(expr, "->base");
				WriteUpcast(resultClass, ptr.Class.Parent);
				break;
			default:
				expr.Accept(this, parent);
				break;
			}
		}

		protected override void WriteInterpolatedStringArg(FuExpr expr)
		{
			FuCallExpr call = IsStringSubstring(expr);
			if (call != null) {
				GetStringSubstringLength(call).Accept(this, FuPriority.Argument);
				Write(", ");
				WriteStringPtrAddCast(call);
			}
			else if (expr.Type is FuClassType klass && klass.Class.Id != FuId.StringClass) {
				Write(this.Namespace);
				Write(klass.Class.Name);
				Write("_ToString(");
				WriteClassPtr(klass.Class, expr, FuPriority.Argument);
				WriteChar(')');
			}
			else
				WriteInterpolatedStringArgBase(expr);
		}

		internal override void VisitInterpolatedString(FuInterpolatedString expr, FuPriority parent)
		{
			Include("stdarg.h");
			Include("stdio.h");
			this.StringFormat = true;
			Write("FuString_Format(");
			WritePrintf(expr, false);
		}

		protected virtual void WriteCamelCaseNotKeyword(string name)
		{
			switch (name) {
			case "this":
				Write("self");
				break;
			case "Asm":
			case "Assert":
			case "Auto":
			case "Bool":
			case "Break":
			case "Byte":
			case "Case":
			case "Char":
			case "Class":
			case "Const":
			case "Continue":
			case "Default":
			case "Do":
			case "Double":
			case "Else":
			case "Enum":
			case "Extern":
			case "False":
			case "Float":
			case "For":
			case "Foreach":
			case "Goto":
			case "If":
			case "Inline":
			case "Int":
			case "Long":
			case "Register":
			case "Restrict":
			case "Return":
			case "Short":
			case "Signed":
			case "Sizeof":
			case "Static":
			case "Struct":
			case "Switch":
			case "True":
			case "Typedef":
			case "Typeof":
			case "Union":
			case "Unsigned":
			case "Void":
			case "Volatile":
			case "While":
			case "asm":
			case "auto":
			case "char":
			case "extern":
			case "goto":
			case "inline":
			case "register":
			case "restrict":
			case "signed":
			case "sizeof":
			case "struct":
			case "typedef":
			case "typeof":
			case "union":
			case "unsigned":
			case "volatile":
				WriteCamelCase(name);
				WriteChar('_');
				break;
			default:
				WriteCamelCase(name);
				break;
			}
		}

		protected override void WriteName(FuSymbol symbol)
		{
			switch (symbol) {
			case FuContainerType _:
				Write(this.Namespace);
				Write(symbol.Name);
				break;
			case FuMethod _:
				Write(this.Namespace);
				Write(symbol.Parent.Name);
				WriteChar('_');
				Write(symbol.Name);
				break;
			case FuConst _:
				if (symbol.Parent is FuContainerType) {
					Write(this.Namespace);
					Write(symbol.Parent.Name);
					WriteChar('_');
				}
				WriteUppercaseWithUnderscores(symbol.Name);
				break;
			default:
				WriteCamelCaseNotKeyword(symbol.Name);
				break;
			}
		}

		void WriteForeachArrayIndexing(FuForeach forEach, FuSymbol symbol)
		{
			forEach.Collection.Accept(this, FuPriority.Primary);
			WriteChar('[');
			WriteCamelCaseNotKeyword(symbol.Name);
			WriteChar(']');
		}

		void WriteSelfForField(FuSymbol fieldClass)
		{
			Write("self->");
			for (FuSymbol klass = this.CurrentClass; klass != fieldClass; klass = klass.Parent)
				Write("base.");
		}

		protected override void WriteLocalName(FuSymbol symbol, FuPriority parent)
		{
			if (symbol.Parent is FuForeach forEach) {
				FuClassType klass = (FuClassType) forEach.Collection.Type;
				switch (klass.Class.Id) {
				case FuId.StringClass:
				case FuId.ListClass:
					if (parent == FuPriority.Primary)
						WriteChar('(');
					WriteChar('*');
					WriteCamelCaseNotKeyword(symbol.Name);
					if (parent == FuPriority.Primary)
						WriteChar(')');
					return;
				case FuId.ArrayStorageClass:
					if (klass.GetElementType() is FuStorageType) {
						if (parent > FuPriority.Add)
							WriteChar('(');
						forEach.Collection.Accept(this, FuPriority.Add);
						Write(" + ");
						WriteCamelCaseNotKeyword(symbol.Name);
						if (parent > FuPriority.Add)
							WriteChar(')');
					}
					else
						WriteForeachArrayIndexing(forEach, symbol);
					return;
				default:
					break;
				}
			}
			if (symbol is FuField)
				WriteSelfForField(symbol.Parent);
			WriteName(symbol);
		}

		void WriteMatchProperty(FuSymbolReference expr, int which)
		{
			this.MatchPos = true;
			Write("FuMatch_GetPos(");
			expr.Left.Accept(this, FuPriority.Argument);
			Write(", ");
			VisitLiteralLong(which);
			WriteChar(')');
		}

		internal override void VisitSymbolReference(FuSymbolReference expr, FuPriority parent)
		{
			switch (expr.Symbol.Id) {
			case FuId.StringLength:
				WriteStringLength(expr.Left);
				break;
			case FuId.ConsoleError:
				Include("stdio.h");
				Write("stderr");
				break;
			case FuId.ListCount:
			case FuId.StackCount:
				WritePostfix(expr.Left, "->len");
				break;
			case FuId.QueueCount:
				expr.Left.Accept(this, FuPriority.Primary);
				if (expr.Left.Type is FuStorageType)
					WriteChar('.');
				else
					Write("->");
				Write("length");
				break;
			case FuId.HashSetCount:
			case FuId.DictionaryCount:
				WriteCall("g_hash_table_size", expr.Left);
				break;
			case FuId.SortedSetCount:
			case FuId.SortedDictionaryCount:
				WriteCall("g_tree_nnodes", expr.Left);
				break;
			case FuId.MatchStart:
				WriteMatchProperty(expr, 0);
				break;
			case FuId.MatchEnd:
				WriteMatchProperty(expr, 1);
				break;
			case FuId.MatchLength:
				WriteMatchProperty(expr, 2);
				break;
			case FuId.MatchValue:
				Write("g_match_info_fetch(");
				expr.Left.Accept(this, FuPriority.Argument);
				Write(", 0)");
				break;
			default:
				if (expr.Left == null || expr.Symbol is FuConst)
					WriteLocalName(expr.Symbol, parent);
				else if (IsDictionaryClassStgIndexing(expr.Left)) {
					WritePostfix(expr.Left, "->");
					WriteName(expr.Symbol);
				}
				else if (expr.Left is FuSymbolReference symbol && symbol.Symbol.Parent is FuForeach forEach && forEach.Collection.Type is FuArrayStorageType array) {
					WriteForeachArrayIndexing(forEach, symbol.Symbol);
					WriteMemberAccess(array.GetElementType(), expr.Symbol.Parent);
					WriteName(expr.Symbol);
				}
				else
					base.VisitSymbolReference(expr, parent);
				break;
			}
		}

		void WriteGlib(string s)
		{
			Include("glib.h");
			Write(s);
		}

		protected virtual void WriteStringPtrType()
		{
			Write("const char *");
		}

		protected virtual void WriteClassType(FuClassType klass, bool space)
		{
			switch (klass.Class.Id) {
			case FuId.None:
				if (!(klass is FuReadWriteClassType))
					Write("const ");
				WriteName(klass.Class);
				if (!(klass is FuStorageType))
					Write(" *");
				else if (space)
					WriteChar(' ');
				break;
			case FuId.StringClass:
				if (klass.Id == FuId.StringStorageType)
					Write("char *");
				else
					WriteStringPtrType();
				break;
			case FuId.ListClass:
			case FuId.StackClass:
				WriteGlib("GArray *");
				break;
			case FuId.QueueClass:
				WriteGlib("GQueue ");
				if (!(klass is FuStorageType))
					WriteChar('*');
				break;
			case FuId.HashSetClass:
			case FuId.DictionaryClass:
				WriteGlib("GHashTable *");
				break;
			case FuId.SortedSetClass:
			case FuId.SortedDictionaryClass:
				WriteGlib("GTree *");
				break;
			case FuId.TextWriterClass:
				Include("stdio.h");
				Write("FILE *");
				break;
			case FuId.RegexClass:
				if (!(klass is FuReadWriteClassType))
					Write("const ");
				WriteGlib("GRegex *");
				break;
			case FuId.MatchClass:
				if (!(klass is FuReadWriteClassType))
					Write("const ");
				WriteGlib("GMatchInfo *");
				break;
			case FuId.LockClass:
				NotYet(klass, "Lock");
				Include("threads.h");
				Write("mtx_t ");
				break;
			default:
				NotSupported(klass, klass.Class.Name);
				break;
			}
		}

		void WriteArrayPrefix(FuType type)
		{
			if (type is FuClassType array && array.IsArray()) {
				WriteArrayPrefix(array.GetElementType());
				if (!(type is FuArrayStorageType)) {
					if (array.GetElementType() is FuArrayStorageType)
						WriteChar('(');
					if (!(type is FuReadWriteClassType))
						Write("const ");
					WriteChar('*');
				}
			}
		}

		void StartDefinition(FuType type, bool promote, bool space)
		{
			FuType baseType = type.GetBaseType();
			switch (baseType) {
			case FuIntegerType _:
				WriteNumericType(GetTypeId(baseType, promote && type == baseType));
				if (space)
					WriteChar(' ');
				break;
			case FuEnum _:
				if (baseType.Id == FuId.BoolType) {
					IncludeStdBool();
					Write("bool");
				}
				else
					WriteName(baseType);
				if (space)
					WriteChar(' ');
				break;
			case FuClassType klass:
				WriteClassType(klass, space);
				break;
			default:
				Write(baseType.Name);
				if (space)
					WriteChar(' ');
				break;
			}
			WriteArrayPrefix(type);
		}

		void EndDefinition(FuType type)
		{
			while (type.IsArray()) {
				FuType elementType = type.AsClassType().GetElementType();
				if (type is FuArrayStorageType arrayStorage) {
					WriteChar('[');
					VisitLiteralLong(arrayStorage.Length);
					WriteChar(']');
				}
				else if (elementType is FuArrayStorageType)
					WriteChar(')');
				type = elementType;
			}
		}

		void WriteReturnType(FuMethod method)
		{
			if (method.Type.Id == FuId.VoidType && method.Throws) {
				IncludeStdBool();
				Write("bool ");
			}
			else
				StartDefinition(method.Type, true, true);
		}

		protected override void WriteType(FuType type, bool promote)
		{
			StartDefinition(type, promote, type is FuClassType arrayPtr && arrayPtr.Class.Id == FuId.ArrayPtrClass);
			EndDefinition(type);
		}

		protected override void WriteTypeAndName(FuNamedValue value)
		{
			StartDefinition(value.Type, true, true);
			WriteName(value);
			EndDefinition(value.Type);
		}

		void WriteDynamicArrayCast(FuType elementType)
		{
			WriteChar('(');
			StartDefinition(elementType, false, true);
			Write(elementType.IsArray() ? "(*)" : "*");
			EndDefinition(elementType);
			Write(") ");
		}

		void WriteXstructorPtr(bool need, FuClass klass, string name)
		{
			if (need) {
				Write("(FuMethodPtr) ");
				WriteName(klass);
				WriteChar('_');
				Write(name);
			}
			else
				Write("NULL");
		}

		static bool IsHeapAllocated(FuType type) => type.Id == FuId.StringStorageType || type is FuDynamicPtrType;

		static bool NeedToDestructType(FuType type)
		{
			if (IsHeapAllocated(type))
				return true;
			if (type is FuStorageType storage) {
				switch (storage.Class.Id) {
				case FuId.ListClass:
				case FuId.QueueClass:
				case FuId.StackClass:
				case FuId.HashSetClass:
				case FuId.SortedSetClass:
				case FuId.DictionaryClass:
				case FuId.SortedDictionaryClass:
				case FuId.MatchClass:
				case FuId.LockClass:
					return true;
				default:
					return NeedsDestructor(storage.Class);
				}
			}
			return false;
		}

		static bool NeedToDestruct(FuSymbol symbol)
		{
			FuType type = symbol.Type;
			while (type is FuArrayStorageType array)
				type = array.GetElementType();
			return NeedToDestructType(type);
		}

		static bool NeedsDestructor(FuClass klass)
		{
			for (FuSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
				if (symbol is FuField field && NeedToDestruct(field))
					return true;
			}
			return klass.Parent is FuClass baseClass && NeedsDestructor(baseClass);
		}

		void WriteXstructorPtrs(FuClass klass)
		{
			WriteXstructorPtr(NeedsConstructor(klass), klass, "Construct");
			Write(", ");
			WriteXstructorPtr(NeedsDestructor(klass), klass, "Destruct");
		}

		protected override void WriteNewArray(FuType elementType, FuExpr lengthExpr, FuPriority parent)
		{
			this.SharedMake = true;
			if (parent > FuPriority.Mul)
				WriteChar('(');
			WriteDynamicArrayCast(elementType);
			Write("FuShared_Make(");
			lengthExpr.Accept(this, FuPriority.Argument);
			Write(", sizeof(");
			WriteType(elementType, false);
			Write("), ");
			switch (elementType) {
			case FuStringStorageType _:
				this.PtrConstruct = true;
				this.ListFrees["String"] = "free(*(void **) ptr)";
				Write("(FuMethodPtr) FuPtr_Construct, FuList_FreeString");
				break;
			case FuStorageType storage:
				WriteXstructorPtrs(storage.Class);
				break;
			case FuDynamicPtrType _:
				this.PtrConstruct = true;
				this.SharedRelease = true;
				this.ListFrees["Shared"] = "FuShared_Release(*(void **) ptr)";
				Write("(FuMethodPtr) FuPtr_Construct, FuList_FreeShared");
				break;
			default:
				Write("NULL, NULL");
				break;
			}
			WriteChar(')');
			if (parent > FuPriority.Mul)
				WriteChar(')');
		}

		void WriteStringStorageValue(FuExpr expr)
		{
			FuCallExpr call = IsStringSubstring(expr);
			if (call != null) {
				Include("string.h");
				this.StringSubstring = true;
				Write("FuString_Substring(");
				WriteStringPtrAddCast(call);
				Write(", ");
				GetStringSubstringLength(call).Accept(this, FuPriority.Argument);
				WriteChar(')');
			}
			else if (expr.IsNewString(false))
				expr.Accept(this, FuPriority.Argument);
			else {
				Include("string.h");
				WriteCall("strdup", expr);
			}
		}

		protected override void WriteArrayStorageInit(FuArrayStorageType array, FuExpr value)
		{
			switch (value) {
			case null:
				if (IsHeapAllocated(array.GetStorageType()))
					Write(" = { NULL }");
				break;
			case FuLiteral literal when literal.IsDefaultValue():
				Write(" = { ");
				literal.Accept(this, FuPriority.Argument);
				Write(" }");
				break;
			default:
				throw new NotImplementedException();
			}
		}

		string GetDictionaryDestroy(FuType type)
		{
			switch (type) {
			case FuStringStorageType _:
			case FuArrayStorageType _:
				return "free";
			case FuStorageType storage:
				switch (storage.Class.Id) {
				case FuId.ListClass:
				case FuId.StackClass:
					return "(GDestroyNotify) g_array_unref";
				case FuId.HashSetClass:
				case FuId.DictionaryClass:
					return "(GDestroyNotify) g_hash_table_unref";
				case FuId.SortedSetClass:
				case FuId.SortedDictionaryClass:
					return "(GDestroyNotify) g_tree_unref";
				default:
					return NeedsDestructor(storage.Class) ? $"(GDestroyNotify) {storage.Class.Name}_Delete" : "free";
				}
			case FuDynamicPtrType _:
				this.SharedRelease = true;
				return "FuShared_Release";
			default:
				return "NULL";
			}
		}

		void WriteHashEqual(FuType keyType)
		{
			Write(keyType is FuStringType ? "g_str_hash, g_str_equal" : "NULL, NULL");
		}

		void WriteNewHashTable(FuType keyType, string valueDestroy)
		{
			Write("g_hash_table_new");
			string keyDestroy = GetDictionaryDestroy(keyType);
			if (keyDestroy == "NULL" && valueDestroy == "NULL") {
				WriteChar('(');
				WriteHashEqual(keyType);
			}
			else {
				Write("_full(");
				WriteHashEqual(keyType);
				Write(", ");
				Write(keyDestroy);
				Write(", ");
				Write(valueDestroy);
			}
			WriteChar(')');
		}

		void WriteNewTree(FuType keyType, string valueDestroy)
		{
			if (keyType.Id == FuId.StringPtrType && valueDestroy == "NULL")
				Write("g_tree_new((GCompareFunc) strcmp");
			else {
				Write("g_tree_new_full(FuTree_Compare");
				switch (keyType) {
				case FuIntegerType _:
					this.TreeCompareInteger = true;
					Write("Integer");
					break;
				case FuStringType _:
					this.TreeCompareString = true;
					Write("String");
					break;
				default:
					NotSupported(keyType, keyType.ToString());
					break;
				}
				Write(", NULL, ");
				Write(GetDictionaryDestroy(keyType));
				Write(", ");
				Write(valueDestroy);
			}
			WriteChar(')');
		}

		protected override void WriteNew(FuReadWriteClassType klass, FuPriority parent)
		{
			switch (klass.Class.Id) {
			case FuId.ListClass:
			case FuId.StackClass:
				Write("g_array_new(FALSE, FALSE, sizeof(");
				WriteType(klass.GetElementType(), false);
				Write("))");
				break;
			case FuId.QueueClass:
				Write("G_QUEUE_INIT");
				break;
			case FuId.HashSetClass:
				WriteNewHashTable(klass.GetElementType(), "NULL");
				break;
			case FuId.SortedSetClass:
				WriteNewTree(klass.GetElementType(), "NULL");
				break;
			case FuId.DictionaryClass:
				WriteNewHashTable(klass.GetKeyType(), GetDictionaryDestroy(klass.GetValueType()));
				break;
			case FuId.SortedDictionaryClass:
				WriteNewTree(klass.GetKeyType(), GetDictionaryDestroy(klass.GetValueType()));
				break;
			default:
				this.SharedMake = true;
				if (parent > FuPriority.Mul)
					WriteChar('(');
				WriteStaticCastType(klass);
				Write("FuShared_Make(1, sizeof(");
				WriteName(klass.Class);
				Write("), ");
				WriteXstructorPtrs(klass.Class);
				WriteChar(')');
				if (parent > FuPriority.Mul)
					WriteChar(')');
				break;
			}
		}

		protected override void WriteStorageInit(FuNamedValue def)
		{
			if (def.Type.AsClassType().Class.TypeParameterCount > 0)
				base.WriteStorageInit(def);
		}

		protected override void WriteVarInit(FuNamedValue def)
		{
			if (def.Value == null && IsHeapAllocated(def.Type))
				Write(" = NULL");
			else
				base.WriteVarInit(def);
		}

		void WriteAssignTemporary(FuType type, FuExpr expr)
		{
			Write(" = ");
			if (expr != null)
				WriteCoerced(type, expr, FuPriority.Argument);
			else
				WriteNewStorage(type);
		}

		int WriteCTemporary(FuType type, FuExpr expr)
		{
			EnsureChildBlock();
			bool assign = expr != null || (type is FuClassType klass && (klass.Class.Id == FuId.ListClass || klass.Class.Id == FuId.DictionaryClass || klass.Class.Id == FuId.SortedDictionaryClass));
			int id = this.CurrentTemporaries.IndexOf(type);
			if (id < 0) {
				id = this.CurrentTemporaries.Count;
				StartDefinition(type, false, true);
				Write("futemp");
				VisitLiteralLong(id);
				EndDefinition(type);
				if (assign)
					WriteAssignTemporary(type, expr);
				WriteCharLine(';');
				this.CurrentTemporaries.Add(expr);
			}
			else if (assign) {
				Write("futemp");
				VisitLiteralLong(id);
				WriteAssignTemporary(type, expr);
				WriteCharLine(';');
				this.CurrentTemporaries[id] = expr;
			}
			return id;
		}

		void WriteStorageTemporary(FuExpr expr)
		{
			if (expr.IsNewString(false) || (expr is FuCallExpr && expr.Type is FuStorageType))
				WriteCTemporary(expr.Type, expr);
		}

		void WriteCTemporaries(FuExpr expr)
		{
			switch (expr) {
			case FuVar def:
				if (def.Value != null)
					WriteCTemporaries(def.Value);
				break;
			case FuAggregateInitializer init:
				foreach (FuExpr item in init.Items) {
					FuBinaryExpr assign = (FuBinaryExpr) item;
					WriteCTemporaries(assign.Right);
				}
				break;
			case FuLiteral _:
			case FuLambdaExpr _:
				break;
			case FuInterpolatedString interp:
				foreach (FuInterpolatedPart part in interp.Parts) {
					WriteCTemporaries(part.Argument);
					if (IsStringSubstring(part.Argument) == null)
						WriteStorageTemporary(part.Argument);
				}
				break;
			case FuSymbolReference symbol:
				if (symbol.Left != null)
					WriteCTemporaries(symbol.Left);
				break;
			case FuUnaryExpr unary:
				if (unary.Inner != null)
					WriteCTemporaries(unary.Inner);
				break;
			case FuBinaryExpr binary:
				WriteCTemporaries(binary.Left);
				if (IsStringSubstring(binary.Left) == null)
					WriteStorageTemporary(binary.Left);
				WriteCTemporaries(binary.Right);
				if (binary.Op != FuToken.Assign)
					WriteStorageTemporary(binary.Right);
				break;
			case FuSelectExpr select:
				WriteCTemporaries(select.Cond);
				break;
			case FuCallExpr call:
				if (call.Method.Left != null) {
					WriteCTemporaries(call.Method.Left);
					WriteStorageTemporary(call.Method.Left);
				}
				FuMethod method = (FuMethod) call.Method.Symbol;
				FuVar param = method.Parameters.FirstParameter();
				foreach (FuExpr arg in call.Arguments) {
					WriteCTemporaries(arg);
					if (call.Method.Symbol.Id != FuId.ConsoleWrite && call.Method.Symbol.Id != FuId.ConsoleWriteLine && !(param.Type is FuStorageType))
						WriteStorageTemporary(arg);
					param = param.NextParameter();
				}
				break;
			default:
				throw new NotImplementedException();
			}
		}

		static bool HasTemporariesToDestruct(FuExpr expr) => ContainsTemporariesToDestruct(expr) || expr.IsNewString(false);

		static bool ContainsTemporariesToDestruct(FuExpr expr)
		{
			switch (expr) {
			case FuAggregateInitializer init:
				return init.Items.Exists(field => HasTemporariesToDestruct(field));
			case FuLiteral _:
			case FuLambdaExpr _:
				return false;
			case FuInterpolatedString interp:
				return interp.Parts.Exists(part => HasTemporariesToDestruct(part.Argument));
			case FuSymbolReference symbol:
				return symbol.Left != null && HasTemporariesToDestruct(symbol.Left);
			case FuUnaryExpr unary:
				return unary.Inner != null && ContainsTemporariesToDestruct(unary.Inner);
			case FuBinaryExpr binary:
				return HasTemporariesToDestruct(binary.Left) || (binary.Op != FuToken.Is && HasTemporariesToDestruct(binary.Right));
			case FuSelectExpr select:
				return ContainsTemporariesToDestruct(select.Cond);
			case FuCallExpr call:
				return (call.Method.Left != null && HasTemporariesToDestruct(call.Method.Left)) || call.Arguments.Exists(arg => HasTemporariesToDestruct(arg));
			default:
				throw new NotImplementedException();
			}
		}

		protected override void CleanupTemporary(int i, FuExpr temp)
		{
			if (temp.Type.Id == FuId.StringStorageType) {
				Write("free(futemp");
				VisitLiteralLong(i);
				WriteLine(");");
			}
		}

		protected override void WriteVar(FuNamedValue def)
		{
			base.WriteVar(def);
			if (NeedToDestruct(def)) {
				FuVar local = (FuVar) def;
				this.VarsToDestruct.Add(local);
			}
		}

		void WriteGPointerCast(FuType type, FuExpr expr)
		{
			if (type is FuNumericType || type is FuEnum) {
				Write("GINT_TO_POINTER(");
				expr.Accept(this, FuPriority.Argument);
				WriteChar(')');
			}
			else if (type.Id == FuId.StringPtrType && expr.Type.Id == FuId.StringPtrType) {
				Write("(gpointer) ");
				expr.Accept(this, FuPriority.Primary);
			}
			else
				WriteCoerced(type, expr, FuPriority.Argument);
		}

		void WriteGConstPointerCast(FuExpr expr)
		{
			if (expr.Type is FuClassType && !(expr.Type is FuStorageType))
				expr.Accept(this, FuPriority.Argument);
			else {
				Write("(gconstpointer) ");
				expr.Accept(this, FuPriority.Primary);
			}
		}

		void WriteQueueObject(FuExpr obj)
		{
			if (obj.Type is FuStorageType) {
				WriteChar('&');
				obj.Accept(this, FuPriority.Primary);
			}
			else
				obj.Accept(this, FuPriority.Argument);
		}

		void WriteQueueGet(string function, FuExpr obj, FuPriority parent)
		{
			FuType elementType = obj.Type.AsClassType().GetElementType();
			bool parenthesis;
			if (parent == FuPriority.Statement)
				parenthesis = false;
			else if (elementType is FuIntegerType && elementType.Id != FuId.LongType) {
				Write("GPOINTER_TO_INT(");
				parenthesis = true;
			}
			else {
				parenthesis = parent > FuPriority.Mul;
				if (parenthesis)
					WriteChar('(');
				WriteStaticCastType(elementType);
			}
			Write(function);
			WriteChar('(');
			WriteQueueObject(obj);
			WriteChar(')');
			if (parenthesis)
				WriteChar(')');
		}

		void StartDictionaryInsert(FuExpr dict, FuExpr key)
		{
			FuClassType type = (FuClassType) dict.Type;
			Write(type.Class.Id == FuId.SortedDictionaryClass ? "g_tree_insert(" : "g_hash_table_insert(");
			dict.Accept(this, FuPriority.Argument);
			Write(", ");
			WriteGPointerCast(type.GetKeyType(), key);
			Write(", ");
		}

		protected override void WriteAssign(FuBinaryExpr expr, FuPriority parent)
		{
			if (expr.Left is FuBinaryExpr indexing && indexing.Op == FuToken.LeftBracket && indexing.Left.Type is FuClassType dict && dict.Class.TypeParameterCount == 2) {
				StartDictionaryInsert(indexing.Left, indexing.Right);
				WriteGPointerCast(dict.GetValueType(), expr.Right);
				WriteChar(')');
			}
			else if (expr.Left.Type.Id == FuId.StringStorageType) {
				FuExpr length = IsTrimSubstring(expr);
				if (length != null && parent == FuPriority.Statement) {
					WriteIndexing(expr.Left, length);
					Write(" = '\\0'");
				}
				else {
					this.StringAssign = true;
					Write("FuString_Assign(&");
					expr.Left.Accept(this, FuPriority.Primary);
					Write(", ");
					WriteStringStorageValue(expr.Right);
					WriteChar(')');
				}
			}
			else if (expr.Left.Type is FuDynamicPtrType dynamic) {
				if (dynamic.Class.Id == FuId.RegexClass) {
					base.WriteAssign(expr, parent);
				}
				else {
					this.SharedAssign = true;
					Write("FuShared_Assign((void **) &");
					expr.Left.Accept(this, FuPriority.Primary);
					Write(", ");
					if (expr.Right is FuSymbolReference) {
						this.SharedAddRef = true;
						Write("FuShared_AddRef(");
						expr.Right.Accept(this, FuPriority.Argument);
						WriteChar(')');
					}
					else
						expr.Right.Accept(this, FuPriority.Argument);
					WriteChar(')');
				}
			}
			else
				base.WriteAssign(expr, parent);
		}

		static FuMethod GetThrowingMethod(FuExpr expr)
		{
			switch (expr) {
			case FuBinaryExpr binary when binary.Op == FuToken.Assign:
				return GetThrowingMethod(binary.Right);
			case FuCallExpr call:
				FuMethod method = (FuMethod) call.Method.Symbol;
				return method.Throws ? method : null;
			default:
				return null;
			}
		}

		static bool HasListDestroy(FuType type)
		{
			return type is FuStorageType list && (list.Class.Id == FuId.ListClass || list.Class.Id == FuId.StackClass) && NeedToDestructType(list.GetElementType());
		}

		protected override bool HasInitCode(FuNamedValue def)
		{
			if (def.IsAssignableStorage())
				return false;
			return (def is FuField && (def.Value != null || IsHeapAllocated(def.Type.GetStorageType()) || (def.Type is FuClassType klass && (klass.Class.Id == FuId.ListClass || klass.Class.Id == FuId.DictionaryClass || klass.Class.Id == FuId.SortedDictionaryClass)))) || (def.Value != null && GetThrowingMethod(def.Value) != null) || (def.Type.GetStorageType() is FuStorageType storage && (storage.Class.Id == FuId.LockClass || NeedsConstructor(storage.Class))) || HasListDestroy(def.Type) || base.HasInitCode(def);
		}

		FuPriority StartForwardThrow(FuMethod throwingMethod)
		{
			Write("if (");
			switch (throwingMethod.Type.Id) {
			case FuId.FloatType:
			case FuId.DoubleType:
				IncludeMath();
				Write("isnan(");
				return FuPriority.Argument;
			case FuId.VoidType:
				WriteChar('!');
				return FuPriority.Primary;
			default:
				return FuPriority.Equality;
			}
		}

		void WriteDestructElement(FuSymbol symbol, int nesting)
		{
			WriteLocalName(symbol, FuPriority.Primary);
			for (int i = 0; i < nesting; i++) {
				Write("[_i");
				VisitLiteralLong(i);
				WriteChar(']');
			}
		}

		void WriteDestruct(FuSymbol symbol)
		{
			if (!NeedToDestruct(symbol))
				return;
			EnsureChildBlock();
			FuType type = symbol.Type;
			int nesting = 0;
			while (type is FuArrayStorageType array) {
				Write("for (int _i");
				VisitLiteralLong(nesting);
				Write(" = ");
				VisitLiteralLong(array.Length - 1);
				Write("; _i");
				VisitLiteralLong(nesting);
				Write(" >= 0; _i");
				VisitLiteralLong(nesting);
				WriteLine("--)");
				this.Indent++;
				nesting++;
				type = array.GetElementType();
			}
			switch (type) {
			case FuDynamicPtrType dynamic:
				if (dynamic.Class.Id == FuId.RegexClass)
					Write("g_regex_unref(");
				else {
					this.SharedRelease = true;
					Write("FuShared_Release(");
				}
				break;
			case FuStorageType storage:
				switch (storage.Class.Id) {
				case FuId.ListClass:
				case FuId.StackClass:
					Write("g_array_unref(");
					break;
				case FuId.QueueClass:
					string destroy = GetDictionaryDestroy(storage.GetElementType());
					if (destroy != "NULL") {
						Write("g_queue_clear_full(&");
						WriteDestructElement(symbol, nesting);
						Write(", ");
						Write(destroy);
						WriteLine(");");
						this.Indent -= nesting;
						return;
					}
					Write("g_queue_clear(&");
					break;
				case FuId.HashSetClass:
				case FuId.DictionaryClass:
					Write("g_hash_table_unref(");
					break;
				case FuId.SortedSetClass:
				case FuId.SortedDictionaryClass:
					Write("g_tree_unref(");
					break;
				case FuId.MatchClass:
					Write("g_match_info_free(");
					break;
				case FuId.LockClass:
					Write("mtx_destroy(&");
					break;
				default:
					WriteName(storage.Class);
					Write("_Destruct(&");
					break;
				}
				break;
			default:
				Write("free(");
				break;
			}
			WriteDestructElement(symbol, nesting);
			WriteLine(");");
			this.Indent -= nesting;
		}

		void WriteDestructAll(FuVar exceptVar = null)
		{
			for (int i = this.VarsToDestruct.Count; --i >= 0;) {
				FuVar def = this.VarsToDestruct[i];
				if (def != exceptVar)
					WriteDestruct(def);
			}
		}

		void WriteThrowReturnValue()
		{
			if (this.CurrentMethod.Type is FuNumericType) {
				if (this.CurrentMethod.Type is FuIntegerType)
					Write("-1");
				else {
					IncludeMath();
					Write("NAN");
				}
			}
			else if (this.CurrentMethod.Type.Id == FuId.VoidType)
				Write("false");
			else
				Write("NULL");
		}

		void WriteThrow()
		{
			WriteDestructAll();
			Write("return ");
			WriteThrowReturnValue();
			WriteCharLine(';');
		}

		void EndForwardThrow(FuMethod throwingMethod)
		{
			switch (throwingMethod.Type.Id) {
			case FuId.FloatType:
			case FuId.DoubleType:
				WriteChar(')');
				break;
			case FuId.VoidType:
				break;
			default:
				Write(throwingMethod.Type is FuIntegerType ? " == -1" : " == NULL");
				break;
			}
			WriteChar(')');
			if (this.VarsToDestruct.Count > 0) {
				WriteChar(' ');
				OpenBlock();
				WriteThrow();
				CloseBlock();
			}
			else {
				WriteNewLine();
				this.Indent++;
				WriteThrow();
				this.Indent--;
			}
		}

		protected override void WriteInitCode(FuNamedValue def)
		{
			if (!HasInitCode(def))
				return;
			FuType type = def.Type;
			int nesting = 0;
			while (type is FuArrayStorageType array) {
				OpenLoop("int", nesting++, array.Length);
				type = array.GetElementType();
			}
			if (type is FuStorageType lok && lok.Class.Id == FuId.LockClass) {
				Write("mtx_init(&");
				WriteArrayElement(def, nesting);
				WriteLine(", mtx_plain | mtx_recursive);");
			}
			else if (type is FuStorageType storage && NeedsConstructor(storage.Class)) {
				WriteName(storage.Class);
				Write("_Construct(&");
				WriteArrayElement(def, nesting);
				WriteLine(");");
			}
			else {
				if (def is FuField) {
					WriteArrayElement(def, nesting);
					if (nesting > 0) {
						Write(" = ");
						if (IsHeapAllocated(type))
							Write("NULL");
						else
							def.Value.Accept(this, FuPriority.Argument);
					}
					else
						WriteVarInit(def);
					WriteCharLine(';');
				}
				if (def.Value != null) {
					FuMethod throwingMethod = GetThrowingMethod(def.Value);
					if (throwingMethod != null) {
						StartForwardThrow(throwingMethod);
						WriteArrayElement(def, nesting);
						EndForwardThrow(throwingMethod);
					}
				}
			}
			if (HasListDestroy(type)) {
				Write("g_array_set_clear_func(");
				WriteArrayElement(def, nesting);
				Write(", ");
				switch (type.AsClassType().GetElementType()) {
				case FuStringStorageType _:
					this.ListFrees["String"] = "free(*(void **) ptr)";
					Write("FuList_FreeString");
					break;
				case FuDynamicPtrType _:
					this.SharedRelease = true;
					this.ListFrees["Shared"] = "FuShared_Release(*(void **) ptr)";
					Write("FuList_FreeShared");
					break;
				case FuStorageType storage:
					switch (storage.Class.Id) {
					case FuId.ListClass:
					case FuId.StackClass:
						this.ListFrees["List"] = "g_array_free(*(GArray **) ptr, TRUE)";
						Write("FuList_FreeList");
						break;
					case FuId.HashSetClass:
					case FuId.DictionaryClass:
						this.ListFrees["HashTable"] = "g_hash_table_unref(*(GHashTable **) ptr)";
						Write("FuList_FreeHashTable");
						break;
					case FuId.SortedSetClass:
					case FuId.SortedDictionaryClass:
						this.ListFrees["Tree"] = "g_tree_unref(*(GTree **) ptr)";
						Write("FuList_FreeTree");
						break;
					default:
						Write("(GDestroyNotify) ");
						WriteName(storage.Class);
						Write("_Destruct");
						break;
					}
					break;
				default:
					throw new NotImplementedException();
				}
				WriteLine(");");
			}
			while (--nesting >= 0)
				CloseBlock();
			base.WriteInitCode(def);
		}

		void WriteMemberAccess(FuType leftType, FuSymbol symbolClass)
		{
			if (leftType is FuStorageType)
				WriteChar('.');
			else
				Write("->");
			for (FuSymbol klass = leftType.AsClassType().Class; klass != symbolClass; klass = klass.Parent)
				Write("base.");
		}

		protected override void WriteMemberOp(FuExpr left, FuSymbolReference symbol)
		{
			WriteMemberAccess(left.Type, symbol.Symbol.Parent);
		}

		protected override void WriteArrayPtr(FuExpr expr, FuPriority parent)
		{
			if (expr.Type is FuClassType list && list.Class.Id == FuId.ListClass) {
				WriteChar('(');
				WriteType(list.GetElementType(), false);
				Write(" *) ");
				WritePostfix(expr, "->data");
			}
			else
				expr.Accept(this, parent);
		}

		protected override void WriteCoercedInternal(FuType type, FuExpr expr, FuPriority parent)
		{
			switch (type) {
			case FuDynamicPtrType dynamic when expr is FuSymbolReference && parent != FuPriority.Equality:
				this.SharedAddRef = true;
				if (dynamic.Class.Id == FuId.ArrayPtrClass)
					WriteDynamicArrayCast(dynamic.GetElementType());
				else {
					WriteChar('(');
					WriteName(dynamic.Class);
					Write(" *) ");
				}
				WriteCall("FuShared_AddRef", expr);
				break;
			case FuClassType klass when klass.Class.Id != FuId.StringClass && klass.Class.Id != FuId.ArrayPtrClass && !(klass is FuStorageType):
				if (klass.Class.Id == FuId.QueueClass && expr.Type is FuStorageType) {
					WriteChar('&');
					expr.Accept(this, FuPriority.Primary);
				}
				else
					WriteClassPtr(klass.Class, expr, parent);
				break;
			default:
				if (type.Id == FuId.StringStorageType)
					WriteStringStorageValue(expr);
				else if (expr.Type.Id == FuId.StringStorageType)
					WriteTemporaryOrExpr(expr, parent);
				else
					base.WriteCoercedInternal(type, expr, parent);
				break;
			}
		}

		protected virtual void WriteSubstringEqual(FuCallExpr call, string literal, FuPriority parent, bool not)
		{
			if (parent > FuPriority.Equality)
				WriteChar('(');
			Include("string.h");
			Write("memcmp(");
			WriteStringPtrAdd(call);
			Write(", ");
			VisitLiteralString(literal);
			Write(", ");
			VisitLiteralLong(literal.Length);
			WriteChar(')');
			Write(GetEqOp(not));
			WriteChar('0');
			if (parent > FuPriority.Equality)
				WriteChar(')');
		}

		protected virtual void WriteEqualStringInternal(FuExpr left, FuExpr right, FuPriority parent, bool not)
		{
			if (parent > FuPriority.Equality)
				WriteChar('(');
			Include("string.h");
			Write("strcmp(");
			WriteTemporaryOrExpr(left, FuPriority.Argument);
			Write(", ");
			WriteTemporaryOrExpr(right, FuPriority.Argument);
			WriteChar(')');
			Write(GetEqOp(not));
			WriteChar('0');
			if (parent > FuPriority.Equality)
				WriteChar(')');
		}

		protected override void WriteEqual(FuExpr left, FuExpr right, FuPriority parent, bool not)
		{
			if (left.Type is FuStringType && right.Type is FuStringType) {
				FuCallExpr call = IsStringSubstring(left);
				if (call != null && right is FuLiteralString literal) {
					FuExpr lengthExpr = GetStringSubstringLength(call);
					int rightLength = literal.GetAsciiLength();
					if (rightLength >= 0) {
						string rightValue = literal.Value;
						if (lengthExpr is FuLiteralLong leftLength) {
							if (leftLength.Value != rightLength)
								NotYet(left, "String comparison with unmatched length");
							WriteSubstringEqual(call, rightValue, parent, not);
						}
						else if (not) {
							if (parent > FuPriority.CondOr)
								WriteChar('(');
							lengthExpr.Accept(this, FuPriority.Equality);
							Write(" != ");
							VisitLiteralLong(rightLength);
							if (rightLength > 0) {
								Write(" || ");
								WriteSubstringEqual(call, rightValue, FuPriority.CondOr, true);
							}
							if (parent > FuPriority.CondOr)
								WriteChar(')');
						}
						else {
							if (parent > FuPriority.CondAnd || parent == FuPriority.CondOr)
								WriteChar('(');
							lengthExpr.Accept(this, FuPriority.Equality);
							Write(" == ");
							VisitLiteralLong(rightLength);
							if (rightLength > 0) {
								Write(" && ");
								WriteSubstringEqual(call, rightValue, FuPriority.CondAnd, false);
							}
							if (parent > FuPriority.CondAnd || parent == FuPriority.CondOr)
								WriteChar(')');
						}
						return;
					}
				}
				WriteEqualStringInternal(left, right, parent, not);
			}
			else
				base.WriteEqual(left, right, parent, not);
		}

		protected override void WriteStringLength(FuExpr expr)
		{
			Include("string.h");
			WriteCall("(int) strlen", expr);
		}

		void WriteStringMethod(string name, FuExpr obj, List<FuExpr> args)
		{
			Include("string.h");
			Write("FuString_");
			WriteCall(name, obj, args[0]);
		}

		void WriteSizeofCompare(FuType elementType)
		{
			Write(", sizeof(");
			FuId typeId = elementType.Id;
			WriteNumericType(typeId);
			Write("), FuCompare_");
			WriteNumericType(typeId);
			WriteChar(')');
			this.Compares.Add(typeId);
		}

		protected void WriteArrayFill(FuExpr obj, List<FuExpr> args)
		{
			Write("for (int _i = 0; _i < ");
			if (args.Count == 1)
				WriteArrayStorageLength(obj);
			else
				args[2].Accept(this, FuPriority.Rel);
			WriteLine("; _i++)");
			WriteChar('\t');
			obj.Accept(this, FuPriority.Primary);
			WriteChar('[');
			if (args.Count > 1)
				StartAdd(args[1]);
			Write("_i] = ");
			args[0].Accept(this, FuPriority.Argument);
		}

		void WriteListAddInsert(FuExpr obj, bool insert, string function, List<FuExpr> args)
		{
			FuType elementType = obj.Type.AsClassType().GetElementType();
			int id = WriteCTemporary(elementType, elementType.IsFinal() ? null : args[^1]);
			if (elementType is FuStorageType storage && NeedsConstructor(storage.Class)) {
				WriteName(storage.Class);
				Write("_Construct(&futemp");
				VisitLiteralLong(id);
				WriteLine(");");
			}
			Write(function);
			WriteChar('(');
			obj.Accept(this, FuPriority.Argument);
			if (insert) {
				Write(", ");
				args[0].Accept(this, FuPriority.Argument);
			}
			Write(", futemp");
			VisitLiteralLong(id);
			WriteChar(')');
			this.CurrentTemporaries[id] = elementType;
		}

		void WriteDictionaryLookup(FuExpr obj, string function, FuExpr key)
		{
			Write(function);
			WriteChar('(');
			obj.Accept(this, FuPriority.Argument);
			Write(", ");
			WriteGConstPointerCast(key);
			WriteChar(')');
		}

		void WriteArgsAndRightParenthesis(FuMethod method, List<FuExpr> args)
		{
			int i = 0;
			for (FuVar param = method.Parameters.FirstParameter(); param != null; param = param.NextParameter()) {
				if (i > 0 || method.CallType != FuCallType.Static)
					Write(", ");
				if (i >= args.Count)
					param.Value.Accept(this, FuPriority.Argument);
				else
					WriteCoerced(param.Type, args[i], FuPriority.Argument);
				i++;
			}
			WriteChar(')');
		}

		void WriteCRegexOptions(List<FuExpr> args)
		{
			if (!WriteRegexOptions(args, "", " | ", "", "G_REGEX_CASELESS", "G_REGEX_MULTILINE", "G_REGEX_DOTALL"))
				WriteChar('0');
		}

		protected void WritePrintfNotInterpolated(List<FuExpr> args, bool newLine)
		{
			Write("\"%");
			switch (args[0].Type) {
			case FuIntegerType intType:
				if (intType.Id == FuId.LongType)
					WritePrintfLongPrefix();
				WriteChar('d');
				break;
			case FuFloatingType _:
				WriteChar('g');
				break;
			default:
				WriteChar('s');
				break;
			}
			if (newLine)
				Write("\\n");
			Write("\", ");
			WriteInterpolatedStringArgBase(args[0]);
			WriteChar(')');
		}

		void WriteTextWriterWrite(FuExpr obj, List<FuExpr> args, bool newLine)
		{
			if (args.Count == 0) {
				Write("putc('\\n', ");
				obj.Accept(this, FuPriority.Argument);
				WriteChar(')');
			}
			else if (args[0] is FuInterpolatedString interpolated) {
				Write("fprintf(");
				obj.Accept(this, FuPriority.Argument);
				Write(", ");
				WritePrintf(interpolated, newLine);
			}
			else if (args[0].Type is FuNumericType) {
				Write("fprintf(");
				obj.Accept(this, FuPriority.Argument);
				Write(", ");
				WritePrintfNotInterpolated(args, newLine);
			}
			else if (!newLine) {
				Write("fputs(");
				args[0].Accept(this, FuPriority.Argument);
				Write(", ");
				obj.Accept(this, FuPriority.Argument);
				WriteChar(')');
			}
			else if (args[0] is FuLiteralString literal) {
				Write("fputs(");
				WriteStringLiteralWithNewLine(literal.Value);
				Write(", ");
				obj.Accept(this, FuPriority.Argument);
				WriteChar(')');
			}
			else {
				Write("fprintf(");
				obj.Accept(this, FuPriority.Argument);
				Write(", \"%s\\n\", ");
				args[0].Accept(this, FuPriority.Argument);
				WriteChar(')');
			}
		}

		void WriteConsoleWrite(List<FuExpr> args, bool newLine)
		{
			Include("stdio.h");
			if (args.Count == 0)
				Write("putchar('\\n')");
			else if (args[0] is FuInterpolatedString interpolated) {
				Write("printf(");
				WritePrintf(interpolated, newLine);
			}
			else if (args[0].Type is FuNumericType) {
				Write("printf(");
				WritePrintfNotInterpolated(args, newLine);
			}
			else if (!newLine) {
				Write("fputs(");
				args[0].Accept(this, FuPriority.Argument);
				Write(", stdout)");
			}
			else
				WriteCall("puts", args[0]);
		}

		static FuClass GetVtblStructClass(FuClass klass)
		{
			while (!klass.AddsVirtualMethods()) {
				FuClass baseClass = (FuClass) klass.Parent;
				klass = baseClass;
			}
			return klass;
		}

		static FuClass GetVtblPtrClass(FuClass klass)
		{
			for (FuClass result = null;;) {
				if (klass.AddsVirtualMethods())
					result = klass;
				if (!(klass.Parent is FuClass baseClass))
					return result;
				klass = baseClass;
			}
		}

		protected void WriteCCall(FuExpr obj, FuMethod method, List<FuExpr> args)
		{
			FuClass klass = this.CurrentClass;
			FuClass declaringClass = (FuClass) method.Parent;
			if (IsReferenceTo(obj, FuId.BasePtr)) {
				WriteName(method);
				Write("(&self->base");
				WriteUpcast(declaringClass, klass.Parent);
			}
			else {
				FuClass definingClass = declaringClass;
				switch (method.CallType) {
				case FuCallType.Abstract:
				case FuCallType.Virtual:
				case FuCallType.Override:
					if (method.CallType == FuCallType.Override) {
						FuClass declaringClass1 = (FuClass) method.GetDeclaringMethod().Parent;
						declaringClass = declaringClass1;
					}
					if (obj != null)
						klass = obj.Type.AsClassType().Class;
					FuClass ptrClass = GetVtblPtrClass(klass);
					FuClass structClass = GetVtblStructClass(definingClass);
					if (structClass != ptrClass) {
						Write("((const ");
						WriteName(structClass);
						Write("Vtbl *) ");
					}
					if (obj != null) {
						obj.Accept(this, FuPriority.Primary);
						WriteMemberAccess(obj.Type, ptrClass);
					}
					else
						WriteSelfForField(ptrClass);
					Write("vtbl");
					if (structClass != ptrClass)
						WriteChar(')');
					Write("->");
					WriteCamelCase(method.Name);
					break;
				default:
					WriteName(method);
					break;
				}
				WriteChar('(');
				if (method.CallType != FuCallType.Static) {
					if (obj != null)
						WriteClassPtr(declaringClass, obj, FuPriority.Argument);
					else if (klass == declaringClass)
						Write("self");
					else {
						Write("&self->base");
						WriteUpcast(declaringClass, klass.Parent);
					}
				}
			}
			WriteArgsAndRightParenthesis(method, args);
		}

		void WriteTryParse(FuExpr obj, List<FuExpr> args)
		{
			IncludeStdBool();
			Write("_TryParse(&");
			obj.Accept(this, FuPriority.Primary);
			Write(", ");
			args[0].Accept(this, FuPriority.Argument);
			if (obj.Type is FuIntegerType)
				WriteTryParseRadix(args);
			WriteChar(')');
		}

		protected void WriteStringSubstring(FuExpr obj, List<FuExpr> args, FuPriority parent)
		{
			if (args.Count == 1) {
				if (parent > FuPriority.Add)
					WriteChar('(');
				WriteAdd(obj, args[0]);
				if (parent > FuPriority.Add)
					WriteChar(')');
			}
			else
				NotSupported(obj, "Substring");
		}

		void StartArrayContains(FuExpr obj)
		{
			Write("FuArray_Contains_");
			FuId typeId = obj.Type.AsClassType().GetElementType().Id;
			switch (typeId) {
			case FuId.None:
				Write("object(");
				break;
			case FuId.StringStorageType:
			case FuId.StringPtrType:
				typeId = FuId.StringPtrType;
				Include("string.h");
				Write("string((const char * const *) ");
				break;
			default:
				WriteNumericType(typeId);
				Write("((const ");
				WriteNumericType(typeId);
				Write(" *) ");
				break;
			}
			this.Contains.Add(typeId);
		}

		void StartArrayIndexing(FuExpr obj, FuType elementType)
		{
			Write("g_array_index(");
			obj.Accept(this, FuPriority.Argument);
			Write(", ");
			WriteType(elementType, false);
			Write(", ");
		}

		protected override void WriteCallExpr(FuExpr obj, FuMethod method, List<FuExpr> args, FuPriority parent)
		{
			switch (method.Id) {
			case FuId.None:
			case FuId.ClassToString:
				WriteCCall(obj, method, args);
				break;
			case FuId.EnumFromInt:
				WriteStaticCast(method.Type, args[0]);
				break;
			case FuId.EnumHasFlag:
				WriteEnumHasFlag(obj, args, parent);
				break;
			case FuId.IntTryParse:
				this.IntTryParse = true;
				Write("FuInt");
				WriteTryParse(obj, args);
				break;
			case FuId.LongTryParse:
				this.LongTryParse = true;
				Write("FuLong");
				WriteTryParse(obj, args);
				break;
			case FuId.DoubleTryParse:
				this.DoubleTryParse = true;
				Write("FuDouble");
				WriteTryParse(obj, args);
				break;
			case FuId.StringContains:
				Include("string.h");
				if (parent > FuPriority.Equality)
					WriteChar('(');
				int c = GetOneAscii(args[0]);
				if (c >= 0) {
					Write("strchr(");
					obj.Accept(this, FuPriority.Argument);
					Write(", ");
					VisitLiteralChar(c);
					WriteChar(')');
				}
				else
					WriteCall("strstr", obj, args[0]);
				Write(" != NULL");
				if (parent > FuPriority.Equality)
					WriteChar(')');
				break;
			case FuId.StringEndsWith:
				this.StringEndsWith = true;
				WriteStringMethod("EndsWith", obj, args);
				break;
			case FuId.StringIndexOf:
				this.StringIndexOf = true;
				WriteStringMethod("IndexOf", obj, args);
				break;
			case FuId.StringLastIndexOf:
				this.StringLastIndexOf = true;
				WriteStringMethod("LastIndexOf", obj, args);
				break;
			case FuId.StringReplace:
				Include("string.h");
				this.StringAppend = true;
				this.StringReplace = true;
				WriteCall("FuString_Replace", obj, args[0], args[1]);
				break;
			case FuId.StringStartsWith:
				if (parent > FuPriority.Equality)
					WriteChar('(');
				int c2 = GetOneAscii(args[0]);
				if (c2 >= 0) {
					WritePostfix(obj, "[0] == ");
					VisitLiteralChar(c2);
				}
				else {
					Include("string.h");
					Write("strncmp(");
					obj.Accept(this, FuPriority.Argument);
					Write(", ");
					args[0].Accept(this, FuPriority.Argument);
					Write(", strlen(");
					args[0].Accept(this, FuPriority.Argument);
					Write(")) == 0");
				}
				if (parent > FuPriority.Equality)
					WriteChar(')');
				break;
			case FuId.StringSubstring:
				WriteStringSubstring(obj, args, parent);
				break;
			case FuId.ArrayBinarySearchAll:
			case FuId.ArrayBinarySearchPart:
				if (parent > FuPriority.Add)
					WriteChar('(');
				Write("(const ");
				FuType elementType2 = obj.Type.AsClassType().GetElementType();
				WriteType(elementType2, false);
				Write(" *) bsearch(&");
				args[0].Accept(this, FuPriority.Primary);
				Write(", ");
				if (args.Count == 1)
					WriteArrayPtr(obj, FuPriority.Argument);
				else
					WriteArrayPtrAdd(obj, args[1]);
				Write(", ");
				if (args.Count == 1)
					WriteArrayStorageLength(obj);
				else
					args[2].Accept(this, FuPriority.Argument);
				WriteSizeofCompare(elementType2);
				Write(" - ");
				WriteArrayPtr(obj, FuPriority.Mul);
				if (parent > FuPriority.Add)
					WriteChar(')');
				break;
			case FuId.ArrayContains:
				StartArrayContains(obj);
				obj.Accept(this, FuPriority.Argument);
				Write(", ");
				WriteArrayStorageLength(obj);
				Write(", ");
				args[0].Accept(this, FuPriority.Argument);
				WriteChar(')');
				break;
			case FuId.ArrayCopyTo:
			case FuId.ListCopyTo:
				Include("string.h");
				FuType elementType = obj.Type.AsClassType().GetElementType();
				if (IsHeapAllocated(elementType))
					NotYet(obj, "CopyTo for this type");
				Write("memcpy(");
				WriteArrayPtrAdd(args[1], args[2]);
				Write(", ");
				WriteArrayPtrAdd(obj, args[0]);
				Write(", ");
				if (elementType.Id == FuId.SByteRange || elementType.Id == FuId.ByteRange)
					args[3].Accept(this, FuPriority.Argument);
				else {
					args[3].Accept(this, FuPriority.Mul);
					Write(" * sizeof(");
					WriteType(elementType, false);
					WriteChar(')');
				}
				WriteChar(')');
				break;
			case FuId.ArrayFillAll:
			case FuId.ArrayFillPart:
				if (args[0] is FuLiteral literal && literal.IsDefaultValue()) {
					Include("string.h");
					Write("memset(");
					if (args.Count == 1) {
						obj.Accept(this, FuPriority.Argument);
						Write(", 0, sizeof(");
						obj.Accept(this, FuPriority.Argument);
						WriteChar(')');
					}
					else {
						WriteArrayPtrAdd(obj, args[1]);
						Write(", 0, ");
						args[2].Accept(this, FuPriority.Mul);
						Write(" * sizeof(");
						WriteType(obj.Type.AsClassType().GetElementType(), false);
						WriteChar(')');
					}
					WriteChar(')');
				}
				else
					WriteArrayFill(obj, args);
				break;
			case FuId.ArraySortAll:
				Write("qsort(");
				WriteArrayPtr(obj, FuPriority.Argument);
				Write(", ");
				WriteArrayStorageLength(obj);
				WriteSizeofCompare(obj.Type.AsClassType().GetElementType());
				break;
			case FuId.ArraySortPart:
			case FuId.ListSortPart:
				Write("qsort(");
				WriteArrayPtrAdd(obj, args[0]);
				Write(", ");
				args[1].Accept(this, FuPriority.Argument);
				WriteSizeofCompare(obj.Type.AsClassType().GetElementType());
				break;
			case FuId.ListAdd:
			case FuId.StackPush:
				switch (obj.Type.AsClassType().GetElementType()) {
				case FuArrayStorageType _:
				case FuStorageType storage when storage.Class.Id == FuId.None && !NeedsConstructor(storage.Class):
					Write("g_array_set_size(");
					obj.Accept(this, FuPriority.Argument);
					Write(", ");
					WritePostfix(obj, "->len + 1)");
					break;
				default:
					WriteListAddInsert(obj, false, "g_array_append_val", args);
					break;
				}
				break;
			case FuId.ListClear:
			case FuId.StackClear:
				Write("g_array_set_size(");
				obj.Accept(this, FuPriority.Argument);
				Write(", 0)");
				break;
			case FuId.ListContains:
				StartArrayContains(obj);
				WritePostfix(obj, "->data, ");
				WritePostfix(obj, "->len, ");
				args[0].Accept(this, FuPriority.Argument);
				WriteChar(')');
				break;
			case FuId.ListInsert:
				WriteListAddInsert(obj, true, "g_array_insert_val", args);
				break;
			case FuId.ListLast:
			case FuId.StackPeek:
				StartArrayIndexing(obj, obj.Type.AsClassType().GetElementType());
				WritePostfix(obj, "->len - 1)");
				break;
			case FuId.ListRemoveAt:
				WriteCall("g_array_remove_index", obj, args[0]);
				break;
			case FuId.ListRemoveRange:
				WriteCall("g_array_remove_range", obj, args[0], args[1]);
				break;
			case FuId.ListSortAll:
				Write("g_array_sort(");
				obj.Accept(this, FuPriority.Argument);
				FuId typeId2 = obj.Type.AsClassType().GetElementType().Id;
				Write(", FuCompare_");
				WriteNumericType(typeId2);
				WriteChar(')');
				this.Compares.Add(typeId2);
				break;
			case FuId.QueueClear:
				string destroy = GetDictionaryDestroy(obj.Type.AsClassType().GetElementType());
				if (destroy == "NULL") {
					Write("g_queue_clear(");
					WriteQueueObject(obj);
				}
				else {
					Write("g_queue_clear_full(");
					WriteQueueObject(obj);
					Write(", ");
					Write(destroy);
				}
				WriteChar(')');
				break;
			case FuId.QueueDequeue:
				WriteQueueGet("g_queue_pop_head", obj, parent);
				break;
			case FuId.QueueEnqueue:
				Write("g_queue_push_tail(");
				WriteQueueObject(obj);
				Write(", ");
				WriteGPointerCast(obj.Type.AsClassType().GetElementType(), args[0]);
				WriteChar(')');
				break;
			case FuId.QueuePeek:
				WriteQueueGet("g_queue_peek_head", obj, parent);
				break;
			case FuId.StackPop:
				if (parent == FuPriority.Statement)
					WritePostfix(obj, "->len--");
				else {
					StartArrayIndexing(obj, obj.Type.AsClassType().GetElementType());
					Write("--");
					WritePostfix(obj, "->len)");
				}
				break;
			case FuId.HashSetAdd:
				Write("g_hash_table_add(");
				obj.Accept(this, FuPriority.Argument);
				Write(", ");
				WriteGPointerCast(obj.Type.AsClassType().GetElementType(), args[0]);
				WriteChar(')');
				break;
			case FuId.HashSetClear:
			case FuId.DictionaryClear:
				WriteCall("g_hash_table_remove_all", obj);
				break;
			case FuId.HashSetContains:
			case FuId.DictionaryContainsKey:
				WriteDictionaryLookup(obj, "g_hash_table_contains", args[0]);
				break;
			case FuId.HashSetRemove:
			case FuId.DictionaryRemove:
				WriteDictionaryLookup(obj, "g_hash_table_remove", args[0]);
				break;
			case FuId.SortedSetAdd:
				Write("g_tree_insert(");
				obj.Accept(this, FuPriority.Argument);
				Write(", ");
				WriteGPointerCast(obj.Type.AsClassType().GetKeyType(), args[0]);
				Write(", NULL)");
				break;
			case FuId.DictionaryAdd:
				StartDictionaryInsert(obj, args[0]);
				FuClassType valueType = obj.Type.AsClassType().GetValueType().AsClassType();
				switch (valueType.Class.Id) {
				case FuId.ListClass:
				case FuId.StackClass:
				case FuId.DictionaryClass:
				case FuId.SortedDictionaryClass:
					WriteNewStorage(valueType);
					break;
				default:
					if (valueType.Class.IsPublic && valueType.Class.Constructor != null && valueType.Class.Constructor.Visibility == FuVisibility.Public) {
						WriteName(valueType.Class);
						Write("_New()");
					}
					else {
						Write("malloc(sizeof(");
						WriteType(valueType, false);
						Write("))");
					}
					break;
				}
				WriteChar(')');
				break;
			case FuId.SortedSetClear:
			case FuId.SortedDictionaryClear:
				Write("g_tree_destroy(g_tree_ref(");
				obj.Accept(this, FuPriority.Argument);
				Write("))");
				break;
			case FuId.SortedSetContains:
			case FuId.SortedDictionaryContainsKey:
				Write("g_tree_lookup_extended(");
				obj.Accept(this, FuPriority.Argument);
				Write(", ");
				WriteGConstPointerCast(args[0]);
				Write(", NULL, NULL)");
				break;
			case FuId.SortedSetRemove:
			case FuId.SortedDictionaryRemove:
				WriteDictionaryLookup(obj, "g_tree_remove", args[0]);
				break;
			case FuId.TextWriterWrite:
				WriteTextWriterWrite(obj, args, false);
				break;
			case FuId.TextWriterWriteChar:
				WriteCall("putc", args[0], obj);
				break;
			case FuId.TextWriterWriteLine:
				WriteTextWriterWrite(obj, args, true);
				break;
			case FuId.ConsoleWrite:
				WriteConsoleWrite(args, false);
				break;
			case FuId.ConsoleWriteLine:
				WriteConsoleWrite(args, true);
				break;
			case FuId.UTF8GetByteCount:
				WriteStringLength(args[0]);
				break;
			case FuId.UTF8GetBytes:
				Include("string.h");
				Write("memcpy(");
				WriteArrayPtrAdd(args[1], args[2]);
				Write(", ");
				args[0].Accept(this, FuPriority.Argument);
				Write(", strlen(");
				args[0].Accept(this, FuPriority.Argument);
				Write("))");
				break;
			case FuId.EnvironmentGetEnvironmentVariable:
				WriteCall("getenv", args[0]);
				break;
			case FuId.RegexCompile:
				WriteGlib("g_regex_new(");
				WriteTemporaryOrExpr(args[0], FuPriority.Argument);
				Write(", ");
				WriteCRegexOptions(args);
				Write(", 0, NULL)");
				break;
			case FuId.RegexEscape:
				WriteGlib("g_regex_escape_string(");
				WriteTemporaryOrExpr(args[0], FuPriority.Argument);
				Write(", -1)");
				break;
			case FuId.RegexIsMatchStr:
				WriteGlib("g_regex_match_simple(");
				WriteTemporaryOrExpr(args[1], FuPriority.Argument);
				Write(", ");
				WriteTemporaryOrExpr(args[0], FuPriority.Argument);
				Write(", ");
				WriteCRegexOptions(args);
				Write(", 0)");
				break;
			case FuId.RegexIsMatchRegex:
				Write("g_regex_match(");
				obj.Accept(this, FuPriority.Argument);
				Write(", ");
				WriteTemporaryOrExpr(args[0], FuPriority.Argument);
				Write(", 0, NULL)");
				break;
			case FuId.MatchFindStr:
				this.MatchFind = true;
				Write("FuMatch_Find(&");
				obj.Accept(this, FuPriority.Primary);
				Write(", ");
				WriteTemporaryOrExpr(args[0], FuPriority.Argument);
				Write(", ");
				WriteTemporaryOrExpr(args[1], FuPriority.Argument);
				Write(", ");
				WriteCRegexOptions(args);
				WriteChar(')');
				break;
			case FuId.MatchFindRegex:
				Write("g_regex_match(");
				args[1].Accept(this, FuPriority.Argument);
				Write(", ");
				WriteTemporaryOrExpr(args[0], FuPriority.Argument);
				Write(", 0, &");
				obj.Accept(this, FuPriority.Primary);
				WriteChar(')');
				break;
			case FuId.MatchGetCapture:
				WriteCall("g_match_info_fetch", obj, args[0]);
				break;
			case FuId.MathMethod:
			case FuId.MathIsFinite:
			case FuId.MathIsNaN:
			case FuId.MathLog2:
				IncludeMath();
				WriteLowercase(method.Name);
				WriteArgsInParentheses(method, args);
				break;
			case FuId.MathAbs:
				switch (args[0].Type.Id) {
				case FuId.LongType:
					WriteCall("llabs", args[0]);
					break;
				case FuId.FloatType:
					IncludeMath();
					WriteCall("fabsf", args[0]);
					break;
				case FuId.FloatIntType:
				case FuId.DoubleType:
					IncludeMath();
					WriteCall("fabs", args[0]);
					break;
				default:
					WriteCall("abs", args[0]);
					break;
				}
				break;
			case FuId.MathCeiling:
				IncludeMath();
				WriteCall("ceil", args[0]);
				break;
			case FuId.MathFusedMultiplyAdd:
				IncludeMath();
				WriteCall("fma", args[0], args[1], args[2]);
				break;
			case FuId.MathIsInfinity:
				IncludeMath();
				WriteCall("isinf", args[0]);
				break;
			case FuId.MathMaxDouble:
				IncludeMath();
				WriteCall("fmax", args[0], args[1]);
				break;
			case FuId.MathMinDouble:
				IncludeMath();
				WriteCall("fmin", args[0], args[1]);
				break;
			case FuId.MathRound:
				IncludeMath();
				WriteCall("round", args[0]);
				break;
			case FuId.MathTruncate:
				IncludeMath();
				WriteCall("trunc", args[0]);
				break;
			default:
				NotSupported(obj, method.Name);
				break;
			}
		}

		void WriteDictionaryIndexing(string function, FuBinaryExpr expr, FuPriority parent)
		{
			FuType valueType = expr.Left.Type.AsClassType().GetValueType();
			if (valueType is FuIntegerType && valueType.Id != FuId.LongType) {
				Write("GPOINTER_TO_INT(");
				WriteDictionaryLookup(expr.Left, function, expr.Right);
				WriteChar(')');
			}
			else {
				if (parent > FuPriority.Mul)
					WriteChar('(');
				if (valueType is FuStorageType storage && (storage.Class.Id == FuId.None || storage.Class.Id == FuId.ArrayStorageClass))
					WriteDynamicArrayCast(valueType);
				else {
					WriteStaticCastType(valueType);
					if (valueType is FuEnum) {
						Debug.Assert(parent <= FuPriority.Mul, "Should close two parens");
						Write("GPOINTER_TO_INT(");
					}
				}
				WriteDictionaryLookup(expr.Left, function, expr.Right);
				if (parent > FuPriority.Mul || valueType is FuEnum)
					WriteChar(')');
			}
		}

		protected override void WriteIndexingExpr(FuBinaryExpr expr, FuPriority parent)
		{
			if (expr.Left.Type is FuClassType klass) {
				switch (klass.Class.Id) {
				case FuId.ListClass:
					if (klass.GetElementType() is FuArrayStorageType) {
						WriteChar('(');
						WriteDynamicArrayCast(klass.GetElementType());
						WritePostfix(expr.Left, "->data)[");
						expr.Right.Accept(this, FuPriority.Argument);
						WriteChar(']');
					}
					else {
						StartArrayIndexing(expr.Left, klass.GetElementType());
						expr.Right.Accept(this, FuPriority.Argument);
						WriteChar(')');
					}
					return;
				case FuId.DictionaryClass:
					WriteDictionaryIndexing("g_hash_table_lookup", expr, parent);
					return;
				case FuId.SortedDictionaryClass:
					WriteDictionaryIndexing("g_tree_lookup", expr, parent);
					return;
				default:
					break;
				}
			}
			base.WriteIndexingExpr(expr, parent);
		}

		internal override void VisitBinaryExpr(FuBinaryExpr expr, FuPriority parent)
		{
			switch (expr.Op) {
			case FuToken.Plus:
				if (expr.Type.Id == FuId.StringStorageType) {
					this.StringFormat = true;
					Include("stdarg.h");
					Include("stdio.h");
					Write("FuString_Format(\"%s%s\", ");
					expr.Left.Accept(this, FuPriority.Argument);
					Write(", ");
					expr.Right.Accept(this, FuPriority.Argument);
					WriteChar(')');
					return;
				}
				break;
			case FuToken.Equal:
			case FuToken.NotEqual:
			case FuToken.Greater:
				FuExpr str = IsStringEmpty(expr);
				if (str != null) {
					WritePostfix(str, expr.Op == FuToken.Equal ? "[0] == '\\0'" : "[0] != '\\0'");
					return;
				}
				break;
			case FuToken.AddAssign:
				if (expr.Left.Type.Id == FuId.StringStorageType) {
					if (expr.Right is FuInterpolatedString rightInterpolated) {
						this.StringAssign = true;
						Write("FuString_Assign(&");
						expr.Left.Accept(this, FuPriority.Primary);
						this.StringFormat = true;
						Include("stdarg.h");
						Include("stdio.h");
						Write(", FuString_Format(\"%s");
						WritePrintfFormat(rightInterpolated);
						Write("\", ");
						expr.Left.Accept(this, FuPriority.Argument);
						WriteInterpolatedStringArgs(rightInterpolated);
						WriteChar(')');
					}
					else {
						Include("string.h");
						this.StringAppend = true;
						Write("FuString_Append(&");
						expr.Left.Accept(this, FuPriority.Primary);
						Write(", ");
						expr.Right.Accept(this, FuPriority.Argument);
					}
					WriteChar(')');
					return;
				}
				break;
			case FuToken.Is:
				NotSupported(expr, "'is' operator");
				break;
			default:
				break;
			}
			base.VisitBinaryExpr(expr, parent);
		}

		protected override void WriteResource(string name, int length)
		{
			Write("FuResource_");
			WriteResourceName(name);
		}

		internal override void VisitLambdaExpr(FuLambdaExpr expr)
		{
			NotSupported(expr, "Lambda expression");
		}

		void WriteDestructLoopOrSwitch(FuCondCompletionStatement loopOrSwitch)
		{
			for (int i = this.VarsToDestruct.Count; --i >= 0;) {
				FuVar def = this.VarsToDestruct[i];
				if (!loopOrSwitch.Encloses(def))
					break;
				WriteDestruct(def);
			}
		}

		void TrimVarsToDestruct(int i)
		{
			this.VarsToDestruct.RemoveRange(i, this.VarsToDestruct.Count - i);
		}

		protected override void CleanupBlock(FuBlock statement)
		{
			int i = this.VarsToDestruct.Count;
			for (; i > 0; i--) {
				FuVar def = this.VarsToDestruct[i - 1];
				if (def.Parent != statement)
					break;
				if (statement.CompletesNormally())
					WriteDestruct(def);
			}
			TrimVarsToDestruct(i);
		}

		internal override void VisitBreak(FuBreak statement)
		{
			WriteDestructLoopOrSwitch(statement.LoopOrSwitch);
			base.VisitBreak(statement);
		}

		internal override void VisitContinue(FuContinue statement)
		{
			WriteDestructLoopOrSwitch(statement.Loop);
			base.VisitContinue(statement);
		}

		internal override void VisitExpr(FuExpr statement)
		{
			WriteCTemporaries(statement);
			FuMethod throwingMethod = GetThrowingMethod(statement);
			if (throwingMethod != null) {
				EnsureChildBlock();
				statement.Accept(this, StartForwardThrow(throwingMethod));
				EndForwardThrow(throwingMethod);
				CleanupTemporaries();
			}
			else if (statement is FuCallExpr && statement.Type.Id == FuId.StringStorageType) {
				Write("free(");
				statement.Accept(this, FuPriority.Argument);
				WriteLine(");");
				CleanupTemporaries();
			}
			else if (statement is FuCallExpr && statement.Type is FuDynamicPtrType) {
				this.SharedRelease = true;
				Write("FuShared_Release(");
				statement.Accept(this, FuPriority.Argument);
				WriteLine(");");
				CleanupTemporaries();
			}
			else
				base.VisitExpr(statement);
		}

		void StartForeachHashTable(FuForeach statement)
		{
			OpenBlock();
			WriteLine("GHashTableIter fudictit;");
			Write("g_hash_table_iter_init(&fudictit, ");
			statement.Collection.Accept(this, FuPriority.Argument);
			WriteLine(");");
		}

		void WriteDictIterVar(FuNamedValue iter, string value)
		{
			WriteTypeAndName(iter);
			Write(" = ");
			if (iter.Type is FuIntegerType && iter.Type.Id != FuId.LongType) {
				Write("GPOINTER_TO_INT(");
				Write(value);
				WriteChar(')');
			}
			else {
				WriteStaticCastType(iter.Type);
				Write(value);
			}
			WriteCharLine(';');
		}

		internal override void VisitForeach(FuForeach statement)
		{
			string element = statement.GetVar().Name;
			switch (statement.Collection.Type) {
			case FuArrayStorageType array:
				Write("for (int ");
				WriteCamelCaseNotKeyword(element);
				Write(" = 0; ");
				WriteCamelCaseNotKeyword(element);
				Write(" < ");
				VisitLiteralLong(array.Length);
				Write("; ");
				WriteCamelCaseNotKeyword(element);
				Write("++)");
				WriteChild(statement.Body);
				break;
			case FuClassType klass:
				switch (klass.Class.Id) {
				case FuId.StringClass:
					Write("for (");
					WriteStringPtrType();
					WriteCamelCaseNotKeyword(element);
					Write(" = ");
					statement.Collection.Accept(this, FuPriority.Argument);
					Write("; *");
					WriteCamelCaseNotKeyword(element);
					Write(" != '\\0'; ");
					WriteCamelCaseNotKeyword(element);
					Write("++)");
					WriteChild(statement.Body);
					break;
				case FuId.ListClass:
					Write("for (");
					FuType elementType = klass.GetElementType();
					WriteType(elementType, false);
					Write(" const *");
					WriteCamelCaseNotKeyword(element);
					Write(" = (");
					WriteType(elementType, false);
					Write(" const *) ");
					WritePostfix(statement.Collection, "->data, ");
					for (; elementType.IsArray(); elementType = elementType.AsClassType().GetElementType())
						WriteChar('*');
					if (elementType is FuClassType)
						Write("* const ");
					Write("*fuend = ");
					WriteCamelCaseNotKeyword(element);
					Write(" + ");
					WritePostfix(statement.Collection, "->len; ");
					WriteCamelCaseNotKeyword(element);
					Write(" < fuend; ");
					WriteCamelCaseNotKeyword(element);
					Write("++)");
					WriteChild(statement.Body);
					break;
				case FuId.HashSetClass:
					StartForeachHashTable(statement);
					WriteLine("gpointer fukey;");
					Write("while (g_hash_table_iter_next(&fudictit, &fukey, NULL)) ");
					OpenBlock();
					WriteDictIterVar(statement.GetVar(), "fukey");
					FlattenBlock(statement.Body);
					CloseBlock();
					CloseBlock();
					break;
				case FuId.SortedSetClass:
					Write("for (GTreeNode *fusetit = g_tree_node_first(");
					statement.Collection.Accept(this, FuPriority.Argument);
					Write("); fusetit != NULL; fusetit = g_tree_node_next(fusetit)) ");
					OpenBlock();
					WriteDictIterVar(statement.GetVar(), "g_tree_node_key(fusetit)");
					FlattenBlock(statement.Body);
					CloseBlock();
					break;
				case FuId.DictionaryClass:
					StartForeachHashTable(statement);
					WriteLine("gpointer fukey, fuvalue;");
					Write("while (g_hash_table_iter_next(&fudictit, &fukey, &fuvalue)) ");
					OpenBlock();
					WriteDictIterVar(statement.GetVar(), "fukey");
					WriteDictIterVar(statement.GetValueVar(), "fuvalue");
					FlattenBlock(statement.Body);
					CloseBlock();
					CloseBlock();
					break;
				case FuId.SortedDictionaryClass:
					Write("for (GTreeNode *fudictit = g_tree_node_first(");
					statement.Collection.Accept(this, FuPriority.Argument);
					Write("); fudictit != NULL; fudictit = g_tree_node_next(fudictit)) ");
					OpenBlock();
					WriteDictIterVar(statement.GetVar(), "g_tree_node_key(fudictit)");
					WriteDictIterVar(statement.GetValueVar(), "g_tree_node_value(fudictit)");
					FlattenBlock(statement.Body);
					CloseBlock();
					break;
				default:
					NotSupported(statement.Collection, klass.Class.Name);
					break;
				}
				break;
			default:
				NotSupported(statement.Collection, statement.Collection.Type.ToString());
				break;
			}
		}

		internal override void VisitLock(FuLock statement)
		{
			Write("mtx_lock(&");
			statement.Lock.Accept(this, FuPriority.Primary);
			WriteLine(");");
			statement.Body.AcceptStatement(this);
			Write("mtx_unlock(&");
			statement.Lock.Accept(this, FuPriority.Primary);
			WriteLine(");");
		}

		internal override void VisitReturn(FuReturn statement)
		{
			if (statement.Value == null) {
				WriteDestructAll();
				WriteLine(this.CurrentMethod.Throws ? "return true;" : "return;");
			}
			else if (statement.Value is FuLiteral || (this.VarsToDestruct.Count == 0 && !ContainsTemporariesToDestruct(statement.Value))) {
				WriteDestructAll();
				WriteCTemporaries(statement.Value);
				base.VisitReturn(statement);
			}
			else {
				if (statement.Value is FuSymbolReference symbol && symbol.Symbol is FuVar local) {
					if (this.VarsToDestruct.Contains(local)) {
						WriteDestructAll(local);
						Write("return ");
						if (this.CurrentMethod.Type is FuClassType resultPtr)
							WriteClassPtr(resultPtr.Class, symbol, FuPriority.Argument);
						else
							symbol.Accept(this, FuPriority.Argument);
						WriteCharLine(';');
						return;
					}
					WriteDestructAll();
					base.VisitReturn(statement);
					return;
				}
				WriteCTemporaries(statement.Value);
				EnsureChildBlock();
				StartDefinition(this.CurrentMethod.Type, true, true);
				Write("returnValue = ");
				WriteCoerced(this.CurrentMethod.Type, statement.Value, FuPriority.Argument);
				WriteCharLine(';');
				CleanupTemporaries();
				WriteDestructAll();
				WriteLine("return returnValue;");
			}
		}

		protected override void WriteSwitchCaseBody(List<FuStatement> statements)
		{
			if (statements[0] is FuVar || (statements[0] is FuConst konst && konst.Type is FuArrayStorageType))
				WriteCharLine(';');
			int varsToDestructCount = this.VarsToDestruct.Count;
			WriteStatements(statements);
			TrimVarsToDestruct(varsToDestructCount);
		}

		internal override void VisitSwitch(FuSwitch statement)
		{
			if (statement.IsTypeMatching())
				NotSupported(statement, "Type-matching 'switch'");
			else
				base.VisitSwitch(statement);
		}

		internal override void VisitThrow(FuThrow statement)
		{
			WriteThrow();
		}

		bool TryWriteCallAndReturn(List<FuStatement> statements, int lastCallIndex, FuExpr returnValue)
		{
			if (this.VarsToDestruct.Count > 0)
				return false;
			for (int i = 0; i < lastCallIndex; i++) {
				if (statements[i] is FuVar def && NeedToDestruct(def))
					return false;
			}
			if (!(statements[lastCallIndex] is FuExpr call))
				return false;
			FuMethod throwingMethod = GetThrowingMethod(call);
			if (throwingMethod == null)
				return false;
			WriteFirstStatements(statements, lastCallIndex);
			Write("return ");
			if (throwingMethod.Type is FuNumericType) {
				if (throwingMethod.Type is FuIntegerType) {
					call.Accept(this, FuPriority.Equality);
					Write(" != -1");
				}
				else {
					IncludeMath();
					Write("!isnan(");
					call.Accept(this, FuPriority.Argument);
					WriteChar(')');
				}
			}
			else if (throwingMethod.Type.Id == FuId.VoidType)
				call.Accept(this, FuPriority.Select);
			else {
				call.Accept(this, FuPriority.Equality);
				Write(" != NULL");
			}
			if (returnValue != null) {
				Write(" ? ");
				returnValue.Accept(this, FuPriority.Select);
				Write(" : ");
				WriteThrowReturnValue();
			}
			WriteCharLine(';');
			return true;
		}

		protected override void WriteStatements(List<FuStatement> statements)
		{
			int i = statements.Count - 2;
			if (i >= 0 && statements[i + 1] is FuReturn ret && TryWriteCallAndReturn(statements, i, ret.Value))
				return;
			base.WriteStatements(statements);
		}

		protected override void WriteEnum(FuEnum enu)
		{
			WriteNewLine();
			WriteDoc(enu.Documentation);
			Write("typedef enum ");
			OpenBlock();
			enu.AcceptValues(this);
			WriteNewLine();
			this.Indent--;
			Write("} ");
			WriteName(enu);
			WriteCharLine(';');
		}

		void WriteTypedef(FuClass klass)
		{
			if (klass.CallType == FuCallType.Static)
				return;
			Write("typedef struct ");
			WriteName(klass);
			WriteChar(' ');
			WriteName(klass);
			WriteCharLine(';');
		}

		protected void WriteTypedefs(FuProgram program, bool pub)
		{
			for (FuSymbol type = program.First; type != null; type = type.Next) {
				switch (type) {
				case FuClass klass:
					if (klass.IsPublic == pub)
						WriteTypedef(klass);
					break;
				case FuEnum enu:
					if (enu.IsPublic == pub)
						WriteEnum(enu);
					break;
				default:
					throw new NotImplementedException();
				}
			}
		}

		void WriteInstanceParameters(FuMethod method)
		{
			WriteChar('(');
			if (!method.IsMutator)
				Write("const ");
			WriteName(method.Parent);
			Write(" *self");
			WriteRemainingParameters(method, false, false);
		}

		void WriteSignature(FuMethod method)
		{
			FuClass klass = (FuClass) method.Parent;
			if (!klass.IsPublic || method.Visibility != FuVisibility.Public)
				Write("static ");
			WriteReturnType(method);
			WriteName(klass);
			WriteChar('_');
			Write(method.Name);
			if (method.CallType != FuCallType.Static)
				WriteInstanceParameters(method);
			else if (method.Parameters.Count() == 0)
				Write("(void)");
			else
				WriteParameters(method, false);
		}

		void WriteVtblFields(FuClass klass)
		{
			if (klass.Parent is FuClass baseClass)
				WriteVtblFields(baseClass);
			for (FuSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
				if (symbol is FuMethod method && method.IsAbstractOrVirtual()) {
					WriteReturnType(method);
					Write("(*");
					WriteCamelCase(method.Name);
					WriteChar(')');
					WriteInstanceParameters(method);
					WriteCharLine(';');
				}
			}
		}

		void WriteVtblStruct(FuClass klass)
		{
			Write("typedef struct ");
			OpenBlock();
			WriteVtblFields(klass);
			this.Indent--;
			Write("} ");
			WriteName(klass);
			WriteLine("Vtbl;");
		}

		protected virtual string GetConst(FuArrayStorageType array) => "const ";

		protected override void WriteConst(FuConst konst)
		{
			if (konst.Type is FuArrayStorageType array) {
				Write("static ");
				Write(GetConst(array));
				WriteTypeAndName(konst);
				Write(" = ");
				konst.Value.Accept(this, FuPriority.Argument);
				WriteCharLine(';');
			}
			else if (konst.Visibility == FuVisibility.Public) {
				Write("#define ");
				WriteName(konst);
				WriteChar(' ');
				konst.Value.Accept(this, FuPriority.Argument);
				WriteNewLine();
			}
		}

		protected override void WriteField(FuField field)
		{
			throw new NotImplementedException();
		}

		static bool HasVtblValue(FuClass klass)
		{
			if (klass.CallType == FuCallType.Static || klass.CallType == FuCallType.Abstract)
				return false;
			for (FuSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
				if (symbol is FuMethod method) {
					switch (method.CallType) {
					case FuCallType.Virtual:
					case FuCallType.Override:
					case FuCallType.Sealed:
						return true;
					default:
						break;
					}
				}
			}
			return false;
		}

		protected override bool NeedsConstructor(FuClass klass)
		{
			if (klass.Id == FuId.MatchClass)
				return false;
			return base.NeedsConstructor(klass) || HasVtblValue(klass) || (klass.Parent is FuClass baseClass && NeedsConstructor(baseClass));
		}

		void WriteXstructorSignature(string name, FuClass klass)
		{
			Write("static void ");
			WriteName(klass);
			WriteChar('_');
			Write(name);
			WriteChar('(');
			WriteName(klass);
			Write(" *self)");
		}

		protected void WriteSignatures(FuClass klass, bool pub)
		{
			for (FuSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
				switch (symbol) {
				case FuConst konst when (konst.Visibility == FuVisibility.Public) == pub:
					if (pub) {
						WriteNewLine();
						WriteDoc(konst.Documentation);
					}
					WriteConst(konst);
					break;
				case FuMethod method when method.IsLive && (method.Visibility == FuVisibility.Public) == pub && method.CallType != FuCallType.Abstract:
					WriteNewLine();
					WriteMethodDoc(method);
					WriteSignature(method);
					WriteCharLine(';');
					break;
				default:
					break;
				}
			}
		}

		protected override void WriteClassInternal(FuClass klass)
		{
			this.CurrentClass = klass;
			if (klass.CallType != FuCallType.Static) {
				WriteNewLine();
				if (klass.AddsVirtualMethods())
					WriteVtblStruct(klass);
				WriteDoc(klass.Documentation);
				Write("struct ");
				WriteName(klass);
				WriteChar(' ');
				OpenBlock();
				if (GetVtblPtrClass(klass) == klass) {
					Write("const ");
					WriteName(klass);
					WriteLine("Vtbl *vtbl;");
				}
				if (klass.Parent is FuClass) {
					WriteName(klass.Parent);
					WriteLine(" base;");
				}
				for (FuSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
					if (symbol is FuField field) {
						WriteDoc(field.Documentation);
						WriteTypeAndName(field);
						WriteCharLine(';');
					}
				}
				this.Indent--;
				WriteLine("};");
				if (NeedsConstructor(klass)) {
					WriteXstructorSignature("Construct", klass);
					WriteCharLine(';');
				}
				if (NeedsDestructor(klass)) {
					WriteXstructorSignature("Destruct", klass);
					WriteCharLine(';');
				}
			}
			WriteSignatures(klass, false);
		}

		void WriteVtbl(FuClass definingClass, FuClass declaringClass)
		{
			if (declaringClass.Parent is FuClass baseClass)
				WriteVtbl(definingClass, baseClass);
			for (FuSymbol symbol = declaringClass.First; symbol != null; symbol = symbol.Next) {
				if (symbol is FuMethod declaredMethod && declaredMethod.IsAbstractOrVirtual()) {
					FuSymbol definedMethod = definingClass.TryLookup(declaredMethod.Name, false);
					if (declaredMethod != definedMethod) {
						WriteChar('(');
						WriteReturnType(declaredMethod);
						Write("(*)");
						WriteInstanceParameters(declaredMethod);
						Write(") ");
					}
					WriteName(definedMethod);
					WriteCharLine(',');
				}
			}
		}

		protected void WriteConstructor(FuClass klass)
		{
			if (!NeedsConstructor(klass))
				return;
			WriteNewLine();
			WriteXstructorSignature("Construct", klass);
			WriteNewLine();
			OpenBlock();
			if (klass.Parent is FuClass baseClass && NeedsConstructor(baseClass)) {
				WriteName(baseClass);
				WriteLine("_Construct(&self->base);");
			}
			if (HasVtblValue(klass)) {
				FuClass structClass = GetVtblStructClass(klass);
				Write("static const ");
				WriteName(structClass);
				Write("Vtbl vtbl = ");
				OpenBlock();
				WriteVtbl(klass, structClass);
				this.Indent--;
				WriteLine("};");
				FuClass ptrClass = GetVtblPtrClass(klass);
				WriteSelfForField(ptrClass);
				Write("vtbl = ");
				if (ptrClass != structClass) {
					Write("(const ");
					WriteName(ptrClass);
					Write("Vtbl *) ");
				}
				WriteLine("&vtbl;");
			}
			WriteConstructorBody(klass);
			CloseBlock();
		}

		void WriteDestructFields(FuSymbol symbol)
		{
			if (symbol != null) {
				WriteDestructFields(symbol.Next);
				if (symbol is FuField field)
					WriteDestruct(field);
			}
		}

		protected void WriteDestructor(FuClass klass)
		{
			if (!NeedsDestructor(klass))
				return;
			WriteNewLine();
			WriteXstructorSignature("Destruct", klass);
			WriteNewLine();
			OpenBlock();
			WriteDestructFields(klass.First);
			if (klass.Parent is FuClass baseClass && NeedsDestructor(baseClass)) {
				WriteName(baseClass);
				WriteLine("_Destruct(&self->base);");
			}
			CloseBlock();
		}

		void WriteNewDelete(FuClass klass, bool define)
		{
			if (!klass.IsPublic || klass.Constructor == null || klass.Constructor.Visibility != FuVisibility.Public)
				return;
			WriteNewLine();
			WriteName(klass);
			Write(" *");
			WriteName(klass);
			Write("_New(void)");
			if (define) {
				WriteNewLine();
				OpenBlock();
				WriteName(klass);
				Write(" *self = (");
				WriteName(klass);
				Write(" *) malloc(sizeof(");
				WriteName(klass);
				WriteLine("));");
				if (NeedsConstructor(klass)) {
					WriteLine("if (self != NULL)");
					this.Indent++;
					WriteName(klass);
					WriteLine("_Construct(self);");
					this.Indent--;
				}
				WriteLine("return self;");
				CloseBlock();
				WriteNewLine();
			}
			else
				WriteCharLine(';');
			Write("void ");
			WriteName(klass);
			Write("_Delete(");
			WriteName(klass);
			Write(" *self)");
			if (define) {
				WriteNewLine();
				OpenBlock();
				if (NeedsDestructor(klass)) {
					WriteLine("if (self == NULL)");
					this.Indent++;
					WriteLine("return;");
					this.Indent--;
					WriteName(klass);
					WriteLine("_Destruct(self);");
				}
				WriteLine("free(self);");
				CloseBlock();
			}
			else
				WriteCharLine(';');
		}

		protected override void WriteMethod(FuMethod method)
		{
			if (!method.IsLive || method.CallType == FuCallType.Abstract)
				return;
			WriteNewLine();
			WriteSignature(method);
			for (FuVar param = method.Parameters.FirstParameter(); param != null; param = param.NextParameter()) {
				if (NeedToDestruct(param))
					this.VarsToDestruct.Add(param);
			}
			WriteNewLine();
			this.CurrentMethod = method;
			OpenBlock();
			if (method.Body is FuBlock block) {
				List<FuStatement> statements = block.Statements;
				if (!block.CompletesNormally())
					WriteStatements(statements);
				else if (method.Throws && method.Type.Id == FuId.VoidType) {
					if (statements.Count == 0 || !TryWriteCallAndReturn(statements, statements.Count - 1, null)) {
						WriteStatements(statements);
						WriteDestructAll();
						WriteLine("return true;");
					}
				}
				else {
					WriteStatements(statements);
					WriteDestructAll();
				}
			}
			else
				method.Body.AcceptStatement(this);
			this.VarsToDestruct.Clear();
			CloseBlock();
			this.CurrentMethod = null;
		}

		void WriteTryParseLibrary(string signature, string call)
		{
			WriteNewLine();
			Write("static bool Fu");
			WriteLine(signature);
			OpenBlock();
			WriteLine("if (*str == '\\0')");
			WriteLine("\treturn false;");
			WriteLine("char *end;");
			Write("*result = strto");
			Write(call);
			WriteLine(");");
			WriteLine("return *end == '\\0';");
			CloseBlock();
		}

		void WriteLibrary()
		{
			if (this.IntTryParse)
				WriteTryParseLibrary("Int_TryParse(int *result, const char *str, int base)", "l(str, &end, base");
			if (this.LongTryParse)
				WriteTryParseLibrary("Long_TryParse(int64_t *result, const char *str, int base)", "ll(str, &end, base");
			if (this.DoubleTryParse)
				WriteTryParseLibrary("Double_TryParse(double *result, const char *str)", "d(str, &end");
			if (this.StringAssign) {
				WriteNewLine();
				WriteLine("static void FuString_Assign(char **str, char *value)");
				OpenBlock();
				WriteLine("free(*str);");
				WriteLine("*str = value;");
				CloseBlock();
			}
			if (this.StringSubstring) {
				WriteNewLine();
				WriteLine("static char *FuString_Substring(const char *str, int len)");
				OpenBlock();
				WriteLine("char *p = malloc(len + 1);");
				WriteLine("memcpy(p, str, len);");
				WriteLine("p[len] = '\\0';");
				WriteLine("return p;");
				CloseBlock();
			}
			if (this.StringAppend) {
				WriteNewLine();
				WriteLine("static void FuString_AppendSubstring(char **str, const char *suffix, size_t suffixLen)");
				OpenBlock();
				WriteLine("if (suffixLen == 0)");
				WriteLine("\treturn;");
				WriteLine("size_t prefixLen = *str == NULL ? 0 : strlen(*str);");
				WriteLine("*str = realloc(*str, prefixLen + suffixLen + 1);");
				WriteLine("memcpy(*str + prefixLen, suffix, suffixLen);");
				WriteLine("(*str)[prefixLen + suffixLen] = '\\0';");
				CloseBlock();
				WriteNewLine();
				WriteLine("static void FuString_Append(char **str, const char *suffix)");
				OpenBlock();
				WriteLine("FuString_AppendSubstring(str, suffix, strlen(suffix));");
				CloseBlock();
			}
			if (this.StringIndexOf) {
				WriteNewLine();
				WriteLine("static int FuString_IndexOf(const char *str, const char *needle)");
				OpenBlock();
				WriteLine("const char *p = strstr(str, needle);");
				WriteLine("return p == NULL ? -1 : (int) (p - str);");
				CloseBlock();
			}
			if (this.StringLastIndexOf) {
				WriteNewLine();
				WriteLine("static int FuString_LastIndexOf(const char *str, const char *needle)");
				OpenBlock();
				WriteLine("if (needle[0] == '\\0')");
				WriteLine("\treturn (int) strlen(str);");
				WriteLine("int result = -1;");
				WriteLine("const char *p = strstr(str, needle);");
				Write("while (p != NULL) ");
				OpenBlock();
				WriteLine("result = (int) (p - str);");
				WriteLine("p = strstr(p + 1, needle);");
				CloseBlock();
				WriteLine("return result;");
				CloseBlock();
			}
			if (this.StringEndsWith) {
				WriteNewLine();
				WriteLine("static bool FuString_EndsWith(const char *str, const char *suffix)");
				OpenBlock();
				WriteLine("size_t strLen = strlen(str);");
				WriteLine("size_t suffixLen = strlen(suffix);");
				WriteLine("return strLen >= suffixLen && memcmp(str + strLen - suffixLen, suffix, suffixLen) == 0;");
				CloseBlock();
			}
			if (this.StringReplace) {
				WriteNewLine();
				WriteLine("static char *FuString_Replace(const char *s, const char *oldValue, const char *newValue)");
				OpenBlock();
				Write("for (char *result = NULL;;) ");
				OpenBlock();
				WriteLine("const char *p = strstr(s, oldValue);");
				WriteLine("if (p == NULL) {");
				WriteLine("\tFuString_Append(&result, s);");
				WriteLine("\treturn result == NULL ? strdup(\"\") : result;");
				WriteCharLine('}');
				WriteLine("FuString_AppendSubstring(&result, s, p - s);");
				WriteLine("FuString_Append(&result, newValue);");
				WriteLine("s = p + strlen(oldValue);");
				CloseBlock();
				CloseBlock();
			}
			if (this.StringFormat) {
				WriteNewLine();
				WriteLine("static char *FuString_Format(const char *format, ...)");
				OpenBlock();
				WriteLine("va_list args1;");
				WriteLine("va_start(args1, format);");
				WriteLine("va_list args2;");
				WriteLine("va_copy(args2, args1);");
				WriteLine("size_t len = vsnprintf(NULL, 0, format, args1) + 1;");
				WriteLine("va_end(args1);");
				WriteLine("char *str = malloc(len);");
				WriteLine("vsnprintf(str, len, format, args2);");
				WriteLine("va_end(args2);");
				WriteLine("return str;");
				CloseBlock();
			}
			if (this.MatchFind) {
				WriteNewLine();
				WriteLine("static bool FuMatch_Find(GMatchInfo **match_info, const char *input, const char *pattern, GRegexCompileFlags options)");
				OpenBlock();
				WriteLine("GRegex *regex = g_regex_new(pattern, options, 0, NULL);");
				WriteLine("bool result = g_regex_match(regex, input, 0, match_info);");
				WriteLine("g_regex_unref(regex);");
				WriteLine("return result;");
				CloseBlock();
			}
			if (this.MatchPos) {
				WriteNewLine();
				WriteLine("static int FuMatch_GetPos(const GMatchInfo *match_info, int which)");
				OpenBlock();
				WriteLine("int start;");
				WriteLine("int end;");
				WriteLine("g_match_info_fetch_pos(match_info, 0, &start, &end);");
				WriteLine("switch (which) {");
				WriteLine("case 0:");
				WriteLine("\treturn start;");
				WriteLine("case 1:");
				WriteLine("\treturn end;");
				WriteLine("default:");
				WriteLine("\treturn end - start;");
				WriteCharLine('}');
				CloseBlock();
			}
			if (this.PtrConstruct) {
				WriteNewLine();
				WriteLine("static void FuPtr_Construct(void **ptr)");
				OpenBlock();
				WriteLine("*ptr = NULL;");
				CloseBlock();
			}
			if (this.SharedMake || this.SharedAddRef || this.SharedRelease) {
				WriteNewLine();
				WriteLine("typedef void (*FuMethodPtr)(void *);");
				WriteLine("typedef struct {");
				this.Indent++;
				WriteLine("size_t count;");
				WriteLine("size_t unitSize;");
				WriteLine("size_t refCount;");
				WriteLine("FuMethodPtr destructor;");
				this.Indent--;
				WriteLine("} FuShared;");
			}
			if (this.SharedMake) {
				WriteNewLine();
				WriteLine("static void *FuShared_Make(size_t count, size_t unitSize, FuMethodPtr constructor, FuMethodPtr destructor)");
				OpenBlock();
				WriteLine("FuShared *self = (FuShared *) malloc(sizeof(FuShared) + count * unitSize);");
				WriteLine("self->count = count;");
				WriteLine("self->unitSize = unitSize;");
				WriteLine("self->refCount = 1;");
				WriteLine("self->destructor = destructor;");
				Write("if (constructor != NULL) ");
				OpenBlock();
				WriteLine("for (size_t i = 0; i < count; i++)");
				WriteLine("\tconstructor((char *) (self + 1) + i * unitSize);");
				CloseBlock();
				WriteLine("return self + 1;");
				CloseBlock();
			}
			if (this.SharedAddRef) {
				WriteNewLine();
				WriteLine("static void *FuShared_AddRef(void *ptr)");
				OpenBlock();
				WriteLine("if (ptr != NULL)");
				WriteLine("\t((FuShared *) ptr)[-1].refCount++;");
				WriteLine("return ptr;");
				CloseBlock();
			}
			if (this.SharedRelease || this.SharedAssign) {
				WriteNewLine();
				WriteLine("static void FuShared_Release(void *ptr)");
				OpenBlock();
				WriteLine("if (ptr == NULL)");
				WriteLine("\treturn;");
				WriteLine("FuShared *self = (FuShared *) ptr - 1;");
				WriteLine("if (--self->refCount != 0)");
				WriteLine("\treturn;");
				Write("if (self->destructor != NULL) ");
				OpenBlock();
				WriteLine("for (size_t i = self->count; i > 0;)");
				WriteLine("\tself->destructor((char *) ptr + --i * self->unitSize);");
				CloseBlock();
				WriteLine("free(self);");
				CloseBlock();
			}
			if (this.SharedAssign) {
				WriteNewLine();
				WriteLine("static void FuShared_Assign(void **ptr, void *value)");
				OpenBlock();
				WriteLine("FuShared_Release(*ptr);");
				WriteLine("*ptr = value;");
				CloseBlock();
			}
			foreach ((string name, string content) in this.ListFrees) {
				WriteNewLine();
				Write("static void FuList_Free");
				Write(name);
				WriteLine("(void *ptr)");
				OpenBlock();
				Write(content);
				WriteCharLine(';');
				CloseBlock();
			}
			if (this.TreeCompareInteger) {
				WriteNewLine();
				Write("static int FuTree_CompareInteger(gconstpointer pa, gconstpointer pb, gpointer user_data)");
				OpenBlock();
				WriteLine("gintptr a = (gintptr) pa;");
				WriteLine("gintptr b = (gintptr) pb;");
				WriteLine("return (a > b) - (a < b);");
				CloseBlock();
			}
			if (this.TreeCompareString) {
				WriteNewLine();
				Write("static int FuTree_CompareString(gconstpointer a, gconstpointer b, gpointer user_data)");
				OpenBlock();
				WriteLine("return strcmp((const char *) a, (const char *) b);");
				CloseBlock();
			}
			foreach (FuId typeId in this.Compares) {
				WriteNewLine();
				Write("static int FuCompare_");
				WriteNumericType(typeId);
				WriteLine("(const void *pa, const void *pb)");
				OpenBlock();
				WriteNumericType(typeId);
				Write(" a = *(const ");
				WriteNumericType(typeId);
				WriteLine(" *) pa;");
				WriteNumericType(typeId);
				Write(" b = *(const ");
				WriteNumericType(typeId);
				WriteLine(" *) pb;");
				switch (typeId) {
				case FuId.ByteRange:
				case FuId.SByteRange:
				case FuId.ShortRange:
				case FuId.UShortRange:
					WriteLine("return a - b;");
					break;
				default:
					WriteLine("return (a > b) - (a < b);");
					break;
				}
				CloseBlock();
			}
			foreach (FuId typeId in this.Contains) {
				WriteNewLine();
				Write("static bool FuArray_Contains_");
				if (typeId == FuId.None)
					Write("object(const void * const *a, size_t len, const void *");
				else if (typeId == FuId.StringPtrType)
					Write("string(const char * const *a, size_t len, const char *");
				else {
					WriteNumericType(typeId);
					Write("(const ");
					WriteNumericType(typeId);
					Write(" *a, size_t len, ");
					WriteNumericType(typeId);
				}
				WriteLine(" value)");
				OpenBlock();
				WriteLine("for (size_t i = 0; i < len; i++)");
				if (typeId == FuId.StringPtrType)
					WriteLine("\tif (strcmp(a[i], value) == 0)");
				else
					WriteLine("\tif (a[i] == value)");
				WriteLine("\t\treturn true;");
				WriteLine("return false;");
				CloseBlock();
			}
		}

		protected void WriteResources(SortedDictionary<string, List<byte>> resources)
		{
			if (resources.Count == 0)
				return;
			WriteNewLine();
			foreach ((string name, List<byte> content) in resources) {
				Write("static const ");
				WriteNumericType(FuId.ByteRange);
				WriteChar(' ');
				WriteResource(name, -1);
				WriteChar('[');
				VisitLiteralLong(content.Count);
				WriteLine("] = {");
				WriteChar('\t');
				WriteBytes(content);
				WriteLine(" };");
			}
		}

		public override void WriteProgram(FuProgram program)
		{
			this.WrittenClasses.Clear();
			this.InHeaderFile = true;
			OpenStringWriter();
			foreach (FuClass klass in program.Classes) {
				WriteNewDelete(klass, false);
				WriteSignatures(klass, true);
			}
			CreateHeaderFile(".h");
			WriteLine("#ifdef __cplusplus");
			WriteLine("extern \"C\" {");
			WriteLine("#endif");
			WriteTypedefs(program, true);
			CloseStringWriter();
			WriteNewLine();
			WriteLine("#ifdef __cplusplus");
			WriteCharLine('}');
			WriteLine("#endif");
			CloseFile();
			this.InHeaderFile = false;
			this.IntTryParse = false;
			this.LongTryParse = false;
			this.DoubleTryParse = false;
			this.StringAssign = false;
			this.StringSubstring = false;
			this.StringAppend = false;
			this.StringIndexOf = false;
			this.StringLastIndexOf = false;
			this.StringEndsWith = false;
			this.StringReplace = false;
			this.StringFormat = false;
			this.MatchFind = false;
			this.MatchPos = false;
			this.PtrConstruct = false;
			this.SharedMake = false;
			this.SharedAddRef = false;
			this.SharedRelease = false;
			this.SharedAssign = false;
			this.ListFrees.Clear();
			this.TreeCompareInteger = false;
			this.TreeCompareString = false;
			this.Compares.Clear();
			this.Contains.Clear();
			OpenStringWriter();
			foreach (FuClass klass in program.Classes)
				WriteClass(klass, program);
			WriteResources(program.Resources);
			foreach (FuClass klass in program.Classes) {
				this.CurrentClass = klass;
				WriteConstructor(klass);
				WriteDestructor(klass);
				WriteNewDelete(klass, true);
				WriteMethods(klass);
			}
			Include("stdlib.h");
			CreateImplementationFile(program, ".h");
			WriteLibrary();
			WriteRegexOptionsEnum(program);
			WriteTypedefs(program, false);
			CloseStringWriter();
			CloseFile();
		}
	}

	public class GenCl : GenC
	{

		bool StringLength;

		bool StringEquals;

		bool StringStartsWith;

		bool BytesEqualsString;

		protected override string GetTargetName() => "OpenCL C";

		protected override void IncludeStdBool()
		{
		}

		protected override void IncludeMath()
		{
		}

		protected override void WriteNumericType(FuId id)
		{
			switch (id) {
			case FuId.SByteRange:
				Write("char");
				break;
			case FuId.ByteRange:
				Write("uchar");
				break;
			case FuId.ShortRange:
				Write("short");
				break;
			case FuId.UShortRange:
				Write("ushort");
				break;
			case FuId.IntType:
				Write("int");
				break;
			case FuId.LongType:
				Write("long");
				break;
			default:
				throw new NotImplementedException();
			}
		}

		protected override void WriteStringPtrType()
		{
			Write("constant char *");
		}

		protected override void WriteClassType(FuClassType klass, bool space)
		{
			switch (klass.Class.Id) {
			case FuId.None:
				if (klass is FuDynamicPtrType)
					NotSupported(klass, "Dynamic reference");
				else
					base.WriteClassType(klass, space);
				break;
			case FuId.StringClass:
				if (klass.Id == FuId.StringStorageType)
					NotSupported(klass, "string()");
				else
					WriteStringPtrType();
				break;
			default:
				NotSupported(klass, klass.Class.Name);
				break;
			}
		}

		protected override void WritePrintfLongPrefix()
		{
			WriteChar('l');
		}

		protected override void WriteInterpolatedStringArgBase(FuExpr expr)
		{
			expr.Accept(this, FuPriority.Argument);
		}

		internal override void VisitInterpolatedString(FuInterpolatedString expr, FuPriority parent)
		{
			NotSupported(expr, "Interpolated strings");
		}

		protected override void WriteCamelCaseNotKeyword(string name)
		{
			switch (name) {
			case "Constant":
			case "Global":
			case "Kernel":
			case "Local":
			case "Private":
			case "constant":
			case "global":
			case "kernel":
			case "local":
			case "private":
				WriteCamelCase(name);
				WriteChar('_');
				break;
			default:
				base.WriteCamelCaseNotKeyword(name);
				break;
			}
		}

		protected override string GetConst(FuArrayStorageType array) => array.PtrTaken ? "const " : "constant ";

		protected override void WriteSubstringEqual(FuCallExpr call, string literal, FuPriority parent, bool not)
		{
			if (not)
				WriteChar('!');
			if (IsUTF8GetString(call)) {
				this.BytesEqualsString = true;
				Write("FuBytes_Equals(");
			}
			else {
				this.StringStartsWith = true;
				Write("FuString_StartsWith(");
			}
			WriteStringPtrAdd(call);
			Write(", ");
			VisitLiteralString(literal);
			WriteChar(')');
		}

		protected override void WriteEqualStringInternal(FuExpr left, FuExpr right, FuPriority parent, bool not)
		{
			this.StringEquals = true;
			if (not)
				WriteChar('!');
			WriteCall("FuString_Equals", left, right);
		}

		protected override void WriteStringLength(FuExpr expr)
		{
			this.StringLength = true;
			WriteCall("strlen", expr);
		}

		void WriteConsoleWrite(List<FuExpr> args, bool newLine)
		{
			Write("printf(");
			if (args.Count == 0)
				Write("\"\\n\")");
			else if (args[0] is FuInterpolatedString interpolated)
				WritePrintf(interpolated, newLine);
			else
				WritePrintfNotInterpolated(args, newLine);
		}

		protected override void WriteCallExpr(FuExpr obj, FuMethod method, List<FuExpr> args, FuPriority parent)
		{
			switch (method.Id) {
			case FuId.None:
			case FuId.ClassToString:
				WriteCCall(obj, method, args);
				break;
			case FuId.EnumFromInt:
				WriteStaticCast(method.Type, args[0]);
				break;
			case FuId.EnumHasFlag:
				WriteEnumHasFlag(obj, args, parent);
				break;
			case FuId.StringStartsWith:
				int c = GetOneAscii(args[0]);
				if (c >= 0) {
					if (parent > FuPriority.Equality)
						WriteChar('(');
					WritePostfix(obj, "[0] == ");
					VisitLiteralChar(c);
					if (parent > FuPriority.Equality)
						WriteChar(')');
				}
				else {
					this.StringStartsWith = true;
					WriteCall("FuString_StartsWith", obj, args[0]);
				}
				break;
			case FuId.StringSubstring:
				WriteStringSubstring(obj, args, parent);
				break;
			case FuId.ArrayCopyTo:
				Write("for (size_t _i = 0; _i < ");
				args[3].Accept(this, FuPriority.Rel);
				WriteLine("; _i++)");
				WriteChar('\t');
				args[1].Accept(this, FuPriority.Primary);
				WriteChar('[');
				StartAdd(args[2]);
				Write("_i] = ");
				obj.Accept(this, FuPriority.Primary);
				WriteChar('[');
				StartAdd(args[0]);
				Write("_i]");
				break;
			case FuId.ArrayFillAll:
			case FuId.ArrayFillPart:
				WriteArrayFill(obj, args);
				break;
			case FuId.ConsoleWrite:
				WriteConsoleWrite(args, false);
				break;
			case FuId.ConsoleWriteLine:
				WriteConsoleWrite(args, true);
				break;
			case FuId.UTF8GetByteCount:
				WriteStringLength(args[0]);
				break;
			case FuId.UTF8GetBytes:
				Write("for (size_t _i = 0; ");
				args[0].Accept(this, FuPriority.Primary);
				WriteLine("[_i] != '\\0'; _i++)");
				WriteChar('\t');
				args[1].Accept(this, FuPriority.Primary);
				WriteChar('[');
				StartAdd(args[2]);
				Write("_i] = ");
				WritePostfix(args[0], "[_i]");
				break;
			case FuId.MathMethod:
			case FuId.MathClamp:
			case FuId.MathIsFinite:
			case FuId.MathIsNaN:
			case FuId.MathLog2:
			case FuId.MathMaxInt:
			case FuId.MathMinInt:
			case FuId.MathRound:
				WriteLowercase(method.Name);
				WriteArgsInParentheses(method, args);
				break;
			case FuId.MathAbs:
				if (args[0].Type is FuFloatingType)
					WriteChar('f');
				WriteCall("abs", args[0]);
				break;
			case FuId.MathCeiling:
				WriteCall("ceil", args[0]);
				break;
			case FuId.MathFusedMultiplyAdd:
				WriteCall("fma", args[0], args[1], args[2]);
				break;
			case FuId.MathIsInfinity:
				WriteCall("isinf", args[0]);
				break;
			case FuId.MathMaxDouble:
				WriteCall("fmax", args[0], args[1]);
				break;
			case FuId.MathMinDouble:
				WriteCall("fmin", args[0], args[1]);
				break;
			case FuId.MathTruncate:
				WriteCall("trunc", args[0]);
				break;
			default:
				NotSupported(obj, method.Name);
				break;
			}
		}

		protected override void WriteAssert(FuAssert statement)
		{
		}

		protected override void WriteSwitchCaseBody(List<FuStatement> statements)
		{
			if (statements.TrueForAll(statement => statement is FuAssert))
				WriteCharLine(';');
			else
				base.WriteSwitchCaseBody(statements);
		}

		void WriteLibrary()
		{
			if (this.StringLength) {
				WriteNewLine();
				WriteLine("static int strlen(constant char *str)");
				OpenBlock();
				WriteLine("int len = 0;");
				WriteLine("while (str[len] != '\\0')");
				WriteLine("\tlen++;");
				WriteLine("return len;");
				CloseBlock();
			}
			if (this.StringEquals) {
				WriteNewLine();
				WriteLine("static bool FuString_Equals(constant char *str1, constant char *str2)");
				OpenBlock();
				WriteLine("for (size_t i = 0; str1[i] == str2[i]; i++) {");
				WriteLine("\tif (str1[i] == '\\0')");
				WriteLine("\t\treturn true;");
				WriteCharLine('}');
				WriteLine("return false;");
				CloseBlock();
			}
			if (this.StringStartsWith) {
				WriteNewLine();
				WriteLine("static bool FuString_StartsWith(constant char *str1, constant char *str2)");
				OpenBlock();
				WriteLine("for (int i = 0; str2[i] != '\\0'; i++) {");
				WriteLine("\tif (str1[i] != str2[i])");
				WriteLine("\t\treturn false;");
				WriteCharLine('}');
				WriteLine("return true;");
				CloseBlock();
			}
			if (this.BytesEqualsString) {
				WriteNewLine();
				WriteLine("static bool FuBytes_Equals(const uchar *mem, constant char *str)");
				OpenBlock();
				WriteLine("for (int i = 0; str[i] != '\\0'; i++) {");
				WriteLine("\tif (mem[i] != str[i])");
				WriteLine("\t\treturn false;");
				WriteCharLine('}');
				WriteLine("return true;");
				CloseBlock();
			}
		}

		public override void WriteProgram(FuProgram program)
		{
			this.WrittenClasses.Clear();
			this.StringLength = false;
			this.StringEquals = false;
			this.StringStartsWith = false;
			this.BytesEqualsString = false;
			OpenStringWriter();
			foreach (FuClass klass in program.Classes) {
				this.CurrentClass = klass;
				WriteConstructor(klass);
				WriteDestructor(klass);
				WriteMethods(klass);
			}
			CreateOutputFile();
			WriteTopLevelNatives(program);
			WriteRegexOptionsEnum(program);
			WriteTypedefs(program, true);
			foreach (FuClass klass in program.Classes)
				WriteSignatures(klass, true);
			WriteTypedefs(program, false);
			foreach (FuClass klass in program.Classes)
				WriteClass(klass, program);
			WriteResources(program.Resources);
			WriteLibrary();
			CloseStringWriter();
			CloseFile();
		}
	}

	public class GenCpp : GenCCpp
	{

		bool UsingStringViewLiterals;

		bool HasEnumFlags;

		bool StringReplace;

		protected override string GetTargetName() => "C++";

		protected override void IncludeStdInt()
		{
			Include("cstdint");
		}

		protected override void IncludeAssert()
		{
			Include("cassert");
		}

		protected override void IncludeMath()
		{
			Include("cmath");
		}

		internal override void VisitLiteralNull()
		{
			Write("nullptr");
		}

		void StartMethodCall(FuExpr obj)
		{
			obj.Accept(this, FuPriority.Primary);
			WriteMemberOp(obj, null);
		}

		protected override void WriteInterpolatedStringArg(FuExpr expr)
		{
			if (expr.Type is FuClassType klass && klass.Class.Id != FuId.StringClass) {
				StartMethodCall(expr);
				Write("toString()");
			}
			else
				base.WriteInterpolatedStringArg(expr);
		}

		internal override void VisitInterpolatedString(FuInterpolatedString expr, FuPriority parent)
		{
			Include("format");
			Write("std::format(\"");
			foreach (FuInterpolatedPart part in expr.Parts) {
				WriteDoubling(part.Prefix, '{');
				WriteChar('{');
				WritePyFormat(part);
			}
			WriteDoubling(expr.Suffix, '{');
			WriteChar('"');
			WriteInterpolatedStringArgs(expr);
			WriteChar(')');
		}

		void WriteCamelCaseNotKeyword(string name)
		{
			WriteCamelCase(name);
			switch (name) {
			case "And":
			case "Asm":
			case "Auto":
			case "Bool":
			case "Break":
			case "Byte":
			case "Case":
			case "Catch":
			case "Char":
			case "Class":
			case "Const":
			case "Continue":
			case "Default":
			case "Delete":
			case "Do":
			case "Double":
			case "Else":
			case "Enum":
			case "Explicit":
			case "Export":
			case "Extern":
			case "False":
			case "Float":
			case "For":
			case "Goto":
			case "If":
			case "Inline":
			case "Int":
			case "Long":
			case "Namespace":
			case "New":
			case "Not":
			case "Nullptr":
			case "Operator":
			case "Or":
			case "Override":
			case "Private":
			case "Protected":
			case "Public":
			case "Register":
			case "Return":
			case "Short":
			case "Signed":
			case "Sizeof":
			case "Static":
			case "Struct":
			case "Switch":
			case "Throw":
			case "True":
			case "Try":
			case "Typedef":
			case "Union":
			case "Unsigned":
			case "Using":
			case "Virtual":
			case "Void":
			case "Volatile":
			case "While":
			case "Xor":
			case "and":
			case "asm":
			case "auto":
			case "catch":
			case "char":
			case "delete":
			case "explicit":
			case "export":
			case "extern":
			case "goto":
			case "inline":
			case "namespace":
			case "not":
			case "nullptr":
			case "operator":
			case "or":
			case "private":
			case "register":
			case "signed":
			case "sizeof":
			case "struct":
			case "try":
			case "typedef":
			case "union":
			case "unsigned":
			case "using":
			case "volatile":
			case "xor":
				WriteChar('_');
				break;
			default:
				break;
			}
		}

		protected override void WriteName(FuSymbol symbol)
		{
			switch (symbol) {
			case FuContainerType _:
				Write(symbol.Name);
				break;
			case FuVar _:
			case FuMember _:
				WriteCamelCaseNotKeyword(symbol.Name);
				break;
			default:
				throw new NotImplementedException();
			}
		}

		protected override void WriteLocalName(FuSymbol symbol, FuPriority parent)
		{
			if (symbol is FuField)
				Write("this->");
			WriteName(symbol);
		}

		void WriteCollectionType(string name, FuType elementType)
		{
			Include(name);
			Write("std::");
			Write(name);
			WriteChar('<');
			WriteType(elementType, false);
			WriteChar('>');
		}

		void WriteClassType(FuClassType klass)
		{
			if (klass.Class.TypeParameterCount == 0) {
				if (!(klass is FuReadWriteClassType))
					Write("const ");
				switch (klass.Class.Id) {
				case FuId.TextWriterClass:
					Include("iostream");
					Write("std::ostream");
					break;
				case FuId.StringWriterClass:
					Include("sstream");
					Write("std::ostringstream");
					break;
				case FuId.RegexClass:
					Include("regex");
					Write("std::regex");
					break;
				case FuId.MatchClass:
					Include("regex");
					Write("std::cmatch");
					break;
				case FuId.LockClass:
					Include("mutex");
					Write("std::recursive_mutex");
					break;
				default:
					Write(klass.Class.Name);
					break;
				}
			}
			else {
				string cppType;
				switch (klass.Class.Id) {
				case FuId.ArrayStorageClass:
					cppType = "array";
					break;
				case FuId.ListClass:
					cppType = "vector";
					break;
				case FuId.QueueClass:
					cppType = "queue";
					break;
				case FuId.StackClass:
					cppType = "stack";
					break;
				case FuId.HashSetClass:
					cppType = "unordered_set";
					break;
				case FuId.SortedSetClass:
					cppType = "set";
					break;
				case FuId.DictionaryClass:
					cppType = "unordered_map";
					break;
				case FuId.SortedDictionaryClass:
					cppType = "map";
					break;
				default:
					NotSupported(klass, klass.Class.Name);
					cppType = "NOT_SUPPORTED";
					break;
				}
				Include(cppType);
				if (!(klass is FuReadWriteClassType))
					Write("const ");
				Write("std::");
				Write(cppType);
				WriteChar('<');
				WriteType(klass.TypeArg0, false);
				if (klass is FuArrayStorageType arrayStorage) {
					Write(", ");
					VisitLiteralLong(arrayStorage.Length);
				}
				else if (klass.Class.TypeParameterCount == 2) {
					Write(", ");
					WriteType(klass.GetValueType(), false);
				}
				WriteChar('>');
			}
		}

		protected override void WriteType(FuType type, bool promote)
		{
			switch (type) {
			case FuIntegerType _:
				WriteNumericType(GetTypeId(type, promote));
				break;
			case FuStringStorageType _:
				Include("string");
				Write("std::string");
				break;
			case FuStringType _:
				Include("string_view");
				Write("std::string_view");
				break;
			case FuDynamicPtrType dynamic:
				switch (dynamic.Class.Id) {
				case FuId.RegexClass:
					Include("regex");
					Write("std::regex");
					break;
				case FuId.ArrayPtrClass:
					Include("memory");
					Write("std::shared_ptr<");
					WriteType(dynamic.GetElementType(), false);
					Write("[]>");
					break;
				default:
					Include("memory");
					Write("std::shared_ptr<");
					WriteClassType(dynamic);
					WriteChar('>');
					break;
				}
				break;
			case FuClassType klass:
				if (klass.Class.Id == FuId.ArrayPtrClass) {
					WriteType(klass.GetElementType(), false);
					if (!(klass is FuReadWriteClassType))
						Write(" const");
				}
				else
					WriteClassType(klass);
				if (!(klass is FuStorageType))
					Write(" *");
				break;
			default:
				Write(type.Name);
				break;
			}
		}

		protected override void WriteNewArray(FuType elementType, FuExpr lengthExpr, FuPriority parent)
		{
			Include("memory");
			Write("std::make_shared<");
			WriteType(elementType, false);
			Write("[]>(");
			lengthExpr.Accept(this, FuPriority.Argument);
			WriteChar(')');
		}

		protected override void WriteNew(FuReadWriteClassType klass, FuPriority parent)
		{
			Include("memory");
			Write("std::make_shared<");
			WriteClassType(klass);
			Write(">()");
		}

		protected override void WriteStorageInit(FuNamedValue def)
		{
		}

		protected override void WriteVarInit(FuNamedValue def)
		{
			if (def.Value != null && def.Type.Id == FuId.StringStorageType) {
				WriteChar('{');
				def.Value.Accept(this, FuPriority.Argument);
				WriteChar('}');
			}
			else if (def.Type is FuArrayStorageType) {
				switch (def.Value) {
				case null:
					break;
				case FuLiteral literal when literal.IsDefaultValue():
					Write(" {}");
					break;
				default:
					throw new NotImplementedException();
				}
			}
			else
				base.WriteVarInit(def);
		}

		static bool IsSharedPtr(FuExpr expr)
		{
			if (expr.Type is FuDynamicPtrType)
				return true;
			return expr is FuSymbolReference symbol && symbol.Symbol.Parent is FuForeach loop && loop.Collection.Type.AsClassType().GetElementType() is FuDynamicPtrType;
		}

		protected override void WriteStaticCast(FuType type, FuExpr expr)
		{
			if (type is FuDynamicPtrType dynamic) {
				Write("std::static_pointer_cast<");
				Write(dynamic.Class.Name);
			}
			else {
				Write("static_cast<");
				WriteType(type, false);
			}
			Write(">(");
			if (expr.Type is FuStorageType) {
				WriteChar('&');
				expr.Accept(this, FuPriority.Primary);
			}
			else if (!(type is FuDynamicPtrType) && IsSharedPtr(expr))
				WritePostfix(expr, ".get()");
			else
				GetStaticCastInner(type, expr).Accept(this, FuPriority.Argument);
			WriteChar(')');
		}

		static bool NeedStringPtrData(FuExpr expr)
		{
			if (expr is FuCallExpr call && call.Method.Symbol.Id == FuId.EnvironmentGetEnvironmentVariable)
				return false;
			return expr.Type.Id == FuId.StringPtrType;
		}

		protected override void WriteEqual(FuExpr left, FuExpr right, FuPriority parent, bool not)
		{
			if (NeedStringPtrData(left) && right.Type.Id == FuId.NullType) {
				WritePostfix(left, ".data()");
				Write(GetEqOp(not));
				Write("nullptr");
			}
			else if (left.Type.Id == FuId.NullType && NeedStringPtrData(right)) {
				Write("nullptr");
				Write(GetEqOp(not));
				WritePostfix(right, ".data()");
			}
			else
				base.WriteEqual(left, right, parent, not);
		}

		static bool IsClassPtr(FuType type) => type is FuClassType ptr && !(type is FuStorageType) && ptr.Class.Id != FuId.StringClass && ptr.Class.Id != FuId.ArrayPtrClass;

		static bool IsCppPtr(FuExpr expr)
		{
			if (IsClassPtr(expr.Type)) {
				if (expr is FuSymbolReference symbol && symbol.Symbol.Parent is FuForeach loop && (symbol.Symbol == loop.GetVar() ? loop.Collection.Type.AsClassType().TypeArg0 : loop.Collection.Type.AsClassType().TypeArg1) is FuStorageType)
					return false;
				return true;
			}
			return false;
		}

		protected override void WriteIndexingExpr(FuBinaryExpr expr, FuPriority parent)
		{
			FuClassType klass = (FuClassType) expr.Left.Type;
			if (parent != FuPriority.Assign) {
				switch (klass.Class.Id) {
				case FuId.DictionaryClass:
				case FuId.SortedDictionaryClass:
				case FuId.OrderedDictionaryClass:
					StartMethodCall(expr.Left);
					Write("find(");
					WriteStronglyCoerced(klass.GetKeyType(), expr.Right);
					Write(")->second");
					return;
				default:
					break;
				}
			}
			if (IsClassPtr(expr.Left.Type)) {
				Write("(*");
				expr.Left.Accept(this, FuPriority.Primary);
				WriteChar(')');
			}
			else
				expr.Left.Accept(this, FuPriority.Primary);
			WriteChar('[');
			switch (klass.Class.Id) {
			case FuId.ArrayPtrClass:
			case FuId.ArrayStorageClass:
			case FuId.ListClass:
				expr.Right.Accept(this, FuPriority.Argument);
				break;
			default:
				WriteStronglyCoerced(klass.GetKeyType(), expr.Right);
				break;
			}
			WriteChar(']');
		}

		protected override void WriteMemberOp(FuExpr left, FuSymbolReference symbol)
		{
			if (symbol != null && symbol.Symbol is FuConst)
				Write("::");
			else if (IsCppPtr(left))
				Write("->");
			else
				WriteChar('.');
		}

		protected override void WriteEnumAsInt(FuExpr expr, FuPriority parent)
		{
			Write("static_cast<int>(");
			expr.Accept(this, FuPriority.Argument);
			WriteChar(')');
		}

		void WriteCollectionObject(FuExpr obj, FuPriority priority)
		{
			if (IsCppPtr(obj)) {
				WriteChar('*');
				obj.Accept(this, FuPriority.Primary);
			}
			else
				obj.Accept(this, priority);
		}

		void WriteBeginEnd(FuExpr obj)
		{
			StartMethodCall(obj);
			Write("begin(), ");
			StartMethodCall(obj);
			Write("end()");
		}

		void WritePtrRange(FuExpr obj, FuExpr index, FuExpr count)
		{
			WriteArrayPtrAdd(obj, index);
			Write(", ");
			WriteArrayPtrAdd(obj, index);
			Write(" + ");
			count.Accept(this, FuPriority.Mul);
		}

		void WriteNotRawStringLiteral(FuExpr obj, FuPriority priority)
		{
			obj.Accept(this, priority);
			if (obj is FuLiteralString) {
				Include("string_view");
				this.UsingStringViewLiterals = true;
				Write("sv");
			}
		}

		void WriteStringMethod(FuExpr obj, string name, FuMethod method, List<FuExpr> args)
		{
			WriteNotRawStringLiteral(obj, FuPriority.Primary);
			WriteChar('.');
			Write(name);
			int c = GetOneAscii(args[0]);
			if (c >= 0) {
				WriteChar('(');
				VisitLiteralChar(c);
				WriteChar(')');
			}
			else
				WriteArgsInParentheses(method, args);
		}

		void WriteAllAnyContains(string function, FuExpr obj, List<FuExpr> args)
		{
			Include("algorithm");
			Write("std::");
			Write(function);
			WriteChar('(');
			WriteBeginEnd(obj);
			Write(", ");
			args[0].Accept(this, FuPriority.Argument);
			WriteChar(')');
		}

		void WriteCString(FuExpr expr)
		{
			if (expr is FuLiteralString)
				expr.Accept(this, FuPriority.Argument);
			else
				WritePostfix(expr, ".data()");
		}

		void WriteRegex(List<FuExpr> args, int argIndex)
		{
			Include("regex");
			Write("std::regex(");
			args[argIndex].Accept(this, FuPriority.Argument);
			WriteRegexOptions(args, ", std::regex::ECMAScript | ", " | ", "", "std::regex::icase", "std::regex::multiline", "std::regex::NOT_SUPPORTED_singleline");
			WriteChar(')');
		}

		void WriteWrite(List<FuExpr> args, bool newLine)
		{
			Include("iostream");
			if (args.Count == 1) {
				if (args[0] is FuInterpolatedString interpolated) {
					bool uppercase = false;
					bool hex = false;
					int flt = 'G';
					foreach (FuInterpolatedPart part in interpolated.Parts) {
						switch (part.Format) {
						case 'E':
						case 'G':
						case 'X':
							if (!uppercase) {
								Write(" << std::uppercase");
								uppercase = true;
							}
							break;
						case 'e':
						case 'g':
						case 'x':
							if (uppercase) {
								Write(" << std::nouppercase");
								uppercase = false;
							}
							break;
						default:
							break;
						}
						switch (part.Format) {
						case 'E':
						case 'e':
							if (flt != 'E') {
								Write(" << std::scientific");
								flt = 'E';
							}
							break;
						case 'F':
						case 'f':
							if (flt != 'F') {
								Write(" << std::fixed");
								flt = 'F';
							}
							break;
						case 'X':
						case 'x':
							if (!hex) {
								Write(" << std::hex");
								hex = true;
							}
							break;
						default:
							if (hex) {
								Write(" << std::dec");
								hex = false;
							}
							if (flt != 'G') {
								Write(" << std::defaultfloat");
								flt = 'G';
							}
							break;
						}
						if (part.Prefix.Length > 0) {
							Write(" << ");
							VisitLiteralString(part.Prefix);
						}
						Write(" << ");
						part.Argument.Accept(this, FuPriority.Mul);
					}
					if (uppercase)
						Write(" << std::nouppercase");
					if (hex)
						Write(" << std::dec");
					if (flt != 'G')
						Write(" << std::defaultfloat");
					if (interpolated.Suffix.Length > 0) {
						Write(" << ");
						if (newLine) {
							WriteStringLiteralWithNewLine(interpolated.Suffix);
							return;
						}
						VisitLiteralString(interpolated.Suffix);
					}
				}
				else {
					Write(" << ");
					if (newLine && args[0] is FuLiteralString literal) {
						WriteStringLiteralWithNewLine(literal.Value);
						return;
					}
					else if (args[0] is FuLiteralChar)
						WriteCall("static_cast<int>", args[0]);
					else
						args[0].Accept(this, FuPriority.Mul);
				}
			}
			if (newLine)
				Write(" << '\\n'");
		}

		void WriteRegexArgument(FuExpr expr)
		{
			if (expr.Type is FuDynamicPtrType)
				expr.Accept(this, FuPriority.Argument);
			else {
				WriteChar('*');
				expr.Accept(this, FuPriority.Primary);
			}
		}

		protected override void WriteCallExpr(FuExpr obj, FuMethod method, List<FuExpr> args, FuPriority parent)
		{
			switch (method.Id) {
			case FuId.None:
			case FuId.ClassToString:
			case FuId.ListClear:
			case FuId.StackPush:
			case FuId.HashSetClear:
			case FuId.HashSetContains:
			case FuId.SortedSetClear:
			case FuId.SortedSetContains:
			case FuId.DictionaryClear:
			case FuId.SortedDictionaryClear:
				if (obj != null) {
					if (IsReferenceTo(obj, FuId.BasePtr)) {
						WriteName(method.Parent);
						Write("::");
					}
					else {
						obj.Accept(this, FuPriority.Primary);
						if (method.CallType == FuCallType.Static)
							Write("::");
						else
							WriteMemberOp(obj, null);
					}
				}
				WriteName(method);
				WriteArgsInParentheses(method, args);
				break;
			case FuId.EnumFromInt:
				WriteStaticCast(method.Type, args[0]);
				break;
			case FuId.EnumHasFlag:
				WriteEnumHasFlag(obj, args, parent);
				break;
			case FuId.IntTryParse:
			case FuId.LongTryParse:
				Include("cstdlib");
				Write("[&] { char *ciend; ");
				obj.Accept(this, FuPriority.Assign);
				Write(" = std::strtol");
				if (method.Id == FuId.LongTryParse)
					WriteChar('l');
				WriteChar('(');
				WriteCString(args[0]);
				Write(", &ciend");
				WriteTryParseRadix(args);
				Write("); return *ciend == '\\0'; }()");
				break;
			case FuId.DoubleTryParse:
				Include("cstdlib");
				Write("[&] { char *ciend; ");
				obj.Accept(this, FuPriority.Assign);
				Write(" = std::strtod(");
				WriteCString(args[0]);
				Write(", &ciend); return *ciend == '\\0'; }()");
				break;
			case FuId.StringContains:
				if (parent > FuPriority.Equality)
					WriteChar('(');
				WriteStringMethod(obj, "find", method, args);
				Write(" != std::string::npos");
				if (parent > FuPriority.Equality)
					WriteChar(')');
				break;
			case FuId.StringEndsWith:
				WriteStringMethod(obj, "ends_with", method, args);
				break;
			case FuId.StringIndexOf:
				Write("static_cast<int>(");
				WriteStringMethod(obj, "find", method, args);
				WriteChar(')');
				break;
			case FuId.StringLastIndexOf:
				Write("static_cast<int>(");
				WriteStringMethod(obj, "rfind", method, args);
				WriteChar(')');
				break;
			case FuId.StringReplace:
				this.StringReplace = true;
				WriteCall("FuString_replace", obj, args[0], args[1]);
				break;
			case FuId.StringStartsWith:
				WriteStringMethod(obj, "starts_with", method, args);
				break;
			case FuId.StringSubstring:
				WriteStringMethod(obj, "substr", method, args);
				break;
			case FuId.ArrayBinarySearchAll:
			case FuId.ArrayBinarySearchPart:
				Include("algorithm");
				if (parent > FuPriority.Add)
					WriteChar('(');
				Write("std::lower_bound(");
				if (args.Count == 1)
					WriteBeginEnd(obj);
				else
					WritePtrRange(obj, args[1], args[2]);
				Write(", ");
				args[0].Accept(this, FuPriority.Argument);
				Write(") - ");
				WriteArrayPtr(obj, FuPriority.Mul);
				if (parent > FuPriority.Add)
					WriteChar(')');
				break;
			case FuId.ArrayContains:
			case FuId.ListContains:
				if (parent > FuPriority.Equality)
					WriteChar('(');
				WriteAllAnyContains("find", obj, args);
				Write(" != ");
				StartMethodCall(obj);
				Write("end()");
				if (parent > FuPriority.Equality)
					WriteChar(')');
				break;
			case FuId.ArrayCopyTo:
			case FuId.ListCopyTo:
				Include("algorithm");
				Write("std::copy_n(");
				WriteArrayPtrAdd(obj, args[0]);
				Write(", ");
				args[3].Accept(this, FuPriority.Argument);
				Write(", ");
				WriteArrayPtrAdd(args[1], args[2]);
				WriteChar(')');
				break;
			case FuId.ArrayFillAll:
				StartMethodCall(obj);
				Write("fill(");
				WriteCoerced(obj.Type.AsClassType().GetElementType(), args[0], FuPriority.Argument);
				WriteChar(')');
				break;
			case FuId.ArrayFillPart:
				Include("algorithm");
				Write("std::fill_n(");
				WriteArrayPtrAdd(obj, args[1]);
				Write(", ");
				args[2].Accept(this, FuPriority.Argument);
				Write(", ");
				WriteCoerced(obj.Type.AsClassType().GetElementType(), args[0], FuPriority.Argument);
				WriteChar(')');
				break;
			case FuId.ArraySortAll:
			case FuId.ListSortAll:
				Include("algorithm");
				Write("std::sort(");
				WriteBeginEnd(obj);
				WriteChar(')');
				break;
			case FuId.ArraySortPart:
			case FuId.ListSortPart:
				Include("algorithm");
				Write("std::sort(");
				WritePtrRange(obj, args[0], args[1]);
				WriteChar(')');
				break;
			case FuId.ListAdd:
				StartMethodCall(obj);
				if (args.Count == 0)
					Write("emplace_back()");
				else {
					Write("push_back(");
					WriteCoerced(obj.Type.AsClassType().GetElementType(), args[0], FuPriority.Argument);
					WriteChar(')');
				}
				break;
			case FuId.ListAddRange:
				StartMethodCall(obj);
				Write("insert(");
				StartMethodCall(obj);
				Write("end(), ");
				WriteBeginEnd(args[0]);
				WriteChar(')');
				break;
			case FuId.ListAll:
				WriteAllAnyContains("all_of", obj, args);
				break;
			case FuId.ListAny:
				Include("algorithm");
				WriteAllAnyContains("any_of", obj, args);
				break;
			case FuId.ListIndexOf:
				{
					FuType elementType = obj.Type.AsClassType().GetElementType();
					Write("[](const ");
					WriteCollectionType("vector", elementType);
					Write(" &v, ");
					WriteType(elementType, false);
					Include("algorithm");
					Write(" value) { auto i = std::find(v.begin(), v.end(), value); return i == v.end() ? -1 : i - v.begin(); }(");
					WriteCollectionObject(obj, FuPriority.Argument);
					Write(", ");
					WriteCoerced(elementType, args[0], FuPriority.Argument);
					WriteChar(')');
				}
				break;
			case FuId.ListInsert:
				StartMethodCall(obj);
				if (args.Count == 1) {
					Write("emplace(");
					WriteArrayPtrAdd(obj, args[0]);
				}
				else {
					Write("insert(");
					WriteArrayPtrAdd(obj, args[0]);
					Write(", ");
					WriteCoerced(obj.Type.AsClassType().GetElementType(), args[1], FuPriority.Argument);
				}
				WriteChar(')');
				break;
			case FuId.ListLast:
				StartMethodCall(obj);
				Write("back()");
				break;
			case FuId.ListRemoveAt:
				StartMethodCall(obj);
				Write("erase(");
				WriteArrayPtrAdd(obj, args[0]);
				WriteChar(')');
				break;
			case FuId.ListRemoveRange:
				StartMethodCall(obj);
				Write("erase(");
				WritePtrRange(obj, args[0], args[1]);
				WriteChar(')');
				break;
			case FuId.QueueClear:
			case FuId.StackClear:
				WriteCollectionObject(obj, FuPriority.Assign);
				Write(" = {}");
				break;
			case FuId.QueueDequeue:
				if (parent == FuPriority.Statement) {
					StartMethodCall(obj);
					Write("pop()");
				}
				else {
					FuType elementType = obj.Type.AsClassType().GetElementType();
					Write("[](");
					WriteCollectionType("queue", elementType);
					Write(" &q) { ");
					WriteType(elementType, false);
					Write(" front = q.front(); q.pop(); return front; }(");
					WriteCollectionObject(obj, FuPriority.Argument);
					WriteChar(')');
				}
				break;
			case FuId.QueueEnqueue:
				WriteMethodCall(obj, "push", args[0]);
				break;
			case FuId.QueuePeek:
				StartMethodCall(obj);
				Write("front()");
				break;
			case FuId.StackPeek:
				StartMethodCall(obj);
				Write("top()");
				break;
			case FuId.StackPop:
				if (parent == FuPriority.Statement) {
					StartMethodCall(obj);
					Write("pop()");
				}
				else {
					FuType elementType = obj.Type.AsClassType().GetElementType();
					Write("[](");
					WriteCollectionType("stack", elementType);
					Write(" &s) { ");
					WriteType(elementType, false);
					Write(" top = s.top(); s.pop(); return top; }(");
					WriteCollectionObject(obj, FuPriority.Argument);
					WriteChar(')');
				}
				break;
			case FuId.HashSetAdd:
			case FuId.SortedSetAdd:
				WriteMethodCall(obj, obj.Type.AsClassType().GetElementType().Id == FuId.StringStorageType && args[0].Type.Id == FuId.StringPtrType ? "emplace" : "insert", args[0]);
				break;
			case FuId.HashSetRemove:
			case FuId.SortedSetRemove:
			case FuId.DictionaryRemove:
			case FuId.SortedDictionaryRemove:
				WriteMethodCall(obj, "erase", args[0]);
				break;
			case FuId.DictionaryAdd:
				WriteIndexing(obj, args[0]);
				break;
			case FuId.DictionaryContainsKey:
			case FuId.SortedDictionaryContainsKey:
				if (parent > FuPriority.Equality)
					WriteChar('(');
				StartMethodCall(obj);
				Write("count(");
				WriteStronglyCoerced(obj.Type.AsClassType().GetKeyType(), args[0]);
				Write(") != 0");
				if (parent > FuPriority.Equality)
					WriteChar(')');
				break;
			case FuId.TextWriterWrite:
				WriteCollectionObject(obj, FuPriority.Shift);
				WriteWrite(args, false);
				break;
			case FuId.TextWriterWriteChar:
				WriteCollectionObject(obj, FuPriority.Shift);
				Write(" << ");
				if (args[0] is FuLiteralChar literalChar && literalChar.Value < 127)
					args[0].Accept(this, FuPriority.Mul);
				else
					WriteCall("static_cast<char>", args[0]);
				break;
			case FuId.TextWriterWriteCodePoint:
				if (args[0] is FuLiteralChar literalChar2 && literalChar2.Value < 127) {
					WriteCollectionObject(obj, FuPriority.Shift);
					Write(" << ");
					args[0].Accept(this, FuPriority.Mul);
				}
				else {
					Write("if (");
					args[0].Accept(this, FuPriority.Rel);
					WriteLine(" < 0x80)");
					WriteChar('\t');
					WriteCollectionObject(obj, FuPriority.Shift);
					Write(" << ");
					WriteCall("static_cast<char>", args[0]);
					WriteCharLine(';');
					Write("else if (");
					args[0].Accept(this, FuPriority.Rel);
					WriteLine(" < 0x800)");
					WriteChar('\t');
					WriteCollectionObject(obj, FuPriority.Shift);
					Write(" << static_cast<char>(0xc0 | ");
					args[0].Accept(this, FuPriority.Shift);
					Write(" >> 6) << static_cast<char>(0x80 | (");
					args[0].Accept(this, FuPriority.And);
					WriteLine(" & 0x3f));");
					Write("else if (");
					args[0].Accept(this, FuPriority.Rel);
					WriteLine(" < 0x10000)");
					WriteChar('\t');
					WriteCollectionObject(obj, FuPriority.Shift);
					Write(" << static_cast<char>(0xe0 | ");
					args[0].Accept(this, FuPriority.Shift);
					Write(" >> 12) << static_cast<char>(0x80 | (");
					args[0].Accept(this, FuPriority.Shift);
					Write(" >> 6 & 0x3f)) << static_cast<char>(0x80 | (");
					args[0].Accept(this, FuPriority.And);
					WriteLine(" & 0x3f));");
					WriteLine("else");
					WriteChar('\t');
					WriteCollectionObject(obj, FuPriority.Shift);
					Write(" << static_cast<char>(0xf0 | ");
					args[0].Accept(this, FuPriority.Shift);
					Write(" >> 18) << static_cast<char>(0x80 | (");
					args[0].Accept(this, FuPriority.Shift);
					Write(" >> 12 & 0x3f)) << static_cast<char>(0x80 | (");
					args[0].Accept(this, FuPriority.Shift);
					Write(" >> 6 & 0x3f)) << static_cast<char>(0x80 | (");
					args[0].Accept(this, FuPriority.And);
					Write(" & 0x3f))");
				}
				break;
			case FuId.TextWriterWriteLine:
				WriteCollectionObject(obj, FuPriority.Shift);
				WriteWrite(args, true);
				break;
			case FuId.StringWriterClear:
				Include("string");
				StartMethodCall(obj);
				Write("str(std::string())");
				break;
			case FuId.ConsoleWrite:
				Write("std::cout");
				WriteWrite(args, false);
				break;
			case FuId.ConsoleWriteLine:
				Write("std::cout");
				WriteWrite(args, true);
				break;
			case FuId.StringWriterToString:
				StartMethodCall(obj);
				Write("str()");
				break;
			case FuId.UTF8GetByteCount:
				if (args[0] is FuLiteral) {
					if (parent > FuPriority.Add)
						WriteChar('(');
					Write("sizeof(");
					args[0].Accept(this, FuPriority.Argument);
					Write(") - 1");
					if (parent > FuPriority.Add)
						WriteChar(')');
				}
				else
					WriteStringLength(args[0]);
				break;
			case FuId.UTF8GetBytes:
				if (args[0] is FuLiteral) {
					Include("algorithm");
					Write("std::copy_n(");
					args[0].Accept(this, FuPriority.Argument);
					Write(", sizeof(");
					args[0].Accept(this, FuPriority.Argument);
					Write(") - 1, ");
					WriteArrayPtrAdd(args[1], args[2]);
					WriteChar(')');
				}
				else {
					WritePostfix(args[0], ".copy(reinterpret_cast<char *>(");
					WriteArrayPtrAdd(args[1], args[2]);
					Write("), ");
					WritePostfix(args[0], ".size())");
				}
				break;
			case FuId.UTF8GetString:
				Include("string_view");
				Write("std::string_view(reinterpret_cast<const char *>(");
				WriteArrayPtrAdd(args[0], args[1]);
				Write("), ");
				args[2].Accept(this, FuPriority.Argument);
				WriteChar(')');
				break;
			case FuId.EnvironmentGetEnvironmentVariable:
				Include("cstdlib");
				Write("std::getenv(");
				WriteCString(args[0]);
				WriteChar(')');
				break;
			case FuId.RegexCompile:
				WriteRegex(args, 0);
				break;
			case FuId.RegexIsMatchStr:
			case FuId.RegexIsMatchRegex:
			case FuId.MatchFindStr:
			case FuId.MatchFindRegex:
				Write("std::regex_search(");
				if (args[0].Type.Id == FuId.StringPtrType && !(args[0] is FuLiteral))
					WriteBeginEnd(args[0]);
				else
					args[0].Accept(this, FuPriority.Argument);
				if (method.Id == FuId.MatchFindStr || method.Id == FuId.MatchFindRegex) {
					Write(", ");
					obj.Accept(this, FuPriority.Argument);
				}
				Write(", ");
				if (method.Id == FuId.RegexIsMatchRegex)
					WriteRegexArgument(obj);
				else if (method.Id == FuId.MatchFindRegex)
					WriteRegexArgument(args[1]);
				else
					WriteRegex(args, 1);
				WriteChar(')');
				break;
			case FuId.MatchGetCapture:
				StartMethodCall(obj);
				WriteCall("str", args[0]);
				break;
			case FuId.MathMethod:
			case FuId.MathAbs:
			case FuId.MathIsFinite:
			case FuId.MathIsNaN:
			case FuId.MathLog2:
			case FuId.MathRound:
				IncludeMath();
				Write("std::");
				WriteLowercase(method.Name);
				WriteArgsInParentheses(method, args);
				break;
			case FuId.MathCeiling:
				IncludeMath();
				WriteCall("std::ceil", args[0]);
				break;
			case FuId.MathClamp:
				Include("algorithm");
				WriteCall("std::clamp", args[0], args[1], args[2]);
				break;
			case FuId.MathFusedMultiplyAdd:
				IncludeMath();
				WriteCall("std::fma", args[0], args[1], args[2]);
				break;
			case FuId.MathIsInfinity:
				IncludeMath();
				WriteCall("std::isinf", args[0]);
				break;
			case FuId.MathMaxInt:
			case FuId.MathMaxDouble:
				Include("algorithm");
				WriteCall("std::max", args[0], args[1]);
				break;
			case FuId.MathMinInt:
			case FuId.MathMinDouble:
				Include("algorithm");
				WriteCall("std::min", args[0], args[1]);
				break;
			case FuId.MathTruncate:
				IncludeMath();
				WriteCall("std::trunc", args[0]);
				break;
			default:
				NotSupported(obj, method.Name);
				break;
			}
		}

		protected override void WriteResource(string name, int length)
		{
			Write("FuResource::");
			WriteResourceName(name);
		}

		protected override void WriteArrayPtr(FuExpr expr, FuPriority parent)
		{
			switch (expr.Type) {
			case FuArrayStorageType _:
			case FuStringType _:
				WritePostfix(expr, ".data()");
				break;
			case FuDynamicPtrType _:
				WritePostfix(expr, ".get()");
				break;
			case FuClassType klass when klass.Class.Id == FuId.ListClass:
				StartMethodCall(expr);
				Write("begin()");
				break;
			default:
				expr.Accept(this, parent);
				break;
			}
		}

		protected override void WriteCoercedInternal(FuType type, FuExpr expr, FuPriority parent)
		{
			if (type is FuClassType klass && !(klass is FuDynamicPtrType) && !(klass is FuStorageType)) {
				if (klass.Class.Id == FuId.StringClass) {
					if (expr.Type.Id == FuId.NullType) {
						Include("string_view");
						Write("std::string_view()");
					}
					else
						expr.Accept(this, parent);
					return;
				}
				if (klass.Class.Id == FuId.ArrayPtrClass) {
					WriteArrayPtr(expr, parent);
					return;
				}
				if (IsSharedPtr(expr)) {
					if (klass.Class.Id == FuId.RegexClass) {
						WriteChar('&');
						expr.Accept(this, FuPriority.Primary);
					}
					else
						WritePostfix(expr, ".get()");
					return;
				}
				if (expr.Type is FuClassType && !IsCppPtr(expr)) {
					WriteChar('&');
					if (expr is FuCallExpr) {
						Write("static_cast<");
						if (!(klass is FuReadWriteClassType))
							Write("const ");
						WriteName(klass.Class);
						Write(" &>(");
						expr.Accept(this, FuPriority.Argument);
						WriteChar(')');
					}
					else
						expr.Accept(this, FuPriority.Primary);
					return;
				}
			}
			base.WriteCoercedInternal(type, expr, parent);
		}

		protected override void WriteSelectValues(FuType type, FuSelectExpr expr)
		{
			if (expr.OnTrue.Type is FuClassType trueClass && expr.OnFalse.Type is FuClassType falseClass && !trueClass.Class.IsSameOrBaseOf(falseClass.Class) && !falseClass.Class.IsSameOrBaseOf(trueClass.Class)) {
				WriteStaticCast(type, expr.OnTrue);
				Write(" : ");
				WriteStaticCast(type, expr.OnFalse);
			}
			else
				base.WriteSelectValues(type, expr);
		}

		protected override void WriteStringLength(FuExpr expr)
		{
			Write("std::ssize(");
			WriteNotRawStringLiteral(expr, FuPriority.Argument);
			WriteChar(')');
		}

		void WriteMatchProperty(FuSymbolReference expr, string name)
		{
			StartMethodCall(expr.Left);
			Write(name);
			Write("()");
		}

		internal override void VisitSymbolReference(FuSymbolReference expr, FuPriority parent)
		{
			switch (expr.Symbol.Id) {
			case FuId.ConsoleError:
				Write("std::cerr");
				break;
			case FuId.ListCount:
			case FuId.QueueCount:
			case FuId.StackCount:
			case FuId.HashSetCount:
			case FuId.SortedSetCount:
			case FuId.DictionaryCount:
			case FuId.SortedDictionaryCount:
			case FuId.OrderedDictionaryCount:
				Write("std::ssize(");
				WriteCollectionObject(expr.Left, FuPriority.Argument);
				WriteChar(')');
				break;
			case FuId.MatchStart:
				WriteMatchProperty(expr, "position");
				break;
			case FuId.MatchEnd:
				if (parent > FuPriority.Add)
					WriteChar('(');
				WriteMatchProperty(expr, "position");
				Write(" + ");
				WriteMatchProperty(expr, "length");
				if (parent > FuPriority.Add)
					WriteChar(')');
				break;
			case FuId.MatchLength:
				WriteMatchProperty(expr, "length");
				break;
			case FuId.MatchValue:
				WriteMatchProperty(expr, "str");
				break;
			default:
				base.VisitSymbolReference(expr, parent);
				break;
			}
		}

		void WriteGtRawPtr(FuExpr expr)
		{
			Write(">(");
			if (IsSharedPtr(expr))
				WritePostfix(expr, ".get()");
			else
				expr.Accept(this, FuPriority.Argument);
			WriteChar(')');
		}

		void WriteIsVar(FuExpr expr, FuVar def, FuPriority parent)
		{
			if (def.Name != "_") {
				if (parent > FuPriority.Assign)
					WriteChar('(');
				WriteName(def);
				Write(" = ");
			}
			if (def.Type is FuDynamicPtrType dynamic) {
				Write("std::dynamic_pointer_cast<");
				Write(dynamic.Class.Name);
				WriteCall(">", expr);
			}
			else {
				Write("dynamic_cast<");
				WriteType(def.Type, true);
				WriteGtRawPtr(expr);
			}
			if (def.Name != "_" && parent > FuPriority.Assign)
				WriteChar(')');
		}

		internal override void VisitBinaryExpr(FuBinaryExpr expr, FuPriority parent)
		{
			switch (expr.Op) {
			case FuToken.Plus:
				if (expr.Type.Id == FuId.StringStorageType) {
					if (parent > FuPriority.Add)
						WriteChar('(');
					WriteStronglyCoerced(expr.Type, expr.Left);
					Write(" + ");
					WriteStronglyCoerced(expr.Type, expr.Right);
					if (parent > FuPriority.Add)
						WriteChar(')');
					return;
				}
				break;
			case FuToken.Equal:
			case FuToken.NotEqual:
			case FuToken.Greater:
				FuExpr str = IsStringEmpty(expr);
				if (str != null) {
					if (expr.Op != FuToken.Equal)
						WriteChar('!');
					WritePostfix(str, ".empty()");
					return;
				}
				break;
			case FuToken.Assign:
				FuExpr length = IsTrimSubstring(expr);
				if (length != null && expr.Left.Type.Id == FuId.StringStorageType && parent == FuPriority.Statement) {
					WriteMethodCall(expr.Left, "resize", length);
					return;
				}
				break;
			case FuToken.Is:
				switch (expr.Right) {
				case FuSymbolReference symbol:
					if (parent == FuPriority.Select || (parent >= FuPriority.Or && parent <= FuPriority.Mul))
						Write("!!");
					Write("dynamic_cast<const ");
					Write(symbol.Symbol.Name);
					Write(" *");
					WriteGtRawPtr(expr.Left);
					return;
				case FuVar def:
					WriteIsVar(expr.Left, def, parent);
					return;
				default:
					throw new NotImplementedException();
				}
			default:
				break;
			}
			base.VisitBinaryExpr(expr, parent);
		}

		internal override void VisitLambdaExpr(FuLambdaExpr expr)
		{
			Write("[](const ");
			WriteType(expr.First.Type, false);
			Write(" &");
			WriteName(expr.First);
			Write(") { ");
			WriteTemporaries(expr.Body);
			Write("return ");
			expr.Body.Accept(this, FuPriority.Argument);
			Write("; }");
		}

		protected override void WriteUnreachable(FuAssert statement)
		{
			Include("cstdlib");
			Write("std::");
			base.WriteUnreachable(statement);
		}

		protected override void WriteConst(FuConst konst)
		{
			Write("static constexpr ");
			WriteTypeAndName(konst);
			Write(" = ");
			konst.Value.Accept(this, FuPriority.Argument);
			WriteCharLine(';');
		}

		internal override void VisitForeach(FuForeach statement)
		{
			FuVar element = statement.GetVar();
			Write("for (");
			FuClassType collectionType = (FuClassType) statement.Collection.Type;
			if (collectionType.Class.Id == FuId.StringClass) {
				WriteTypeAndName(element);
				Write(" : ");
				WriteNotRawStringLiteral(statement.Collection, FuPriority.Argument);
			}
			else {
				if (statement.Count() == 2) {
					Write("const auto &[");
					WriteCamelCaseNotKeyword(element.Name);
					Write(", ");
					WriteCamelCaseNotKeyword(statement.GetValueVar().Name);
					WriteChar(']');
				}
				else {
					switch (collectionType.GetElementType()) {
					case FuStorageType storage:
						if (!(element.Type is FuReadWriteClassType))
							Write("const ");
						Write(storage.Class.Name);
						Write(" &");
						WriteCamelCaseNotKeyword(element.Name);
						break;
					case FuDynamicPtrType dynamic:
						Write("const ");
						WriteType(dynamic, true);
						Write(" &");
						WriteCamelCaseNotKeyword(element.Name);
						break;
					default:
						WriteTypeAndName(element);
						break;
					}
				}
				Write(" : ");
				WriteCollectionObject(statement.Collection, FuPriority.Argument);
			}
			WriteChar(')');
			WriteChild(statement.Body);
		}

		protected override bool EmbedIfWhileIsVar(FuExpr expr, bool write)
		{
			if (expr is FuBinaryExpr binary && binary.Op == FuToken.Is && binary.Right is FuVar def) {
				if (write)
					WriteType(def.Type, true);
				return true;
			}
			return false;
		}

		internal override void VisitLock(FuLock statement)
		{
			OpenBlock();
			Write("const std::lock_guard<std::recursive_mutex> lock(");
			statement.Lock.Accept(this, FuPriority.Argument);
			WriteLine(");");
			FlattenBlock(statement.Body);
			CloseBlock();
		}

		protected override void WriteStronglyCoerced(FuType type, FuExpr expr)
		{
			if (type.Id == FuId.StringStorageType && expr.Type.Id == FuId.StringPtrType && !(expr is FuLiteral)) {
				Write("std::string(");
				expr.Accept(this, FuPriority.Argument);
				WriteChar(')');
			}
			else {
				FuCallExpr call = IsStringSubstring(expr);
				if (call != null && type.Id == FuId.StringStorageType && GetStringSubstringPtr(call).Type.Id != FuId.StringStorageType) {
					Write("std::string(");
					bool cast = IsUTF8GetString(call);
					if (cast)
						Write("reinterpret_cast<const char *>(");
					WriteStringPtrAdd(call);
					if (cast)
						WriteChar(')');
					Write(", ");
					GetStringSubstringLength(call).Accept(this, FuPriority.Argument);
					WriteChar(')');
				}
				else
					base.WriteStronglyCoerced(type, expr);
			}
		}

		protected override void WriteSwitchCaseCond(FuSwitch statement, FuExpr value, FuPriority parent)
		{
			if (value is FuVar def) {
				if (parent == FuPriority.Argument && def.Name != "_")
					WriteType(def.Type, true);
				WriteIsVar(statement.Value, def, parent);
			}
			else
				base.WriteSwitchCaseCond(statement, value, parent);
		}

		static bool IsIsVar(FuExpr expr) => expr is FuBinaryExpr binary && binary.Op == FuToken.Is && binary.Right is FuVar;

		bool HasVariables(FuStatement statement)
		{
			switch (statement) {
			case FuVar _:
				return true;
			case FuAssert asrt:
				return IsIsVar(asrt.Cond);
			case FuBlock _:
			case FuBreak _:
			case FuConst _:
			case FuContinue _:
			case FuLock _:
			case FuNative _:
			case FuThrow _:
				return false;
			case FuIf ifStatement:
				return HasTemporaries(ifStatement.Cond) && !IsIsVar(ifStatement.Cond);
			case FuLoop loop:
				return loop.Cond != null && HasTemporaries(loop.Cond);
			case FuReturn ret:
				return ret.Value != null && HasTemporaries(ret.Value);
			case FuSwitch switch_:
				return HasTemporaries(switch_.Value);
			case FuExpr expr:
				return HasTemporaries(expr);
			default:
				throw new NotImplementedException();
			}
		}

		protected override void WriteSwitchCaseBody(List<FuStatement> statements)
		{
			bool block = false;
			foreach (FuStatement statement in statements) {
				if (!block && HasVariables(statement)) {
					OpenBlock();
					block = true;
				}
				statement.AcceptStatement(this);
			}
			if (block)
				CloseBlock();
		}

		internal override void VisitSwitch(FuSwitch statement)
		{
			if (statement.IsTypeMatching())
				WriteSwitchAsIfsWithGoto(statement);
			else
				base.VisitSwitch(statement);
		}

		internal override void VisitThrow(FuThrow statement)
		{
			Include("exception");
			WriteLine("throw std::exception();");
		}

		void OpenNamespace()
		{
			if (this.Namespace.Length == 0)
				return;
			WriteNewLine();
			Write("namespace ");
			WriteLine(this.Namespace);
			WriteCharLine('{');
		}

		void CloseNamespace()
		{
			if (this.Namespace.Length != 0)
				WriteCharLine('}');
		}

		protected override void WriteEnum(FuEnum enu)
		{
			WriteNewLine();
			WriteDoc(enu.Documentation);
			Write("enum class ");
			WriteLine(enu.Name);
			OpenBlock();
			enu.AcceptValues(this);
			WriteNewLine();
			this.Indent--;
			WriteLine("};");
			if (enu is FuEnumFlags) {
				Include("type_traits");
				this.HasEnumFlags = true;
				Write("FU_ENUM_FLAG_OPERATORS(");
				Write(enu.Name);
				WriteCharLine(')');
			}
		}

		static FuVisibility GetConstructorVisibility(FuClass klass)
		{
			switch (klass.CallType) {
			case FuCallType.Static:
				return FuVisibility.Private;
			case FuCallType.Abstract:
				return FuVisibility.Protected;
			default:
				return FuVisibility.Public;
			}
		}

		static bool HasMembersOfVisibility(FuClass klass, FuVisibility visibility)
		{
			for (FuSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
				if (symbol is FuMember member && member.Visibility == visibility)
					return true;
			}
			return false;
		}

		protected override void WriteField(FuField field)
		{
			WriteDoc(field.Documentation);
			WriteVar(field);
			WriteCharLine(';');
		}

		void WriteParametersAndConst(FuMethod method, bool defaultArguments)
		{
			WriteParameters(method, defaultArguments);
			if (method.CallType != FuCallType.Static && !method.IsMutator)
				Write(" const");
		}

		void WriteDeclarations(FuClass klass, FuVisibility visibility, string visibilityKeyword)
		{
			bool constructor = GetConstructorVisibility(klass) == visibility;
			bool destructor = visibility == FuVisibility.Public && (klass.HasSubclasses || klass.AddsVirtualMethods());
			if (!constructor && !destructor && !HasMembersOfVisibility(klass, visibility))
				return;
			Write(visibilityKeyword);
			WriteCharLine(':');
			this.Indent++;
			if (constructor) {
				if (klass.Constructor != null)
					WriteDoc(klass.Constructor.Documentation);
				Write(klass.Name);
				Write("()");
				if (klass.CallType == FuCallType.Static)
					Write(" = delete");
				else if (!NeedsConstructor(klass))
					Write(" = default");
				WriteCharLine(';');
			}
			if (destructor) {
				Write("virtual ~");
				Write(klass.Name);
				WriteLine("() = default;");
			}
			for (FuSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
				if (!(symbol is FuMember member) || member.Visibility != visibility)
					continue;
				switch (member) {
				case FuConst konst:
					WriteDoc(konst.Documentation);
					WriteConst(konst);
					break;
				case FuField field:
					WriteField(field);
					break;
				case FuMethod method:
					WriteMethodDoc(method);
					switch (method.CallType) {
					case FuCallType.Static:
						Write("static ");
						break;
					case FuCallType.Abstract:
					case FuCallType.Virtual:
						Write("virtual ");
						break;
					default:
						break;
					}
					WriteTypeAndName(method);
					WriteParametersAndConst(method, true);
					switch (method.CallType) {
					case FuCallType.Abstract:
						Write(" = 0");
						break;
					case FuCallType.Override:
						Write(" override");
						break;
					case FuCallType.Sealed:
						Write(" final");
						break;
					default:
						break;
					}
					WriteCharLine(';');
					break;
				default:
					throw new NotImplementedException();
				}
			}
			this.Indent--;
		}

		protected override void WriteClassInternal(FuClass klass)
		{
			WriteNewLine();
			WriteDoc(klass.Documentation);
			OpenClass(klass, klass.CallType == FuCallType.Sealed ? " final" : "", " : public ");
			this.Indent--;
			WriteDeclarations(klass, FuVisibility.Public, "public");
			WriteDeclarations(klass, FuVisibility.Protected, "protected");
			WriteDeclarations(klass, FuVisibility.Internal, "public");
			WriteDeclarations(klass, FuVisibility.Private, "private");
			WriteLine("};");
		}

		void WriteConstructor(FuClass klass)
		{
			if (!NeedsConstructor(klass))
				return;
			Write(klass.Name);
			Write("::");
			Write(klass.Name);
			WriteLine("()");
			OpenBlock();
			WriteConstructorBody(klass);
			CloseBlock();
		}

		protected override void WriteMethod(FuMethod method)
		{
			if (method.CallType == FuCallType.Abstract)
				return;
			WriteNewLine();
			WriteType(method.Type, true);
			WriteChar(' ');
			Write(method.Parent.Name);
			Write("::");
			WriteCamelCaseNotKeyword(method.Name);
			WriteParametersAndConst(method, false);
			WriteBody(method);
		}

		void WriteResources(SortedDictionary<string, List<byte>> resources, bool define)
		{
			if (resources.Count == 0)
				return;
			WriteNewLine();
			WriteLine("namespace");
			OpenBlock();
			WriteLine("namespace FuResource");
			OpenBlock();
			foreach ((string name, List<byte> content) in resources) {
				if (!define)
					Write("extern ");
				Include("array");
				Include("cstdint");
				Write("const std::array<uint8_t, ");
				VisitLiteralLong(content.Count);
				Write("> ");
				WriteResourceName(name);
				if (define) {
					WriteLine(" = {");
					WriteChar('\t');
					WriteBytes(content);
					Write(" }");
				}
				WriteCharLine(';');
			}
			CloseBlock();
			CloseBlock();
		}

		public override void WriteProgram(FuProgram program)
		{
			this.WrittenClasses.Clear();
			this.InHeaderFile = true;
			this.UsingStringViewLiterals = false;
			this.HasEnumFlags = false;
			this.StringReplace = false;
			OpenStringWriter();
			OpenNamespace();
			WriteRegexOptionsEnum(program);
			for (FuSymbol type = program.First; type != null; type = type.Next) {
				if (type is FuEnum enu)
					WriteEnum(enu);
				else {
					Write("class ");
					Write(type.Name);
					WriteCharLine(';');
				}
			}
			foreach (FuClass klass in program.Classes)
				WriteClass(klass, program);
			CloseNamespace();
			CreateHeaderFile(".hpp");
			if (this.HasEnumFlags) {
				WriteLine("#define FU_ENUM_FLAG_OPERATORS(T) \\");
				WriteLine("\tinline constexpr T operator~(T a) { return static_cast<T>(~static_cast<std::underlying_type_t<T>>(a)); } \\");
				WriteLine("\tinline constexpr T operator&(T a, T b) { return static_cast<T>(static_cast<std::underlying_type_t<T>>(a) & static_cast<std::underlying_type_t<T>>(b)); } \\");
				WriteLine("\tinline constexpr T operator|(T a, T b) { return static_cast<T>(static_cast<std::underlying_type_t<T>>(a) | static_cast<std::underlying_type_t<T>>(b)); } \\");
				WriteLine("\tinline constexpr T operator^(T a, T b) { return static_cast<T>(static_cast<std::underlying_type_t<T>>(a) ^ static_cast<std::underlying_type_t<T>>(b)); } \\");
				WriteLine("\tinline constexpr T &operator&=(T &a, T b) { return (a = a & b); } \\");
				WriteLine("\tinline constexpr T &operator|=(T &a, T b) { return (a = a | b); } \\");
				WriteLine("\tinline constexpr T &operator^=(T &a, T b) { return (a = a ^ b); }");
			}
			CloseStringWriter();
			CloseFile();
			this.InHeaderFile = false;
			OpenStringWriter();
			WriteResources(program.Resources, false);
			OpenNamespace();
			foreach (FuClass klass in program.Classes) {
				WriteConstructor(klass);
				WriteMethods(klass);
			}
			WriteResources(program.Resources, true);
			CloseNamespace();
			if (this.StringReplace) {
				Include("string");
				Include("string_view");
			}
			CreateImplementationFile(program, ".hpp");
			if (this.UsingStringViewLiterals)
				WriteLine("using namespace std::string_view_literals;");
			if (this.StringReplace) {
				WriteNewLine();
				WriteLine("static std::string FuString_replace(std::string_view s, std::string_view oldValue, std::string_view newValue)");
				OpenBlock();
				WriteLine("std::string result;");
				WriteLine("result.reserve(s.size());");
				WriteLine("for (std::string_view::size_type i = 0;;) {");
				WriteLine("\tauto j = s.find(oldValue, i);");
				WriteLine("\tif (j == std::string::npos) {");
				WriteLine("\t\tresult.append(s, i);");
				WriteLine("\t\treturn result;");
				WriteLine("\t}");
				WriteLine("\tresult.append(s, i, j - i);");
				WriteLine("\tresult.append(newValue);");
				WriteLine("\ti = j + oldValue.size();");
				WriteCharLine('}');
				CloseBlock();
			}
			CloseStringWriter();
			CloseFile();
		}
	}

	public class GenCs : GenTyped
	{

		protected override string GetTargetName() => "C++";

		protected override void StartDocLine()
		{
			Write("/// ");
		}

		protected override void WriteDocPara(FuDocPara para, bool many)
		{
			if (many) {
				WriteNewLine();
				Write("/// <para>");
			}
			foreach (FuDocInline inline in para.Children) {
				switch (inline) {
				case FuDocText text:
					WriteXmlDoc(text.Text);
					break;
				case FuDocCode code:
					switch (code.Text) {
					case "true":
					case "false":
					case "null":
						Write("<see langword=\"");
						Write(code.Text);
						Write("\" />");
						break;
					default:
						Write("<c>");
						WriteXmlDoc(code.Text);
						Write("</c>");
						break;
					}
					break;
				case FuDocLine _:
					WriteNewLine();
					StartDocLine();
					break;
				default:
					throw new NotImplementedException();
				}
			}
			if (many)
				Write("</para>");
		}

		protected override void WriteDocList(FuDocList list)
		{
			WriteNewLine();
			WriteLine("/// <list type=\"bullet\">");
			foreach (FuDocPara item in list.Items) {
				Write("/// <item>");
				WriteDocPara(item, false);
				WriteLine("</item>");
			}
			Write("/// </list>");
		}

		protected override void WriteDoc(FuCodeDoc doc)
		{
			if (doc == null)
				return;
			Write("/// <summary>");
			WriteDocPara(doc.Summary, false);
			WriteLine("</summary>");
			if (doc.Details.Count > 0) {
				Write("/// <remarks>");
				if (doc.Details.Count == 1)
					WriteDocBlock(doc.Details[0], false);
				else {
					foreach (FuDocBlock block in doc.Details)
						WriteDocBlock(block, true);
				}
				WriteLine("</remarks>");
			}
		}

		protected override void WriteName(FuSymbol symbol)
		{
			if (symbol is FuConst konst && konst.InMethod != null)
				Write(konst.InMethod.Name);
			Write(symbol.Name);
			switch (symbol.Name) {
			case "as":
			case "await":
			case "catch":
			case "char":
			case "checked":
			case "decimal":
			case "delegate":
			case "event":
			case "explicit":
			case "extern":
			case "finally":
			case "fixed":
			case "goto":
			case "implicit":
			case "interface":
			case "is":
			case "lock":
			case "namespace":
			case "object":
			case "operator":
			case "out":
			case "params":
			case "private":
			case "readonly":
			case "ref":
			case "sbyte":
			case "sizeof":
			case "stackalloc":
			case "struct":
			case "try":
			case "typeof":
			case "ulong":
			case "unchecked":
			case "unsafe":
			case "using":
			case "volatile":
				WriteChar('_');
				break;
			default:
				break;
			}
		}

		protected override int GetLiteralChars() => 65536;

		void WriteVisibility(FuVisibility visibility)
		{
			switch (visibility) {
			case FuVisibility.Private:
				break;
			case FuVisibility.Internal:
				Write("internal ");
				break;
			case FuVisibility.Protected:
				Write("protected ");
				break;
			case FuVisibility.Public:
				Write("public ");
				break;
			default:
				throw new NotImplementedException();
			}
		}

		void WriteCallType(FuCallType callType, string sealedString)
		{
			switch (callType) {
			case FuCallType.Static:
				Write("static ");
				break;
			case FuCallType.Normal:
				break;
			case FuCallType.Abstract:
				Write("abstract ");
				break;
			case FuCallType.Virtual:
				Write("virtual ");
				break;
			case FuCallType.Override:
				Write("override ");
				break;
			case FuCallType.Sealed:
				Write(sealedString);
				break;
			}
		}

		void WriteElementType(FuType elementType)
		{
			Include("System.Collections.Generic");
			WriteChar('<');
			WriteType(elementType, false);
			WriteChar('>');
		}

		protected override void WriteType(FuType type, bool promote)
		{
			switch (type) {
			case FuIntegerType _:
				switch (GetTypeId(type, promote)) {
				case FuId.SByteRange:
					Write("sbyte");
					break;
				case FuId.ByteRange:
					Write("byte");
					break;
				case FuId.ShortRange:
					Write("short");
					break;
				case FuId.UShortRange:
					Write("ushort");
					break;
				case FuId.IntType:
					Write("int");
					break;
				case FuId.LongType:
					Write("long");
					break;
				default:
					throw new NotImplementedException();
				}
				break;
			case FuClassType klass:
				switch (klass.Class.Id) {
				case FuId.StringClass:
					Write("string");
					break;
				case FuId.ArrayPtrClass:
				case FuId.ArrayStorageClass:
					WriteType(klass.GetElementType(), false);
					Write("[]");
					break;
				case FuId.ListClass:
				case FuId.QueueClass:
				case FuId.StackClass:
				case FuId.HashSetClass:
				case FuId.SortedSetClass:
					Write(klass.Class.Name);
					WriteElementType(klass.GetElementType());
					break;
				case FuId.DictionaryClass:
				case FuId.SortedDictionaryClass:
					Include("System.Collections.Generic");
					Write(klass.Class.Name);
					WriteChar('<');
					WriteType(klass.GetKeyType(), false);
					Write(", ");
					WriteType(klass.GetValueType(), false);
					WriteChar('>');
					break;
				case FuId.OrderedDictionaryClass:
					Include("System.Collections.Specialized");
					Write("OrderedDictionary");
					break;
				case FuId.TextWriterClass:
				case FuId.StringWriterClass:
					Include("System.IO");
					Write(klass.Class.Name);
					break;
				case FuId.RegexClass:
				case FuId.MatchClass:
					Include("System.Text.RegularExpressions");
					Write(klass.Class.Name);
					break;
				case FuId.LockClass:
					Write("object");
					break;
				default:
					Write(klass.Class.Name);
					break;
				}
				break;
			default:
				Write(type.Name);
				break;
			}
		}

		protected override void WriteNewWithFields(FuReadWriteClassType type, FuAggregateInitializer init)
		{
			Write("new ");
			WriteType(type, false);
			WriteObjectLiteral(init, " = ");
		}

		protected override void WriteCoercedLiteral(FuType type, FuExpr expr)
		{
			if (expr is FuLiteralChar && type is FuRangeType range && range.Max <= 255)
				WriteStaticCast(type, expr);
			else
				base.WriteCoercedLiteral(type, expr);
		}

		protected override bool IsPromoted(FuExpr expr) => base.IsPromoted(expr) || expr is FuLiteralChar;

		internal override void VisitInterpolatedString(FuInterpolatedString expr, FuPriority parent)
		{
			Write("$\"");
			foreach (FuInterpolatedPart part in expr.Parts) {
				WriteDoubling(part.Prefix, '{');
				WriteChar('{');
				part.Argument.Accept(this, FuPriority.Argument);
				if (part.WidthExpr != null) {
					WriteChar(',');
					VisitLiteralLong(part.Width);
				}
				if (part.Format != ' ') {
					WriteChar(':');
					WriteChar(part.Format);
					if (part.Precision >= 0)
						VisitLiteralLong(part.Precision);
				}
				WriteChar('}');
			}
			WriteDoubling(expr.Suffix, '{');
			WriteChar('"');
		}

		protected override void WriteNewArray(FuType elementType, FuExpr lengthExpr, FuPriority parent)
		{
			Write("new ");
			WriteType(elementType.GetBaseType(), false);
			WriteChar('[');
			lengthExpr.Accept(this, FuPriority.Argument);
			WriteChar(']');
			while (elementType is FuClassType array && array.IsArray()) {
				Write("[]");
				elementType = array.GetElementType();
			}
		}

		protected override void WriteNew(FuReadWriteClassType klass, FuPriority parent)
		{
			Write("new ");
			WriteType(klass, false);
			Write("()");
		}

		protected override bool HasInitCode(FuNamedValue def) => def.Type is FuArrayStorageType array && array.GetElementType() is FuStorageType;

		protected override void WriteInitCode(FuNamedValue def)
		{
			if (!HasInitCode(def))
				return;
			FuArrayStorageType array = (FuArrayStorageType) def.Type;
			int nesting = 0;
			while (array.GetElementType() is FuArrayStorageType innerArray) {
				OpenLoop("int", nesting++, array.Length);
				WriteArrayElement(def, nesting);
				Write(" = ");
				WriteNewArray(innerArray.GetElementType(), innerArray.LengthExpr, FuPriority.Argument);
				WriteCharLine(';');
				array = innerArray;
			}
			if (array.GetElementType() is FuStorageType klass) {
				OpenLoop("int", nesting++, array.Length);
				WriteArrayElement(def, nesting);
				Write(" = ");
				WriteNew(klass, FuPriority.Argument);
				WriteCharLine(';');
			}
			while (--nesting >= 0)
				CloseBlock();
		}

		protected override void WriteResource(string name, int length)
		{
			Write("FuResource.");
			WriteResourceName(name);
		}

		protected override void WriteStringLength(FuExpr expr)
		{
			WritePostfix(expr, ".Length");
		}

		internal override void VisitSymbolReference(FuSymbolReference expr, FuPriority parent)
		{
			switch (expr.Symbol.Id) {
			case FuId.ConsoleError:
				Include("System");
				Write("Console.Error");
				break;
			case FuId.MatchStart:
				WritePostfix(expr.Left, ".Index");
				break;
			case FuId.MatchEnd:
				if (parent > FuPriority.Add)
					WriteChar('(');
				WritePostfix(expr.Left, ".Index + ");
				WriteStringLength(expr.Left);
				if (parent > FuPriority.Add)
					WriteChar(')');
				break;
			case FuId.MathNaN:
			case FuId.MathNegativeInfinity:
			case FuId.MathPositiveInfinity:
				Write("float.");
				Write(expr.Symbol.Name);
				break;
			default:
				if (expr.Symbol.Parent is FuForeach forEach && forEach.Collection.Type is FuClassType dict && dict.Class.Id == FuId.OrderedDictionaryClass) {
					if (parent == FuPriority.Primary)
						WriteChar('(');
					FuVar element = forEach.GetVar();
					if (expr.Symbol == element) {
						WriteStaticCastType(dict.GetKeyType());
						WriteName(element);
						Write(".Key");
					}
					else {
						WriteStaticCastType(dict.GetValueType());
						WriteName(element);
						Write(".Value");
					}
					if (parent == FuPriority.Primary)
						WriteChar(')');
				}
				else
					base.VisitSymbolReference(expr, parent);
				break;
			}
		}

		protected override void WriteCallExpr(FuExpr obj, FuMethod method, List<FuExpr> args, FuPriority parent)
		{
			switch (method.Id) {
			case FuId.EnumFromInt:
				WriteStaticCast(method.Type, args[0]);
				break;
			case FuId.IntTryParse:
			case FuId.LongTryParse:
			case FuId.DoubleTryParse:
				Write(obj.Type.Name);
				Write(".TryParse(");
				args[0].Accept(this, FuPriority.Argument);
				if (args.Count == 2) {
					if (!(args[1] is FuLiteralLong radix) || radix.Value != 16)
						NotSupported(args[1], "Radix");
					Include("System.Globalization");
					Write(", NumberStyles.HexNumber, null");
				}
				Write(", out ");
				obj.Accept(this, FuPriority.Argument);
				WriteChar(')');
				break;
			case FuId.StringIndexOf:
			case FuId.StringLastIndexOf:
				obj.Accept(this, FuPriority.Primary);
				WriteChar('.');
				Write(method.Name);
				WriteChar('(');
				int c = GetOneAscii(args[0]);
				if (c >= 0)
					VisitLiteralChar(c);
				else
					args[0].Accept(this, FuPriority.Argument);
				WriteChar(')');
				break;
			case FuId.ArrayBinarySearchAll:
			case FuId.ArrayBinarySearchPart:
				Include("System");
				Write("Array.BinarySearch(");
				obj.Accept(this, FuPriority.Argument);
				Write(", ");
				if (args.Count == 3) {
					args[1].Accept(this, FuPriority.Argument);
					Write(", ");
					args[2].Accept(this, FuPriority.Argument);
					Write(", ");
				}
				WriteNotPromoted(obj.Type.AsClassType().GetElementType(), args[0]);
				WriteChar(')');
				break;
			case FuId.ArrayContains:
				Include("System.Linq");
				WriteMethodCall(obj, "Contains", args[0]);
				break;
			case FuId.ArrayCopyTo:
				Include("System");
				Write("Array.Copy(");
				obj.Accept(this, FuPriority.Argument);
				Write(", ");
				WriteArgs(method, args);
				WriteChar(')');
				break;
			case FuId.ArrayFillAll:
			case FuId.ArrayFillPart:
				Include("System");
				if (args[0] is FuLiteral literal && literal.IsDefaultValue()) {
					Write("Array.Clear(");
					obj.Accept(this, FuPriority.Argument);
					if (args.Count == 1) {
						Write(", 0, ");
						WriteArrayStorageLength(obj);
					}
				}
				else {
					Write("Array.Fill(");
					obj.Accept(this, FuPriority.Argument);
					Write(", ");
					WriteNotPromoted(obj.Type.AsClassType().GetElementType(), args[0]);
				}
				if (args.Count == 3) {
					Write(", ");
					args[1].Accept(this, FuPriority.Argument);
					Write(", ");
					args[2].Accept(this, FuPriority.Argument);
				}
				WriteChar(')');
				break;
			case FuId.ArraySortAll:
				Include("System");
				WriteCall("Array.Sort", obj);
				break;
			case FuId.ArraySortPart:
				Include("System");
				WriteCall("Array.Sort", obj, args[0], args[1]);
				break;
			case FuId.ListAdd:
				WriteListAdd(obj, "Add", args);
				break;
			case FuId.ListAll:
				WriteMethodCall(obj, "TrueForAll", args[0]);
				break;
			case FuId.ListAny:
				WriteMethodCall(obj, "Exists", args[0]);
				break;
			case FuId.ListInsert:
				WriteListInsert(obj, "Insert", args);
				break;
			case FuId.ListLast:
				WritePostfix(obj, "[^1]");
				break;
			case FuId.ListSortPart:
				WritePostfix(obj, ".Sort(");
				WriteArgs(method, args);
				Write(", null)");
				break;
			case FuId.DictionaryAdd:
				WritePostfix(obj, ".Add(");
				args[0].Accept(this, FuPriority.Argument);
				Write(", ");
				WriteNewStorage(obj.Type.AsClassType().GetValueType());
				WriteChar(')');
				break;
			case FuId.OrderedDictionaryContainsKey:
				WriteMethodCall(obj, "Contains", args[0]);
				break;
			case FuId.TextWriterWrite:
			case FuId.TextWriterWriteLine:
			case FuId.ConsoleWrite:
			case FuId.ConsoleWriteLine:
				Include("System");
				obj.Accept(this, FuPriority.Primary);
				WriteChar('.');
				Write(method.Name);
				WriteChar('(');
				if (args.Count != 0) {
					if (args[0] is FuLiteralChar) {
						Write("(int) ");
						args[0].Accept(this, FuPriority.Primary);
					}
					else
						args[0].Accept(this, FuPriority.Argument);
				}
				WriteChar(')');
				break;
			case FuId.StringWriterClear:
				WritePostfix(obj, ".GetStringBuilder().Clear()");
				break;
			case FuId.TextWriterWriteChar:
				WriteCharMethodCall(obj, "Write", args[0]);
				break;
			case FuId.TextWriterWriteCodePoint:
				WritePostfix(obj, ".Write(");
				if (args[0] is FuLiteralChar literalChar && literalChar.Value < 65536)
					args[0].Accept(this, FuPriority.Argument);
				else {
					Include("System.Text");
					WriteCall("new Rune", args[0]);
				}
				WriteChar(')');
				break;
			case FuId.EnvironmentGetEnvironmentVariable:
				Include("System");
				obj.Accept(this, FuPriority.Primary);
				WriteChar('.');
				Write(method.Name);
				WriteArgsInParentheses(method, args);
				break;
			case FuId.UTF8GetByteCount:
				Include("System.Text");
				Write("Encoding.UTF8.GetByteCount(");
				args[0].Accept(this, FuPriority.Argument);
				WriteChar(')');
				break;
			case FuId.UTF8GetBytes:
				Include("System.Text");
				Write("Encoding.UTF8.GetBytes(");
				args[0].Accept(this, FuPriority.Argument);
				Write(", 0, ");
				WritePostfix(args[0], ".Length, ");
				args[1].Accept(this, FuPriority.Argument);
				Write(", ");
				args[2].Accept(this, FuPriority.Argument);
				WriteChar(')');
				break;
			case FuId.UTF8GetString:
				Include("System.Text");
				Write("Encoding.UTF8.GetString");
				WriteArgsInParentheses(method, args);
				break;
			case FuId.RegexCompile:
				Include("System.Text.RegularExpressions");
				Write("new Regex");
				WriteArgsInParentheses(method, args);
				break;
			case FuId.RegexEscape:
			case FuId.RegexIsMatchStr:
			case FuId.RegexIsMatchRegex:
				Include("System.Text.RegularExpressions");
				obj.Accept(this, FuPriority.Primary);
				WriteChar('.');
				Write(method.Name);
				WriteArgsInParentheses(method, args);
				break;
			case FuId.MatchFindStr:
				Include("System.Text.RegularExpressions");
				WriteChar('(');
				obj.Accept(this, FuPriority.Assign);
				Write(" = Regex.Match");
				WriteArgsInParentheses(method, args);
				Write(").Success");
				break;
			case FuId.MatchFindRegex:
				Include("System.Text.RegularExpressions");
				WriteChar('(');
				obj.Accept(this, FuPriority.Assign);
				Write(" = ");
				WriteMethodCall(args[1], "Match", args[0]);
				Write(").Success");
				break;
			case FuId.MatchGetCapture:
				WritePostfix(obj, ".Groups[");
				args[0].Accept(this, FuPriority.Argument);
				Write("].Value");
				break;
			case FuId.MathMethod:
			case FuId.MathAbs:
			case FuId.MathCeiling:
			case FuId.MathClamp:
			case FuId.MathFusedMultiplyAdd:
			case FuId.MathLog2:
			case FuId.MathMaxInt:
			case FuId.MathMaxDouble:
			case FuId.MathMinInt:
			case FuId.MathMinDouble:
			case FuId.MathRound:
			case FuId.MathTruncate:
				Include("System");
				Write("Math.");
				Write(method.Name);
				WriteArgsInParentheses(method, args);
				break;
			case FuId.MathIsFinite:
			case FuId.MathIsInfinity:
			case FuId.MathIsNaN:
				Write("double.");
				WriteCall(method.Name, args[0]);
				break;
			default:
				if (obj != null) {
					obj.Accept(this, FuPriority.Primary);
					WriteChar('.');
				}
				WriteName(method);
				WriteArgsInParentheses(method, args);
				break;
			}
		}

		void WriteOrderedDictionaryIndexing(FuBinaryExpr expr)
		{
			if (expr.Right.Type.Id == FuId.IntType || expr.Right.Type is FuRangeType) {
				WritePostfix(expr.Left, "[(object) ");
				expr.Right.Accept(this, FuPriority.Primary);
				WriteChar(']');
			}
			else
				base.WriteIndexingExpr(expr, FuPriority.And);
		}

		protected override void WriteIndexingExpr(FuBinaryExpr expr, FuPriority parent)
		{
			if (expr.Left.Type is FuClassType dict && dict.Class.Id == FuId.OrderedDictionaryClass) {
				if (parent == FuPriority.Primary)
					WriteChar('(');
				WriteStaticCastType(expr.Type);
				WriteOrderedDictionaryIndexing(expr);
				if (parent == FuPriority.Primary)
					WriteChar(')');
			}
			else
				base.WriteIndexingExpr(expr, parent);
		}

		protected override void WriteAssign(FuBinaryExpr expr, FuPriority parent)
		{
			if (expr.Left is FuBinaryExpr indexing && indexing.Op == FuToken.LeftBracket && indexing.Left.Type is FuClassType dict && dict.Class.Id == FuId.OrderedDictionaryClass) {
				WriteOrderedDictionaryIndexing(indexing);
				Write(" = ");
				WriteAssignRight(expr);
			}
			else
				base.WriteAssign(expr, parent);
		}

		internal override void VisitBinaryExpr(FuBinaryExpr expr, FuPriority parent)
		{
			switch (expr.Op) {
			case FuToken.AndAssign:
			case FuToken.OrAssign:
			case FuToken.XorAssign:
				if (parent > FuPriority.Assign)
					WriteChar('(');
				expr.Left.Accept(this, FuPriority.Assign);
				WriteChar(' ');
				Write(expr.GetOpString());
				WriteChar(' ');
				WriteAssignRight(expr);
				if (parent > FuPriority.Assign)
					WriteChar(')');
				break;
			default:
				base.VisitBinaryExpr(expr, parent);
				break;
			}
		}

		internal override void VisitLambdaExpr(FuLambdaExpr expr)
		{
			WriteName(expr.First);
			Write(" => ");
			expr.Body.Accept(this, FuPriority.Statement);
		}

		protected override void DefineObjectLiteralTemporary(FuUnaryExpr expr)
		{
		}

		protected override void DefineIsVar(FuBinaryExpr binary)
		{
		}

		protected override void WriteAssert(FuAssert statement)
		{
			if (statement.CompletesNormally()) {
				Include("System.Diagnostics");
				Write("Debug.Assert(");
				statement.Cond.Accept(this, FuPriority.Argument);
				if (statement.Message != null) {
					Write(", ");
					statement.Message.Accept(this, FuPriority.Argument);
				}
			}
			else {
				Include("System");
				Write("throw new NotImplementedException(");
				if (statement.Message != null)
					statement.Message.Accept(this, FuPriority.Argument);
			}
			WriteLine(");");
		}

		internal override void VisitForeach(FuForeach statement)
		{
			Write("foreach (");
			if (statement.Collection.Type is FuClassType dict && dict.Class.TypeParameterCount == 2) {
				if (dict.Class.Id == FuId.OrderedDictionaryClass) {
					Include("System.Collections");
					Write("DictionaryEntry ");
					WriteName(statement.GetVar());
				}
				else {
					WriteChar('(');
					WriteTypeAndName(statement.GetVar());
					Write(", ");
					WriteTypeAndName(statement.GetValueVar());
					WriteChar(')');
				}
			}
			else
				WriteTypeAndName(statement.GetVar());
			Write(" in ");
			statement.Collection.Accept(this, FuPriority.Argument);
			WriteChar(')');
			WriteChild(statement.Body);
		}

		internal override void VisitLock(FuLock statement)
		{
			WriteCall("lock ", statement.Lock);
			WriteChild(statement.Body);
		}

		internal override void VisitThrow(FuThrow statement)
		{
			Include("System");
			Write("throw new Exception(");
			statement.Message.Accept(this, FuPriority.Argument);
			WriteLine(");");
		}

		protected override void WriteEnum(FuEnum enu)
		{
			WriteNewLine();
			WriteDoc(enu.Documentation);
			if (enu is FuEnumFlags) {
				Include("System");
				WriteLine("[Flags]");
			}
			WritePublic(enu);
			Write("enum ");
			WriteLine(enu.Name);
			OpenBlock();
			enu.AcceptValues(this);
			WriteNewLine();
			CloseBlock();
		}

		protected override void WriteRegexOptionsEnum(FuProgram program)
		{
			if (program.RegexOptionsEnum)
				Include("System.Text.RegularExpressions");
		}

		protected override void WriteConst(FuConst konst)
		{
			WriteNewLine();
			WriteDoc(konst.Documentation);
			WriteVisibility(konst.Visibility);
			Write(konst.Type is FuArrayStorageType ? "static readonly " : "const ");
			WriteTypeAndName(konst);
			Write(" = ");
			WriteCoercedExpr(konst.Type, konst.Value);
			WriteCharLine(';');
		}

		protected override void WriteField(FuField field)
		{
			WriteNewLine();
			WriteDoc(field.Documentation);
			WriteVisibility(field.Visibility);
			if (field.Type.IsFinal() && !field.IsAssignableStorage())
				Write("readonly ");
			WriteVar(field);
			WriteCharLine(';');
		}

		protected override void WriteParameterDoc(FuVar param, bool first)
		{
			Write("/// <param name=\"");
			WriteName(param);
			Write("\">");
			WriteDocPara(param.Documentation.Summary, false);
			WriteLine("</param>");
		}

		protected override bool IsShortMethod(FuMethod method) => method.Body is FuReturn;

		protected override void WriteMethod(FuMethod method)
		{
			if (method.Id == FuId.ClassToString && method.CallType == FuCallType.Abstract)
				return;
			WriteNewLine();
			WriteDoc(method.Documentation);
			WriteParametersDoc(method);
			WriteVisibility(method.Visibility);
			if (method.Id == FuId.ClassToString)
				Write("override ");
			else
				WriteCallType(method.CallType, "sealed override ");
			WriteTypeAndName(method);
			WriteParameters(method, true);
			WriteBody(method);
		}

		protected override void WriteClass(FuClass klass, FuProgram program)
		{
			WriteNewLine();
			WriteDoc(klass.Documentation);
			WritePublic(klass);
			WriteCallType(klass.CallType, "sealed ");
			OpenClass(klass, "", " : ");
			if (NeedsConstructor(klass)) {
				if (klass.Constructor != null) {
					WriteDoc(klass.Constructor.Documentation);
					WriteVisibility(klass.Constructor.Visibility);
				}
				else
					Write("internal ");
				Write(klass.Name);
				WriteLine("()");
				OpenBlock();
				WriteConstructorBody(klass);
				CloseBlock();
			}
			WriteMembers(klass, true);
			CloseBlock();
		}

		void WriteResources(SortedDictionary<string, List<byte>> resources)
		{
			WriteNewLine();
			WriteLine("internal static class FuResource");
			OpenBlock();
			foreach ((string name, List<byte> content) in resources) {
				Write("internal static readonly byte[] ");
				WriteResourceName(name);
				WriteLine(" = {");
				WriteChar('\t');
				WriteBytes(content);
				WriteLine(" };");
			}
			CloseBlock();
		}

		public override void WriteProgram(FuProgram program)
		{
			OpenStringWriter();
			if (this.Namespace.Length != 0) {
				Write("namespace ");
				WriteLine(this.Namespace);
				OpenBlock();
			}
			WriteTopLevelNatives(program);
			WriteTypes(program);
			if (program.Resources.Count > 0)
				WriteResources(program.Resources);
			if (this.Namespace.Length != 0)
				CloseBlock();
			CreateOutputFile();
			WriteIncludes("using ", ";");
			CloseStringWriter();
			CloseFile();
		}
	}

	public class GenD : GenCCppD
	{

		bool HasListInsert;

		bool HasListRemoveAt;

		bool HasQueueDequeue;

		bool HasStackPop;

		bool HasSortedDictionaryInsert;

		bool HasSortedDictionaryFind;

		protected override string GetTargetName() => "D";

		protected override void StartDocLine()
		{
			Write("/// ");
		}

		protected override void WriteDocPara(FuDocPara para, bool many)
		{
			if (many) {
				WriteNewLine();
				StartDocLine();
			}
			foreach (FuDocInline inline in para.Children) {
				switch (inline) {
				case FuDocText text:
					WriteXmlDoc(text.Text);
					break;
				case FuDocCode code:
					WriteChar('`');
					WriteXmlDoc(code.Text);
					WriteChar('`');
					break;
				case FuDocLine _:
					WriteNewLine();
					StartDocLine();
					break;
				default:
					throw new NotImplementedException();
				}
			}
			if (many)
				WriteNewLine();
		}

		protected override void WriteParameterDoc(FuVar param, bool first)
		{
			if (first) {
				StartDocLine();
				WriteLine("Params:");
			}
			StartDocLine();
			WriteName(param);
			Write(" = ");
			WriteDocPara(param.Documentation.Summary, false);
			WriteNewLine();
		}

		protected override void WriteDocList(FuDocList list)
		{
			WriteLine("///");
			WriteLine("/// <ul>");
			foreach (FuDocPara item in list.Items) {
				Write("/// <li>");
				WriteDocPara(item, false);
				WriteLine("</li>");
			}
			WriteLine("/// </ul>");
			Write("///");
		}

		protected override void WriteDoc(FuCodeDoc doc)
		{
			if (doc == null)
				return;
			StartDocLine();
			WriteDocPara(doc.Summary, false);
			WriteNewLine();
			if (doc.Details.Count > 0) {
				StartDocLine();
				if (doc.Details.Count == 1)
					WriteDocBlock(doc.Details[0], false);
				else {
					foreach (FuDocBlock block in doc.Details)
						WriteDocBlock(block, true);
				}
				WriteNewLine();
			}
		}

		protected override void WriteName(FuSymbol symbol)
		{
			if (symbol is FuContainerType) {
				Write(symbol.Name);
				return;
			}
			WriteCamelCase(symbol.Name);
			switch (symbol.Name) {
			case "Abstract":
			case "Alias":
			case "Align":
			case "Asm":
			case "Assert":
			case "Auto":
			case "Body":
			case "Bool":
			case "Break":
			case "Byte":
			case "Case":
			case "Cast":
			case "Catch":
			case "Cdouble":
			case "Cent":
			case "Cfloat":
			case "Char":
			case "Class":
			case "Const":
			case "Continue":
			case "Creal":
			case "Dchar":
			case "Debug":
			case "Default":
			case "Delegate":
			case "Delete":
			case "Deprecated":
			case "Do":
			case "Double":
			case "Else":
			case "Enum":
			case "Export":
			case "Extern":
			case "False":
			case "Final":
			case "Finally":
			case "Float":
			case "For":
			case "Foreach":
			case "Foreach_reverse":
			case "Function":
			case "Goto":
			case "Idouble":
			case "If":
			case "IfLoat":
			case "Immutable":
			case "Import":
			case "In":
			case "Inout":
			case "Int":
			case "Interface":
			case "Invariant":
			case "Ireal":
			case "Is":
			case "Lazy":
			case "Long":
			case "Macro":
			case "Mixin":
			case "Module":
			case "New":
			case "Nothrow":
			case "Null":
			case "Out":
			case "Override":
			case "Package":
			case "Pragma":
			case "Private":
			case "Protected":
			case "Public":
			case "Pure":
			case "Real":
			case "Ref":
			case "Return":
			case "Scope":
			case "Shared":
			case "Short":
			case "Sizeof":
			case "Static":
			case "String":
			case "Struct":
			case "Super":
			case "Switch":
			case "Synchronized":
			case "Template":
			case "Throw":
			case "True":
			case "Try":
			case "Typeid":
			case "Typeof":
			case "Ubyte":
			case "Ucent":
			case "Uint":
			case "Ulong":
			case "Union":
			case "Unittest":
			case "Ushort":
			case "Version":
			case "Void":
			case "Wchar":
			case "While":
			case "With":
			case "alias":
			case "align":
			case "asm":
			case "auto":
			case "body":
			case "cast":
			case "catch":
			case "cdouble":
			case "cent":
			case "cfloat":
			case "char":
			case "creal":
			case "dchar":
			case "debug":
			case "delegate":
			case "delete":
			case "deprecated":
			case "export":
			case "extern":
			case "final":
			case "finally":
			case "foreach_reverse":
			case "function":
			case "goto":
			case "idouble":
			case "ifloat":
			case "immutable":
			case "import":
			case "in":
			case "inout":
			case "interface":
			case "invariant":
			case "ireal":
			case "lazy":
			case "macro":
			case "mixin":
			case "module":
			case "nothrow":
			case "out":
			case "package":
			case "pragma":
			case "private":
			case "pure":
			case "real":
			case "ref":
			case "scope":
			case "shared":
			case "sizeof":
			case "struct":
			case "super":
			case "synchronized":
			case "template":
			case "try":
			case "typeid":
			case "typeof":
			case "ubyte":
			case "ucent":
			case "uint":
			case "ulong":
			case "union":
			case "unittest":
			case "ushort":
			case "version":
			case "wchar":
			case "with":
			case "__FILE__":
			case "__FILE_FULL_PATH__":
			case "__MODULE__":
			case "__LINE__":
			case "__FUNCTION__":
			case "__PRETTY_FUNCTION__":
			case "__gshared":
			case "__traits":
			case "__vector":
			case "__parameters":
				WriteChar('_');
				break;
			default:
				break;
			}
		}

		protected override int GetLiteralChars() => 65536;

		void WriteVisibility(FuVisibility visibility)
		{
			switch (visibility) {
			case FuVisibility.Private:
				Write("private ");
				break;
			case FuVisibility.Internal:
			case FuVisibility.Public:
				break;
			case FuVisibility.Protected:
				Write("protected ");
				break;
			default:
				throw new NotImplementedException();
			}
		}

		void WriteCallType(FuCallType callType, string sealedString)
		{
			switch (callType) {
			case FuCallType.Static:
				Write("static ");
				break;
			case FuCallType.Normal:
				break;
			case FuCallType.Abstract:
				Write("abstract ");
				break;
			case FuCallType.Virtual:
				break;
			case FuCallType.Override:
				Write("override ");
				break;
			case FuCallType.Sealed:
				Write(sealedString);
				break;
			}
		}

		static bool IsCreateWithNew(FuType type)
		{
			if (type is FuClassType klass) {
				if (klass is FuStorageType stg)
					return stg.Class.Id != FuId.ArrayStorageClass;
				return true;
			}
			return false;
		}

		static bool IsTransitiveConst(FuClassType array)
		{
			while (!(array is FuReadWriteClassType)) {
				if (!(array.GetElementType() is FuClassType element))
					return true;
				if (element.Class.Id != FuId.ArrayPtrClass)
					return false;
				array = element;
			}
			return false;
		}

		static bool IsStructPtr(FuType type) => type is FuClassType ptr && (ptr.Class.Id == FuId.ListClass || ptr.Class.Id == FuId.StackClass || ptr.Class.Id == FuId.QueueClass);

		void WriteElementType(FuType type)
		{
			WriteType(type, false);
			if (IsStructPtr(type))
				WriteChar('*');
		}

		protected override void WriteType(FuType type, bool promote)
		{
			switch (type) {
			case FuIntegerType _:
				switch (GetTypeId(type, promote)) {
				case FuId.SByteRange:
					Write("byte");
					break;
				case FuId.ByteRange:
					Write("ubyte");
					break;
				case FuId.ShortRange:
					Write("short");
					break;
				case FuId.UShortRange:
					Write("ushort");
					break;
				case FuId.IntType:
					Write("int");
					break;
				case FuId.LongType:
					Write("long");
					break;
				default:
					throw new NotImplementedException();
				}
				break;
			case FuClassType klass:
				switch (klass.Class.Id) {
				case FuId.StringClass:
					Write("string");
					break;
				case FuId.ArrayStorageClass:
				case FuId.ArrayPtrClass:
					if (promote && IsTransitiveConst(klass)) {
						Write("const(");
						WriteElementType(klass.GetElementType());
						WriteChar(')');
					}
					else
						WriteElementType(klass.GetElementType());
					WriteChar('[');
					if (klass is FuArrayStorageType arrayStorage)
						VisitLiteralLong(arrayStorage.Length);
					WriteChar(']');
					break;
				case FuId.ListClass:
				case FuId.StackClass:
					Include("std.container.array");
					Write("Array!(");
					WriteElementType(klass.GetElementType());
					WriteChar(')');
					break;
				case FuId.QueueClass:
					Include("std.container.dlist");
					Write("DList!(");
					WriteElementType(klass.GetElementType());
					WriteChar(')');
					break;
				case FuId.HashSetClass:
					Write("bool[");
					WriteElementType(klass.GetElementType());
					WriteChar(']');
					break;
				case FuId.DictionaryClass:
					WriteElementType(klass.GetValueType());
					WriteChar('[');
					WriteType(klass.GetKeyType(), false);
					WriteChar(']');
					break;
				case FuId.SortedSetClass:
					Include("std.container.rbtree");
					Write("RedBlackTree!(");
					WriteElementType(klass.GetElementType());
					WriteChar(')');
					break;
				case FuId.SortedDictionaryClass:
					Include("std.container.rbtree");
					Include("std.typecons");
					Write("RedBlackTree!(Tuple!(");
					WriteElementType(klass.GetKeyType());
					Write(", ");
					WriteElementType(klass.GetValueType());
					Write("), \"a[0] < b[0]\")");
					break;
				case FuId.OrderedDictionaryClass:
					Include("std.typecons");
					Write("Tuple!(Array!(");
					WriteElementType(klass.GetValueType());
					Write("), \"data\", size_t[");
					WriteType(klass.GetKeyType(), false);
					Write("], \"dict\")");
					break;
				case FuId.TextWriterClass:
					Include("std.stdio");
					Write("File");
					break;
				case FuId.RegexClass:
					Include("std.regex");
					Write("Regex!char");
					break;
				case FuId.MatchClass:
					Include("std.regex");
					Write("Captures!string");
					break;
				case FuId.LockClass:
					Write("Object");
					break;
				default:
					Write(klass.Class.Name);
					break;
				}
				break;
			default:
				Write(type.Name);
				break;
			}
		}

		protected override void WriteTypeAndName(FuNamedValue value)
		{
			WriteType(value.Type, true);
			if (IsStructPtr(value.Type))
				WriteChar('*');
			WriteChar(' ');
			WriteName(value);
		}

		internal override void VisitAggregateInitializer(FuAggregateInitializer expr)
		{
			Write("[ ");
			WriteCoercedLiterals(expr.Type.AsClassType().GetElementType(), expr.Items);
			Write(" ]");
		}

		protected override void WriteStaticCast(FuType type, FuExpr expr)
		{
			Write("cast(");
			WriteType(type, false);
			Write(")(");
			GetStaticCastInner(type, expr).Accept(this, FuPriority.Argument);
			WriteChar(')');
		}

		internal override void VisitInterpolatedString(FuInterpolatedString expr, FuPriority parent)
		{
			Include("std.format");
			Write("format(");
			WritePrintf(expr, false);
		}

		protected override void WriteStorageInit(FuNamedValue def)
		{
			Write(" = ");
			WriteNewStorage(def.Type);
		}

		protected override void WriteVarInit(FuNamedValue def)
		{
			if (def.Type is FuArrayStorageType)
				return;
			base.WriteVarInit(def);
		}

		protected override bool HasInitCode(FuNamedValue def)
		{
			if (def.Value != null && !(def.Value is FuLiteral))
				return true;
			FuType type = def.Type;
			if (type is FuArrayStorageType array) {
				while (array.GetElementType() is FuArrayStorageType innerArray)
					array = innerArray;
				type = array.GetElementType();
			}
			return type is FuStorageType;
		}

		protected override void WriteInitField(FuField field)
		{
			WriteInitCode(field);
		}

		protected override void WriteInitCode(FuNamedValue def)
		{
			if (!HasInitCode(def))
				return;
			if (def.Type is FuArrayStorageType array) {
				int nesting = 0;
				while (array.GetElementType() is FuArrayStorageType innerArray) {
					OpenLoop("size_t", nesting++, array.Length);
					array = innerArray;
				}
				if (array.GetElementType() is FuStorageType klass) {
					OpenLoop("size_t", nesting++, array.Length);
					WriteArrayElement(def, nesting);
					Write(" = ");
					WriteNew(klass, FuPriority.Argument);
					WriteCharLine(';');
				}
				while (--nesting >= 0)
					CloseBlock();
			}
			else {
				if (def.Type is FuReadWriteClassType klass) {
					switch (klass.Class.Id) {
					case FuId.StringClass:
					case FuId.ArrayStorageClass:
					case FuId.ArrayPtrClass:
					case FuId.DictionaryClass:
					case FuId.HashSetClass:
					case FuId.SortedDictionaryClass:
					case FuId.OrderedDictionaryClass:
					case FuId.RegexClass:
					case FuId.MatchClass:
					case FuId.LockClass:
						break;
					default:
						if (def.Parent is FuClass) {
							WriteName(def);
							Write(" = ");
							if (def.Value == null)
								WriteNew(klass, FuPriority.Argument);
							else
								WriteCoercedExpr(def.Type, def.Value);
							WriteCharLine(';');
						}
						base.WriteInitCode(def);
						break;
					}
				}
			}
		}

		protected override void WriteNewArray(FuType elementType, FuExpr lengthExpr, FuPriority parent)
		{
			Write("new ");
			WriteType(elementType, false);
			WriteChar('[');
			lengthExpr.Accept(this, FuPriority.Argument);
			WriteChar(']');
		}

		void WriteStaticInitializer(FuType type)
		{
			WriteChar('(');
			WriteType(type, false);
			Write(").init");
		}

		protected override void WriteNew(FuReadWriteClassType klass, FuPriority parent)
		{
			if (IsCreateWithNew(klass)) {
				Write("new ");
				WriteType(klass, false);
			}
			else
				WriteStaticInitializer(klass);
		}

		protected override void WriteResource(string name, int length)
		{
			Write("FuResource.");
			WriteResourceName(name);
		}

		protected override void WriteStringLength(FuExpr expr)
		{
			WritePostfix(expr, ".length");
		}

		void WriteClassReference(FuExpr expr, FuPriority priority = FuPriority.Primary)
		{
			if (IsStructPtr(expr.Type)) {
				Write("(*");
				expr.Accept(this, priority);
				WriteChar(')');
			}
			else
				expr.Accept(this, priority);
		}

		internal override void VisitSymbolReference(FuSymbolReference expr, FuPriority parent)
		{
			switch (expr.Symbol.Id) {
			case FuId.ConsoleError:
				Write("stderr");
				break;
			case FuId.ListCount:
			case FuId.StackCount:
			case FuId.HashSetCount:
			case FuId.DictionaryCount:
			case FuId.SortedSetCount:
			case FuId.SortedDictionaryCount:
				WriteStringLength(expr.Left);
				break;
			case FuId.QueueCount:
				Include("std.range");
				WriteClassReference(expr.Left);
				Write("[].walkLength");
				break;
			case FuId.MatchStart:
				WritePostfix(expr.Left, ".pre.length");
				break;
			case FuId.MatchEnd:
				if (parent > FuPriority.Add)
					WriteChar('(');
				WritePostfix(expr.Left, ".pre.length + ");
				WritePostfix(expr.Left, ".hit.length");
				if (parent > FuPriority.Add)
					WriteChar(')');
				break;
			case FuId.MatchLength:
				WritePostfix(expr.Left, ".hit.length");
				break;
			case FuId.MatchValue:
				WritePostfix(expr.Left, ".hit");
				break;
			case FuId.MathNaN:
				Write("double.nan");
				break;
			case FuId.MathNegativeInfinity:
				Write("-double.infinity");
				break;
			case FuId.MathPositiveInfinity:
				Write("double.infinity");
				break;
			default:
				base.VisitSymbolReference(expr, parent);
				break;
			}
		}

		void WriteWrite(List<FuExpr> args, bool newLine)
		{
			Include("std.stdio");
			if (args.Count == 0)
				Write("writeln()");
			else if (args[0] is FuInterpolatedString interpolated) {
				Write(newLine ? "writefln(" : "writef(");
				WritePrintf(interpolated, false);
			}
			else
				WriteCall(newLine ? "writeln" : "write", args[0]);
		}

		void WriteInsertedArg(FuType type, List<FuExpr> args, int index = 0)
		{
			if (args.Count <= index) {
				FuReadWriteClassType klass = (FuReadWriteClassType) type;
				WriteNew(klass, FuPriority.Argument);
			}
			else
				WriteCoercedExpr(type, args[index]);
			WriteChar(')');
		}

		protected override void WriteCallExpr(FuExpr obj, FuMethod method, List<FuExpr> args, FuPriority parent)
		{
			switch (method.Id) {
			case FuId.EnumFromInt:
				WriteStaticCast(method.Type, args[0]);
				break;
			case FuId.EnumHasFlag:
				WriteEnumHasFlag(obj, args, parent);
				break;
			case FuId.IntTryParse:
			case FuId.LongTryParse:
			case FuId.DoubleTryParse:
				Include("std.conv");
				Write("() { try { ");
				WritePostfix(obj, " = ");
				WritePostfix(args[0], ".to!");
				Write(obj.Type.Name);
				if (args.Count == 2) {
					WriteChar('(');
					args[1].Accept(this, FuPriority.Argument);
					WriteChar(')');
				}
				Write("; return true; } catch (ConvException e) return false; }()");
				break;
			case FuId.StringContains:
				Include("std.algorithm");
				WritePostfix(obj, ".canFind");
				WriteArgsInParentheses(method, args);
				break;
			case FuId.StringEndsWith:
				Include("std.string");
				WriteMethodCall(obj, "endsWith", args[0]);
				break;
			case FuId.StringIndexOf:
				Include("std.string");
				WriteMethodCall(obj, "indexOf", args[0]);
				break;
			case FuId.StringLastIndexOf:
				Include("std.string");
				WriteMethodCall(obj, "lastIndexOf", args[0]);
				break;
			case FuId.StringReplace:
				Include("std.string");
				WriteMethodCall(obj, "replace", args[0], args[1]);
				break;
			case FuId.StringStartsWith:
				Include("std.string");
				WriteMethodCall(obj, "startsWith", args[0]);
				break;
			case FuId.StringSubstring:
				obj.Accept(this, FuPriority.Primary);
				WriteChar('[');
				WritePostfix(args[0], " .. $]");
				if (args.Count > 1) {
					Write("[0 .. ");
					args[1].Accept(this, FuPriority.Argument);
					WriteChar(']');
				}
				break;
			case FuId.ArrayBinarySearchAll:
			case FuId.ArrayBinarySearchPart:
				Include("std.range");
				Write("() { size_t fubegin = ");
				if (args.Count == 3)
					args[1].Accept(this, FuPriority.Argument);
				else
					WriteChar('0');
				Write("; auto fusearch = ");
				WriteClassReference(obj);
				WriteChar('[');
				if (args.Count == 3) {
					Write("fubegin .. fubegin + ");
					args[2].Accept(this, FuPriority.Add);
				}
				Write("].assumeSorted.trisect(");
				WriteNotPromoted(obj.Type.AsClassType().GetElementType(), args[0]);
				Write("); return fusearch[1].length ? fubegin + fusearch[0].length : -1; }()");
				break;
			case FuId.ArrayContains:
			case FuId.ListContains:
				Include("std.algorithm");
				WriteClassReference(obj);
				WriteCall("[].canFind", args[0]);
				break;
			case FuId.ArrayCopyTo:
			case FuId.ListCopyTo:
				Include("std.algorithm");
				WriteClassReference(obj);
				WriteChar('[');
				args[0].Accept(this, FuPriority.Argument);
				Write(" .. $][0 .. ");
				args[3].Accept(this, FuPriority.Argument);
				Write("].copy(");
				args[1].Accept(this, FuPriority.Argument);
				WriteChar('[');
				args[2].Accept(this, FuPriority.Argument);
				Write(" .. $])");
				break;
			case FuId.ArrayFillAll:
			case FuId.ArrayFillPart:
				Include("std.algorithm");
				WriteClassReference(obj);
				WriteChar('[');
				if (args.Count == 3) {
					args[1].Accept(this, FuPriority.Argument);
					Write(" .. $][0 .. ");
					args[2].Accept(this, FuPriority.Argument);
				}
				Write("].fill(");
				WriteNotPromoted(obj.Type.AsClassType().GetElementType(), args[0]);
				WriteChar(')');
				break;
			case FuId.ArraySortAll:
			case FuId.ArraySortPart:
			case FuId.ListSortAll:
			case FuId.ListSortPart:
				Include("std.algorithm");
				WriteClassReference(obj);
				WriteChar('[');
				if (args.Count == 2) {
					args[0].Accept(this, FuPriority.Argument);
					Write(" .. $][0 .. ");
					args[1].Accept(this, FuPriority.Argument);
				}
				Write("].sort");
				break;
			case FuId.ListAdd:
			case FuId.QueueEnqueue:
				WritePostfix(obj, ".insertBack(");
				WriteInsertedArg(obj.Type.AsClassType().GetElementType(), args);
				break;
			case FuId.ListAddRange:
				WriteClassReference(obj);
				Write(" ~= ");
				WriteClassReference(args[0]);
				Write("[]");
				break;
			case FuId.ListAll:
				Include("std.algorithm");
				WriteClassReference(obj);
				WriteCall("[].all!", args[0]);
				break;
			case FuId.ListAny:
				Include("std.algorithm");
				WriteClassReference(obj);
				Write("[].any!(");
				args[0].Accept(this, FuPriority.Argument);
				WriteChar(')');
				break;
			case FuId.ListInsert:
				this.HasListInsert = true;
				WritePostfix(obj, ".insertInPlace(");
				args[0].Accept(this, FuPriority.Argument);
				Write(", ");
				WriteInsertedArg(obj.Type.AsClassType().GetElementType(), args, 1);
				break;
			case FuId.ListLast:
				WritePostfix(obj, ".back");
				break;
			case FuId.ListRemoveAt:
			case FuId.ListRemoveRange:
				this.HasListRemoveAt = true;
				WritePostfix(obj, ".removeAt");
				WriteArgsInParentheses(method, args);
				break;
			case FuId.ListIndexOf:
				Include("std.algorithm");
				WriteClassReference(obj);
				Write("[].countUntil");
				WriteArgsInParentheses(method, args);
				break;
			case FuId.QueueDequeue:
				this.HasQueueDequeue = true;
				Include("std.container.dlist");
				WriteClassReference(obj);
				Write(".dequeue()");
				break;
			case FuId.QueuePeek:
				WritePostfix(obj, ".front");
				break;
			case FuId.StackPeek:
				WritePostfix(obj, ".back");
				break;
			case FuId.StackPush:
				WriteClassReference(obj);
				Write(" ~= ");
				args[0].Accept(this, FuPriority.Assign);
				break;
			case FuId.StackPop:
				this.HasStackPop = true;
				WriteClassReference(obj);
				Write(".pop()");
				break;
			case FuId.HashSetAdd:
				WritePostfix(obj, ".require(");
				args[0].Accept(this, FuPriority.Argument);
				Write(", true)");
				break;
			case FuId.HashSetClear:
			case FuId.DictionaryClear:
				WritePostfix(obj, ".clear()");
				break;
			case FuId.HashSetContains:
			case FuId.DictionaryContainsKey:
				WriteChar('(');
				args[0].Accept(this, FuPriority.Rel);
				Write(" in ");
				obj.Accept(this, FuPriority.Primary);
				WriteChar(')');
				break;
			case FuId.SortedSetAdd:
				WritePostfix(obj, ".insert(");
				WriteInsertedArg(obj.Type.AsClassType().GetElementType(), args, 0);
				break;
			case FuId.SortedSetRemove:
				WritePostfix(obj, ".removeKey");
				WriteArgsInParentheses(method, args);
				break;
			case FuId.DictionaryAdd:
				if (obj.Type.AsClassType().Class.Id == FuId.SortedDictionaryClass) {
					this.HasSortedDictionaryInsert = true;
					WritePostfix(obj, ".replace(");
				}
				else
					WritePostfix(obj, ".require(");
				args[0].Accept(this, FuPriority.Argument);
				Write(", ");
				WriteInsertedArg(obj.Type.AsClassType().GetValueType(), args, 1);
				break;
			case FuId.SortedDictionaryContainsKey:
				Write("tuple(");
				args[0].Accept(this, FuPriority.Argument);
				Write(", ");
				WriteStaticInitializer(obj.Type.AsClassType().GetValueType());
				Write(") in ");
				WriteClassReference(obj);
				break;
			case FuId.SortedDictionaryRemove:
				WriteClassReference(obj);
				Write(".removeKey(tuple(");
				args[0].Accept(this, FuPriority.Argument);
				Write(", ");
				WriteStaticInitializer(obj.Type.AsClassType().GetValueType());
				Write("))");
				break;
			case FuId.TextWriterWrite:
			case FuId.TextWriterWriteLine:
				WritePostfix(obj, ".");
				WriteWrite(args, method.Id == FuId.TextWriterWriteLine);
				break;
			case FuId.TextWriterWriteChar:
				WritePostfix(obj, ".write(");
				if (!(args[0] is FuLiteralChar))
					Write("cast(char) ");
				args[0].Accept(this, FuPriority.Primary);
				WriteChar(')');
				break;
			case FuId.TextWriterWriteCodePoint:
				WritePostfix(obj, ".write(cast(dchar) ");
				args[0].Accept(this, FuPriority.Primary);
				WriteChar(')');
				break;
			case FuId.ConsoleWrite:
			case FuId.ConsoleWriteLine:
				WriteWrite(args, method.Id == FuId.ConsoleWriteLine);
				break;
			case FuId.EnvironmentGetEnvironmentVariable:
				Include("std.process");
				Write("environment.get");
				WriteArgsInParentheses(method, args);
				break;
			case FuId.UTF8GetByteCount:
				WritePostfix(args[0], ".length");
				break;
			case FuId.UTF8GetBytes:
				Include("std.string");
				Include("std.algorithm");
				WritePostfix(args[0], ".representation.copy(");
				WritePostfix(args[1], "[");
				args[2].Accept(this, FuPriority.Argument);
				Write(" .. $])");
				break;
			case FuId.UTF8GetString:
				Write("cast(string) (");
				WritePostfix(args[0], "[");
				args[1].Accept(this, FuPriority.Argument);
				Write(" .. $][0 .. ");
				args[2].Accept(this, FuPriority.Argument);
				Write("])");
				break;
			case FuId.RegexCompile:
				Include("std.regex");
				Write("regex(");
				args[0].Accept(this, FuPriority.Argument);
				WriteRegexOptions(args, ", \"", "", "\"", "i", "m", "s");
				WriteChar(')');
				break;
			case FuId.RegexEscape:
				Include("std.regex");
				Include("std.conv");
				WritePostfix(args[0], ".escaper.to!string");
				break;
			case FuId.RegexIsMatchRegex:
				Include("std.regex");
				WritePostfix(args[0], ".matchFirst(");
				(args.Count > 1 ? args[1] : obj).Accept(this, FuPriority.Argument);
				WriteChar(')');
				break;
			case FuId.RegexIsMatchStr:
				Include("std.regex");
				WritePostfix(args[0], ".matchFirst(");
				if (GetRegexOptions(args) != RegexOptions.None)
					Write("regex(");
				(args.Count > 1 ? args[1] : obj).Accept(this, FuPriority.Argument);
				WriteRegexOptions(args, ", \"", "", "\")", "i", "m", "s");
				WriteChar(')');
				break;
			case FuId.MatchFindStr:
				Include("std.regex");
				WriteChar('(');
				obj.Accept(this, FuPriority.Assign);
				Write(" = ");
				args[0].Accept(this, FuPriority.Primary);
				Write(".matchFirst(");
				if (GetRegexOptions(args) != RegexOptions.None)
					Write("regex(");
				args[1].Accept(this, FuPriority.Argument);
				WriteRegexOptions(args, ", \"", "", "\")", "i", "m", "s");
				Write("))");
				break;
			case FuId.MatchFindRegex:
				Include("std.regex");
				WriteChar('(');
				obj.Accept(this, FuPriority.Assign);
				Write(" = ");
				WriteMethodCall(args[0], "matchFirst", args[1]);
				WriteChar(')');
				break;
			case FuId.MatchGetCapture:
				WriteIndexing(obj, args[0]);
				break;
			case FuId.MathMethod:
			case FuId.MathAbs:
			case FuId.MathIsFinite:
			case FuId.MathIsInfinity:
			case FuId.MathIsNaN:
			case FuId.MathLog2:
			case FuId.MathRound:
				Include("std.math");
				WriteCamelCase(method.Name);
				WriteArgsInParentheses(method, args);
				break;
			case FuId.MathCeiling:
				Include("std.math");
				WriteCall("ceil", args[0]);
				break;
			case FuId.MathClamp:
			case FuId.MathMaxInt:
			case FuId.MathMaxDouble:
			case FuId.MathMinInt:
			case FuId.MathMinDouble:
				Include("std.algorithm");
				WriteLowercase(method.Name);
				WriteArgsInParentheses(method, args);
				break;
			case FuId.MathFusedMultiplyAdd:
				Include("std.math");
				WriteCall("fma", args[0], args[1], args[2]);
				break;
			case FuId.MathTruncate:
				Include("std.math");
				WriteCall("trunc", args[0]);
				break;
			default:
				if (obj != null) {
					if (IsReferenceTo(obj, FuId.BasePtr))
						Write("super.");
					else {
						WriteClassReference(obj);
						WriteChar('.');
					}
				}
				WriteName(method);
				WriteArgsInParentheses(method, args);
				break;
			}
		}

		protected override void WriteIndexingExpr(FuBinaryExpr expr, FuPriority parent)
		{
			WriteClassReference(expr.Left);
			FuClassType klass = (FuClassType) expr.Left.Type;
			switch (klass.Class.Id) {
			case FuId.ArrayPtrClass:
			case FuId.ArrayStorageClass:
			case FuId.DictionaryClass:
			case FuId.ListClass:
				WriteChar('[');
				expr.Right.Accept(this, FuPriority.Argument);
				WriteChar(']');
				break;
			case FuId.SortedDictionaryClass:
				Debug.Assert(parent != FuPriority.Assign);
				this.HasSortedDictionaryFind = true;
				Include("std.container.rbtree");
				Include("std.typecons");
				Write(".find(");
				WriteStronglyCoerced(klass.GetKeyType(), expr.Right);
				WriteChar(')');
				break;
			case FuId.OrderedDictionaryClass:
				NotSupported(expr, "OrderedDictionary");
				break;
			default:
				throw new NotImplementedException();
			}
		}

		static bool IsIsComparable(FuExpr expr) => expr is FuLiteralNull || (expr.Type is FuClassType klass && klass.Class.Id == FuId.ArrayPtrClass);

		protected override void WriteEqual(FuExpr left, FuExpr right, FuPriority parent, bool not)
		{
			if (IsIsComparable(left) || IsIsComparable(right))
				WriteEqualExpr(left, right, parent, not ? " !is " : " is ");
			else
				base.WriteEqual(left, right, parent, not);
		}

		protected override void WriteAssign(FuBinaryExpr expr, FuPriority parent)
		{
			if (expr.Left is FuBinaryExpr indexing && indexing.Op == FuToken.LeftBracket && indexing.Left.Type is FuClassType dict) {
				switch (dict.Class.Id) {
				case FuId.SortedDictionaryClass:
					this.HasSortedDictionaryInsert = true;
					WritePostfix(indexing.Left, ".replace(");
					indexing.Right.Accept(this, FuPriority.Argument);
					Write(", ");
					WriteNotPromoted(expr.Type, expr.Right);
					WriteChar(')');
					return;
				default:
					break;
				}
			}
			base.WriteAssign(expr, parent);
		}

		void WriteIsVar(FuExpr expr, FuVar def, FuPriority parent)
		{
			FuPriority thisPriority = def.Name == "_" ? FuPriority.Primary : FuPriority.Assign;
			if (parent > thisPriority)
				WriteChar('(');
			if (def.Name != "_") {
				WriteName(def);
				Write(" = ");
			}
			Write("cast(");
			WriteType(def.Type, true);
			Write(") ");
			expr.Accept(this, FuPriority.Primary);
			if (parent > thisPriority)
				WriteChar(')');
		}

		internal override void VisitBinaryExpr(FuBinaryExpr expr, FuPriority parent)
		{
			switch (expr.Op) {
			case FuToken.Is:
				if (parent >= FuPriority.Or && parent <= FuPriority.Mul)
					parent = FuPriority.Primary;
				if (parent > FuPriority.Equality)
					WriteChar('(');
				switch (expr.Right) {
				case FuSymbolReference symbol:
					Write("cast(");
					Write(symbol.Symbol.Name);
					Write(") ");
					expr.Left.Accept(this, FuPriority.Primary);
					break;
				case FuVar def:
					WriteIsVar(expr.Left, def, FuPriority.Equality);
					break;
				default:
					throw new NotImplementedException();
				}
				Write(" !is null");
				if (parent > FuPriority.Equality)
					WriteChar(')');
				return;
			case FuToken.Plus:
				if (expr.Type.Id == FuId.StringStorageType) {
					expr.Left.Accept(this, FuPriority.Assign);
					Write(" ~ ");
					expr.Right.Accept(this, FuPriority.Assign);
					return;
				}
				break;
			case FuToken.AddAssign:
				if (expr.Left.Type.Id == FuId.StringStorageType) {
					expr.Left.Accept(this, FuPriority.Assign);
					Write(" ~= ");
					WriteAssignRight(expr);
					return;
				}
				break;
			default:
				break;
			}
			base.VisitBinaryExpr(expr, parent);
		}

		internal override void VisitLambdaExpr(FuLambdaExpr expr)
		{
			WriteName(expr.First);
			Write(" => ");
			expr.Body.Accept(this, FuPriority.Statement);
		}

		protected override void WriteAssert(FuAssert statement)
		{
			Write("assert(");
			statement.Cond.Accept(this, FuPriority.Argument);
			if (statement.Message != null) {
				Write(", ");
				statement.Message.Accept(this, FuPriority.Argument);
			}
			WriteLine(");");
		}

		internal override void VisitForeach(FuForeach statement)
		{
			Write("foreach (");
			if (statement.Collection.Type is FuClassType dict && dict.Class.TypeParameterCount == 2) {
				WriteTypeAndName(statement.GetVar());
				Write(", ");
				WriteTypeAndName(statement.GetValueVar());
			}
			else
				WriteTypeAndName(statement.GetVar());
			Write("; ");
			WriteClassReference(statement.Collection);
			if (statement.Collection.Type is FuClassType set && set.Class.Id == FuId.HashSetClass)
				Write(".byKey");
			WriteChar(')');
			WriteChild(statement.Body);
		}

		internal override void VisitLock(FuLock statement)
		{
			WriteCall("synchronized ", statement.Lock);
			WriteChild(statement.Body);
		}

		protected override void WriteSwitchCaseTypeVar(FuExpr value)
		{
			DefineVar(value);
		}

		protected override void WriteSwitchCaseCond(FuSwitch statement, FuExpr value, FuPriority parent)
		{
			if (value is FuVar def) {
				WriteIsVar(statement.Value, def, FuPriority.Equality);
				Write(" !is null");
			}
			else
				base.WriteSwitchCaseCond(statement, value, parent);
		}

		internal override void VisitSwitch(FuSwitch statement)
		{
			WriteTemporaries(statement.Value);
			if (statement.IsTypeMatching() || statement.HasWhen())
				WriteSwitchAsIfsWithGoto(statement);
			else {
				StartSwitch(statement);
				WriteLine("default:");
				this.Indent++;
				if (statement.DefaultBody.Count > 0)
					WriteSwitchCaseBody(statement.DefaultBody);
				else
					WriteLine("assert(false);");
				this.Indent--;
				WriteCharLine('}');
			}
		}

		internal override void VisitThrow(FuThrow statement)
		{
			Include("std.exception");
			Write("throw new Exception(");
			statement.Message.Accept(this, FuPriority.Argument);
			WriteLine(");");
		}

		protected override void WriteEnum(FuEnum enu)
		{
			WriteNewLine();
			WriteDoc(enu.Documentation);
			WritePublic(enu);
			Write("enum ");
			Write(enu.Name);
			OpenBlock();
			enu.AcceptValues(this);
			WriteNewLine();
			CloseBlock();
		}

		protected override void WriteConst(FuConst konst)
		{
			WriteDoc(konst.Documentation);
			Write("static immutable ");
			WriteTypeAndName(konst);
			Write(" = ");
			WriteCoercedExpr(konst.Type, konst.Value);
			WriteCharLine(';');
		}

		protected override void WriteField(FuField field)
		{
			WriteNewLine();
			WriteDoc(field.Documentation);
			WriteVisibility(field.Visibility);
			WriteTypeAndName(field);
			if (field.Value is FuLiteral) {
				Write(" = ");
				WriteCoercedExpr(field.Type, field.Value);
			}
			WriteCharLine(';');
		}

		protected override bool IsShortMethod(FuMethod method) => method.Body is FuReturn ret && !HasTemporaries(ret.Value);

		protected override void WriteMethod(FuMethod method)
		{
			if (method.Id == FuId.ClassToString && method.CallType == FuCallType.Abstract)
				return;
			WriteNewLine();
			WriteDoc(method.Documentation);
			WriteParametersDoc(method);
			WriteVisibility(method.Visibility);
			if (method.Id == FuId.ClassToString)
				Write("override ");
			else
				WriteCallType(method.CallType, "final override ");
			WriteTypeAndName(method);
			WriteParameters(method, true);
			WriteBody(method);
		}

		protected override void WriteClass(FuClass klass, FuProgram program)
		{
			WriteNewLine();
			WriteDoc(klass.Documentation);
			if (klass.CallType == FuCallType.Sealed)
				Write("final ");
			OpenClass(klass, "", " : ");
			if (NeedsConstructor(klass)) {
				if (klass.Constructor != null) {
					WriteDoc(klass.Constructor.Documentation);
					WriteVisibility(klass.Constructor.Visibility);
				}
				else
					Write("private ");
				WriteLine("this()");
				OpenBlock();
				WriteConstructorBody(klass);
				CloseBlock();
			}
			for (FuSymbol symbol = klass.First; symbol != null; symbol = symbol.Next) {
				if (!(symbol is FuMember))
					continue;
				switch (symbol) {
				case FuConst konst:
					WriteConst(konst);
					break;
				case FuField field:
					WriteField(field);
					break;
				case FuMethod method:
					WriteMethod(method);
					this.CurrentTemporaries.Clear();
					break;
				case FuVar _:
					break;
				default:
					throw new NotImplementedException();
				}
			}
			CloseBlock();
		}

		static bool IsLong(FuSymbolReference expr)
		{
			switch (expr.Symbol.Id) {
			case FuId.ArrayLength:
			case FuId.StringLength:
			case FuId.ListCount:
				return true;
			default:
				return false;
			}
		}

		protected override void WriteCoercedInternal(FuType type, FuExpr expr, FuPriority parent)
		{
			if (type is FuRangeType)
				WriteStaticCast(type, expr);
			else if (type is FuIntegerType && expr is FuSymbolReference symref && IsLong(symref))
				WriteStaticCast(type, expr);
			else if (type is FuFloatingType && !(expr.Type is FuFloatingType))
				WriteStaticCast(type, expr);
			else if (type is FuClassType && !(type is FuArrayStorageType) && expr.Type is FuArrayStorageType) {
				base.WriteCoercedInternal(type, expr, FuPriority.Primary);
				Write("[]");
			}
			else
				base.WriteCoercedInternal(type, expr, parent);
		}

		void WriteResources(SortedDictionary<string, List<byte>> resources)
		{
			WriteNewLine();
			WriteLine("private static struct FuResource");
			OpenBlock();
			foreach ((string name, List<byte> content) in resources) {
				Write("private static immutable ubyte[] ");
				WriteResourceName(name);
				WriteLine(" = [");
				WriteChar('\t');
				WriteBytes(content);
				WriteLine(" ];");
			}
			CloseBlock();
		}

		public override void WriteProgram(FuProgram program)
		{
			this.HasListInsert = false;
			this.HasListRemoveAt = false;
			this.HasQueueDequeue = false;
			this.HasStackPop = false;
			this.HasSortedDictionaryInsert = false;
			this.HasSortedDictionaryFind = false;
			OpenStringWriter();
			if (this.Namespace.Length != 0) {
				Write("struct ");
				WriteLine(this.Namespace);
				OpenBlock();
				WriteLine("static:");
			}
			WriteTopLevelNatives(program);
			WriteTypes(program);
			if (program.Resources.Count > 0)
				WriteResources(program.Resources);
			if (this.Namespace.Length != 0)
				CloseBlock();
			CreateOutputFile();
			if (this.HasListInsert || this.HasListRemoveAt || this.HasStackPop)
				Include("std.container.array");
			if (this.HasSortedDictionaryInsert) {
				Include("std.container.rbtree");
				Include("std.typecons");
			}
			WriteIncludes("import ", ";");
			if (this.HasListInsert) {
				WriteNewLine();
				WriteLine("private void insertInPlace(T, U...)(Array!T* arr, size_t pos, auto ref U stuff)");
				OpenBlock();
				WriteLine("arr.insertAfter((*arr)[0 .. pos], stuff);");
				CloseBlock();
			}
			if (this.HasListRemoveAt) {
				WriteNewLine();
				WriteLine("private void removeAt(T)(Array!T* arr, size_t pos, size_t count = 1)");
				OpenBlock();
				WriteLine("arr.linearRemove((*arr)[pos .. pos + count]);");
				CloseBlock();
			}
			if (this.HasQueueDequeue) {
				WriteNewLine();
				WriteLine("private T dequeue(T)(ref DList!T q)");
				OpenBlock();
				WriteLine("scope(exit) q.removeFront(); return q.front;");
				CloseBlock();
			}
			if (this.HasStackPop) {
				WriteNewLine();
				WriteLine("private T pop(T)(ref Array!T stack)");
				OpenBlock();
				WriteLine("scope(exit) stack.removeBack(); return stack.back;");
				CloseBlock();
			}
			if (this.HasSortedDictionaryFind) {
				WriteNewLine();
				WriteLine("private U find(T, U)(RedBlackTree!(Tuple!(T, U), \"a[0] < b[0]\") dict, T key)");
				OpenBlock();
				WriteLine("return dict.equalRange(tuple(key, U.init)).front[1];");
				CloseBlock();
			}
			if (this.HasSortedDictionaryInsert) {
				WriteNewLine();
				WriteLine("private void replace(T, U)(RedBlackTree!(Tuple!(T, U), \"a[0] < b[0]\") dict, T key, lazy U value)");
				OpenBlock();
				WriteLine("dict.removeKey(tuple(key, U.init));");
				WriteLine("dict.insert(tuple(key, value));");
				CloseBlock();
			}
			CloseStringWriter();
			CloseFile();
		}
	}

	public class GenJava : GenTyped
	{

		int SwitchCaseDiscards;

		protected override string GetTargetName() => "Java";

		internal override void VisitLiteralLong(long value)
		{
			base.VisitLiteralLong(value);
			if (value < -2147483648 || value > 2147483647)
				WriteChar('L');
		}

		protected override int GetLiteralChars() => 65536;

		void WriteToString(FuExpr expr, FuPriority parent)
		{
			switch (expr.Type.Id) {
			case FuId.LongType:
				Write("Long");
				break;
			case FuId.FloatType:
				Write("Float");
				break;
			case FuId.DoubleType:
			case FuId.FloatIntType:
				Write("Double");
				break;
			case FuId.StringPtrType:
			case FuId.StringStorageType:
				expr.Accept(this, parent);
				return;
			default:
				if (expr.Type is FuIntegerType)
					Write("Integer");
				else if (expr.Type is FuClassType) {
					WritePostfix(expr, ".toString()");
					return;
				}
				else
					throw new NotImplementedException();
				break;
			}
			WriteCall(".toString", expr);
		}

		protected override void WritePrintfWidth(FuInterpolatedPart part)
		{
			if (part.Precision >= 0 && part.Argument.Type is FuIntegerType) {
				WriteChar('0');
				VisitLiteralLong(part.Precision);
			}
			else
				base.WritePrintfWidth(part);
		}

		internal override void VisitInterpolatedString(FuInterpolatedString expr, FuPriority parent)
		{
			if (expr.Suffix.Length == 0 && expr.Parts.Count == 1 && expr.Parts[0].Prefix.Length == 0 && expr.Parts[0].WidthExpr == null && expr.Parts[0].Format == ' ')
				WriteToString(expr.Parts[0].Argument, parent);
			else {
				Write("String.format(");
				WritePrintf(expr, false);
			}
		}

		void WriteCamelCaseNotKeyword(string name)
		{
			WriteCamelCase(name);
			switch (name) {
			case "Abstract":
			case "Assert":
			case "Boolean":
			case "Break":
			case "Byte":
			case "Case":
			case "Catch":
			case "Char":
			case "Class":
			case "Const":
			case "Continue":
			case "Default":
			case "Do":
			case "Double":
			case "Else":
			case "Enum":
			case "Extends":
			case "False":
			case "Final":
			case "Finally":
			case "Float":
			case "For":
			case "Foreach":
			case "Goto":
			case "If":
			case "Implements":
			case "Import":
			case "Instanceof":
			case "Int":
			case "Interface":
			case "Long":
			case "Native":
			case "New":
			case "Null":
			case "Package":
			case "Private":
			case "Protected":
			case "Public":
			case "Return":
			case "Short":
			case "Static":
			case "Strictfp":
			case "String":
			case "Super":
			case "Switch":
			case "Synchronized":
			case "Transient":
			case "Throw":
			case "Throws":
			case "True":
			case "Try":
			case "Void":
			case "Volatile":
			case "While":
			case "Yield":
			case "boolean":
			case "catch":
			case "char":
			case "extends":
			case "final":
			case "finally":
			case "goto":
			case "implements":
			case "import":
			case "instanceof":
			case "interface":
			case "package":
			case "private":
			case "strictfp":
			case "super":
			case "synchronized":
			case "transient":
			case "try":
			case "volatile":
			case "yield":
				WriteChar('_');
				break;
			default:
				break;
			}
		}

		protected override void WriteName(FuSymbol symbol)
		{
			switch (symbol) {
			case FuContainerType _:
				Write(symbol.Name);
				break;
			case FuConst konst:
				if (konst.InMethod != null) {
					WriteUppercaseWithUnderscores(konst.InMethod.Name);
					WriteChar('_');
				}
				WriteUppercaseWithUnderscores(symbol.Name);
				break;
			case FuVar _:
				if (symbol.Parent is FuForeach forEach && forEach.Count() == 2) {
					FuVar element = forEach.GetVar();
					WriteCamelCaseNotKeyword(element.Name);
					Write(symbol == element ? ".getKey()" : ".getValue()");
				}
				else
					WriteCamelCaseNotKeyword(symbol.Name);
				break;
			case FuMember _:
				WriteCamelCaseNotKeyword(symbol.Name);
				break;
			default:
				throw new NotImplementedException();
			}
		}

		void WriteVisibility(FuVisibility visibility)
		{
			switch (visibility) {
			case FuVisibility.Private:
				Write("private ");
				break;
			case FuVisibility.Internal:
				break;
			case FuVisibility.Protected:
				Write("protected ");
				break;
			case FuVisibility.Public:
				Write("public ");
				break;
			default:
				throw new NotImplementedException();
			}
		}

		protected override FuId GetTypeId(FuType type, bool promote)
		{
			FuId id = base.GetTypeId(type, promote);
			switch (id) {
			case FuId.ByteRange:
				return FuId.SByteRange;
			case FuId.UShortRange:
				return FuId.IntType;
			default:
				return id;
			}
		}

		static bool IsJavaEnum(FuEnum enu)
		{
			for (FuSymbol symbol = enu.First; symbol != null; symbol = symbol.Next) {
				if (symbol is FuConst konst && !(konst.Value is FuImplicitEnumValue))
					return false;
			}
			return true;
		}

		void WriteCollectionType(string name, FuType elementType)
		{
			Include("java.util." + name);
			Write(name);
			WriteChar('<');
			WriteJavaType(elementType, false, true);
			WriteChar('>');
		}

		void WriteDictType(string name, FuClassType dict)
		{
			Write(name);
			WriteChar('<');
			WriteJavaType(dict.GetKeyType(), false, true);
			Write(", ");
			WriteJavaType(dict.GetValueType(), false, true);
			WriteChar('>');
		}

		void WriteJavaType(FuType type, bool promote, bool needClass)
		{
			switch (type) {
			case FuNumericType _:
				switch (GetTypeId(type, promote)) {
				case FuId.SByteRange:
					Write(needClass ? "Byte" : "byte");
					break;
				case FuId.ShortRange:
					Write(needClass ? "Short" : "short");
					break;
				case FuId.IntType:
					Write(needClass ? "Integer" : "int");
					break;
				case FuId.LongType:
					Write(needClass ? "Long" : "long");
					break;
				case FuId.FloatType:
					Write(needClass ? "Float" : "float");
					break;
				case FuId.DoubleType:
					Write(needClass ? "Double" : "double");
					break;
				default:
					throw new NotImplementedException();
				}
				break;
			case FuEnum enu:
				Write(enu.Id == FuId.BoolType ? needClass ? "Boolean" : "boolean" : IsJavaEnum(enu) ? enu.Name : needClass ? "Integer" : "int");
				break;
			case FuClassType klass:
				switch (klass.Class.Id) {
				case FuId.StringClass:
					Write("String");
					break;
				case FuId.ArrayPtrClass:
				case FuId.ArrayStorageClass:
					WriteType(klass.GetElementType(), false);
					Write("[]");
					break;
				case FuId.ListClass:
					WriteCollectionType("ArrayList", klass.GetElementType());
					break;
				case FuId.QueueClass:
					WriteCollectionType("ArrayDeque", klass.GetElementType());
					break;
				case FuId.StackClass:
					WriteCollectionType("Stack", klass.GetElementType());
					break;
				case FuId.HashSetClass:
					WriteCollectionType("HashSet", klass.GetElementType());
					break;
				case FuId.SortedSetClass:
					WriteCollectionType("TreeSet", klass.GetElementType());
					break;
				case FuId.DictionaryClass:
					Include("java.util.HashMap");
					WriteDictType("HashMap", klass);
					break;
				case FuId.SortedDictionaryClass:
					Include("java.util.TreeMap");
					WriteDictType("TreeMap", klass);
					break;
				case FuId.OrderedDictionaryClass:
					Include("java.util.LinkedHashMap");
					WriteDictType("LinkedHashMap", klass);
					break;
				case FuId.TextWriterClass:
					Write("Appendable");
					break;
				case FuId.StringWriterClass:
					Include("java.io.StringWriter");
					Write("StringWriter");
					break;
				case FuId.RegexClass:
					Include("java.util.regex.Pattern");
					Write("Pattern");
					break;
				case FuId.MatchClass:
					Include("java.util.regex.Matcher");
					Write("Matcher");
					break;
				case FuId.LockClass:
					Write("Object");
					break;
				default:
					Write(klass.Class.Name);
					break;
				}
				break;
			default:
				Write(type.Name);
				break;
			}
		}

		protected override void WriteType(FuType type, bool promote)
		{
			WriteJavaType(type, promote, false);
		}

		protected override void WriteNew(FuReadWriteClassType klass, FuPriority parent)
		{
			Write("new ");
			WriteType(klass, false);
			Write("()");
		}

		protected override void WriteResource(string name, int length)
		{
			Write("FuResource.getByteArray(");
			VisitLiteralString(name);
			Write(", ");
			VisitLiteralLong(length);
			WriteChar(')');
		}

		static bool IsUnsignedByte(FuType type) => type.Id == FuId.ByteRange && type is FuRangeType range && range.Max > 127;

		static bool IsUnsignedByteIndexing(FuExpr expr) => expr.IsIndexing() && IsUnsignedByte(expr.Type);

		void WriteIndexingInternal(FuBinaryExpr expr)
		{
			if (expr.Left.Type.IsArray())
				base.WriteIndexingExpr(expr, FuPriority.And);
			else
				WriteMethodCall(expr.Left, "get", expr.Right);
		}

		internal override void VisitPrefixExpr(FuPrefixExpr expr, FuPriority parent)
		{
			if ((expr.Op == FuToken.Increment || expr.Op == FuToken.Decrement) && IsUnsignedByteIndexing(expr.Inner)) {
				if (parent > FuPriority.And)
					WriteChar('(');
				Write(expr.Op == FuToken.Increment ? "++" : "--");
				FuBinaryExpr indexing = (FuBinaryExpr) expr.Inner;
				WriteIndexingInternal(indexing);
				if (parent != FuPriority.Statement)
					Write(" & 0xff");
				if (parent > FuPriority.And)
					WriteChar(')');
			}
			else
				base.VisitPrefixExpr(expr, parent);
		}

		internal override void VisitPostfixExpr(FuPostfixExpr expr, FuPriority parent)
		{
			if ((expr.Op == FuToken.Increment || expr.Op == FuToken.Decrement) && IsUnsignedByteIndexing(expr.Inner)) {
				if (parent > FuPriority.And)
					WriteChar('(');
				FuBinaryExpr indexing = (FuBinaryExpr) expr.Inner;
				WriteIndexingInternal(indexing);
				Write(expr.Op == FuToken.Increment ? "++" : "--");
				if (parent != FuPriority.Statement)
					Write(" & 0xff");
				if (parent > FuPriority.And)
					WriteChar(')');
			}
			else
				base.VisitPostfixExpr(expr, parent);
		}

		void WriteSByteLiteral(FuLiteralLong literal)
		{
			if (literal.Value >= 128)
				Write("(byte) ");
			literal.Accept(this, FuPriority.Primary);
		}

		protected override void WriteEqual(FuExpr left, FuExpr right, FuPriority parent, bool not)
		{
			if ((left.Type is FuStringType && right.Type.Id != FuId.NullType) || (right.Type is FuStringType && left.Type.Id != FuId.NullType)) {
				if (not)
					WriteChar('!');
				WriteMethodCall(left, "equals", right);
			}
			else if (IsUnsignedByteIndexing(left) && right is FuLiteralLong rightLiteral && rightLiteral.Type.Id == FuId.ByteRange) {
				if (parent > FuPriority.Equality)
					WriteChar('(');
				FuBinaryExpr indexing = (FuBinaryExpr) left;
				WriteIndexingInternal(indexing);
				Write(GetEqOp(not));
				WriteSByteLiteral(rightLiteral);
				if (parent > FuPriority.Equality)
					WriteChar(')');
			}
			else
				base.WriteEqual(left, right, parent, not);
		}

		protected override void WriteCoercedLiteral(FuType type, FuExpr expr)
		{
			if (IsUnsignedByte(type)) {
				FuLiteralLong literal = (FuLiteralLong) expr;
				WriteSByteLiteral(literal);
			}
			else
				base.WriteCoercedLiteral(type, expr);
		}

		protected override void WriteRel(FuBinaryExpr expr, FuPriority parent, string op)
		{
			if (expr.Left.Type is FuEnum enu && IsJavaEnum(enu)) {
				if (parent > FuPriority.CondAnd)
					WriteChar('(');
				WriteMethodCall(expr.Left, "compareTo", expr.Right);
				Write(op);
				WriteChar('0');
				if (parent > FuPriority.CondAnd)
					WriteChar(')');
			}
			else
				base.WriteRel(expr, parent, op);
		}

		protected override void WriteAnd(FuBinaryExpr expr, FuPriority parent)
		{
			if (IsUnsignedByteIndexing(expr.Left) && expr.Right is FuLiteralLong rightLiteral) {
				if (parent > FuPriority.CondAnd && parent != FuPriority.And)
					WriteChar('(');
				FuBinaryExpr indexing = (FuBinaryExpr) expr.Left;
				WriteIndexingInternal(indexing);
				Write(" & ");
				VisitLiteralLong(255 & rightLiteral.Value);
				if (parent > FuPriority.CondAnd && parent != FuPriority.And)
					WriteChar(')');
			}
			else
				base.WriteAnd(expr, parent);
		}

		protected override void WriteStringLength(FuExpr expr)
		{
			WritePostfix(expr, ".length()");
		}

		protected override void WriteCharAt(FuBinaryExpr expr)
		{
			WriteMethodCall(expr.Left, "charAt", expr.Right);
		}

		internal override void VisitSymbolReference(FuSymbolReference expr, FuPriority parent)
		{
			switch (expr.Symbol.Id) {
			case FuId.ConsoleError:
				Write("System.err");
				break;
			case FuId.ListCount:
			case FuId.QueueCount:
			case FuId.StackCount:
			case FuId.HashSetCount:
			case FuId.SortedSetCount:
			case FuId.DictionaryCount:
			case FuId.SortedDictionaryCount:
			case FuId.OrderedDictionaryCount:
				expr.Left.Accept(this, FuPriority.Primary);
				WriteMemberOp(expr.Left, expr);
				Write("size()");
				break;
			case FuId.MathNaN:
				Write("Float.NaN");
				break;
			case FuId.MathNegativeInfinity:
				Write("Float.NEGATIVE_INFINITY");
				break;
			case FuId.MathPositiveInfinity:
				Write("Float.POSITIVE_INFINITY");
				break;
			default:
				if (!WriteJavaMatchProperty(expr, parent))
					base.VisitSymbolReference(expr, parent);
				break;
			}
		}

		void WriteArrayBinarySearchFill(FuExpr obj, string method, List<FuExpr> args)
		{
			Include("java.util.Arrays");
			Write("Arrays.");
			Write(method);
			WriteChar('(');
			obj.Accept(this, FuPriority.Argument);
			Write(", ");
			if (args.Count == 3) {
				WriteStartEnd(args[1], args[2]);
				Write(", ");
			}
			WriteNotPromoted(obj.Type.AsClassType().GetElementType(), args[0]);
			WriteChar(')');
		}

		void WriteWrite(FuMethod method, List<FuExpr> args, bool newLine)
		{
			if (args.Count == 1 && args[0] is FuInterpolatedString interpolated) {
				Write(".format(");
				WritePrintf(interpolated, newLine);
			}
			else {
				Write(".print");
				if (newLine)
					Write("ln");
				WriteArgsInParentheses(method, args);
			}
		}

		void WriteCompileRegex(List<FuExpr> args, int argIndex)
		{
			Include("java.util.regex.Pattern");
			Write("Pattern.compile(");
			args[argIndex].Accept(this, FuPriority.Argument);
			WriteRegexOptions(args, ", ", " | ", "", "Pattern.CASE_INSENSITIVE", "Pattern.MULTILINE", "Pattern.DOTALL");
			WriteChar(')');
		}

		protected override void WriteCallExpr(FuExpr obj, FuMethod method, List<FuExpr> args, FuPriority parent)
		{
			switch (method.Id) {
			case FuId.None:
			case FuId.ClassToString:
			case FuId.StringContains:
			case FuId.StringEndsWith:
			case FuId.StringIndexOf:
			case FuId.StringLastIndexOf:
			case FuId.StringReplace:
			case FuId.StringStartsWith:
			case FuId.ListClear:
			case FuId.ListContains:
			case FuId.ListIndexOf:
			case FuId.QueueClear:
			case FuId.StackClear:
			case FuId.StackPeek:
			case FuId.StackPush:
			case FuId.StackPop:
			case FuId.HashSetAdd:
			case FuId.HashSetClear:
			case FuId.HashSetContains:
			case FuId.HashSetRemove:
			case FuId.SortedSetAdd:
			case FuId.SortedSetClear:
			case FuId.SortedSetContains:
			case FuId.SortedSetRemove:
			case FuId.DictionaryClear:
			case FuId.DictionaryContainsKey:
			case FuId.DictionaryRemove:
			case FuId.SortedDictionaryClear:
			case FuId.SortedDictionaryContainsKey:
			case FuId.SortedDictionaryRemove:
			case FuId.OrderedDictionaryClear:
			case FuId.OrderedDictionaryContainsKey:
			case FuId.OrderedDictionaryRemove:
			case FuId.StringWriterToString:
			case FuId.MathMethod:
			case FuId.MathAbs:
			case FuId.MathMaxInt:
			case FuId.MathMaxDouble:
			case FuId.MathMinInt:
			case FuId.MathMinDouble:
				if (obj != null) {
					if (IsReferenceTo(obj, FuId.BasePtr))
						Write("super");
					else
						obj.Accept(this, FuPriority.Primary);
					WriteChar('.');
				}
				WriteName(method);
				WriteArgsInParentheses(method, args);
				break;
			case FuId.EnumFromInt:
				args[0].Accept(this, parent);
				break;
			case FuId.EnumHasFlag:
				WriteEnumHasFlag(obj, args, parent);
				break;
			case FuId.DoubleTryParse:
				Include("java.util.function.DoubleSupplier");
				Write("!Double.isNaN(");
				obj.Accept(this, FuPriority.Assign);
				Write(" = ((DoubleSupplier) () -> { try { return Double.parseDouble(");
				args[0].Accept(this, FuPriority.Argument);
				Write("); } catch (NumberFormatException e) { return Double.NaN; } }).getAsDouble())");
				break;
			case FuId.StringSubstring:
				WritePostfix(obj, ".substring(");
				args[0].Accept(this, FuPriority.Argument);
				if (args.Count == 2) {
					Write(", ");
					WriteAdd(args[0], args[1]);
				}
				WriteChar(')');
				break;
			case FuId.ArrayBinarySearchAll:
			case FuId.ArrayBinarySearchPart:
				WriteArrayBinarySearchFill(obj, "binarySearch", args);
				break;
			case FuId.ArrayCopyTo:
				Write("System.arraycopy(");
				obj.Accept(this, FuPriority.Argument);
				Write(", ");
				WriteArgs(method, args);
				WriteChar(')');
				break;
			case FuId.ArrayFillAll:
			case FuId.ArrayFillPart:
				WriteArrayBinarySearchFill(obj, "fill", args);
				break;
			case FuId.ArraySortAll:
				Include("java.util.Arrays");
				WriteCall("Arrays.sort", obj);
				break;
			case FuId.ArraySortPart:
				Include("java.util.Arrays");
				Write("Arrays.sort(");
				obj.Accept(this, FuPriority.Argument);
				Write(", ");
				WriteStartEnd(args[0], args[1]);
				WriteChar(')');
				break;
			case FuId.ListAdd:
				WriteListAdd(obj, "add", args);
				break;
			case FuId.ListAddRange:
				WriteMethodCall(obj, "addAll", args[0]);
				break;
			case FuId.ListAll:
				WriteMethodCall(obj, "stream().allMatch", args[0]);
				break;
			case FuId.ListAny:
				WriteMethodCall(obj, "stream().anyMatch", args[0]);
				break;
			case FuId.ListCopyTo:
				Write("for (int _i = 0; _i < ");
				args[3].Accept(this, FuPriority.Rel);
				WriteLine("; _i++)");
				Write("\t");
				args[1].Accept(this, FuPriority.Primary);
				WriteChar('[');
				StartAdd(args[2]);
				Write("_i] = ");
				WritePostfix(obj, ".get(");
				StartAdd(args[0]);
				Write("_i)");
				break;
			case FuId.ListInsert:
				WriteListInsert(obj, "add", args);
				break;
			case FuId.ListLast:
				WritePostfix(obj, ".get(");
				WritePostfix(obj, ".size() - 1)");
				break;
			case FuId.ListRemoveAt:
				WriteMethodCall(obj, "remove", args[0]);
				break;
			case FuId.ListRemoveRange:
				WritePostfix(obj, ".subList(");
				WriteStartEnd(args[0], args[1]);
				Write(").clear()");
				break;
			case FuId.ListSortAll:
				WritePostfix(obj, ".sort(null)");
				break;
			case FuId.ListSortPart:
				WritePostfix(obj, ".subList(");
				WriteStartEnd(args[0], args[1]);
				Write(").sort(null)");
				break;
			case FuId.QueueDequeue:
				WritePostfix(obj, ".remove()");
				break;
			case FuId.QueueEnqueue:
				WriteMethodCall(obj, "add", args[0]);
				break;
			case FuId.QueuePeek:
				WritePostfix(obj, ".element()");
				break;
			case FuId.DictionaryAdd:
				WritePostfix(obj, ".put(");
				args[0].Accept(this, FuPriority.Argument);
				Write(", ");
				WriteNewStorage(obj.Type.AsClassType().GetValueType());
				WriteChar(')');
				break;
			case FuId.TextWriterWrite:
				if (IsReferenceTo(obj, FuId.ConsoleError)) {
					Write("System.err");
					WriteWrite(method, args, false);
				}
				else if (obj.Type.AsClassType().Class.Id == FuId.StringWriterClass) {
					WritePostfix(obj, ".append(");
					WriteToString(args[0], FuPriority.Argument);
					WriteChar(')');
				}
				else {
					Write("try { ");
					WritePostfix(obj, ".append(");
					WriteToString(args[0], FuPriority.Argument);
					Include("java.io.IOException");
					Write("); } catch (IOException e) { throw new RuntimeException(e); }");
				}
				break;
			case FuId.TextWriterWriteChar:
				if (IsReferenceTo(obj, FuId.ConsoleError))
					WriteCharMethodCall(obj, "print", args[0]);
				else if (obj.Type.AsClassType().Class.Id == FuId.StringWriterClass)
					WriteCharMethodCall(obj, "append", args[0]);
				else {
					Write("try { ");
					WriteCharMethodCall(obj, "append", args[0]);
					Include("java.io.IOException");
					Write("; } catch (IOException e) { throw new RuntimeException(e); }");
				}
				break;
			case FuId.TextWriterWriteCodePoint:
				if (IsReferenceTo(obj, FuId.ConsoleError)) {
					WriteCall("System.err.print(Character.toChars", args[0]);
					WriteChar(')');
				}
				else {
					Write("try { ");
					WriteMethodCall(obj, "append(Character.toString", args[0]);
					Include("java.io.IOException");
					Write("); } catch (IOException e) { throw new RuntimeException(e); }");
				}
				break;
			case FuId.TextWriterWriteLine:
				if (IsReferenceTo(obj, FuId.ConsoleError)) {
					Write("System.err");
					WriteWrite(method, args, true);
				}
				else {
					Write("try { ");
					WritePostfix(obj, ".append(");
					if (args.Count == 0)
						Write("'\\n'");
					else if (args[0] is FuInterpolatedString interpolated) {
						Write("String.format(");
						WritePrintf(interpolated, true);
					}
					else {
						WriteToString(args[0], FuPriority.Argument);
						Write(").append('\\n'");
					}
					Include("java.io.IOException");
					Write("); } catch (IOException e) { throw new RuntimeException(e); }");
				}
				break;
			case FuId.StringWriterClear:
				WritePostfix(obj, ".getBuffer().setLength(0)");
				break;
			case FuId.ConsoleWrite:
				Write("System.out");
				WriteWrite(method, args, false);
				break;
			case FuId.ConsoleWriteLine:
				Write("System.out");
				WriteWrite(method, args, true);
				break;
			case FuId.UTF8GetByteCount:
				Include("java.nio.charset.StandardCharsets");
				WritePostfix(args[0], ".getBytes(StandardCharsets.UTF_8).length");
				break;
			case FuId.UTF8GetBytes:
				Include("java.nio.ByteBuffer");
				Include("java.nio.CharBuffer");
				Include("java.nio.charset.StandardCharsets");
				Write("StandardCharsets.UTF_8.newEncoder().encode(CharBuffer.wrap(");
				args[0].Accept(this, FuPriority.Argument);
				Write("), ByteBuffer.wrap(");
				args[1].Accept(this, FuPriority.Argument);
				Write(", ");
				args[2].Accept(this, FuPriority.Argument);
				Write(", ");
				WritePostfix(args[1], ".length");
				if (!args[2].IsLiteralZero()) {
					Write(" - ");
					args[2].Accept(this, FuPriority.Mul);
				}
				Write("), true)");
				break;
			case FuId.UTF8GetString:
				Include("java.nio.charset.StandardCharsets");
				Write("new String(");
				WriteArgs(method, args);
				Write(", StandardCharsets.UTF_8)");
				break;
			case FuId.EnvironmentGetEnvironmentVariable:
				WriteCall("System.getenv", args[0]);
				break;
			case FuId.RegexCompile:
				WriteCompileRegex(args, 0);
				break;
			case FuId.RegexEscape:
				Include("java.util.regex.Pattern");
				WriteCall("Pattern.quote", args[0]);
				break;
			case FuId.RegexIsMatchStr:
				WriteCompileRegex(args, 1);
				WriteCall(".matcher", args[0]);
				Write(".find()");
				break;
			case FuId.RegexIsMatchRegex:
				WriteMethodCall(obj, "matcher", args[0]);
				Write(".find()");
				break;
			case FuId.MatchFindStr:
			case FuId.MatchFindRegex:
				WriteChar('(');
				obj.Accept(this, FuPriority.Assign);
				Write(" = ");
				if (method.Id == FuId.MatchFindStr)
					WriteCompileRegex(args, 1);
				else
					args[1].Accept(this, FuPriority.Primary);
				WriteCall(".matcher", args[0]);
				Write(").find()");
				break;
			case FuId.MatchGetCapture:
				WriteMethodCall(obj, "group", args[0]);
				break;
			case FuId.MathCeiling:
				WriteCall("Math.ceil", args[0]);
				break;
			case FuId.MathClamp:
				Write("Math.min(Math.max(");
				WriteClampAsMinMax(args);
				break;
			case FuId.MathFusedMultiplyAdd:
				WriteCall("Math.fma", args[0], args[1], args[2]);
				break;
			case FuId.MathIsFinite:
				WriteCall("Double.isFinite", args[0]);
				break;
			case FuId.MathIsInfinity:
				WriteCall("Double.isInfinite", args[0]);
				break;
			case FuId.MathIsNaN:
				WriteCall("Double.isNaN", args[0]);
				break;
			case FuId.MathLog2:
				if (parent > FuPriority.Mul)
					WriteChar('(');
				WriteCall("Math.log", args[0]);
				Write(" * 1.4426950408889635");
				if (parent > FuPriority.Mul)
					WriteChar(')');
				break;
			case FuId.MathRound:
				WriteCall("Math.rint", args[0]);
				break;
			default:
				NotSupported(obj, method.Name);
				break;
			}
		}

		protected override void WriteIndexingExpr(FuBinaryExpr expr, FuPriority parent)
		{
			if (parent != FuPriority.Assign && IsUnsignedByte(expr.Type)) {
				if (parent > FuPriority.And)
					WriteChar('(');
				WriteIndexingInternal(expr);
				Write(" & 0xff");
				if (parent > FuPriority.And)
					WriteChar(')');
			}
			else
				WriteIndexingInternal(expr);
		}

		protected override bool IsPromoted(FuExpr expr) => base.IsPromoted(expr) || IsUnsignedByteIndexing(expr);

		protected override void WriteAssignRight(FuBinaryExpr expr)
		{
			if (!IsUnsignedByteIndexing(expr.Left) && expr.Right is FuBinaryExpr rightBinary && rightBinary.IsAssign() && IsUnsignedByte(expr.Right.Type)) {
				WriteChar('(');
				base.WriteAssignRight(expr);
				Write(") & 0xff");
			}
			else
				base.WriteAssignRight(expr);
		}

		protected override void WriteAssign(FuBinaryExpr expr, FuPriority parent)
		{
			if (expr.Left is FuBinaryExpr indexing && indexing.Op == FuToken.LeftBracket && indexing.Left.Type is FuClassType klass && !klass.IsArray()) {
				WritePostfix(indexing.Left, klass.Class.Id == FuId.ListClass ? ".set(" : ".put(");
				indexing.Right.Accept(this, FuPriority.Argument);
				Write(", ");
				WriteNotPromoted(expr.Type, expr.Right);
				WriteChar(')');
			}
			else
				base.WriteAssign(expr, parent);
		}

		protected override string GetIsOperator() => " instanceof ";

		protected override void WriteVar(FuNamedValue def)
		{
			if (def.Type.IsFinal() && !def.IsAssignableStorage())
				Write("final ");
			base.WriteVar(def);
		}

		protected override bool HasInitCode(FuNamedValue def) => (def.Type is FuArrayStorageType && def.Type.GetStorageType() is FuStorageType) || base.HasInitCode(def);

		protected override void WriteInitCode(FuNamedValue def)
		{
			if (!HasInitCode(def))
				return;
			if (def.Type is FuArrayStorageType array) {
				int nesting = 0;
				while (array.GetElementType() is FuArrayStorageType innerArray) {
					OpenLoop("int", nesting++, array.Length);
					array = innerArray;
				}
				OpenLoop("int", nesting++, array.Length);
				WriteArrayElement(def, nesting);
				Write(" = ");
				FuStorageType storage = (FuStorageType) array.GetElementType();
				WriteNew(storage, FuPriority.Argument);
				WriteCharLine(';');
				while (--nesting >= 0)
					CloseBlock();
			}
			else
				base.WriteInitCode(def);
		}

		internal override void VisitLambdaExpr(FuLambdaExpr expr)
		{
			WriteName(expr.First);
			Write(" -> ");
			expr.Body.Accept(this, FuPriority.Statement);
		}

		protected override void DefineIsVar(FuBinaryExpr binary)
		{
		}

		protected override void WriteAssert(FuAssert statement)
		{
			if (statement.CompletesNormally()) {
				Write("assert ");
				statement.Cond.Accept(this, FuPriority.Argument);
				if (statement.Message != null) {
					Write(" : ");
					statement.Message.Accept(this, FuPriority.Argument);
				}
			}
			else {
				Write("throw new AssertionError(");
				if (statement.Message != null)
					statement.Message.Accept(this, FuPriority.Argument);
				WriteChar(')');
			}
			WriteCharLine(';');
		}

		protected override void StartBreakGoto()
		{
			Write("break fuswitch");
		}

		internal override void VisitForeach(FuForeach statement)
		{
			Write("for (");
			FuClassType klass = (FuClassType) statement.Collection.Type;
			switch (klass.Class.Id) {
			case FuId.StringClass:
				Write("int _i = 0; _i < ");
				WriteStringLength(statement.Collection);
				Write("; _i++) ");
				OpenBlock();
				WriteTypeAndName(statement.GetVar());
				Write(" = ");
				statement.Collection.Accept(this, FuPriority.Primary);
				WriteLine(".charAt(_i);");
				FlattenBlock(statement.Body);
				CloseBlock();
				return;
			case FuId.DictionaryClass:
			case FuId.SortedDictionaryClass:
			case FuId.OrderedDictionaryClass:
				Include("java.util.Map");
				WriteDictType("Map.Entry", klass);
				WriteChar(' ');
				Write(statement.GetVar().Name);
				Write(" : ");
				WritePostfix(statement.Collection, ".entrySet()");
				break;
			default:
				WriteTypeAndName(statement.GetVar());
				Write(" : ");
				statement.Collection.Accept(this, FuPriority.Argument);
				break;
			}
			WriteChar(')');
			WriteChild(statement.Body);
		}

		internal override void VisitLock(FuLock statement)
		{
			WriteCall("synchronized ", statement.Lock);
			WriteChild(statement.Body);
		}

		protected override void WriteSwitchValue(FuExpr expr)
		{
			if (IsUnsignedByteIndexing(expr)) {
				FuBinaryExpr indexing = (FuBinaryExpr) expr;
				WriteIndexingInternal(indexing);
			}
			else
				base.WriteSwitchValue(expr);
		}

		protected override void WriteSwitchCaseValue(FuSwitch statement, FuExpr value)
		{
			if (value is FuSymbolReference symbol && symbol.Symbol.Parent is FuEnum enu && IsJavaEnum(enu))
				WriteUppercaseWithUnderscores(symbol.Name);
			else
				base.WriteSwitchCaseValue(statement, value);
		}

		bool WriteSwitchCaseVar(FuExpr expr)
		{
			expr.Accept(this, FuPriority.Argument);
			if (expr is FuVar def && def.Name == "_") {
				VisitLiteralLong(this.SwitchCaseDiscards++);
				return true;
			}
			return false;
		}

		protected override void WriteSwitchCase(FuSwitch statement, FuCase kase)
		{
			if (statement.IsTypeMatching()) {
				foreach (FuExpr expr in kase.Values) {
					Write("case ");
					bool discard;
					if (expr is FuBinaryExpr when1) {
						discard = WriteSwitchCaseVar(when1.Left);
						Write(" when ");
						when1.Right.Accept(this, FuPriority.Argument);
					}
					else
						discard = WriteSwitchCaseVar(expr);
					WriteCharLine(':');
					this.Indent++;
					WriteSwitchCaseBody(kase.Body);
					this.Indent--;
					if (discard)
						this.SwitchCaseDiscards--;
				}
			}
			else
				base.WriteSwitchCase(statement, kase);
		}

		internal override void VisitSwitch(FuSwitch statement)
		{
			if (!statement.IsTypeMatching() && statement.HasWhen()) {
				if (statement.Cases.Exists(kase => FuSwitch.HasEarlyBreakAndContinue(kase.Body)) || FuSwitch.HasEarlyBreakAndContinue(statement.DefaultBody)) {
					Write("fuswitch");
					VisitLiteralLong(this.SwitchesWithGoto.Count);
					Write(": ");
					this.SwitchesWithGoto.Add(statement);
					WriteSwitchAsIfs(statement, false);
				}
				else
					WriteSwitchAsIfs(statement, true);
			}
			else
				base.VisitSwitch(statement);
		}

		internal override void VisitThrow(FuThrow statement)
		{
			Write("throw new Exception(");
			statement.Message.Accept(this, FuPriority.Argument);
			WriteLine(");");
		}

		void CreateJavaFile(string className)
		{
			CreateFile(this.OutputFile, className + ".java");
			if (this.Namespace.Length != 0) {
				Write("package ");
				Write(this.Namespace);
				WriteCharLine(';');
			}
		}

		internal override void VisitEnumValue(FuConst konst, FuConst previous)
		{
			WriteDoc(konst.Documentation);
			Write("int ");
			WriteUppercaseWithUnderscores(konst.Name);
			Write(" = ");
			if (konst.Value is FuImplicitEnumValue imp)
				VisitLiteralLong(imp.Value);
			else
				konst.Value.Accept(this, FuPriority.Argument);
			WriteCharLine(';');
		}

		protected override void WriteEnum(FuEnum enu)
		{
			CreateJavaFile(enu.Name);
			WriteNewLine();
			WriteDoc(enu.Documentation);
			WritePublic(enu);
			bool javaEnum = IsJavaEnum(enu);
			Write(javaEnum ? "enum " : "interface ");
			WriteLine(enu.Name);
			OpenBlock();
			if (javaEnum) {
				for (FuSymbol symbol = enu.GetFirstValue();;) {
					WriteDoc(symbol.Documentation);
					WriteUppercaseWithUnderscores(symbol.Name);
					symbol = symbol.Next;
					if (symbol == null)
						break;
					WriteCharLine(',');
				}
				WriteNewLine();
			}
			else
				enu.AcceptValues(this);
			CloseBlock();
			CloseFile();
		}

		void WriteSignature(FuMethod method, int paramCount)
		{
			WriteNewLine();
			WriteMethodDoc(method);
			WriteVisibility(method.Visibility);
			switch (method.CallType) {
			case FuCallType.Static:
				Write("static ");
				break;
			case FuCallType.Virtual:
				break;
			case FuCallType.Abstract:
				Write("abstract ");
				break;
			case FuCallType.Override:
				Write("@Override ");
				break;
			case FuCallType.Normal:
				if (method.Visibility != FuVisibility.Private)
					Write("final ");
				break;
			case FuCallType.Sealed:
				Write("final @Override ");
				break;
			default:
				throw new NotImplementedException();
			}
			WriteTypeAndName(method);
			WriteChar('(');
			FuVar param = method.Parameters.FirstParameter();
			for (int i = 0; i < paramCount; i++) {
				if (i > 0)
					Write(", ");
				WriteTypeAndName(param);
				param = param.NextParameter();
			}
			WriteChar(')');
			if (method.Throws)
				Write(" throws Exception");
		}

		void WriteOverloads(FuMethod method, int paramCount)
		{
			if (paramCount + 1 < method.Parameters.Count())
				WriteOverloads(method, paramCount + 1);
			WriteSignature(method, paramCount);
			WriteNewLine();
			OpenBlock();
			if (method.Type.Id != FuId.VoidType)
				Write("return ");
			WriteName(method);
			WriteChar('(');
			FuVar param = method.Parameters.FirstParameter();
			for (int i = 0; i < paramCount; i++) {
				WriteName(param);
				Write(", ");
				param = param.NextParameter();
			}
			param.Value.Accept(this, FuPriority.Argument);
			WriteLine(");");
			CloseBlock();
		}

		protected override void WriteConst(FuConst konst)
		{
			WriteNewLine();
			WriteDoc(konst.Documentation);
			WriteVisibility(konst.Visibility);
			Write("static final ");
			WriteTypeAndName(konst);
			Write(" = ");
			WriteCoercedExpr(konst.Type, konst.Value);
			WriteCharLine(';');
		}

		protected override void WriteField(FuField field)
		{
			WriteDoc(field.Documentation);
			WriteVisibility(field.Visibility);
			WriteVar(field);
			WriteCharLine(';');
		}

		protected override void WriteMethod(FuMethod method)
		{
			WriteSignature(method, method.Parameters.Count());
			WriteBody(method);
			int i = 0;
			for (FuVar param = method.Parameters.FirstParameter(); param != null; param = param.NextParameter()) {
				if (param.Value != null) {
					WriteOverloads(method, i);
					break;
				}
				i++;
			}
		}

		protected override void WriteClass(FuClass klass, FuProgram program)
		{
			OpenStringWriter();
			WriteDoc(klass.Documentation);
			WritePublic(klass);
			switch (klass.CallType) {
			case FuCallType.Normal:
				break;
			case FuCallType.Abstract:
				Write("abstract ");
				break;
			case FuCallType.Static:
			case FuCallType.Sealed:
				Write("final ");
				break;
			default:
				throw new NotImplementedException();
			}
			OpenClass(klass, "", " extends ");
			if (klass.CallType == FuCallType.Static) {
				Write("private ");
				Write(klass.Name);
				WriteLine("()");
				OpenBlock();
				CloseBlock();
			}
			else if (NeedsConstructor(klass)) {
				if (klass.Constructor != null) {
					WriteDoc(klass.Constructor.Documentation);
					WriteVisibility(klass.Constructor.Visibility);
				}
				Write(klass.Name);
				WriteLine("()");
				OpenBlock();
				WriteConstructorBody(klass);
				CloseBlock();
			}
			WriteMembers(klass, true);
			CloseBlock();
			CreateJavaFile(klass.Name);
			WriteTopLevelNatives(program);
			WriteIncludes("import ", ";");
			WriteNewLine();
			CloseStringWriter();
			CloseFile();
		}

		void WriteResources()
		{
			CreateJavaFile("FuResource");
			WriteLine("import java.io.DataInputStream;");
			WriteLine("import java.io.IOException;");
			WriteNewLine();
			Write("class FuResource");
			WriteNewLine();
			OpenBlock();
			WriteLine("static byte[] getByteArray(String name, int length)");
			OpenBlock();
			Write("DataInputStream dis = new DataInputStream(");
			WriteLine("FuResource.class.getResourceAsStream(name));");
			WriteLine("byte[] result = new byte[length];");
			Write("try ");
			OpenBlock();
			Write("try ");
			OpenBlock();
			WriteLine("dis.readFully(result);");
			CloseBlock();
			Write("finally ");
			OpenBlock();
			WriteLine("dis.close();");
			CloseBlock();
			CloseBlock();
			Write("catch (IOException e) ");
			OpenBlock();
			WriteLine("throw new RuntimeException();");
			CloseBlock();
			WriteLine("return result;");
			CloseBlock();
			CloseBlock();
			CloseFile();
		}

		public override void WriteProgram(FuProgram program)
		{
			this.SwitchCaseDiscards = 0;
			WriteTypes(program);
			if (program.Resources.Count > 0)
				WriteResources();
		}
	}

	public class GenJsNoModule : GenBase
	{

		bool StringWriter = false;

		protected override string GetTargetName() => "JavaScript";

		void WriteCamelCaseNotKeyword(string name)
		{
			WriteCamelCase(name);
			switch (name) {
			case "Constructor":
			case "arguments":
			case "await":
			case "catch":
			case "debugger":
			case "delete":
			case "export":
			case "extends":
			case "finally":
			case "function":
			case "implements":
			case "import":
			case "instanceof":
			case "interface":
			case "let":
			case "package":
			case "private":
			case "super":
			case "try":
			case "typeof":
			case "var":
			case "with":
			case "yield":
				WriteChar('_');
				break;
			default:
				break;
			}
		}

		protected override void WriteName(FuSymbol symbol)
		{
			switch (symbol) {
			case FuContainerType _:
				Write(symbol.Name);
				break;
			case FuConst konst:
				if (konst.Visibility == FuVisibility.Private)
					WriteChar('#');
				if (konst.InMethod != null) {
					WriteUppercaseWithUnderscores(konst.InMethod.Name);
					WriteChar('_');
				}
				WriteUppercaseWithUnderscores(symbol.Name);
				break;
			case FuVar _:
				WriteCamelCaseNotKeyword(symbol.Name);
				break;
			case FuMember member:
				if (member.Visibility == FuVisibility.Private) {
					WriteChar('#');
					WriteCamelCase(symbol.Name);
					if (symbol.Name == "Constructor")
						WriteChar('_');
				}
				else
					WriteCamelCaseNotKeyword(symbol.Name);
				break;
			default:
				throw new NotImplementedException();
			}
		}

		protected override void WriteTypeAndName(FuNamedValue value)
		{
			WriteName(value);
		}

		protected void WriteArrayElementType(FuType type)
		{
			switch (type.Id) {
			case FuId.SByteRange:
				Write("Int8");
				break;
			case FuId.ByteRange:
				Write("Uint8");
				break;
			case FuId.ShortRange:
				Write("Int16");
				break;
			case FuId.UShortRange:
				Write("Uint16");
				break;
			case FuId.IntType:
				Write("Int32");
				break;
			case FuId.LongType:
				Write("BigInt64");
				break;
			case FuId.FloatType:
				Write("Float32");
				break;
			case FuId.DoubleType:
				Write("Float64");
				break;
			default:
				throw new NotImplementedException();
			}
		}

		internal override void VisitAggregateInitializer(FuAggregateInitializer expr)
		{
			FuArrayStorageType array = (FuArrayStorageType) expr.Type;
			bool numeric = false;
			if (array.GetElementType() is FuNumericType number) {
				Write("new ");
				WriteArrayElementType(number);
				Write("Array(");
				numeric = true;
			}
			Write("[ ");
			WriteCoercedLiterals(null, expr.Items);
			Write(" ]");
			if (numeric)
				WriteChar(')');
		}

		protected override void WriteNew(FuReadWriteClassType klass, FuPriority parent)
		{
			switch (klass.Class.Id) {
			case FuId.ListClass:
			case FuId.QueueClass:
			case FuId.StackClass:
				Write("[]");
				break;
			case FuId.HashSetClass:
			case FuId.SortedSetClass:
				Write("new Set()");
				break;
			case FuId.DictionaryClass:
			case FuId.SortedDictionaryClass:
				Write("{}");
				break;
			case FuId.OrderedDictionaryClass:
				Write("new Map()");
				break;
			case FuId.LockClass:
				NotSupported(klass, "Lock");
				break;
			default:
				Write("new ");
				if (klass.Class.Id == FuId.StringWriterClass)
					this.StringWriter = true;
				Write(klass.Class.Name);
				Write("()");
				break;
			}
		}

		protected override void WriteNewWithFields(FuReadWriteClassType type, FuAggregateInitializer init)
		{
			Write("Object.assign(");
			WriteNew(type, FuPriority.Argument);
			WriteChar(',');
			WriteObjectLiteral(init, ": ");
			WriteChar(')');
		}

		protected override void WriteVar(FuNamedValue def)
		{
			Write(def.Type.IsFinal() && !def.IsAssignableStorage() ? "const " : "let ");
			base.WriteVar(def);
		}

		void WriteInterpolatedLiteral(string s)
		{
			int i = 0;
			foreach (int c in s) {
				i++;
				if (c == '`' || (c == '$' && i < s.Length && s[i] == '{'))
					WriteChar('\\');
				WriteChar(c);
			}
		}

		internal override void VisitInterpolatedString(FuInterpolatedString expr, FuPriority parent)
		{
			WriteChar('`');
			foreach (FuInterpolatedPart part in expr.Parts) {
				WriteInterpolatedLiteral(part.Prefix);
				Write("${");
				if (part.Width != 0 || part.Format != ' ') {
					if (part.Argument is FuLiteralLong || part.Argument is FuPrefixExpr) {
						WriteChar('(');
						part.Argument.Accept(this, FuPriority.Primary);
						WriteChar(')');
					}
					else
						part.Argument.Accept(this, FuPriority.Primary);
					if (part.Argument.Type is FuNumericType) {
						switch (part.Format) {
						case 'E':
							Write(".toExponential(");
							if (part.Precision >= 0)
								VisitLiteralLong(part.Precision);
							Write(").toUpperCase()");
							break;
						case 'e':
							Write(".toExponential(");
							if (part.Precision >= 0)
								VisitLiteralLong(part.Precision);
							WriteChar(')');
							break;
						case 'F':
						case 'f':
							Write(".toFixed(");
							if (part.Precision >= 0)
								VisitLiteralLong(part.Precision);
							WriteChar(')');
							break;
						case 'X':
							Write(".toString(16).toUpperCase()");
							break;
						case 'x':
							Write(".toString(16)");
							break;
						default:
							Write(".toString()");
							break;
						}
						if (part.Precision >= 0) {
							switch (part.Format) {
							case 'D':
							case 'd':
							case 'X':
							case 'x':
								Write(".padStart(");
								VisitLiteralLong(part.Precision);
								Write(", \"0\")");
								break;
							default:
								break;
							}
						}
					}
					if (part.Width > 0) {
						Write(".padStart(");
						VisitLiteralLong(part.Width);
						WriteChar(')');
					}
					else if (part.Width < 0) {
						Write(".padEnd(");
						VisitLiteralLong(-part.Width);
						WriteChar(')');
					}
				}
				else
					part.Argument.Accept(this, FuPriority.Argument);
				WriteChar('}');
			}
			WriteInterpolatedLiteral(expr.Suffix);
			WriteChar('`');
		}

		protected override void WriteLocalName(FuSymbol symbol, FuPriority parent)
		{
			if (symbol is FuMember member) {
				if (!member.IsStatic())
					Write("this");
				else if (this.CurrentMethod != null)
					Write(this.CurrentMethod.Parent.Name);
				else if (symbol is FuConst konst) {
					konst.Value.Accept(this, parent);
					return;
				}
				else
					throw new NotImplementedException();
				WriteChar('.');
			}
			WriteName(symbol);
			if (symbol.Parent is FuForeach forEach && forEach.Collection.Type is FuStringType)
				Write(".codePointAt(0)");
		}

		protected override void WriteCoercedInternal(FuType type, FuExpr expr, FuPriority parent)
		{
			if (type is FuNumericType) {
				if (type.Id == FuId.LongType) {
					if (expr is FuLiteralLong) {
						expr.Accept(this, FuPriority.Primary);
						WriteChar('n');
						return;
					}
					if (expr.Type.Id != FuId.LongType) {
						WriteCall("BigInt", expr);
						return;
					}
				}
				else if (expr.Type.Id == FuId.LongType) {
					WriteCall("Number", expr);
					return;
				}
			}
			expr.Accept(this, parent);
		}

		protected override void WriteNewArray(FuType elementType, FuExpr lengthExpr, FuPriority parent)
		{
			Write("new ");
			if (elementType is FuNumericType)
				WriteArrayElementType(elementType);
			WriteCall("Array", lengthExpr);
		}

		protected override bool HasInitCode(FuNamedValue def) => def.Type is FuArrayStorageType array && array.GetElementType() is FuStorageType;

		protected override void WriteInitCode(FuNamedValue def)
		{
			if (!HasInitCode(def))
				return;
			FuArrayStorageType array = (FuArrayStorageType) def.Type;
			int nesting = 0;
			while (array.GetElementType() is FuArrayStorageType innerArray) {
				OpenLoop("let", nesting++, array.Length);
				WriteArrayElement(def, nesting);
				Write(" = ");
				WriteNewArray(innerArray.GetElementType(), innerArray.LengthExpr, FuPriority.Argument);
				WriteCharLine(';');
				array = innerArray;
			}
			if (array.GetElementType() is FuStorageType klass) {
				OpenLoop("let", nesting++, array.Length);
				WriteArrayElement(def, nesting);
				Write(" = ");
				WriteNew(klass, FuPriority.Argument);
				WriteCharLine(';');
			}
			while (--nesting >= 0)
				CloseBlock();
		}

		protected override void WriteResource(string name, int length)
		{
			Write("Fu.");
			WriteResourceName(name);
		}

		internal override void VisitSymbolReference(FuSymbolReference expr, FuPriority parent)
		{
			switch (expr.Symbol.Id) {
			case FuId.ConsoleError:
				Write("process.stderr");
				break;
			case FuId.ListCount:
			case FuId.QueueCount:
			case FuId.StackCount:
				WritePostfix(expr.Left, ".length");
				break;
			case FuId.HashSetCount:
			case FuId.SortedSetCount:
			case FuId.OrderedDictionaryCount:
				WritePostfix(expr.Left, ".size");
				break;
			case FuId.DictionaryCount:
			case FuId.SortedDictionaryCount:
				WriteCall("Object.keys", expr.Left);
				Write(".length");
				break;
			case FuId.MatchStart:
				WritePostfix(expr.Left, ".index");
				break;
			case FuId.MatchEnd:
				if (parent > FuPriority.Add)
					WriteChar('(');
				WritePostfix(expr.Left, ".index + ");
				WritePostfix(expr.Left, "[0].length");
				if (parent > FuPriority.Add)
					WriteChar(')');
				break;
			case FuId.MatchLength:
				WritePostfix(expr.Left, "[0].length");
				break;
			case FuId.MatchValue:
				WritePostfix(expr.Left, "[0]");
				break;
			case FuId.MathNaN:
				Write("NaN");
				break;
			case FuId.MathNegativeInfinity:
				Write("-Infinity");
				break;
			case FuId.MathPositiveInfinity:
				Write("Infinity");
				break;
			default:
				base.VisitSymbolReference(expr, parent);
				break;
			}
		}

		protected override void WriteStringLength(FuExpr expr)
		{
			WritePostfix(expr, ".length");
		}

		protected override void WriteCharAt(FuBinaryExpr expr)
		{
			WriteMethodCall(expr.Left, "charCodeAt", expr.Right);
		}

		protected override void WriteBinaryOperand(FuExpr expr, FuPriority parent, FuBinaryExpr binary)
		{
			WriteCoerced(binary.IsRel() ? expr.Type : binary.Type, expr, parent);
		}

		static bool IsIdentifier(string s)
		{
			if (s.Length == 0 || s[0] < 'A')
				return false;
			foreach (int c in s) {
				if (!FuLexer.IsLetterOrDigit(c))
					return false;
			}
			return true;
		}

		void WriteNewRegex(List<FuExpr> args, int argIndex)
		{
			FuExpr pattern = args[argIndex];
			if (pattern is FuLiteralString literal) {
				WriteChar('/');
				bool escaped = false;
				foreach (int c in literal.Value) {
					switch (c) {
					case '\\':
						if (!escaped) {
							escaped = true;
							continue;
						}
						escaped = false;
						break;
					case '"':
					case '\'':
						escaped = false;
						break;
					case '/':
						escaped = true;
						break;
					default:
						break;
					}
					if (escaped) {
						WriteChar('\\');
						escaped = false;
					}
					WriteChar(c);
				}
				WriteChar('/');
				WriteRegexOptions(args, "", "", "", "i", "m", "s");
			}
			else {
				Write("new RegExp(");
				pattern.Accept(this, FuPriority.Argument);
				WriteRegexOptions(args, ", \"", "", "\"", "i", "m", "s");
				WriteChar(')');
			}
		}

		static bool HasLong(List<FuExpr> args) => args.Exists(arg => arg.Type.Id == FuId.LongType);

		void WriteMathMaxMin(FuMethod method, string name, int op, List<FuExpr> args)
		{
			if (HasLong(args)) {
				Write("((x, y) => x ");
				WriteChar(op);
				Write(" y ? x : y)");
				WriteArgsInParentheses(method, args);
			}
			else
				WriteCall(name, args[0], args[1]);
		}

		protected override void WriteCallExpr(FuExpr obj, FuMethod method, List<FuExpr> args, FuPriority parent)
		{
			switch (method.Id) {
			case FuId.None:
			case FuId.ClassToString:
			case FuId.StringEndsWith:
			case FuId.StringIndexOf:
			case FuId.StringLastIndexOf:
			case FuId.StringStartsWith:
			case FuId.ArraySortAll:
			case FuId.ListIndexOf:
			case FuId.StackPush:
			case FuId.StackPop:
			case FuId.HashSetAdd:
			case FuId.HashSetClear:
			case FuId.SortedSetAdd:
			case FuId.SortedSetClear:
			case FuId.OrderedDictionaryClear:
			case FuId.StringWriterClear:
			case FuId.StringWriterToString:
			case FuId.MathMethod:
			case FuId.MathLog2:
			case FuId.MathMaxDouble:
			case FuId.MathMinDouble:
			case FuId.MathRound:
				if (obj == null)
					WriteLocalName(method, FuPriority.Primary);
				else {
					if (IsReferenceTo(obj, FuId.BasePtr))
						Write("super");
					else
						obj.Accept(this, FuPriority.Primary);
					WriteChar('.');
					WriteName(method);
				}
				WriteArgsInParentheses(method, args);
				break;
			case FuId.EnumFromInt:
				args[0].Accept(this, parent);
				break;
			case FuId.EnumHasFlag:
				WriteEnumHasFlag(obj, args, parent);
				break;
			case FuId.IntTryParse:
				Write("!isNaN(");
				obj.Accept(this, FuPriority.Assign);
				Write(" = parseInt(");
				args[0].Accept(this, FuPriority.Argument);
				WriteTryParseRadix(args);
				Write("))");
				break;
			case FuId.LongTryParse:
				if (args.Count != 1)
					NotSupported(args[1], "Radix");
				Write("(() => { try { ");
				obj.Accept(this, FuPriority.Assign);
				Write("  = BigInt(");
				args[0].Accept(this, FuPriority.Argument);
				Write("); return true; } catch { return false; }})()");
				break;
			case FuId.DoubleTryParse:
				Write("!isNaN(");
				obj.Accept(this, FuPriority.Assign);
				Write(" = parseFloat(");
				args[0].Accept(this, FuPriority.Argument);
				Write("))");
				break;
			case FuId.StringContains:
			case FuId.ArrayContains:
			case FuId.ListContains:
				WriteMethodCall(obj, "includes", args[0]);
				break;
			case FuId.StringReplace:
				WriteMethodCall(obj, "replaceAll", args[0], args[1]);
				break;
			case FuId.StringSubstring:
				WritePostfix(obj, ".substring(");
				args[0].Accept(this, FuPriority.Argument);
				if (args.Count == 2) {
					Write(", ");
					WriteAdd(args[0], args[1]);
				}
				WriteChar(')');
				break;
			case FuId.ArrayFillAll:
			case FuId.ArrayFillPart:
				WritePostfix(obj, ".fill(");
				args[0].Accept(this, FuPriority.Argument);
				if (args.Count == 3) {
					Write(", ");
					WriteStartEnd(args[1], args[2]);
				}
				WriteChar(')');
				break;
			case FuId.ArrayCopyTo:
			case FuId.ListCopyTo:
				args[1].Accept(this, FuPriority.Primary);
				bool wholeSource = obj.Type is FuArrayStorageType sourceStorage && args[0].IsLiteralZero() && args[3] is FuLiteralLong literalLength && literalLength.Value == sourceStorage.Length;
				if (obj.Type.AsClassType().GetElementType() is FuNumericType) {
					Write(".set(");
					if (wholeSource)
						obj.Accept(this, FuPriority.Argument);
					else {
						WritePostfix(obj, method.Id == FuId.ArrayCopyTo ? ".subarray(" : ".slice(");
						WriteStartEnd(args[0], args[3]);
						WriteChar(')');
					}
					if (!args[2].IsLiteralZero()) {
						Write(", ");
						args[2].Accept(this, FuPriority.Argument);
					}
				}
				else {
					Write(".splice(");
					args[2].Accept(this, FuPriority.Argument);
					Write(", ");
					args[3].Accept(this, FuPriority.Argument);
					Write(", ...");
					obj.Accept(this, FuPriority.Primary);
					if (!wholeSource) {
						Write(".slice(");
						WriteStartEnd(args[0], args[3]);
						WriteChar(')');
					}
				}
				WriteChar(')');
				break;
			case FuId.ArraySortPart:
				WritePostfix(obj, ".subarray(");
				WriteStartEnd(args[0], args[1]);
				Write(").sort()");
				break;
			case FuId.ListAdd:
				WriteListAdd(obj, "push", args);
				break;
			case FuId.ListAddRange:
				WritePostfix(obj, ".push(...");
				args[0].Accept(this, FuPriority.Argument);
				WriteChar(')');
				break;
			case FuId.ListAll:
				WriteMethodCall(obj, "every", args[0]);
				break;
			case FuId.ListAny:
				WriteMethodCall(obj, "some", args[0]);
				break;
			case FuId.ListClear:
			case FuId.QueueClear:
			case FuId.StackClear:
				WritePostfix(obj, ".length = 0");
				break;
			case FuId.ListInsert:
				WriteListInsert(obj, "splice", args, ", 0, ");
				break;
			case FuId.ListLast:
			case FuId.StackPeek:
				WritePostfix(obj, ".at(-1)");
				break;
			case FuId.ListRemoveAt:
				WritePostfix(obj, ".splice(");
				args[0].Accept(this, FuPriority.Argument);
				Write(", 1)");
				break;
			case FuId.ListRemoveRange:
				WriteMethodCall(obj, "splice", args[0], args[1]);
				break;
			case FuId.ListSortAll:
				WritePostfix(obj, ".sort((a, b) => a - b)");
				break;
			case FuId.ListSortPart:
				WritePostfix(obj, ".splice(");
				args[0].Accept(this, FuPriority.Argument);
				Write(", ");
				args[1].Accept(this, FuPriority.Argument);
				Write(", ...");
				WritePostfix(obj, ".slice(");
				WriteStartEnd(args[0], args[1]);
				Write(").sort((a, b) => a - b))");
				break;
			case FuId.QueueDequeue:
				WritePostfix(obj, ".shift()");
				break;
			case FuId.QueueEnqueue:
				WriteMethodCall(obj, "push", args[0]);
				break;
			case FuId.QueuePeek:
				WritePostfix(obj, "[0]");
				break;
			case FuId.HashSetContains:
			case FuId.SortedSetContains:
			case FuId.OrderedDictionaryContainsKey:
				WriteMethodCall(obj, "has", args[0]);
				break;
			case FuId.HashSetRemove:
			case FuId.SortedSetRemove:
			case FuId.OrderedDictionaryRemove:
				WriteMethodCall(obj, "delete", args[0]);
				break;
			case FuId.DictionaryAdd:
				WriteDictionaryAdd(obj, args);
				break;
			case FuId.DictionaryClear:
			case FuId.SortedDictionaryClear:
				Write("for (const key in ");
				obj.Accept(this, FuPriority.Argument);
				WriteCharLine(')');
				Write("\tdelete ");
				WritePostfix(obj, "[key];");
				break;
			case FuId.DictionaryContainsKey:
			case FuId.SortedDictionaryContainsKey:
				WriteMethodCall(obj, "hasOwnProperty", args[0]);
				break;
			case FuId.DictionaryRemove:
			case FuId.SortedDictionaryRemove:
				Write("delete ");
				WriteIndexing(obj, args[0]);
				break;
			case FuId.TextWriterWrite:
				WritePostfix(obj, ".write(");
				if (args[0].Type is FuStringType)
					args[0].Accept(this, FuPriority.Argument);
				else
					WriteCall("String", args[0]);
				WriteChar(')');
				break;
			case FuId.TextWriterWriteChar:
				WriteMethodCall(obj, "write(String.fromCharCode", args[0]);
				WriteChar(')');
				break;
			case FuId.TextWriterWriteCodePoint:
				WriteMethodCall(obj, "write(String.fromCodePoint", args[0]);
				WriteChar(')');
				break;
			case FuId.TextWriterWriteLine:
				if (IsReferenceTo(obj, FuId.ConsoleError)) {
					Write("console.error(");
					if (args.Count == 0)
						Write("\"\"");
					else
						args[0].Accept(this, FuPriority.Argument);
					WriteChar(')');
				}
				else {
					WritePostfix(obj, ".write(");
					if (args.Count != 0) {
						args[0].Accept(this, FuPriority.Add);
						Write(" + ");
					}
					Write("\"\\n\")");
				}
				break;
			case FuId.ConsoleWrite:
				Write("process.stdout.write(");
				if (args[0].Type is FuStringType)
					args[0].Accept(this, FuPriority.Argument);
				else
					WriteCall("String", args[0]);
				WriteChar(')');
				break;
			case FuId.ConsoleWriteLine:
				Write("console.log(");
				if (args.Count == 0)
					Write("\"\"");
				else
					args[0].Accept(this, FuPriority.Argument);
				WriteChar(')');
				break;
			case FuId.UTF8GetByteCount:
				Write("new TextEncoder().encode(");
				args[0].Accept(this, FuPriority.Argument);
				Write(").length");
				break;
			case FuId.UTF8GetBytes:
				Write("new TextEncoder().encodeInto(");
				args[0].Accept(this, FuPriority.Argument);
				Write(", ");
				if (args[2].IsLiteralZero())
					args[1].Accept(this, FuPriority.Argument);
				else
					WriteMethodCall(args[1], "subarray", args[2]);
				WriteChar(')');
				break;
			case FuId.UTF8GetString:
				Write("new TextDecoder().decode(");
				WritePostfix(args[0], ".subarray(");
				args[1].Accept(this, FuPriority.Argument);
				Write(", ");
				WriteAdd(args[1], args[2]);
				Write("))");
				break;
			case FuId.EnvironmentGetEnvironmentVariable:
				if (args[0] is FuLiteralString literal && IsIdentifier(literal.Value)) {
					Write("process.env.");
					Write(literal.Value);
				}
				else {
					Write("process.env[");
					args[0].Accept(this, FuPriority.Argument);
					WriteChar(']');
				}
				break;
			case FuId.RegexCompile:
				WriteNewRegex(args, 0);
				break;
			case FuId.RegexEscape:
				WritePostfix(args[0], ".replace(/[-\\/\\\\^$*+?.()|[\\]{}]/g, '\\\\$&')");
				break;
			case FuId.RegexIsMatchStr:
				WriteNewRegex(args, 1);
				WriteCall(".test", args[0]);
				break;
			case FuId.RegexIsMatchRegex:
				WriteMethodCall(obj, "test", args[0]);
				break;
			case FuId.MatchFindStr:
			case FuId.MatchFindRegex:
				if (parent > FuPriority.Equality)
					WriteChar('(');
				WriteChar('(');
				obj.Accept(this, FuPriority.Assign);
				Write(" = ");
				if (method.Id == FuId.MatchFindStr)
					WriteNewRegex(args, 1);
				else
					args[1].Accept(this, FuPriority.Primary);
				WriteCall(".exec", args[0]);
				Write(") != null");
				if (parent > FuPriority.Equality)
					WriteChar(')');
				break;
			case FuId.MatchGetCapture:
				WriteIndexing(obj, args[0]);
				break;
			case FuId.MathAbs:
				WriteCall(args[0].Type.Id == FuId.LongType ? "(x => x < 0n ? -x : x)" : "Math.abs", args[0]);
				break;
			case FuId.MathCeiling:
				WriteCall("Math.ceil", args[0]);
				break;
			case FuId.MathClamp:
				if (method.Type.Id == FuId.IntType && HasLong(args)) {
					Write("((x, min, max) => x < min ? min : x > max ? max : x)");
					WriteArgsInParentheses(method, args);
				}
				else {
					Write("Math.min(Math.max(");
					WriteClampAsMinMax(args);
				}
				break;
			case FuId.MathFusedMultiplyAdd:
				if (parent > FuPriority.Add)
					WriteChar('(');
				args[0].Accept(this, FuPriority.Mul);
				Write(" * ");
				args[1].Accept(this, FuPriority.Mul);
				Write(" + ");
				args[2].Accept(this, FuPriority.Add);
				if (parent > FuPriority.Add)
					WriteChar(')');
				break;
			case FuId.MathIsFinite:
			case FuId.MathIsNaN:
				WriteCamelCase(method.Name);
				WriteArgsInParentheses(method, args);
				break;
			case FuId.MathIsInfinity:
				if (parent > FuPriority.Equality)
					WriteChar('(');
				WriteCall("Math.abs", args[0]);
				Write(" == Infinity");
				if (parent > FuPriority.Equality)
					WriteChar(')');
				break;
			case FuId.MathMaxInt:
				WriteMathMaxMin(method, "Math.max", '>', args);
				break;
			case FuId.MathMinInt:
				WriteMathMaxMin(method, "Math.min", '<', args);
				break;
			case FuId.MathTruncate:
				WriteCall("Math.trunc", args[0]);
				break;
			default:
				NotSupported(obj, method.Name);
				break;
			}
		}

		protected override void WriteIndexingExpr(FuBinaryExpr expr, FuPriority parent)
		{
			if (expr.Left.Type is FuClassType dict && dict.Class.Id == FuId.OrderedDictionaryClass)
				WriteMethodCall(expr.Left, "get", expr.Right);
			else
				base.WriteIndexingExpr(expr, parent);
		}

		protected override void WriteAssign(FuBinaryExpr expr, FuPriority parent)
		{
			if (expr.Left is FuBinaryExpr indexing && indexing.Op == FuToken.LeftBracket && indexing.Left.Type is FuClassType dict && dict.Class.Id == FuId.OrderedDictionaryClass)
				WriteMethodCall(indexing.Left, "set", indexing.Right, expr.Right);
			else
				base.WriteAssign(expr, parent);
		}

		protected override string GetIsOperator() => " instanceof ";

		protected virtual void WriteBoolAndOr(FuBinaryExpr expr)
		{
			Write("!!");
			base.VisitBinaryExpr(expr, FuPriority.Primary);
		}

		void WriteBoolAndOrAssign(FuBinaryExpr expr, FuPriority parent)
		{
			expr.Right.Accept(this, parent);
			WriteCharLine(')');
			WriteChar('\t');
			expr.Left.Accept(this, FuPriority.Assign);
		}

		void WriteIsVar(FuExpr expr, FuVar def, bool assign, FuPriority parent)
		{
			if (parent > FuPriority.Rel)
				WriteChar('(');
			if (assign) {
				WriteChar('(');
				WriteCamelCaseNotKeyword(def.Name);
				Write(" = ");
				expr.Accept(this, FuPriority.Argument);
				WriteChar(')');
			}
			else
				expr.Accept(this, FuPriority.Rel);
			Write(" instanceof ");
			Write(def.Type.Name);
			if (parent > FuPriority.Rel)
				WriteChar(')');
		}

		internal override void VisitBinaryExpr(FuBinaryExpr expr, FuPriority parent)
		{
			switch (expr.Op) {
			case FuToken.Slash when expr.Type is FuIntegerType && expr.Type.Id != FuId.LongType:
				if (parent > FuPriority.Or)
					WriteChar('(');
				expr.Left.Accept(this, FuPriority.Mul);
				Write(" / ");
				expr.Right.Accept(this, FuPriority.Primary);
				Write(" | 0");
				if (parent > FuPriority.Or)
					WriteChar(')');
				break;
			case FuToken.DivAssign when expr.Type is FuIntegerType && expr.Type.Id != FuId.LongType:
				if (parent > FuPriority.Assign)
					WriteChar('(');
				expr.Left.Accept(this, FuPriority.Assign);
				Write(" = ");
				expr.Left.Accept(this, FuPriority.Mul);
				Write(" / ");
				expr.Right.Accept(this, FuPriority.Primary);
				Write(" | 0");
				if (parent > FuPriority.Assign)
					WriteChar(')');
				break;
			case FuToken.And when expr.Type.Id == FuId.BoolType:
			case FuToken.Or when expr.Type.Id == FuId.BoolType:
				WriteBoolAndOr(expr);
				break;
			case FuToken.Xor when expr.Type.Id == FuId.BoolType:
				WriteEqual(expr.Left, expr.Right, parent, true);
				break;
			case FuToken.AndAssign when expr.Type.Id == FuId.BoolType:
				Write("if (!");
				WriteBoolAndOrAssign(expr, FuPriority.Primary);
				Write(" = false");
				break;
			case FuToken.OrAssign when expr.Type.Id == FuId.BoolType:
				Write("if (");
				WriteBoolAndOrAssign(expr, FuPriority.Argument);
				Write(" = true");
				break;
			case FuToken.XorAssign when expr.Type.Id == FuId.BoolType:
				expr.Left.Accept(this, FuPriority.Assign);
				Write(" = ");
				WriteEqual(expr.Left, expr.Right, FuPriority.Argument, true);
				break;
			case FuToken.Is when expr.Right is FuVar def:
				WriteIsVar(expr.Left, def, true, parent);
				break;
			default:
				base.VisitBinaryExpr(expr, parent);
				break;
			}
		}

		internal override void VisitLambdaExpr(FuLambdaExpr expr)
		{
			WriteName(expr.First);
			Write(" => ");
			if (HasTemporaries(expr.Body)) {
				OpenBlock();
				WriteTemporaries(expr.Body);
				Write("return ");
				expr.Body.Accept(this, FuPriority.Argument);
				WriteCharLine(';');
				CloseBlock();
			}
			else
				expr.Body.Accept(this, FuPriority.Statement);
		}

		protected override void StartTemporaryVar(FuType type)
		{
			throw new NotImplementedException();
		}

		protected override void DefineObjectLiteralTemporary(FuUnaryExpr expr)
		{
		}

		protected virtual void WriteAsType(FuVar def)
		{
		}

		void WriteVarCast(FuVar def, FuExpr value)
		{
			Write(def.IsAssigned ? "let " : "const ");
			WriteCamelCaseNotKeyword(def.Name);
			Write(" = ");
			value.Accept(this, FuPriority.Argument);
			WriteAsType(def);
			WriteCharLine(';');
		}

		protected override void WriteAssertCast(FuBinaryExpr expr)
		{
			FuVar def = (FuVar) expr.Right;
			WriteVarCast(def, expr.Left);
		}

		protected override void WriteAssert(FuAssert statement)
		{
			if (statement.CompletesNormally()) {
				WriteTemporaries(statement.Cond);
				Write("console.assert(");
				statement.Cond.Accept(this, FuPriority.Argument);
				if (statement.Message != null) {
					Write(", ");
					statement.Message.Accept(this, FuPriority.Argument);
				}
			}
			else {
				Write("throw new Error(");
				if (statement.Message != null)
					statement.Message.Accept(this, FuPriority.Argument);
			}
			WriteLine(");");
		}

		protected override void StartBreakGoto()
		{
			Write("break fuswitch");
		}

		internal override void VisitForeach(FuForeach statement)
		{
			Write("for (const ");
			FuClassType klass = (FuClassType) statement.Collection.Type;
			switch (klass.Class.Id) {
			case FuId.StringClass:
			case FuId.ArrayStorageClass:
			case FuId.ListClass:
			case FuId.HashSetClass:
				WriteName(statement.GetVar());
				Write(" of ");
				statement.Collection.Accept(this, FuPriority.Argument);
				break;
			case FuId.SortedSetClass:
				WriteName(statement.GetVar());
				Write(" of ");
				switch (klass.GetElementType()) {
				case FuNumericType number:
					Write("new ");
					WriteArrayElementType(number);
					Write("Array(");
					break;
				case FuEnum _:
					Write("new Int32Array(");
					break;
				default:
					Write("Array.from(");
					break;
				}
				statement.Collection.Accept(this, FuPriority.Argument);
				Write(").sort()");
				break;
			case FuId.DictionaryClass:
			case FuId.SortedDictionaryClass:
			case FuId.OrderedDictionaryClass:
				WriteChar('[');
				WriteName(statement.GetVar());
				Write(", ");
				WriteName(statement.GetValueVar());
				Write("] of ");
				if (klass.Class.Id == FuId.OrderedDictionaryClass)
					statement.Collection.Accept(this, FuPriority.Argument);
				else {
					WriteCall("Object.entries", statement.Collection);
					switch (statement.GetVar().Type) {
					case FuStringType _:
						if (klass.Class.Id == FuId.SortedDictionaryClass)
							Write(".sort((a, b) => a[0].localeCompare(b[0]))");
						break;
					case FuNumericType _:
					case FuEnum _:
						Write(".map(e => [+e[0], e[1]])");
						if (klass.Class.Id == FuId.SortedDictionaryClass)
							Write(".sort((a, b) => a[0] - b[0])");
						break;
					default:
						throw new NotImplementedException();
					}
				}
				break;
			default:
				throw new NotImplementedException();
			}
			WriteChar(')');
			WriteChild(statement.Body);
		}

		internal override void VisitLock(FuLock statement)
		{
			NotSupported(statement, "'lock'");
		}

		protected override void WriteSwitchCaseCond(FuSwitch statement, FuExpr value, FuPriority parent)
		{
			if (value is FuVar def)
				WriteIsVar(statement.Value, def, parent == FuPriority.CondAnd && def.Name != "_", parent);
			else
				base.WriteSwitchCaseCond(statement, value, parent);
		}

		protected override void WriteIfCaseBody(List<FuStatement> body, bool doWhile, FuSwitch statement, FuCase kase)
		{
			if (kase != null && kase.Values[0] is FuVar caseVar && caseVar.Name != "_") {
				WriteChar(' ');
				OpenBlock();
				WriteVarCast(caseVar, statement.Value);
				WriteFirstStatements(kase.Body, FuSwitch.LengthWithoutTrailingBreak(kase.Body));
				CloseBlock();
			}
			else
				base.WriteIfCaseBody(body, doWhile, statement, kase);
		}

		internal override void VisitSwitch(FuSwitch statement)
		{
			if (statement.IsTypeMatching() || statement.HasWhen()) {
				if (statement.Cases.Exists(kase => FuSwitch.HasEarlyBreak(kase.Body)) || FuSwitch.HasEarlyBreak(statement.DefaultBody)) {
					Write("fuswitch");
					VisitLiteralLong(this.SwitchesWithGoto.Count);
					this.SwitchesWithGoto.Add(statement);
					Write(": ");
					OpenBlock();
					WriteSwitchAsIfs(statement, false);
					CloseBlock();
				}
				else
					WriteSwitchAsIfs(statement, false);
			}
			else
				base.VisitSwitch(statement);
		}

		internal override void VisitThrow(FuThrow statement)
		{
			Write("throw ");
			statement.Message.Accept(this, FuPriority.Argument);
			WriteCharLine(';');
		}

		protected virtual void StartContainerType(FuContainerType container)
		{
			WriteNewLine();
			WriteDoc(container.Documentation);
		}

		internal override void VisitEnumValue(FuConst konst, FuConst previous)
		{
			if (previous != null)
				WriteCharLine(',');
			WriteDoc(konst.Documentation);
			WriteUppercaseWithUnderscores(konst.Name);
			Write(" : ");
			VisitLiteralLong(konst.Value.IntValue());
		}

		protected override void WriteEnum(FuEnum enu)
		{
			StartContainerType(enu);
			Write("const ");
			Write(enu.Name);
			Write(" = ");
			OpenBlock();
			enu.AcceptValues(this);
			WriteNewLine();
			CloseBlock();
		}

		protected override void WriteConst(FuConst konst)
		{
			if (konst.Visibility != FuVisibility.Private || konst.Type is FuArrayStorageType) {
				WriteNewLine();
				WriteDoc(konst.Documentation);
				Write("static ");
				WriteName(konst);
				Write(" = ");
				konst.Value.Accept(this, FuPriority.Argument);
				WriteCharLine(';');
			}
		}

		protected override void WriteField(FuField field)
		{
			WriteDoc(field.Documentation);
			base.WriteVar(field);
			WriteCharLine(';');
		}

		protected override void WriteMethod(FuMethod method)
		{
			if (method.CallType == FuCallType.Abstract)
				return;
			WriteNewLine();
			WriteMethodDoc(method);
			if (method.CallType == FuCallType.Static)
				Write("static ");
			WriteName(method);
			WriteParameters(method, true);
			WriteBody(method);
		}

		protected void WriteConstructor(FuClass klass)
		{
			WriteLine("constructor()");
			OpenBlock();
			if (klass.Parent is FuClass)
				WriteLine("super();");
			WriteConstructorBody(klass);
			CloseBlock();
		}

		protected override void WriteClass(FuClass klass, FuProgram program)
		{
			if (!WriteBaseClass(klass, program))
				return;
			StartContainerType(klass);
			OpenClass(klass, "", " extends ");
			if (NeedsConstructor(klass)) {
				if (klass.Constructor != null)
					WriteDoc(klass.Constructor.Documentation);
				WriteConstructor(klass);
			}
			WriteMembers(klass, true);
			CloseBlock();
		}

		protected void WriteLib(SortedDictionary<string, List<byte>> resources)
		{
			if (this.StringWriter) {
				WriteNewLine();
				WriteLine("class StringWriter");
				OpenBlock();
				WriteLine("#buf = \"\";");
				WriteNewLine();
				WriteLine("write(s)");
				OpenBlock();
				WriteLine("this.#buf += s;");
				CloseBlock();
				WriteNewLine();
				WriteLine("clear()");
				OpenBlock();
				WriteLine("this.#buf = \"\";");
				CloseBlock();
				WriteNewLine();
				WriteLine("toString()");
				OpenBlock();
				WriteLine("return this.#buf;");
				CloseBlock();
				CloseBlock();
			}
			if (resources.Count == 0)
				return;
			WriteNewLine();
			WriteLine("class Fu");
			OpenBlock();
			foreach ((string name, List<byte> content) in resources) {
				Write("static ");
				WriteResourceName(name);
				WriteLine(" = new Uint8Array([");
				WriteChar('\t');
				WriteBytes(content);
				WriteLine(" ]);");
			}
			WriteNewLine();
			CloseBlock();
		}

		protected virtual void WriteUseStrict()
		{
			WriteNewLine();
			WriteLine("\"use strict\";");
		}

		public override void WriteProgram(FuProgram program)
		{
			CreateOutputFile();
			WriteTopLevelNatives(program);
			WriteTypes(program);
			WriteLib(program.Resources);
			CloseFile();
		}
	}

	public class GenJs : GenJsNoModule
	{

		protected override void StartContainerType(FuContainerType container)
		{
			base.StartContainerType(container);
			if (container.IsPublic)
				Write("export ");
		}

		protected override void WriteUseStrict()
		{
		}
	}

	public class GenTs : GenJs
	{

		FuSystem System;

		bool GenFullCode = false;

		protected override string GetTargetName() => "TypeScript";

		public GenTs WithGenFullCode()
		{
			this.GenFullCode = true;
			return this;
		}

		internal override void VisitEnumValue(FuConst konst, FuConst previous)
		{
			WriteEnumValue(konst);
			WriteCharLine(',');
		}

		protected override void WriteEnum(FuEnum enu)
		{
			StartContainerType(enu);
			Write("enum ");
			Write(enu.Name);
			WriteChar(' ');
			OpenBlock();
			enu.AcceptValues(this);
			CloseBlock();
		}

		protected override void WriteTypeAndName(FuNamedValue value)
		{
			WriteName(value);
			Write(": ");
			WriteType(value.Type);
		}

		void WriteType(FuType type, bool readOnly = false)
		{
			switch (type) {
			case FuNumericType _:
				Write(type.Id == FuId.LongType ? "bigint" : "number");
				break;
			case FuEnum enu:
				Write(enu.Id == FuId.BoolType ? "boolean" : enu.Name);
				break;
			case FuClassType klass:
				readOnly |= !(klass is FuReadWriteClassType);
				switch (klass.Class.Id) {
				case FuId.StringClass:
					Write("string");
					break;
				case FuId.ArrayPtrClass when !(klass.GetElementType() is FuNumericType):
				case FuId.ArrayStorageClass when !(klass.GetElementType() is FuNumericType):
				case FuId.ListClass:
				case FuId.QueueClass:
				case FuId.StackClass:
					if (readOnly)
						Write("readonly ");
					if (klass.GetElementType().Nullable)
						WriteChar('(');
					WriteType(klass.GetElementType());
					if (klass.GetElementType().Nullable)
						WriteChar(')');
					Write("[]");
					break;
				default:
					if (readOnly && klass.Class.TypeParameterCount > 0)
						Write("Readonly<");
					switch (klass.Class.Id) {
					case FuId.ArrayPtrClass:
					case FuId.ArrayStorageClass:
						WriteArrayElementType(klass.GetElementType());
						Write("Array");
						break;
					case FuId.HashSetClass:
					case FuId.SortedSetClass:
						Write("Set<");
						WriteType(klass.GetElementType(), false);
						WriteChar('>');
						break;
					case FuId.DictionaryClass:
					case FuId.SortedDictionaryClass:
						if (klass.GetKeyType() is FuEnum)
							Write("Partial<");
						Write("Record<");
						WriteType(klass.GetKeyType());
						Write(", ");
						WriteType(klass.GetValueType());
						WriteChar('>');
						if (klass.GetKeyType() is FuEnum)
							WriteChar('>');
						break;
					case FuId.OrderedDictionaryClass:
						Write("Map<");
						WriteType(klass.GetKeyType());
						Write(", ");
						WriteType(klass.GetValueType());
						WriteChar('>');
						break;
					case FuId.RegexClass:
						Write("RegExp");
						break;
					case FuId.MatchClass:
						Write("RegExpMatchArray");
						break;
					default:
						Write(klass.Class.Name);
						break;
					}
					if (readOnly && klass.Class.TypeParameterCount > 0)
						WriteChar('>');
					break;
				}
				if (type.Nullable)
					Write(" | null");
				break;
			default:
				Write(type.Name);
				break;
			}
		}

		protected override void WriteAsType(FuVar def)
		{
			Write(" as ");
			Write(def.Type.Name);
		}

		protected override void WriteBinaryOperand(FuExpr expr, FuPriority parent, FuBinaryExpr binary)
		{
			FuType type = binary.Type;
			if (expr.Type is FuNumericType && binary.IsRel()) {
				type = this.System.PromoteNumericTypes(binary.Left.Type, binary.Right.Type);
			}
			WriteCoerced(type, expr, parent);
		}

		protected override void WriteEqualOperand(FuExpr expr, FuExpr other)
		{
			if (expr.Type is FuNumericType)
				WriteCoerced(this.System.PromoteNumericTypes(expr.Type, other.Type), expr, FuPriority.Equality);
			else
				expr.Accept(this, FuPriority.Equality);
		}

		protected override void WriteBoolAndOr(FuBinaryExpr expr)
		{
			Write("[ ");
			expr.Left.Accept(this, FuPriority.Argument);
			Write(", ");
			expr.Right.Accept(this, FuPriority.Argument);
			Write(" ].");
			Write(expr.Op == FuToken.And ? "every" : "some");
			Write("(Boolean)");
		}

		protected override void DefineIsVar(FuBinaryExpr binary)
		{
			if (binary.Right is FuVar def) {
				EnsureChildBlock();
				Write("let ");
				WriteName(def);
				Write(": ");
				WriteType(binary.Left.Type);
				EndStatement();
			}
		}

		void WriteVisibility(FuVisibility visibility)
		{
			switch (visibility) {
			case FuVisibility.Private:
			case FuVisibility.Internal:
				break;
			case FuVisibility.Protected:
				Write("protected ");
				break;
			case FuVisibility.Public:
				Write("public ");
				break;
			default:
				throw new NotImplementedException();
			}
		}

		protected override void WriteConst(FuConst konst)
		{
			WriteNewLine();
			WriteDoc(konst.Documentation);
			WriteVisibility(konst.Visibility);
			Write("static readonly ");
			WriteName(konst);
			Write(": ");
			WriteType(konst.Type, true);
			if (this.GenFullCode) {
				Write(" = ");
				konst.Value.Accept(this, FuPriority.Argument);
			}
			WriteCharLine(';');
		}

		protected override void WriteField(FuField field)
		{
			WriteDoc(field.Documentation);
			WriteVisibility(field.Visibility);
			if (field.Type.IsFinal() && !field.IsAssignableStorage())
				Write("readonly ");
			WriteTypeAndName(field);
			if (this.GenFullCode)
				WriteVarInit(field);
			WriteCharLine(';');
		}

		protected override void WriteMethod(FuMethod method)
		{
			WriteNewLine();
			WriteMethodDoc(method);
			WriteVisibility(method.Visibility);
			switch (method.CallType) {
			case FuCallType.Static:
				Write("static ");
				break;
			case FuCallType.Virtual:
				break;
			case FuCallType.Abstract:
				Write("abstract ");
				break;
			case FuCallType.Override:
				break;
			case FuCallType.Normal:
				break;
			case FuCallType.Sealed:
				break;
			default:
				throw new NotImplementedException();
			}
			WriteName(method);
			WriteChar('(');
			int i = 0;
			for (FuVar param = method.Parameters.FirstParameter(); param != null; param = param.NextParameter()) {
				if (i > 0)
					Write(", ");
				WriteName(param);
				if (param.Value != null && !this.GenFullCode)
					WriteChar('?');
				Write(": ");
				WriteType(param.Type);
				if (param.Value != null && this.GenFullCode)
					WriteVarInit(param);
				i++;
			}
			Write("): ");
			WriteType(method.Type);
			if (this.GenFullCode)
				WriteBody(method);
			else
				WriteCharLine(';');
		}

		protected override void WriteClass(FuClass klass, FuProgram program)
		{
			if (!WriteBaseClass(klass, program))
				return;
			StartContainerType(klass);
			switch (klass.CallType) {
			case FuCallType.Normal:
				break;
			case FuCallType.Abstract:
				Write("abstract ");
				break;
			case FuCallType.Static:
			case FuCallType.Sealed:
				break;
			default:
				throw new NotImplementedException();
			}
			OpenClass(klass, "", " extends ");
			if (NeedsConstructor(klass) || klass.CallType == FuCallType.Static) {
				if (klass.Constructor != null) {
					WriteDoc(klass.Constructor.Documentation);
					WriteVisibility(klass.Constructor.Visibility);
				}
				else if (klass.CallType == FuCallType.Static)
					Write("private ");
				if (this.GenFullCode)
					WriteConstructor(klass);
				else
					WriteLine("constructor();");
			}
			WriteMembers(klass, this.GenFullCode);
			CloseBlock();
		}

		public override void WriteProgram(FuProgram program)
		{
			this.System = program.System;
			CreateOutputFile();
			if (this.GenFullCode)
				WriteTopLevelNatives(program);
			WriteTypes(program);
			if (this.GenFullCode)
				WriteLib(program.Resources);
			CloseFile();
		}
	}

	public abstract class GenPySwift : GenBase
	{

		protected override void WriteDocPara(FuDocPara para, bool many)
		{
			if (many) {
				WriteNewLine();
				StartDocLine();
				WriteNewLine();
				StartDocLine();
			}
			foreach (FuDocInline inline in para.Children) {
				switch (inline) {
				case FuDocText text:
					Write(text.Text);
					break;
				case FuDocCode code:
					WriteChar('`');
					Write(code.Text);
					WriteChar('`');
					break;
				case FuDocLine _:
					WriteNewLine();
					StartDocLine();
					break;
				default:
					throw new NotImplementedException();
				}
			}
		}

		protected abstract string GetDocBullet();

		protected override void WriteDocList(FuDocList list)
		{
			WriteNewLine();
			foreach (FuDocPara item in list.Items) {
				Write(GetDocBullet());
				WriteDocPara(item, false);
				WriteNewLine();
			}
			StartDocLine();
		}

		protected override void WriteLocalName(FuSymbol symbol, FuPriority parent)
		{
			if (symbol is FuMember member) {
				if (member.IsStatic())
					WriteName(this.CurrentMethod.Parent);
				else
					Write("self");
				WriteChar('.');
			}
			WriteName(symbol);
		}

		internal override void VisitAggregateInitializer(FuAggregateInitializer expr)
		{
			Write("[ ");
			WriteCoercedLiterals(expr.Type.AsClassType().GetElementType(), expr.Items);
			Write(" ]");
		}

		internal override void VisitPrefixExpr(FuPrefixExpr expr, FuPriority parent)
		{
			switch (expr.Op) {
			case FuToken.Increment:
			case FuToken.Decrement:
				expr.Inner.Accept(this, parent);
				break;
			default:
				base.VisitPrefixExpr(expr, parent);
				break;
			}
		}

		internal override void VisitPostfixExpr(FuPostfixExpr expr, FuPriority parent)
		{
			switch (expr.Op) {
			case FuToken.Increment:
			case FuToken.Decrement:
				expr.Inner.Accept(this, parent);
				break;
			default:
				base.VisitPostfixExpr(expr, parent);
				break;
			}
		}

		static bool IsPtr(FuExpr expr) => expr.Type is FuClassType klass && klass.Class.Id != FuId.StringClass && !(klass is FuStorageType);

		protected abstract string GetReferenceEqOp(bool not);

		protected override void WriteEqual(FuExpr left, FuExpr right, FuPriority parent, bool not)
		{
			if (IsPtr(left) || IsPtr(right))
				WriteEqualExpr(left, right, parent, GetReferenceEqOp(not));
			else
				base.WriteEqual(left, right, parent, not);
		}

		protected virtual void WriteExpr(FuExpr expr, FuPriority parent)
		{
			expr.Accept(this, parent);
		}

		protected void WriteListAppend(FuExpr obj, List<FuExpr> args)
		{
			WritePostfix(obj, ".append(");
			FuType elementType = obj.Type.AsClassType().GetElementType();
			if (args.Count == 0)
				WriteNewStorage(elementType);
			else
				WriteCoerced(elementType, args[0], FuPriority.Argument);
			WriteChar(')');
		}

		protected virtual bool VisitPreCall(FuCallExpr call) => false;

		protected bool VisitXcrement(FuExpr expr, bool postfix, bool write)
		{
			bool seen;
			switch (expr) {
			case FuVar def:
				return def.Value != null && VisitXcrement(def.Value, postfix, write);
			case FuAggregateInitializer _:
			case FuLiteral _:
			case FuLambdaExpr _:
				return false;
			case FuInterpolatedString interp:
				seen = false;
				foreach (FuInterpolatedPart part in interp.Parts)
					seen |= VisitXcrement(part.Argument, postfix, write);
				return seen;
			case FuSymbolReference symbol:
				return symbol.Left != null && VisitXcrement(symbol.Left, postfix, write);
			case FuUnaryExpr unary:
				if (unary.Inner == null)
					return false;
				seen = VisitXcrement(unary.Inner, postfix, write);
				if ((unary.Op == FuToken.Increment || unary.Op == FuToken.Decrement) && postfix == unary is FuPostfixExpr) {
					if (write) {
						WriteExpr(unary.Inner, FuPriority.Assign);
						WriteLine(unary.Op == FuToken.Increment ? " += 1" : " -= 1");
					}
					seen = true;
				}
				return seen;
			case FuBinaryExpr binary:
				seen = VisitXcrement(binary.Left, postfix, write);
				if (binary.Op == FuToken.Is)
					return seen;
				if (binary.Op == FuToken.CondAnd || binary.Op == FuToken.CondOr)
					Debug.Assert(!VisitXcrement(binary.Right, postfix, false));
				else
					seen |= VisitXcrement(binary.Right, postfix, write);
				return seen;
			case FuSelectExpr select:
				seen = VisitXcrement(select.Cond, postfix, write);
				Debug.Assert(!VisitXcrement(select.OnTrue, postfix, false));
				Debug.Assert(!VisitXcrement(select.OnFalse, postfix, false));
				return seen;
			case FuCallExpr call:
				seen = VisitXcrement(call.Method, postfix, write);
				foreach (FuExpr arg in call.Arguments)
					seen |= VisitXcrement(arg, postfix, write);
				if (!postfix)
					seen |= VisitPreCall(call);
				return seen;
			default:
				throw new NotImplementedException();
			}
		}

		internal override void VisitExpr(FuExpr statement)
		{
			VisitXcrement(statement, false, true);
			if (!(statement is FuUnaryExpr unary) || (unary.Op != FuToken.Increment && unary.Op != FuToken.Decrement)) {
				WriteExpr(statement, FuPriority.Statement);
				WriteNewLine();
				if (statement is FuVar def)
					WriteInitCode(def);
			}
			VisitXcrement(statement, true, true);
			CleanupTemporaries();
		}

		protected override void EndStatement()
		{
			WriteNewLine();
		}

		protected abstract void OpenChild();

		protected abstract void CloseChild();

		protected override void WriteChild(FuStatement statement)
		{
			OpenChild();
			statement.AcceptStatement(this);
			CloseChild();
		}

		internal override void VisitBlock(FuBlock statement)
		{
			WriteStatements(statement.Statements);
		}

		bool OpenCond(string statement, FuExpr cond, FuPriority parent)
		{
			VisitXcrement(cond, false, true);
			Write(statement);
			WriteExpr(cond, parent);
			OpenChild();
			return VisitXcrement(cond, true, true);
		}

		protected virtual void WriteContinueDoWhile(FuExpr cond)
		{
			OpenCond("if ", cond, FuPriority.Argument);
			WriteLine("continue");
			CloseChild();
			VisitXcrement(cond, true, true);
			WriteLine("break");
		}

		protected virtual bool NeedCondXcrement(FuLoop loop) => loop.Cond != null;

		void EndBody(FuLoop loop)
		{
			if (loop is FuFor forLoop) {
				if (forLoop.IsRange)
					return;
				VisitOptionalStatement(forLoop.Advance);
			}
			if (NeedCondXcrement(loop))
				VisitXcrement(loop.Cond, false, true);
		}

		internal override void VisitContinue(FuContinue statement)
		{
			if (statement.Loop is FuDoWhile doWhile)
				WriteContinueDoWhile(doWhile.Cond);
			else {
				EndBody(statement.Loop);
				WriteLine("continue");
			}
		}

		void OpenWhileTrue()
		{
			Write("while ");
			VisitLiteralTrue();
			OpenChild();
		}

		protected abstract string GetIfNot();

		internal override void VisitDoWhile(FuDoWhile statement)
		{
			OpenWhileTrue();
			statement.Body.AcceptStatement(this);
			if (statement.Body.CompletesNormally()) {
				OpenCond(GetIfNot(), statement.Cond, FuPriority.Primary);
				WriteLine("break");
				CloseChild();
				VisitXcrement(statement.Cond, true, true);
			}
			CloseChild();
		}

		protected virtual void OpenWhile(FuLoop loop)
		{
			OpenCond("while ", loop.Cond, FuPriority.Argument);
		}

		void CloseWhile(FuLoop loop)
		{
			loop.Body.AcceptStatement(this);
			if (loop.Body.CompletesNormally())
				EndBody(loop);
			CloseChild();
			if (NeedCondXcrement(loop)) {
				if (loop.HasBreak && VisitXcrement(loop.Cond, true, false)) {
					Write("else");
					OpenChild();
					VisitXcrement(loop.Cond, true, true);
					CloseChild();
				}
				else
					VisitXcrement(loop.Cond, true, true);
			}
		}

		protected abstract void WriteForRange(FuVar iter, FuBinaryExpr cond, long rangeStep);

		internal override void VisitFor(FuFor statement)
		{
			if (statement.IsRange) {
				FuVar iter = (FuVar) statement.Init;
				Write("for ");
				if (statement.IsIteratorUsed)
					WriteName(iter);
				else
					WriteChar('_');
				Write(" in ");
				FuBinaryExpr cond = (FuBinaryExpr) statement.Cond;
				WriteForRange(iter, cond, statement.RangeStep);
				WriteChild(statement.Body);
			}
			else {
				VisitOptionalStatement(statement.Init);
				if (statement.Cond != null)
					OpenWhile(statement);
				else
					OpenWhileTrue();
				CloseWhile(statement);
			}
		}

		protected abstract void WriteElseIf();

		internal override void VisitIf(FuIf statement)
		{
			bool condPostXcrement = OpenCond("if ", statement.Cond, FuPriority.Argument);
			statement.OnTrue.AcceptStatement(this);
			CloseChild();
			if (statement.OnFalse == null && condPostXcrement && !statement.OnTrue.CompletesNormally())
				VisitXcrement(statement.Cond, true, true);
			else if (statement.OnFalse != null || condPostXcrement) {
				if (!condPostXcrement && statement.OnFalse is FuIf childIf && !VisitXcrement(childIf.Cond, false, false)) {
					WriteElseIf();
					VisitIf(childIf);
				}
				else {
					Write("else");
					OpenChild();
					VisitXcrement(statement.Cond, true, true);
					VisitOptionalStatement(statement.OnFalse);
					CloseChild();
				}
			}
		}

		protected abstract void WriteResultVar();

		internal override void VisitReturn(FuReturn statement)
		{
			if (statement.Value == null)
				WriteLine("return");
			else {
				VisitXcrement(statement.Value, false, true);
				WriteTemporaries(statement.Value);
				if (VisitXcrement(statement.Value, true, false)) {
					WriteResultVar();
					Write(" = ");
					WriteCoercedExpr(this.CurrentMethod.Type, statement.Value);
					WriteNewLine();
					VisitXcrement(statement.Value, true, true);
					WriteLine("return result");
				}
				else {
					Write("return ");
					WriteCoercedExpr(this.CurrentMethod.Type, statement.Value);
					WriteNewLine();
				}
				CleanupTemporaries();
			}
		}

		internal override void VisitWhile(FuWhile statement)
		{
			OpenWhile(statement);
			CloseWhile(statement);
		}
	}

	public class GenSwift : GenPySwift
	{

		FuSystem System;

		bool Throw;

		bool ArrayRef;

		bool StringCharAt;

		bool StringIndexOf;

		bool StringSubstring;

		readonly List<HashSet<string>> VarsAtIndent = new List<HashSet<string>>();

		readonly List<bool> VarBytesAtIndent = new List<bool>();

		protected override string GetTargetName() => "Swift";

		protected override void StartDocLine()
		{
			Write("/// ");
		}

		protected override string GetDocBullet() => "/// * ";

		protected override void WriteDoc(FuCodeDoc doc)
		{
			if (doc != null)
				WriteContent(doc);
		}

		void WriteCamelCaseNotKeyword(string name)
		{
			switch (name) {
			case "this":
				Write("self");
				break;
			case "As":
			case "Associatedtype":
			case "Await":
			case "Break":
			case "Case":
			case "Catch":
			case "Class":
			case "Continue":
			case "Default":
			case "Defer":
			case "Deinit":
			case "Do":
			case "Else":
			case "Enum":
			case "Extension":
			case "Fallthrough":
			case "False":
			case "Fileprivate":
			case "For":
			case "Foreach":
			case "Func":
			case "Guard":
			case "If":
			case "Import":
			case "In":
			case "Init":
			case "Inout":
			case "Int":
			case "Internal":
			case "Is":
			case "Let":
			case "Nil":
			case "Operator":
			case "Private":
			case "Protocol":
			case "Public":
			case "Repeat":
			case "Rethrows":
			case "Return":
			case "Self":
			case "Static":
			case "Struct":
			case "Switch":
			case "Subscript":
			case "Super":
			case "Throw":
			case "Throws":
			case "True":
			case "Try":
			case "Typealias":
			case "Var":
			case "Void":
			case "Where":
			case "While":
			case "as":
			case "associatedtype":
			case "await":
			case "catch":
			case "defer":
			case "deinit":
			case "extension":
			case "fallthrough":
			case "fileprivate":
			case "func":
			case "guard":
			case "import":
			case "init":
			case "inout":
			case "is":
			case "let":
			case "nil":
			case "operator":
			case "private":
			case "protocol":
			case "repeat":
			case "rethrows":
			case "self":
			case "struct":
			case "subscript":
			case "super":
			case "try":
			case "typealias":
			case "var":
			case "where":
				WriteCamelCase(name);
				WriteChar('_');
				break;
			default:
				WriteCamelCase(name);
				break;
			}
		}

		protected override void WriteName(FuSymbol symbol)
		{
			switch (symbol) {
			case FuContainerType _:
				Write(symbol.Name);
				break;
			case FuConst konst when konst.InMethod != null:
				WriteCamelCase(konst.InMethod.Name);
				WritePascalCase(symbol.Name);
				break;
			case FuVar _:
			case FuMember _:
				WriteCamelCaseNotKeyword(symbol.Name);
				break;
			default:
				throw new NotImplementedException();
			}
		}

		protected override void WriteLocalName(FuSymbol symbol, FuPriority parent)
		{
			if (symbol.Parent is FuForeach forEach && forEach.Collection.Type is FuStringType) {
				Write("Int(");
				WriteCamelCaseNotKeyword(symbol.Name);
				Write(".value)");
			}
			else
				base.WriteLocalName(symbol, parent);
		}

		protected override void WriteMemberOp(FuExpr left, FuSymbolReference symbol)
		{
			if (left.Type != null && left.Type.Nullable)
				WriteChar('!');
			WriteChar('.');
		}

		void OpenIndexing(FuExpr collection)
		{
			collection.Accept(this, FuPriority.Primary);
			if (collection.Type.Nullable)
				WriteChar('!');
			WriteChar('[');
		}

		static bool IsArrayRef(FuArrayStorageType array) => array.PtrTaken || array.GetElementType() is FuStorageType;

		void WriteClassName(FuClassType klass)
		{
			switch (klass.Class.Id) {
			case FuId.StringClass:
				Write("String");
				break;
			case FuId.ArrayPtrClass:
				this.ArrayRef = true;
				Write("ArrayRef<");
				WriteType(klass.GetElementType());
				WriteChar('>');
				break;
			case FuId.ListClass:
			case FuId.QueueClass:
			case FuId.StackClass:
				WriteChar('[');
				WriteType(klass.GetElementType());
				WriteChar(']');
				break;
			case FuId.HashSetClass:
			case FuId.SortedSetClass:
				Write("Set<");
				WriteType(klass.GetElementType());
				WriteChar('>');
				break;
			case FuId.DictionaryClass:
			case FuId.SortedDictionaryClass:
				WriteChar('[');
				WriteType(klass.GetKeyType());
				Write(": ");
				WriteType(klass.GetValueType());
				WriteChar(']');
				break;
			case FuId.OrderedDictionaryClass:
				NotSupported(klass, "OrderedDictionary");
				break;
			case FuId.LockClass:
				Include("Foundation");
				Write("NSRecursiveLock");
				break;
			default:
				Write(klass.Class.Name);
				break;
			}
		}

		void WriteType(FuType type)
		{
			switch (type) {
			case FuNumericType _:
				switch (type.Id) {
				case FuId.SByteRange:
					Write("Int8");
					break;
				case FuId.ByteRange:
					Write("UInt8");
					break;
				case FuId.ShortRange:
					Write("Int16");
					break;
				case FuId.UShortRange:
					Write("UInt16");
					break;
				case FuId.IntType:
					Write("Int");
					break;
				case FuId.LongType:
					Write("Int64");
					break;
				case FuId.FloatType:
					Write("Float");
					break;
				case FuId.DoubleType:
					Write("Double");
					break;
				default:
					throw new NotImplementedException();
				}
				break;
			case FuEnum _:
				Write(type.Id == FuId.BoolType ? "Bool" : type.Name);
				break;
			case FuArrayStorageType arrayStg:
				if (IsArrayRef(arrayStg)) {
					this.ArrayRef = true;
					Write("ArrayRef<");
					WriteType(arrayStg.GetElementType());
					WriteChar('>');
				}
				else {
					WriteChar('[');
					WriteType(arrayStg.GetElementType());
					WriteChar(']');
				}
				break;
			case FuClassType klass:
				WriteClassName(klass);
				if (klass.Nullable)
					WriteChar('?');
				break;
			default:
				Write(type.Name);
				break;
			}
		}

		protected override void WriteTypeAndName(FuNamedValue value)
		{
			WriteName(value);
			if (!value.Type.IsFinal() || value.IsAssignableStorage()) {
				Write(" : ");
				WriteType(value.Type);
			}
		}

		internal override void VisitLiteralNull()
		{
			Write("nil");
		}

		void WriteUnwrapped(FuExpr expr, FuPriority parent, bool substringOk)
		{
			if (expr.Type.Nullable) {
				expr.Accept(this, FuPriority.Primary);
				WriteChar('!');
			}
			else if (!substringOk && expr is FuCallExpr call && call.Method.Symbol.Id == FuId.StringSubstring)
				WriteCall("String", expr);
			else
				expr.Accept(this, parent);
		}

		internal override void VisitInterpolatedString(FuInterpolatedString expr, FuPriority parent)
		{
			if (expr.Parts.Exists(part => part.WidthExpr != null || part.Format != ' ' || part.Precision >= 0)) {
				Include("Foundation");
				Write("String(format: ");
				WritePrintf(expr, false);
			}
			else {
				WriteChar('"');
				foreach (FuInterpolatedPart part in expr.Parts) {
					Write(part.Prefix);
					Write("\\(");
					WriteUnwrapped(part.Argument, FuPriority.Argument, true);
					WriteChar(')');
				}
				Write(expr.Suffix);
				WriteChar('"');
			}
		}

		protected override void WriteCoercedInternal(FuType type, FuExpr expr, FuPriority parent)
		{
			if (type is FuNumericType && !(expr is FuLiteral) && GetTypeId(type, false) != GetTypeId(expr.Type, expr is FuBinaryExpr binary && binary.Op != FuToken.LeftBracket)) {
				WriteType(type);
				WriteChar('(');
				if (type is FuIntegerType && expr is FuCallExpr call && call.Method.Symbol.Id == FuId.MathTruncate)
					call.Arguments[0].Accept(this, FuPriority.Argument);
				else
					expr.Accept(this, FuPriority.Argument);
				WriteChar(')');
			}
			else if (!type.Nullable)
				WriteUnwrapped(expr, parent, false);
			else
				expr.Accept(this, parent);
		}

		protected override void WriteStringLength(FuExpr expr)
		{
			WriteUnwrapped(expr, FuPriority.Primary, true);
			Write(".count");
		}

		protected override void WriteCharAt(FuBinaryExpr expr)
		{
			this.StringCharAt = true;
			Write("fuStringCharAt(");
			WriteUnwrapped(expr.Left, FuPriority.Argument, false);
			Write(", ");
			expr.Right.Accept(this, FuPriority.Argument);
			WriteChar(')');
		}

		internal override void VisitSymbolReference(FuSymbolReference expr, FuPriority parent)
		{
			switch (expr.Symbol.Id) {
			case FuId.MathNaN:
				Write("Float.nan");
				break;
			case FuId.MathNegativeInfinity:
				Write("-Float.infinity");
				break;
			case FuId.MathPositiveInfinity:
				Write("Float.infinity");
				break;
			default:
				base.VisitSymbolReference(expr, parent);
				break;
			}
		}

		protected override string GetReferenceEqOp(bool not) => not ? " !== " : " === ";

		void WriteStringContains(FuExpr obj, string name, List<FuExpr> args)
		{
			WriteUnwrapped(obj, FuPriority.Primary, true);
			WriteChar('.');
			Write(name);
			WriteChar('(');
			WriteUnwrapped(args[0], FuPriority.Argument, true);
			WriteChar(')');
		}

		void WriteRange(FuExpr startIndex, FuExpr length)
		{
			WriteCoerced(this.System.IntType, startIndex, FuPriority.Shift);
			Write("..<");
			WriteAdd(startIndex, length);
		}

		bool AddVar(string name)
		{
			HashSet<string> vars = this.VarsAtIndent[this.Indent];
			if (vars.Contains(name))
				return false;
			vars.Add(name);
			return true;
		}

		protected override void WriteCallExpr(FuExpr obj, FuMethod method, List<FuExpr> args, FuPriority parent)
		{
			switch (method.Id) {
			case FuId.None:
			case FuId.ArrayContains:
			case FuId.ListContains:
			case FuId.ListSortAll:
			case FuId.HashSetContains:
			case FuId.HashSetRemove:
			case FuId.SortedSetContains:
			case FuId.SortedSetRemove:
				if (obj == null) {
					if (method.IsStatic()) {
						WriteName(this.CurrentMethod.Parent);
						WriteChar('.');
					}
				}
				else if (IsReferenceTo(obj, FuId.BasePtr))
					Write("super.");
				else {
					obj.Accept(this, FuPriority.Primary);
					WriteMemberOp(obj, null);
				}
				WriteName(method);
				WriteArgsInParentheses(method, args);
				break;
			case FuId.ClassToString:
				obj.Accept(this, FuPriority.Primary);
				WriteMemberOp(obj, null);
				Write("description");
				break;
			case FuId.EnumFromInt:
				Write(method.Type.Name);
				Write("(rawValue: ");
				args[0].Accept(this, FuPriority.Argument);
				WriteChar(')');
				break;
			case FuId.EnumHasFlag:
				WriteMethodCall(obj, "contains", args[0]);
				break;
			case FuId.StringContains:
				WriteStringContains(obj, "contains", args);
				break;
			case FuId.StringEndsWith:
				WriteStringContains(obj, "hasSuffix", args);
				break;
			case FuId.StringIndexOf:
				Include("Foundation");
				this.StringIndexOf = true;
				Write("fuStringIndexOf(");
				WriteUnwrapped(obj, FuPriority.Argument, true);
				Write(", ");
				WriteUnwrapped(args[0], FuPriority.Argument, true);
				WriteChar(')');
				break;
			case FuId.StringLastIndexOf:
				Include("Foundation");
				this.StringIndexOf = true;
				Write("fuStringIndexOf(");
				WriteUnwrapped(obj, FuPriority.Argument, true);
				Write(", ");
				WriteUnwrapped(args[0], FuPriority.Argument, true);
				Write(", .backwards)");
				break;
			case FuId.StringReplace:
				WriteUnwrapped(obj, FuPriority.Primary, true);
				Write(".replacingOccurrences(of: ");
				WriteUnwrapped(args[0], FuPriority.Argument, true);
				Write(", with: ");
				WriteUnwrapped(args[1], FuPriority.Argument, true);
				WriteChar(')');
				break;
			case FuId.StringStartsWith:
				WriteStringContains(obj, "hasPrefix", args);
				break;
			case FuId.StringSubstring:
				if (args[0].IsLiteralZero())
					WriteUnwrapped(obj, FuPriority.Primary, true);
				else {
					this.StringSubstring = true;
					Write("fuStringSubstring(");
					WriteUnwrapped(obj, FuPriority.Argument, false);
					Write(", ");
					WriteCoerced(this.System.IntType, args[0], FuPriority.Argument);
					WriteChar(')');
				}
				if (args.Count == 2) {
					Write(".prefix(");
					WriteCoerced(this.System.IntType, args[1], FuPriority.Argument);
					WriteChar(')');
				}
				break;
			case FuId.ArrayCopyTo:
			case FuId.ListCopyTo:
				OpenIndexing(args[1]);
				WriteRange(args[2], args[3]);
				Write("] = ");
				OpenIndexing(obj);
				WriteRange(args[0], args[3]);
				WriteChar(']');
				break;
			case FuId.ArrayFillAll:
				obj.Accept(this, FuPriority.Assign);
				if (obj.Type is FuArrayStorageType array && !IsArrayRef(array)) {
					Write(" = [");
					WriteType(array.GetElementType());
					Write("](repeating: ");
					WriteCoerced(array.GetElementType(), args[0], FuPriority.Argument);
					Write(", count: ");
					VisitLiteralLong(array.Length);
					WriteChar(')');
				}
				else {
					Write(".fill");
					WriteArgsInParentheses(method, args);
				}
				break;
			case FuId.ArrayFillPart:
				if (obj.Type is FuArrayStorageType array2 && !IsArrayRef(array2)) {
					OpenIndexing(obj);
					WriteRange(args[1], args[2]);
					Write("] = ArraySlice(repeating: ");
					WriteCoerced(array2.GetElementType(), args[0], FuPriority.Argument);
					Write(", count: ");
					WriteCoerced(this.System.IntType, args[2], FuPriority.Argument);
					WriteChar(')');
				}
				else {
					obj.Accept(this, FuPriority.Primary);
					WriteMemberOp(obj, null);
					Write("fill");
					WriteArgsInParentheses(method, args);
				}
				break;
			case FuId.ArraySortAll:
				WritePostfix(obj, "[0..<");
				FuArrayStorageType array3 = (FuArrayStorageType) obj.Type;
				VisitLiteralLong(array3.Length);
				Write("].sort()");
				break;
			case FuId.ArraySortPart:
			case FuId.ListSortPart:
				OpenIndexing(obj);
				WriteRange(args[0], args[1]);
				Write("].sort()");
				break;
			case FuId.ListAdd:
			case FuId.QueueEnqueue:
			case FuId.StackPush:
				WriteListAppend(obj, args);
				break;
			case FuId.ListAddRange:
				obj.Accept(this, FuPriority.Assign);
				Write(" += ");
				args[0].Accept(this, FuPriority.Argument);
				break;
			case FuId.ListAll:
				WritePostfix(obj, ".allSatisfy ");
				args[0].Accept(this, FuPriority.Argument);
				break;
			case FuId.ListAny:
				WritePostfix(obj, ".contains ");
				args[0].Accept(this, FuPriority.Argument);
				break;
			case FuId.ListClear:
			case FuId.QueueClear:
			case FuId.StackClear:
			case FuId.HashSetClear:
			case FuId.SortedSetClear:
			case FuId.DictionaryClear:
			case FuId.SortedDictionaryClear:
				WritePostfix(obj, ".removeAll()");
				break;
			case FuId.ListIndexOf:
				if (parent > FuPriority.Rel)
					WriteChar('(');
				WritePostfix(obj, ".firstIndex(of: ");
				args[0].Accept(this, FuPriority.Argument);
				Write(") ?? -1");
				if (parent > FuPriority.Rel)
					WriteChar(')');
				break;
			case FuId.ListInsert:
				WritePostfix(obj, ".insert(");
				FuType elementType = obj.Type.AsClassType().GetElementType();
				if (args.Count == 1)
					WriteNewStorage(elementType);
				else
					WriteCoerced(elementType, args[1], FuPriority.Argument);
				Write(", at: ");
				WriteCoerced(this.System.IntType, args[0], FuPriority.Argument);
				WriteChar(')');
				break;
			case FuId.ListLast:
			case FuId.StackPeek:
				WritePostfix(obj, ".last");
				break;
			case FuId.ListRemoveAt:
				WritePostfix(obj, ".remove(at: ");
				WriteCoerced(this.System.IntType, args[0], FuPriority.Argument);
				WriteChar(')');
				break;
			case FuId.ListRemoveRange:
				WritePostfix(obj, ".removeSubrange(");
				WriteRange(args[0], args[1]);
				WriteChar(')');
				break;
			case FuId.QueueDequeue:
				WritePostfix(obj, ".removeFirst()");
				break;
			case FuId.QueuePeek:
				WritePostfix(obj, ".first");
				break;
			case FuId.StackPop:
				WritePostfix(obj, ".removeLast()");
				break;
			case FuId.HashSetAdd:
			case FuId.SortedSetAdd:
				WritePostfix(obj, ".insert(");
				WriteCoerced(obj.Type.AsClassType().GetElementType(), args[0], FuPriority.Argument);
				WriteChar(')');
				break;
			case FuId.DictionaryAdd:
				WriteDictionaryAdd(obj, args);
				break;
			case FuId.DictionaryContainsKey:
			case FuId.SortedDictionaryContainsKey:
				if (parent > FuPriority.Equality)
					WriteChar('(');
				WriteIndexing(obj, args[0]);
				Write(" != nil");
				if (parent > FuPriority.Equality)
					WriteChar(')');
				break;
			case FuId.DictionaryRemove:
			case FuId.SortedDictionaryRemove:
				WritePostfix(obj, ".removeValue(forKey: ");
				args[0].Accept(this, FuPriority.Argument);
				WriteChar(')');
				break;
			case FuId.ConsoleWrite:
				Write("print(");
				WriteUnwrapped(args[0], FuPriority.Argument, true);
				Write(", terminator: \"\")");
				break;
			case FuId.ConsoleWriteLine:
				Write("print(");
				if (args.Count == 1)
					WriteUnwrapped(args[0], FuPriority.Argument, true);
				WriteChar(')');
				break;
			case FuId.UTF8GetByteCount:
				WriteUnwrapped(args[0], FuPriority.Primary, true);
				Write(".utf8.count");
				break;
			case FuId.UTF8GetBytes:
				if (AddVar("fubytes"))
					Write(this.VarBytesAtIndent[this.Indent] ? "var " : "let ");
				Write("fubytes = [UInt8](");
				WriteUnwrapped(args[0], FuPriority.Primary, true);
				WriteLine(".utf8)");
				OpenIndexing(args[1]);
				WriteCoerced(this.System.IntType, args[2], FuPriority.Shift);
				if (args[2].IsLiteralZero())
					Write("..<");
				else {
					Write(" ..< ");
					WriteCoerced(this.System.IntType, args[2], FuPriority.Add);
					Write(" + ");
				}
				WriteLine("fubytes.count] = fubytes[...]");
				break;
			case FuId.UTF8GetString:
				Write("String(decoding: ");
				OpenIndexing(args[0]);
				WriteRange(args[1], args[2]);
				Write("], as: UTF8.self)");
				break;
			case FuId.EnvironmentGetEnvironmentVariable:
				Include("Foundation");
				Write("ProcessInfo.processInfo.environment[");
				WriteUnwrapped(args[0], FuPriority.Argument, false);
				WriteChar(']');
				break;
			case FuId.MathMethod:
			case FuId.MathLog2:
				Include("Foundation");
				WriteCamelCase(method.Name);
				WriteArgsInParentheses(method, args);
				break;
			case FuId.MathAbs:
			case FuId.MathMaxInt:
			case FuId.MathMaxDouble:
			case FuId.MathMinInt:
			case FuId.MathMinDouble:
				WriteCamelCase(method.Name);
				WriteArgsInParentheses(method, args);
				break;
			case FuId.MathCeiling:
				Include("Foundation");
				WriteCall("ceil", args[0]);
				break;
			case FuId.MathClamp:
				Write("min(max(");
				WriteClampAsMinMax(args);
				break;
			case FuId.MathFusedMultiplyAdd:
				Include("Foundation");
				WriteCall("fma", args[0], args[1], args[2]);
				break;
			case FuId.MathIsFinite:
				WritePostfix(args[0], ".isFinite");
				break;
			case FuId.MathIsInfinity:
				WritePostfix(args[0], ".isInfinite");
				break;
			case FuId.MathIsNaN:
				WritePostfix(args[0], ".isNaN");
				break;
			case FuId.MathRound:
				WritePostfix(args[0], ".rounded()");
				break;
			case FuId.MathTruncate:
				Include("Foundation");
				WriteCall("trunc", args[0]);
				break;
			default:
				NotSupported(obj, method.Name);
				break;
			}
		}

		protected override void WriteNewArrayStorage(FuArrayStorageType array)
		{
			if (IsArrayRef(array))
				base.WriteNewArrayStorage(array);
			else {
				WriteChar('[');
				WriteType(array.GetElementType());
				Write("](repeating: ");
				WriteDefaultValue(array.GetElementType());
				Write(", count: ");
				VisitLiteralLong(array.Length);
				WriteChar(')');
			}
		}

		protected override void WriteNew(FuReadWriteClassType klass, FuPriority parent)
		{
			WriteClassName(klass);
			Write("()");
		}

		void WriteDefaultValue(FuType type)
		{
			switch (type) {
			case FuNumericType _:
				WriteChar('0');
				break;
			case FuEnum enu:
				if (enu.Id == FuId.BoolType)
					Write("false");
				else {
					WriteName(enu);
					WriteChar('.');
					WriteName(enu.GetFirstValue());
				}
				break;
			case FuStringType _ when !type.Nullable:
				Write("\"\"");
				break;
			case FuArrayStorageType array:
				WriteNewArrayStorage(array);
				break;
			default:
				Write("nil");
				break;
			}
		}

		protected override void WriteNewArray(FuType elementType, FuExpr lengthExpr, FuPriority parent)
		{
			this.ArrayRef = true;
			Write("ArrayRef<");
			WriteType(elementType);
			Write(">(");
			switch (elementType) {
			case FuArrayStorageType _:
				Write("factory: { ");
				WriteNewStorage(elementType);
				Write(" }");
				break;
			case FuStorageType klass:
				Write("factory: ");
				WriteName(klass.Class);
				Write(".init");
				break;
			default:
				Write("repeating: ");
				WriteDefaultValue(elementType);
				break;
			}
			Write(", count: ");
			lengthExpr.Accept(this, FuPriority.Argument);
			WriteChar(')');
		}

		internal override void VisitPrefixExpr(FuPrefixExpr expr, FuPriority parent)
		{
			if (expr.Op == FuToken.Tilde && expr.Type is FuEnumFlags) {
				Write(expr.Type.Name);
				Write("(rawValue: ~");
				WritePostfix(expr.Inner, ".rawValue)");
			}
			else
				base.VisitPrefixExpr(expr, parent);
		}

		protected override void WriteIndexingExpr(FuBinaryExpr expr, FuPriority parent)
		{
			OpenIndexing(expr.Left);
			FuClassType klass = (FuClassType) expr.Left.Type;
			FuType indexType;
			switch (klass.Class.Id) {
			case FuId.ArrayPtrClass:
			case FuId.ArrayStorageClass:
			case FuId.ListClass:
				indexType = this.System.IntType;
				break;
			default:
				indexType = klass.GetKeyType();
				break;
			}
			WriteCoerced(indexType, expr.Right, FuPriority.Argument);
			WriteChar(']');
			if (parent != FuPriority.Assign && expr.Left.Type is FuClassType dict && dict.Class.TypeParameterCount == 2)
				WriteChar('!');
		}

		protected override void WriteBinaryOperand(FuExpr expr, FuPriority parent, FuBinaryExpr binary)
		{
			if (expr.Type.Id != FuId.BoolType) {
				if (binary.Op == FuToken.Plus && binary.Type.Id == FuId.StringStorageType) {
					WriteUnwrapped(expr, parent, true);
					return;
				}
				switch (binary.Op) {
				case FuToken.Plus:
				case FuToken.Minus:
				case FuToken.Asterisk:
				case FuToken.Slash:
				case FuToken.Mod:
				case FuToken.And:
				case FuToken.Or:
				case FuToken.Xor:
				case FuToken.ShiftLeft when expr == binary.Left:
				case FuToken.ShiftRight when expr == binary.Left:
					if (!(expr is FuLiteral)) {
						FuType type = this.System.PromoteNumericTypes(binary.Left.Type, binary.Right.Type);
						if (type != expr.Type) {
							WriteCoerced(type, expr, parent);
							return;
						}
					}
					break;
				case FuToken.Equal:
				case FuToken.NotEqual:
				case FuToken.Less:
				case FuToken.LessOrEqual:
				case FuToken.Greater:
				case FuToken.GreaterOrEqual:
					FuType typeComp = this.System.PromoteFloatingTypes(binary.Left.Type, binary.Right.Type);
					if (typeComp != null && typeComp != expr.Type) {
						WriteCoerced(typeComp, expr, parent);
						return;
					}
					break;
				default:
					break;
				}
			}
			expr.Accept(this, parent);
		}

		void WriteEnumFlagsAnd(FuExpr left, string method, string notMethod, FuExpr right)
		{
			if (right is FuPrefixExpr negation && negation.Op == FuToken.Tilde)
				WriteMethodCall(left, notMethod, negation.Inner);
			else
				WriteMethodCall(left, method, right);
		}

		FuExpr WriteAssignNested(FuBinaryExpr expr)
		{
			if (expr.Right is FuBinaryExpr rightBinary && rightBinary.IsAssign()) {
				VisitBinaryExpr(rightBinary, FuPriority.Statement);
				WriteNewLine();
				return rightBinary.Left;
			}
			return expr.Right;
		}

		void WriteSwiftAssign(FuBinaryExpr expr, FuExpr right)
		{
			expr.Left.Accept(this, FuPriority.Assign);
			WriteChar(' ');
			Write(expr.GetOpString());
			WriteChar(' ');
			if (right is FuLiteralNull && expr.Left is FuBinaryExpr leftBinary && leftBinary.Op == FuToken.LeftBracket && leftBinary.Left.Type is FuClassType dict && dict.Class.TypeParameterCount == 2) {
				WriteType(dict.GetValueType());
				Write(".none");
			}
			else
				WriteCoerced(expr.Type, right, FuPriority.Argument);
		}

		internal override void VisitBinaryExpr(FuBinaryExpr expr, FuPriority parent)
		{
			FuExpr right;
			switch (expr.Op) {
			case FuToken.ShiftLeft:
				WriteBinaryExpr(expr, parent > FuPriority.Mul, FuPriority.Primary, " << ", FuPriority.Primary);
				break;
			case FuToken.ShiftRight:
				WriteBinaryExpr(expr, parent > FuPriority.Mul, FuPriority.Primary, " >> ", FuPriority.Primary);
				break;
			case FuToken.And:
				if (expr.Type.Id == FuId.BoolType)
					WriteCall("{ a, b in a && b }", expr.Left, expr.Right);
				else if (expr.Type is FuEnumFlags)
					WriteEnumFlagsAnd(expr.Left, "intersection", "subtracting", expr.Right);
				else
					WriteBinaryExpr(expr, parent > FuPriority.Mul, FuPriority.Mul, " & ", FuPriority.Primary);
				break;
			case FuToken.Or:
				if (expr.Type.Id == FuId.BoolType)
					WriteCall("{ a, b in a || b }", expr.Left, expr.Right);
				else if (expr.Type is FuEnumFlags)
					WriteMethodCall(expr.Left, "union", expr.Right);
				else
					WriteBinaryExpr(expr, parent > FuPriority.Add, FuPriority.Add, " | ", FuPriority.Mul);
				break;
			case FuToken.Xor:
				if (expr.Type.Id == FuId.BoolType)
					WriteEqual(expr.Left, expr.Right, parent, true);
				else if (expr.Type is FuEnumFlags)
					WriteMethodCall(expr.Left, "symmetricDifference", expr.Right);
				else
					WriteBinaryExpr(expr, parent > FuPriority.Add, FuPriority.Add, " ^ ", FuPriority.Mul);
				break;
			case FuToken.Assign:
			case FuToken.AddAssign:
			case FuToken.SubAssign:
			case FuToken.MulAssign:
			case FuToken.DivAssign:
			case FuToken.ModAssign:
			case FuToken.ShiftLeftAssign:
			case FuToken.ShiftRightAssign:
				WriteSwiftAssign(expr, WriteAssignNested(expr));
				break;
			case FuToken.AndAssign:
				right = WriteAssignNested(expr);
				if (expr.Type.Id == FuId.BoolType) {
					Write("if ");
					if (right is FuPrefixExpr negation && negation.Op == FuToken.ExclamationMark) {
						negation.Inner.Accept(this, FuPriority.Argument);
					}
					else {
						WriteChar('!');
						right.Accept(this, FuPriority.Primary);
					}
					OpenChild();
					expr.Left.Accept(this, FuPriority.Assign);
					WriteLine(" = false");
					this.Indent--;
					WriteChar('}');
				}
				else if (expr.Type is FuEnumFlags)
					WriteEnumFlagsAnd(expr.Left, "formIntersection", "subtract", right);
				else
					WriteSwiftAssign(expr, right);
				break;
			case FuToken.OrAssign:
				right = WriteAssignNested(expr);
				if (expr.Type.Id == FuId.BoolType) {
					Write("if ");
					right.Accept(this, FuPriority.Argument);
					OpenChild();
					expr.Left.Accept(this, FuPriority.Assign);
					WriteLine(" = true");
					this.Indent--;
					WriteChar('}');
				}
				else if (expr.Type is FuEnumFlags)
					WriteMethodCall(expr.Left, "formUnion", right);
				else
					WriteSwiftAssign(expr, right);
				break;
			case FuToken.XorAssign:
				right = WriteAssignNested(expr);
				if (expr.Type.Id == FuId.BoolType) {
					expr.Left.Accept(this, FuPriority.Assign);
					Write(" = ");
					expr.Left.Accept(this, FuPriority.Equality);
					Write(" != ");
					expr.Right.Accept(this, FuPriority.Equality);
				}
				else if (expr.Type is FuEnumFlags)
					WriteMethodCall(expr.Left, "formSymmetricDifference", right);
				else
					WriteSwiftAssign(expr, right);
				break;
			default:
				base.VisitBinaryExpr(expr, parent);
				break;
			}
		}

		protected override void WriteResource(string name, int length)
		{
			Write("FuResource.");
			WriteResourceName(name);
		}

		static bool Throws(FuExpr expr)
		{
			switch (expr) {
			case FuVar _:
			case FuLiteral _:
			case FuLambdaExpr _:
				return false;
			case FuAggregateInitializer init:
				return init.Items.Exists(field => Throws(field));
			case FuInterpolatedString interp:
				return interp.Parts.Exists(part => Throws(part.Argument));
			case FuSymbolReference symbol:
				return symbol.Left != null && Throws(symbol.Left);
			case FuUnaryExpr unary:
				return unary.Inner != null && Throws(unary.Inner);
			case FuBinaryExpr binary:
				return Throws(binary.Left) || Throws(binary.Right);
			case FuSelectExpr select:
				return Throws(select.Cond) || Throws(select.OnTrue) || Throws(select.OnFalse);
			case FuCallExpr call:
				FuMethod method = (FuMethod) call.Method.Symbol;
				return method.Throws || (call.Method.Left != null && Throws(call.Method.Left)) || call.Arguments.Exists(arg => Throws(arg));
			default:
				throw new NotImplementedException();
			}
		}

		protected override void WriteExpr(FuExpr expr, FuPriority parent)
		{
			if (Throws(expr))
				Write("try ");
			base.WriteExpr(expr, parent);
		}

		protected override void WriteCoercedExpr(FuType type, FuExpr expr)
		{
			if (Throws(expr))
				Write("try ");
			base.WriteCoercedExpr(type, expr);
		}

		protected override void StartTemporaryVar(FuType type)
		{
			Write("var ");
		}

		internal override void VisitExpr(FuExpr statement)
		{
			WriteTemporaries(statement);
			if (statement is FuCallExpr call && statement.Type.Id != FuId.VoidType)
				Write("_ = ");
			base.VisitExpr(statement);
		}

		void InitVarsAtIndent()
		{
			while (this.VarsAtIndent.Count <= this.Indent) {
				this.VarsAtIndent.Add(new HashSet<string>());
				this.VarBytesAtIndent.Add(false);
			}
			this.VarsAtIndent[this.Indent].Clear();
			this.VarBytesAtIndent[this.Indent] = false;
		}

		protected override void OpenChild()
		{
			WriteChar(' ');
			OpenBlock();
			InitVarsAtIndent();
		}

		protected override void CloseChild()
		{
			CloseBlock();
		}

		protected override void WriteVar(FuNamedValue def)
		{
			if (def is FuField || AddVar(def.Name)) {
				Write((def.Type is FuArrayStorageType array ? IsArrayRef(array) : def.Type is FuStorageType stg ? stg.Class.TypeParameterCount == 0 && !def.IsAssignableStorage() : def is FuVar local && !local.IsAssigned) ? "let " : "var ");
				base.WriteVar(def);
			}
			else {
				WriteName(def);
				WriteVarInit(def);
			}
		}

		static bool NeedsVarBytes(List<FuStatement> statements)
		{
			int count = 0;
			foreach (FuStatement statement in statements) {
				if (statement is FuCallExpr call && call.Method.Symbol.Id == FuId.UTF8GetBytes) {
					if (++count == 2)
						return true;
				}
			}
			return false;
		}

		protected override void WriteStatements(List<FuStatement> statements)
		{
			this.VarBytesAtIndent[this.Indent] = NeedsVarBytes(statements);
			base.WriteStatements(statements);
		}

		internal override void VisitLambdaExpr(FuLambdaExpr expr)
		{
			Write("{ ");
			WriteName(expr.First);
			Write(" in ");
			expr.Body.Accept(this, FuPriority.Statement);
			Write(" }");
		}

		protected override void WriteAssertCast(FuBinaryExpr expr)
		{
			Write("let ");
			FuVar def = (FuVar) expr.Right;
			WriteCamelCaseNotKeyword(def.Name);
			Write(" = ");
			expr.Left.Accept(this, FuPriority.Equality);
			Write(" as! ");
			WriteLine(def.Type.Name);
		}

		protected override void WriteAssert(FuAssert statement)
		{
			Write("assert(");
			WriteExpr(statement.Cond, FuPriority.Argument);
			if (statement.Message != null) {
				Write(", ");
				WriteExpr(statement.Message, FuPriority.Argument);
			}
			WriteCharLine(')');
		}

		internal override void VisitBreak(FuBreak statement)
		{
			WriteLine("break");
		}

		protected override bool NeedCondXcrement(FuLoop loop) => loop.Cond != null && (!loop.HasBreak || !VisitXcrement(loop.Cond, true, false));

		protected override string GetIfNot() => "if !";

		protected override void WriteContinueDoWhile(FuExpr cond)
		{
			VisitXcrement(cond, false, true);
			WriteLine("continue");
		}

		internal override void VisitDoWhile(FuDoWhile statement)
		{
			if (VisitXcrement(statement.Cond, true, false))
				base.VisitDoWhile(statement);
			else {
				Write("repeat");
				OpenChild();
				statement.Body.AcceptStatement(this);
				if (statement.Body.CompletesNormally())
					VisitXcrement(statement.Cond, false, true);
				CloseChild();
				Write("while ");
				WriteExpr(statement.Cond, FuPriority.Argument);
				WriteNewLine();
			}
		}

		protected override void WriteElseIf()
		{
			Write("else ");
		}

		protected override void OpenWhile(FuLoop loop)
		{
			if (NeedCondXcrement(loop))
				base.OpenWhile(loop);
			else {
				Write("while true");
				OpenChild();
				VisitXcrement(loop.Cond, false, true);
				Write("let fuDoLoop = ");
				loop.Cond.Accept(this, FuPriority.Argument);
				WriteNewLine();
				VisitXcrement(loop.Cond, true, true);
				Write("if !fuDoLoop");
				OpenChild();
				WriteLine("break");
				CloseChild();
			}
		}

		protected override void WriteForRange(FuVar iter, FuBinaryExpr cond, long rangeStep)
		{
			if (rangeStep == 1) {
				WriteExpr(iter.Value, FuPriority.Shift);
				switch (cond.Op) {
				case FuToken.Less:
					Write("..<");
					cond.Right.Accept(this, FuPriority.Shift);
					break;
				case FuToken.LessOrEqual:
					Write("...");
					cond.Right.Accept(this, FuPriority.Shift);
					break;
				default:
					throw new NotImplementedException();
				}
			}
			else {
				Write("stride(from: ");
				WriteExpr(iter.Value, FuPriority.Argument);
				switch (cond.Op) {
				case FuToken.Less:
				case FuToken.Greater:
					Write(", to: ");
					WriteExpr(cond.Right, FuPriority.Argument);
					break;
				case FuToken.LessOrEqual:
				case FuToken.GreaterOrEqual:
					Write(", through: ");
					WriteExpr(cond.Right, FuPriority.Argument);
					break;
				default:
					throw new NotImplementedException();
				}
				Write(", by: ");
				VisitLiteralLong(rangeStep);
				WriteChar(')');
			}
		}

		internal override void VisitForeach(FuForeach statement)
		{
			Write("for ");
			if (statement.Count() == 2) {
				WriteChar('(');
				WriteName(statement.GetVar());
				Write(", ");
				WriteName(statement.GetValueVar());
				WriteChar(')');
			}
			else
				WriteName(statement.GetVar());
			Write(" in ");
			FuClassType klass = (FuClassType) statement.Collection.Type;
			switch (klass.Class.Id) {
			case FuId.StringClass:
				WritePostfix(statement.Collection, ".unicodeScalars");
				break;
			case FuId.SortedSetClass:
				WritePostfix(statement.Collection, ".sorted()");
				break;
			case FuId.SortedDictionaryClass:
				WritePostfix(statement.Collection, klass.GetKeyType().Nullable ? ".sorted(by: { $0.key! < $1.key! })" : ".sorted(by: { $0.key < $1.key })");
				break;
			default:
				WriteExpr(statement.Collection, FuPriority.Argument);
				break;
			}
			WriteChild(statement.Body);
		}

		internal override void VisitLock(FuLock statement)
		{
			statement.Lock.Accept(this, FuPriority.Primary);
			WriteLine(".lock()");
			Write("do");
			OpenChild();
			Write("defer { ");
			statement.Lock.Accept(this, FuPriority.Primary);
			WriteLine(".unlock() }");
			statement.Body.AcceptStatement(this);
			CloseChild();
		}

		protected override void WriteResultVar()
		{
			Write("let result : ");
			WriteType(this.CurrentMethod.Type);
		}

		void WriteSwitchCaseVar(FuVar def)
		{
			if (def.Name == "_")
				Write("is ");
			else {
				Write("let ");
				WriteCamelCaseNotKeyword(def.Name);
				Write(" as ");
			}
			WriteType(def.Type);
		}

		void WriteSwiftSwitchCaseBody(FuSwitch statement, List<FuStatement> body)
		{
			this.Indent++;
			VisitXcrement(statement.Value, true, true);
			InitVarsAtIndent();
			WriteSwitchCaseBody(body);
			this.Indent--;
		}

		internal override void VisitSwitch(FuSwitch statement)
		{
			VisitXcrement(statement.Value, false, true);
			Write("switch ");
			WriteExpr(statement.Value, FuPriority.Argument);
			WriteLine(" {");
			foreach (FuCase kase in statement.Cases) {
				Write("case ");
				for (int i = 0; i < kase.Values.Count; i++) {
					WriteComma(i);
					switch (kase.Values[i]) {
					case FuBinaryExpr when1 when when1.Op == FuToken.When:
						if (when1.Left is FuVar whenVar)
							WriteSwitchCaseVar(whenVar);
						else
							WriteCoerced(statement.Value.Type, when1.Left, FuPriority.Argument);
						Write(" where ");
						WriteExpr(when1.Right, FuPriority.Argument);
						break;
					case FuVar def:
						WriteSwitchCaseVar(def);
						break;
					default:
						WriteCoerced(statement.Value.Type, kase.Values[i], FuPriority.Argument);
						break;
					}
				}
				WriteCharLine(':');
				WriteSwiftSwitchCaseBody(statement, kase.Body);
			}
			if (statement.DefaultBody.Count > 0) {
				WriteLine("default:");
				WriteSwiftSwitchCaseBody(statement, statement.DefaultBody);
			}
			WriteCharLine('}');
		}

		internal override void VisitThrow(FuThrow statement)
		{
			this.Throw = true;
			VisitXcrement(statement.Message, false, true);
			Write("throw FuError.error(");
			WriteExpr(statement.Message, FuPriority.Argument);
			WriteCharLine(')');
		}

		void WriteReadOnlyParameter(FuVar param)
		{
			Write("fuParam");
			WritePascalCase(param.Name);
		}

		protected override void WriteParameter(FuVar param)
		{
			Write("_ ");
			if (param.IsAssigned)
				WriteReadOnlyParameter(param);
			else
				WriteName(param);
			Write(" : ");
			WriteType(param.Type);
		}

		internal override void VisitEnumValue(FuConst konst, FuConst previous)
		{
			WriteDoc(konst.Documentation);
			Write("static let ");
			WriteName(konst);
			Write(" = ");
			Write(konst.Parent.Name);
			WriteChar('(');
			int i = konst.Value.IntValue();
			if (i == 0)
				Write("[]");
			else {
				Write("rawValue: ");
				VisitLiteralLong(i);
			}
			WriteCharLine(')');
		}

		protected override void WriteEnum(FuEnum enu)
		{
			WriteNewLine();
			WriteDoc(enu.Documentation);
			WritePublic(enu);
			if (enu is FuEnumFlags) {
				Write("struct ");
				Write(enu.Name);
				WriteLine(" : OptionSet");
				OpenBlock();
				WriteLine("let rawValue : Int");
				enu.AcceptValues(this);
			}
			else {
				Write("enum ");
				Write(enu.Name);
				if (enu.HasExplicitValue)
					Write(" : Int");
				WriteNewLine();
				OpenBlock();
				Dictionary<int, FuConst> valueToConst = new Dictionary<int, FuConst>();
				for (FuSymbol symbol = enu.First; symbol != null; symbol = symbol.Next) {
					if (symbol is FuConst konst) {
						WriteDoc(konst.Documentation);
						int i = konst.Value.IntValue();
						if (valueToConst.ContainsKey(i)) {
							Write("static let ");
							WriteName(konst);
							Write(" = ");
							WriteName(valueToConst[i]);
						}
						else {
							Write("case ");
							WriteName(konst);
							if (!(konst.Value is FuImplicitEnumValue)) {
								Write(" = ");
								VisitLiteralLong(i);
							}
							valueToConst[i] = konst;
						}
						WriteNewLine();
					}
				}
			}
			CloseBlock();
		}

		void WriteVisibility(FuVisibility visibility)
		{
			switch (visibility) {
			case FuVisibility.Private:
				Write("private ");
				break;
			case FuVisibility.Internal:
				Write("fileprivate ");
				break;
			case FuVisibility.Protected:
			case FuVisibility.Public:
				Write("public ");
				break;
			default:
				throw new NotImplementedException();
			}
		}

		protected override void WriteConst(FuConst konst)
		{
			WriteNewLine();
			WriteDoc(konst.Documentation);
			WriteVisibility(konst.Visibility);
			Write("static let ");
			WriteName(konst);
			Write(" = ");
			if (konst.Type.Id == FuId.IntType || konst.Type is FuEnum || konst.Type.Id == FuId.StringPtrType)
				konst.Value.Accept(this, FuPriority.Argument);
			else {
				WriteType(konst.Type);
				WriteChar('(');
				konst.Value.Accept(this, FuPriority.Argument);
				WriteChar(')');
			}
			WriteNewLine();
		}

		protected override void WriteField(FuField field)
		{
			WriteNewLine();
			WriteDoc(field.Documentation);
			WriteVisibility(field.Visibility);
			if (field.Type is FuClassType klass && klass.Class.Id != FuId.StringClass && !(klass is FuDynamicPtrType) && !(klass is FuStorageType))
				Write("unowned ");
			WriteVar(field);
			if (field.Value == null && (field.Type is FuNumericType || field.Type is FuEnum || field.Type.Id == FuId.StringStorageType)) {
				Write(" = ");
				WriteDefaultValue(field.Type);
			}
			else if (field.IsAssignableStorage()) {
				Write(" = ");
				WriteName(field.Type.AsClassType().Class);
				Write("()");
			}
			WriteNewLine();
		}

		protected override void WriteParameterDoc(FuVar param, bool first)
		{
			Write("/// - parameter ");
			WriteName(param);
			WriteChar(' ');
			WriteDocPara(param.Documentation.Summary, false);
			WriteNewLine();
		}

		protected override void WriteMethod(FuMethod method)
		{
			WriteNewLine();
			WriteDoc(method.Documentation);
			WriteParametersDoc(method);
			switch (method.CallType) {
			case FuCallType.Static:
				WriteVisibility(method.Visibility);
				Write("static ");
				break;
			case FuCallType.Normal:
				WriteVisibility(method.Visibility);
				break;
			case FuCallType.Abstract:
			case FuCallType.Virtual:
				Write(method.Visibility == FuVisibility.Internal ? "fileprivate " : "open ");
				break;
			case FuCallType.Override:
				Write(method.Visibility == FuVisibility.Internal ? "fileprivate " : "open ");
				Write("override ");
				break;
			case FuCallType.Sealed:
				WriteVisibility(method.Visibility);
				Write("final override ");
				break;
			}
			if (method.Id == FuId.ClassToString)
				Write("var description : String");
			else {
				Write("func ");
				WriteName(method);
				WriteParameters(method, true);
				if (method.Throws)
					Write(" throws");
				if (method.Type.Id != FuId.VoidType) {
					Write(" -> ");
					WriteType(method.Type);
				}
			}
			WriteNewLine();
			OpenBlock();
			if (method.CallType == FuCallType.Abstract)
				WriteLine("preconditionFailure(\"Abstract method called\")");
			else {
				for (FuVar param = method.Parameters.FirstParameter(); param != null; param = param.NextParameter()) {
					if (param.IsAssigned) {
						Write("var ");
						WriteTypeAndName(param);
						Write(" = ");
						WriteReadOnlyParameter(param);
						WriteNewLine();
					}
				}
				InitVarsAtIndent();
				this.CurrentMethod = method;
				method.Body.AcceptStatement(this);
				this.CurrentMethod = null;
			}
			CloseBlock();
		}

		protected override void WriteClass(FuClass klass, FuProgram program)
		{
			WriteNewLine();
			WriteDoc(klass.Documentation);
			WritePublic(klass);
			if (klass.CallType == FuCallType.Sealed)
				Write("final ");
			StartClass(klass, "", " : ");
			if (klass.AddsToString()) {
				Write(klass.HasBaseClass() ? ", " : " : ");
				Write("CustomStringConvertible");
			}
			WriteNewLine();
			OpenBlock();
			if (NeedsConstructor(klass)) {
				if (klass.Constructor != null) {
					WriteDoc(klass.Constructor.Documentation);
					WriteVisibility(klass.Constructor.Visibility);
				}
				else
					Write("fileprivate ");
				if (klass.HasBaseClass())
					Write("override ");
				WriteLine("init()");
				OpenBlock();
				InitVarsAtIndent();
				WriteConstructorBody(klass);
				CloseBlock();
			}
			WriteMembers(klass, true);
			CloseBlock();
		}

		void WriteLibrary()
		{
			if (this.Throw) {
				WriteNewLine();
				WriteLine("public enum FuError : Error");
				OpenBlock();
				WriteLine("case error(String)");
				CloseBlock();
			}
			if (this.ArrayRef) {
				WriteNewLine();
				WriteLine("public class ArrayRef<T> : Sequence");
				OpenBlock();
				WriteLine("var array : [T]");
				WriteNewLine();
				WriteLine("init(_ array : [T])");
				OpenBlock();
				WriteLine("self.array = array");
				CloseBlock();
				WriteNewLine();
				WriteLine("init(repeating: T, count: Int)");
				OpenBlock();
				WriteLine("self.array = [T](repeating: repeating, count: count)");
				CloseBlock();
				WriteNewLine();
				WriteLine("init(factory: () -> T, count: Int)");
				OpenBlock();
				WriteLine("self.array = (1...count).map({_ in factory() })");
				CloseBlock();
				WriteNewLine();
				WriteLine("subscript(index: Int) -> T");
				OpenBlock();
				WriteLine("get");
				OpenBlock();
				WriteLine("return array[index]");
				CloseBlock();
				WriteLine("set(value)");
				OpenBlock();
				WriteLine("array[index] = value");
				CloseBlock();
				CloseBlock();
				WriteLine("subscript(bounds: Range<Int>) -> ArraySlice<T>");
				OpenBlock();
				WriteLine("get");
				OpenBlock();
				WriteLine("return array[bounds]");
				CloseBlock();
				WriteLine("set(value)");
				OpenBlock();
				WriteLine("array[bounds] = value");
				CloseBlock();
				CloseBlock();
				WriteNewLine();
				WriteLine("func fill(_ value: T)");
				OpenBlock();
				WriteLine("array = [T](repeating: value, count: array.count)");
				CloseBlock();
				WriteNewLine();
				WriteLine("func fill(_ value: T, _ startIndex : Int, _ count : Int)");
				OpenBlock();
				WriteLine("array[startIndex ..< startIndex + count] = ArraySlice(repeating: value, count: count)");
				CloseBlock();
				WriteNewLine();
				WriteLine("public func makeIterator() -> IndexingIterator<Array<T>>");
				OpenBlock();
				WriteLine("return array.makeIterator()");
				CloseBlock();
				CloseBlock();
			}
			if (this.StringCharAt) {
				WriteNewLine();
				WriteLine("fileprivate func fuStringCharAt(_ s: String, _ offset: Int) -> Int");
				OpenBlock();
				WriteLine("return Int(s.unicodeScalars[s.index(s.startIndex, offsetBy: offset)].value)");
				CloseBlock();
			}
			if (this.StringIndexOf) {
				WriteNewLine();
				WriteLine("fileprivate func fuStringIndexOf<S1 : StringProtocol, S2 : StringProtocol>(_ haystack: S1, _ needle: S2, _ options: String.CompareOptions = .literal) -> Int");
				OpenBlock();
				WriteLine("guard let index = haystack.range(of: needle, options: options) else { return -1 }");
				WriteLine("return haystack.distance(from: haystack.startIndex, to: index.lowerBound)");
				CloseBlock();
			}
			if (this.StringSubstring) {
				WriteNewLine();
				WriteLine("fileprivate func fuStringSubstring(_ s: String, _ offset: Int) -> Substring");
				OpenBlock();
				WriteLine("return s[s.index(s.startIndex, offsetBy: offset)...]");
				CloseBlock();
			}
		}

		void WriteResources(SortedDictionary<string, List<byte>> resources)
		{
			if (resources.Count == 0)
				return;
			this.ArrayRef = true;
			WriteNewLine();
			WriteLine("fileprivate final class FuResource");
			OpenBlock();
			foreach ((string name, List<byte> content) in resources) {
				Write("static let ");
				WriteResourceName(name);
				WriteLine(" = ArrayRef<UInt8>([");
				WriteChar('\t');
				WriteBytes(content);
				WriteLine(" ])");
			}
			CloseBlock();
		}

		public override void WriteProgram(FuProgram program)
		{
			this.System = program.System;
			this.Throw = false;
			this.ArrayRef = false;
			this.StringCharAt = false;
			this.StringIndexOf = false;
			this.StringSubstring = false;
			OpenStringWriter();
			WriteTypes(program);
			CreateOutputFile();
			WriteTopLevelNatives(program);
			WriteIncludes("import ", "");
			CloseStringWriter();
			WriteLibrary();
			WriteResources(program.Resources);
			CloseFile();
		}
	}

	public class GenPy : GenPySwift
	{

		bool ChildPass;

		bool SwitchBreak;

		protected override string GetTargetName() => "Python";

		protected override void WriteBanner()
		{
			WriteLine("# Generated automatically with \"fut\". Do not edit.");
		}

		protected override void StartDocLine()
		{
		}

		protected override string GetDocBullet() => " * ";

		void StartDoc(FuCodeDoc doc)
		{
			Write("\"\"\"");
			WriteDocPara(doc.Summary, false);
			if (doc.Details.Count > 0) {
				WriteNewLine();
				foreach (FuDocBlock block in doc.Details) {
					WriteNewLine();
					WriteDocBlock(block, false);
				}
			}
		}

		protected override void WriteDoc(FuCodeDoc doc)
		{
			if (doc != null) {
				StartDoc(doc);
				WriteLine("\"\"\"");
			}
		}

		protected override void WriteParameterDoc(FuVar param, bool first)
		{
			if (first) {
				WriteNewLine();
				WriteNewLine();
			}
			Write(":param ");
			WriteName(param);
			Write(": ");
			WriteDocPara(param.Documentation.Summary, false);
			WriteNewLine();
		}

		void WritePyDoc(FuMethod method)
		{
			if (method.Documentation == null)
				return;
			StartDoc(method.Documentation);
			WriteParametersDoc(method);
			WriteLine("\"\"\"");
		}

		internal override void VisitLiteralNull()
		{
			Write("None");
		}

		internal override void VisitLiteralFalse()
		{
			Write("False");
		}

		internal override void VisitLiteralTrue()
		{
			Write("True");
		}

		void WriteNameNotKeyword(string name)
		{
			switch (name) {
			case "this":
				Write("self");
				break;
			case "and":
			case "array":
			case "as":
			case "async":
			case "await":
			case "def":
			case "del":
			case "elif":
			case "enum":
			case "except":
			case "finally":
			case "from":
			case "global":
			case "import":
			case "is":
			case "lambda":
			case "len":
			case "math":
			case "nonlocal":
			case "not":
			case "or":
			case "pass":
			case "pyfma":
			case "raise":
			case "re":
			case "sys":
			case "try":
			case "with":
			case "yield":
				Write(name);
				WriteChar('_');
				break;
			default:
				WriteLowercaseWithUnderscores(name);
				break;
			}
		}

		protected override void WriteName(FuSymbol symbol)
		{
			switch (symbol) {
			case FuContainerType container:
				if (!container.IsPublic)
					WriteChar('_');
				Write(symbol.Name);
				break;
			case FuConst konst:
				if (konst.Visibility != FuVisibility.Public)
					WriteChar('_');
				if (konst.InMethod != null) {
					WriteUppercaseWithUnderscores(konst.InMethod.Name);
					WriteChar('_');
				}
				WriteUppercaseWithUnderscores(symbol.Name);
				break;
			case FuVar _:
				WriteNameNotKeyword(symbol.Name);
				break;
			case FuMember member:
				if (member.Id == FuId.ClassToString)
					Write("__str__");
				else if (member.Visibility == FuVisibility.Public)
					WriteNameNotKeyword(symbol.Name);
				else {
					WriteChar('_');
					WriteLowercaseWithUnderscores(symbol.Name);
				}
				break;
			default:
				throw new NotImplementedException();
			}
		}

		protected override void WriteTypeAndName(FuNamedValue value)
		{
			WriteName(value);
		}

		protected override void WriteLocalName(FuSymbol symbol, FuPriority parent)
		{
			if (symbol.Parent is FuForeach forEach && forEach.Collection.Type is FuStringType) {
				Write("ord(");
				WriteNameNotKeyword(symbol.Name);
				WriteChar(')');
			}
			else
				base.WriteLocalName(symbol, parent);
		}

		static int GetArrayCode(FuType type)
		{
			switch (type.Id) {
			case FuId.SByteRange:
				return 'b';
			case FuId.ByteRange:
				return 'B';
			case FuId.ShortRange:
				return 'h';
			case FuId.UShortRange:
				return 'H';
			case FuId.IntType:
				return 'i';
			case FuId.LongType:
				return 'q';
			case FuId.FloatType:
				return 'f';
			case FuId.DoubleType:
				return 'd';
			default:
				throw new NotImplementedException();
			}
		}

		internal override void VisitAggregateInitializer(FuAggregateInitializer expr)
		{
			FuArrayStorageType array = (FuArrayStorageType) expr.Type;
			if (array.GetElementType() is FuNumericType number) {
				int c = GetArrayCode(number);
				if (c == 'B')
					Write("bytes(");
				else {
					Include("array");
					Write("array.array(\"");
					WriteChar(c);
					Write("\", ");
				}
				base.VisitAggregateInitializer(expr);
				WriteChar(')');
			}
			else
				base.VisitAggregateInitializer(expr);
		}

		internal override void VisitInterpolatedString(FuInterpolatedString expr, FuPriority parent)
		{
			Write("f\"");
			foreach (FuInterpolatedPart part in expr.Parts) {
				WriteDoubling(part.Prefix, '{');
				WriteChar('{');
				part.Argument.Accept(this, FuPriority.Argument);
				WritePyFormat(part);
			}
			WriteDoubling(expr.Suffix, '{');
			WriteChar('"');
		}

		internal override void VisitPrefixExpr(FuPrefixExpr expr, FuPriority parent)
		{
			if (expr.Op == FuToken.ExclamationMark) {
				if (parent > FuPriority.CondAnd)
					WriteChar('(');
				Write("not ");
				expr.Inner.Accept(this, FuPriority.Or);
				if (parent > FuPriority.CondAnd)
					WriteChar(')');
			}
			else
				base.VisitPrefixExpr(expr, parent);
		}

		protected override string GetReferenceEqOp(bool not) => not ? " is not " : " is ";

		protected override void WriteCharAt(FuBinaryExpr expr)
		{
			Write("ord(");
			WriteIndexingExpr(expr, FuPriority.Argument);
			WriteChar(')');
		}

		protected override void WriteStringLength(FuExpr expr)
		{
			WriteCall("len", expr);
		}

		internal override void VisitSymbolReference(FuSymbolReference expr, FuPriority parent)
		{
			switch (expr.Symbol.Id) {
			case FuId.ConsoleError:
				Include("sys");
				Write("sys.stderr");
				break;
			case FuId.ListCount:
			case FuId.QueueCount:
			case FuId.StackCount:
			case FuId.HashSetCount:
			case FuId.SortedSetCount:
			case FuId.DictionaryCount:
			case FuId.SortedDictionaryCount:
			case FuId.OrderedDictionaryCount:
				WriteStringLength(expr.Left);
				break;
			case FuId.MathNaN:
				Include("math");
				Write("math.nan");
				break;
			case FuId.MathNegativeInfinity:
				Include("math");
				Write("-math.inf");
				break;
			case FuId.MathPositiveInfinity:
				Include("math");
				Write("math.inf");
				break;
			default:
				if (!WriteJavaMatchProperty(expr, parent))
					base.VisitSymbolReference(expr, parent);
				break;
			}
		}

		internal override void VisitBinaryExpr(FuBinaryExpr expr, FuPriority parent)
		{
			switch (expr.Op) {
			case FuToken.Slash:
				if (expr.Type is FuIntegerType) {
					bool floorDiv;
					if (expr.Left is FuRangeType leftRange && leftRange.Min >= 0 && expr.Right is FuRangeType rightRange && rightRange.Min >= 0) {
						if (parent > FuPriority.Or)
							WriteChar('(');
						floorDiv = true;
					}
					else {
						Write("int(");
						floorDiv = false;
					}
					expr.Left.Accept(this, FuPriority.Mul);
					Write(floorDiv ? " // " : " / ");
					expr.Right.Accept(this, FuPriority.Primary);
					if (!floorDiv || parent > FuPriority.Or)
						WriteChar(')');
				}
				else
					base.VisitBinaryExpr(expr, parent);
				break;
			case FuToken.CondAnd:
				WriteBinaryExpr(expr, parent > FuPriority.CondAnd || parent == FuPriority.CondOr, FuPriority.CondAnd, " and ", FuPriority.CondAnd);
				break;
			case FuToken.CondOr:
				WriteBinaryExpr2(expr, parent, FuPriority.CondOr, " or ");
				break;
			case FuToken.Assign:
				if (this.AtLineStart) {
					for (FuExpr right = expr.Right; right is FuBinaryExpr rightBinary && rightBinary.IsAssign(); right = rightBinary.Right) {
						if (rightBinary.Op != FuToken.Assign) {
							VisitBinaryExpr(rightBinary, FuPriority.Statement);
							WriteNewLine();
							break;
						}
					}
				}
				expr.Left.Accept(this, FuPriority.Assign);
				Write(" = ");
				{
					(expr.Right is FuBinaryExpr rightBinary && rightBinary.IsAssign() && rightBinary.Op != FuToken.Assign ? rightBinary.Left : expr.Right).Accept(this, FuPriority.Assign);
				}
				break;
			case FuToken.AddAssign:
			case FuToken.SubAssign:
			case FuToken.MulAssign:
			case FuToken.DivAssign:
			case FuToken.ModAssign:
			case FuToken.ShiftLeftAssign:
			case FuToken.ShiftRightAssign:
			case FuToken.AndAssign:
			case FuToken.OrAssign:
			case FuToken.XorAssign:
				{
					FuExpr right = expr.Right;
					if (right is FuBinaryExpr rightBinary && rightBinary.IsAssign()) {
						VisitBinaryExpr(rightBinary, FuPriority.Statement);
						WriteNewLine();
						right = rightBinary.Left;
					}
					expr.Left.Accept(this, FuPriority.Assign);
					WriteChar(' ');
					if (expr.Op == FuToken.DivAssign && expr.Type is FuIntegerType)
						WriteChar('/');
					Write(expr.GetOpString());
					WriteChar(' ');
					right.Accept(this, FuPriority.Argument);
				}
				break;
			case FuToken.Is:
				if (expr.Right is FuSymbolReference symbol) {
					Write("isinstance(");
					expr.Left.Accept(this, FuPriority.Argument);
					Write(", ");
					WriteName(symbol.Symbol);
					WriteChar(')');
				}
				else
					NotSupported(expr, "'is' with a variable");
				break;
			default:
				base.VisitBinaryExpr(expr, parent);
				break;
			}
		}

		protected override void WriteCoercedSelect(FuType type, FuSelectExpr expr, FuPriority parent)
		{
			if (parent > FuPriority.Select)
				WriteChar('(');
			WriteCoerced(type, expr.OnTrue, FuPriority.Select);
			Write(" if ");
			expr.Cond.Accept(this, FuPriority.SelectCond);
			Write(" else ");
			WriteCoerced(type, expr.OnFalse, FuPriority.Select);
			if (parent > FuPriority.Select)
				WriteChar(')');
		}

		void WriteDefaultValue(FuType type)
		{
			if (type is FuNumericType)
				WriteChar('0');
			else if (type.Id == FuId.BoolType)
				Write("False");
			else if (type.Id == FuId.StringStorageType)
				Write("\"\"");
			else
				Write("None");
		}

		void WritePyNewArray(FuType elementType, FuExpr value, FuExpr lengthExpr)
		{
			switch (elementType) {
			case FuStorageType _:
				Write("[ ");
				WriteNewStorage(elementType);
				Write(" for _ in range(");
				lengthExpr.Accept(this, FuPriority.Argument);
				Write(") ]");
				break;
			case FuNumericType _:
				int c = GetArrayCode(elementType);
				if (c == 'B' && (value == null || value.IsLiteralZero()))
					WriteCall("bytearray", lengthExpr);
				else {
					Include("array");
					Write("array.array(\"");
					WriteChar(c);
					Write("\", [ ");
					if (value == null)
						WriteChar('0');
					else
						value.Accept(this, FuPriority.Argument);
					Write(" ]) * ");
					lengthExpr.Accept(this, FuPriority.Mul);
				}
				break;
			default:
				Write("[ ");
				if (value == null)
					WriteDefaultValue(elementType);
				else
					value.Accept(this, FuPriority.Argument);
				Write(" ] * ");
				lengthExpr.Accept(this, FuPriority.Mul);
				break;
			}
		}

		protected override void WriteNewArray(FuType elementType, FuExpr lengthExpr, FuPriority parent)
		{
			WritePyNewArray(elementType, null, lengthExpr);
		}

		protected override void WriteArrayStorageInit(FuArrayStorageType array, FuExpr value)
		{
			Write(" = ");
			WritePyNewArray(array.GetElementType(), null, array.LengthExpr);
		}

		protected override void WriteNew(FuReadWriteClassType klass, FuPriority parent)
		{
			switch (klass.Class.Id) {
			case FuId.ListClass:
			case FuId.StackClass:
				if (klass.GetElementType() is FuNumericType number) {
					int c = GetArrayCode(number);
					if (c == 'B')
						Write("bytearray()");
					else {
						Include("array");
						Write("array.array(\"");
						WriteChar(c);
						Write("\")");
					}
				}
				else
					Write("[]");
				break;
			case FuId.QueueClass:
				Include("collections");
				Write("collections.deque()");
				break;
			case FuId.HashSetClass:
			case FuId.SortedSetClass:
				Write("set()");
				break;
			case FuId.DictionaryClass:
			case FuId.SortedDictionaryClass:
				Write("{}");
				break;
			case FuId.OrderedDictionaryClass:
				Include("collections");
				Write("collections.OrderedDict()");
				break;
			case FuId.StringWriterClass:
				Include("io");
				Write("io.StringIO()");
				break;
			case FuId.LockClass:
				Include("threading");
				Write("threading.RLock()");
				break;
			default:
				WriteName(klass.Class);
				Write("()");
				break;
			}
		}

		void WriteContains(FuExpr haystack, FuExpr needle)
		{
			needle.Accept(this, FuPriority.Rel);
			Write(" in ");
			haystack.Accept(this, FuPriority.Rel);
		}

		void WriteSlice(FuExpr startIndex, FuExpr length)
		{
			WriteChar('[');
			startIndex.Accept(this, FuPriority.Argument);
			WriteChar(':');
			if (length != null)
				WriteAdd(startIndex, length);
			WriteChar(']');
		}

		void WriteAssignSorted(FuExpr obj, string byteArray)
		{
			Write(" = ");
			int c = GetArrayCode(obj.Type.AsClassType().GetElementType());
			if (c == 'B') {
				Write(byteArray);
				WriteChar('(');
			}
			else {
				Include("array");
				Write("array.array(\"");
				WriteChar(c);
				Write("\", ");
			}
			Write("sorted(");
		}

		void WriteAllAny(string function, FuExpr obj, List<FuExpr> args)
		{
			Write(function);
			WriteChar('(');
			FuLambdaExpr lambda = (FuLambdaExpr) args[0];
			lambda.Body.Accept(this, FuPriority.Argument);
			Write(" for ");
			WriteName(lambda.First);
			Write(" in ");
			obj.Accept(this, FuPriority.Argument);
			WriteChar(')');
		}

		void WritePyRegexOptions(List<FuExpr> args)
		{
			Include("re");
			WriteRegexOptions(args, ", ", " | ", "", "re.I", "re.M", "re.S");
		}

		void WriteRegexSearch(List<FuExpr> args)
		{
			Write("re.search(");
			args[1].Accept(this, FuPriority.Argument);
			Write(", ");
			args[0].Accept(this, FuPriority.Argument);
			WritePyRegexOptions(args);
			WriteChar(')');
		}

		protected override void WriteCallExpr(FuExpr obj, FuMethod method, List<FuExpr> args, FuPriority parent)
		{
			switch (method.Id) {
			case FuId.EnumFromInt:
				WriteName(method.Type);
				WriteArgsInParentheses(method, args);
				break;
			case FuId.EnumHasFlag:
			case FuId.StringContains:
			case FuId.ArrayContains:
			case FuId.ListContains:
			case FuId.HashSetContains:
			case FuId.SortedSetContains:
			case FuId.DictionaryContainsKey:
			case FuId.SortedDictionaryContainsKey:
			case FuId.OrderedDictionaryContainsKey:
				WriteContains(obj, args[0]);
				break;
			case FuId.StringEndsWith:
				WriteMethodCall(obj, "endswith", args[0]);
				break;
			case FuId.StringIndexOf:
				WriteMethodCall(obj, "find", args[0]);
				break;
			case FuId.StringLastIndexOf:
				WriteMethodCall(obj, "rfind", args[0]);
				break;
			case FuId.StringStartsWith:
				WriteMethodCall(obj, "startswith", args[0]);
				break;
			case FuId.StringSubstring:
				obj.Accept(this, FuPriority.Primary);
				WriteSlice(args[0], args.Count == 2 ? args[1] : null);
				break;
			case FuId.ArrayBinarySearchAll:
				Include("bisect");
				WriteCall("bisect.bisect_left", obj, args[0]);
				break;
			case FuId.ArrayBinarySearchPart:
				Include("bisect");
				Write("bisect.bisect_left(");
				obj.Accept(this, FuPriority.Argument);
				Write(", ");
				args[0].Accept(this, FuPriority.Argument);
				Write(", ");
				args[1].Accept(this, FuPriority.Argument);
				Write(", ");
				args[2].Accept(this, FuPriority.Argument);
				WriteChar(')');
				break;
			case FuId.ArrayCopyTo:
			case FuId.ListCopyTo:
				args[1].Accept(this, FuPriority.Primary);
				WriteSlice(args[2], args[3]);
				Write(" = ");
				obj.Accept(this, FuPriority.Primary);
				WriteSlice(args[0], args[3]);
				break;
			case FuId.ArrayFillAll:
			case FuId.ArrayFillPart:
				obj.Accept(this, FuPriority.Primary);
				if (args.Count == 1) {
					Write("[:] = ");
					FuArrayStorageType array = (FuArrayStorageType) obj.Type;
					WritePyNewArray(array.GetElementType(), args[0], array.LengthExpr);
				}
				else {
					WriteSlice(args[1], args[2]);
					Write(" = ");
					WritePyNewArray(obj.Type.AsClassType().GetElementType(), args[0], args[2]);
				}
				break;
			case FuId.ArraySortAll:
			case FuId.ListSortAll:
				obj.Accept(this, FuPriority.Assign);
				WriteAssignSorted(obj, "bytearray");
				obj.Accept(this, FuPriority.Argument);
				Write("))");
				break;
			case FuId.ArraySortPart:
			case FuId.ListSortPart:
				obj.Accept(this, FuPriority.Primary);
				WriteSlice(args[0], args[1]);
				WriteAssignSorted(obj, "bytes");
				obj.Accept(this, FuPriority.Primary);
				WriteSlice(args[0], args[1]);
				Write("))");
				break;
			case FuId.ListAdd:
				WriteListAdd(obj, "append", args);
				break;
			case FuId.ListAddRange:
				obj.Accept(this, FuPriority.Assign);
				Write(" += ");
				args[0].Accept(this, FuPriority.Argument);
				break;
			case FuId.ListAll:
				WriteAllAny("all", obj, args);
				break;
			case FuId.ListAny:
				WriteAllAny("any", obj, args);
				break;
			case FuId.ListClear:
			case FuId.StackClear:
				if (obj.Type.AsClassType().GetElementType() is FuNumericType number && GetArrayCode(number) != 'B') {
					Write("del ");
					WritePostfix(obj, "[:]");
				}
				else
					WritePostfix(obj, ".clear()");
				break;
			case FuId.ListIndexOf:
				if (parent > FuPriority.Select)
					WriteChar('(');
				WriteMethodCall(obj, "index", args[0]);
				Write(" if ");
				WriteContains(obj, args[0]);
				Write(" else -1");
				if (parent > FuPriority.Select)
					WriteChar(')');
				break;
			case FuId.ListInsert:
				WriteListInsert(obj, "insert", args);
				break;
			case FuId.ListLast:
			case FuId.StackPeek:
				WritePostfix(obj, "[-1]");
				break;
			case FuId.ListRemoveAt:
			case FuId.DictionaryRemove:
			case FuId.SortedDictionaryRemove:
			case FuId.OrderedDictionaryRemove:
				Write("del ");
				WriteIndexing(obj, args[0]);
				break;
			case FuId.ListRemoveRange:
				Write("del ");
				obj.Accept(this, FuPriority.Primary);
				WriteSlice(args[0], args[1]);
				break;
			case FuId.QueueDequeue:
				WritePostfix(obj, ".popleft()");
				break;
			case FuId.QueueEnqueue:
			case FuId.StackPush:
				WriteListAppend(obj, args);
				break;
			case FuId.QueuePeek:
				WritePostfix(obj, "[0]");
				break;
			case FuId.DictionaryAdd:
				WriteDictionaryAdd(obj, args);
				break;
			case FuId.TextWriterWrite:
				Write("print(");
				args[0].Accept(this, FuPriority.Argument);
				Write(", end=\"\", file=");
				obj.Accept(this, FuPriority.Argument);
				WriteChar(')');
				break;
			case FuId.TextWriterWriteChar:
			case FuId.TextWriterWriteCodePoint:
				WriteMethodCall(obj, "write(chr", args[0]);
				WriteChar(')');
				break;
			case FuId.TextWriterWriteLine:
				Write("print(");
				if (args.Count == 1) {
					args[0].Accept(this, FuPriority.Argument);
					Write(", ");
				}
				Write("file=");
				obj.Accept(this, FuPriority.Argument);
				WriteChar(')');
				break;
			case FuId.ConsoleWrite:
				Write("print(");
				args[0].Accept(this, FuPriority.Argument);
				Write(", end=\"\")");
				break;
			case FuId.ConsoleWriteLine:
				Write("print(");
				if (args.Count == 1)
					args[0].Accept(this, FuPriority.Argument);
				WriteChar(')');
				break;
			case FuId.StringWriterClear:
				WritePostfix(obj, ".seek(0)");
				WriteNewLine();
				WritePostfix(obj, ".truncate(0)");
				break;
			case FuId.StringWriterToString:
				WritePostfix(obj, ".getvalue()");
				break;
			case FuId.UTF8GetByteCount:
				Write("len(");
				WritePostfix(args[0], ".encode(\"utf8\"))");
				break;
			case FuId.UTF8GetBytes:
				Write("fubytes = ");
				args[0].Accept(this, FuPriority.Primary);
				WriteLine(".encode(\"utf8\")");
				args[1].Accept(this, FuPriority.Primary);
				WriteChar('[');
				args[2].Accept(this, FuPriority.Argument);
				WriteChar(':');
				StartAdd(args[2]);
				WriteLine("len(fubytes)] = fubytes");
				break;
			case FuId.UTF8GetString:
				args[0].Accept(this, FuPriority.Primary);
				WriteSlice(args[1], args[2]);
				Write(".decode(\"utf8\")");
				break;
			case FuId.EnvironmentGetEnvironmentVariable:
				Include("os");
				WriteCall("os.getenv", args[0]);
				break;
			case FuId.RegexCompile:
				Write("re.compile(");
				args[0].Accept(this, FuPriority.Argument);
				WritePyRegexOptions(args);
				WriteChar(')');
				break;
			case FuId.RegexEscape:
				Include("re");
				WriteCall("re.escape", args[0]);
				break;
			case FuId.RegexIsMatchStr:
				if (parent > FuPriority.Equality)
					WriteChar('(');
				WriteRegexSearch(args);
				Write(" is not None");
				if (parent > FuPriority.Equality)
					WriteChar(')');
				break;
			case FuId.RegexIsMatchRegex:
				if (parent > FuPriority.Equality)
					WriteChar('(');
				WriteMethodCall(obj, "search", args[0]);
				Write(" is not None");
				if (parent > FuPriority.Equality)
					WriteChar(')');
				break;
			case FuId.MatchFindStr:
			case FuId.MatchFindRegex:
				if (parent > FuPriority.Equality)
					WriteChar('(');
				obj.Accept(this, FuPriority.Equality);
				Write(" is not None");
				if (parent > FuPriority.Equality)
					WriteChar(')');
				break;
			case FuId.MatchGetCapture:
				WriteMethodCall(obj, "group", args[0]);
				break;
			case FuId.MathMethod:
			case FuId.MathIsFinite:
			case FuId.MathIsNaN:
			case FuId.MathLog2:
				Include("math");
				Write("math.");
				WriteLowercase(method.Name);
				WriteArgsInParentheses(method, args);
				break;
			case FuId.MathAbs:
				WriteCall("abs", args[0]);
				break;
			case FuId.MathCeiling:
				Include("math");
				WriteCall("math.ceil", args[0]);
				break;
			case FuId.MathClamp:
				Write("min(max(");
				WriteClampAsMinMax(args);
				break;
			case FuId.MathFusedMultiplyAdd:
				Include("pyfma");
				WriteCall("pyfma.fma", args[0], args[1], args[2]);
				break;
			case FuId.MathIsInfinity:
				Include("math");
				WriteCall("math.isinf", args[0]);
				break;
			case FuId.MathMaxInt:
			case FuId.MathMaxDouble:
				WriteCall("max", args[0], args[1]);
				break;
			case FuId.MathMinInt:
			case FuId.MathMinDouble:
				WriteCall("min", args[0], args[1]);
				break;
			case FuId.MathRound:
				WriteCall("round", args[0]);
				break;
			case FuId.MathTruncate:
				Include("math");
				WriteCall("math.trunc", args[0]);
				break;
			default:
				if (obj == null)
					WriteLocalName(method, FuPriority.Primary);
				else if (IsReferenceTo(obj, FuId.BasePtr)) {
					WriteName(method.Parent);
					WriteChar('.');
					WriteName(method);
					Write("(self");
					if (args.Count > 0) {
						Write(", ");
						WriteArgs(method, args);
					}
					WriteChar(')');
					break;
				}
				else {
					obj.Accept(this, FuPriority.Primary);
					WriteChar('.');
					WriteName(method);
				}
				WriteArgsInParentheses(method, args);
				break;
			}
		}

		protected override void WriteResource(string name, int length)
		{
			Write("_FuResource.");
			WriteResourceName(name);
		}

		protected override bool VisitPreCall(FuCallExpr call)
		{
			switch (call.Method.Symbol.Id) {
			case FuId.MatchFindStr:
				call.Method.Left.Accept(this, FuPriority.Assign);
				Write(" = ");
				WriteRegexSearch(call.Arguments);
				WriteNewLine();
				return true;
			case FuId.MatchFindRegex:
				call.Method.Left.Accept(this, FuPriority.Assign);
				Write(" = ");
				WriteMethodCall(call.Arguments[1], "search", call.Arguments[0]);
				WriteNewLine();
				return true;
			default:
				return false;
			}
		}

		protected override void StartTemporaryVar(FuType type)
		{
		}

		protected override bool HasInitCode(FuNamedValue def) => (def.Value != null || def.Type.IsFinal()) && !def.IsAssignableStorage();

		internal override void VisitExpr(FuExpr statement)
		{
			if (!(statement is FuVar def) || HasInitCode(def)) {
				WriteTemporaries(statement);
				base.VisitExpr(statement);
			}
		}

		protected override void StartLine()
		{
			base.StartLine();
			this.ChildPass = false;
		}

		protected override void OpenChild()
		{
			WriteCharLine(':');
			this.Indent++;
			this.ChildPass = true;
		}

		protected override void CloseChild()
		{
			if (this.ChildPass)
				WriteLine("pass");
			this.Indent--;
		}

		internal override void VisitLambdaExpr(FuLambdaExpr expr)
		{
			throw new NotImplementedException();
		}

		protected override void WriteAssertCast(FuBinaryExpr expr)
		{
			FuVar def = (FuVar) expr.Right;
			Write(def.Name);
			Write(" = ");
			expr.Left.Accept(this, FuPriority.Argument);
			WriteNewLine();
		}

		protected override void WriteAssert(FuAssert statement)
		{
			Write("assert ");
			statement.Cond.Accept(this, FuPriority.Argument);
			if (statement.Message != null) {
				Write(", ");
				statement.Message.Accept(this, FuPriority.Argument);
			}
			WriteNewLine();
		}

		internal override void VisitBreak(FuBreak statement)
		{
			WriteLine(statement.LoopOrSwitch is FuSwitch ? "raise _CiBreak()" : "break");
		}

		protected override string GetIfNot() => "if not ";

		void WriteInclusiveLimit(FuExpr limit, int increment, string incrementString)
		{
			if (limit is FuLiteralLong literal)
				VisitLiteralLong(literal.Value + increment);
			else {
				limit.Accept(this, FuPriority.Add);
				Write(incrementString);
			}
		}

		protected override void WriteForRange(FuVar iter, FuBinaryExpr cond, long rangeStep)
		{
			Write("range(");
			if (rangeStep != 1 || !iter.Value.IsLiteralZero()) {
				iter.Value.Accept(this, FuPriority.Argument);
				Write(", ");
			}
			switch (cond.Op) {
			case FuToken.Less:
			case FuToken.Greater:
				cond.Right.Accept(this, FuPriority.Argument);
				break;
			case FuToken.LessOrEqual:
				WriteInclusiveLimit(cond.Right, 1, " + 1");
				break;
			case FuToken.GreaterOrEqual:
				WriteInclusiveLimit(cond.Right, -1, " - 1");
				break;
			default:
				throw new NotImplementedException();
			}
			if (rangeStep != 1) {
				Write(", ");
				VisitLiteralLong(rangeStep);
			}
			WriteChar(')');
		}

		internal override void VisitForeach(FuForeach statement)
		{
			Write("for ");
			WriteName(statement.GetVar());
			FuClassType klass = (FuClassType) statement.Collection.Type;
			if (klass.Class.TypeParameterCount == 2) {
				Write(", ");
				WriteName(statement.GetValueVar());
				Write(" in ");
				if (klass.Class.Id == FuId.SortedDictionaryClass) {
					Write("sorted(");
					WritePostfix(statement.Collection, ".items())");
				}
				else
					WritePostfix(statement.Collection, ".items()");
			}
			else {
				Write(" in ");
				if (klass.Class.Id == FuId.SortedSetClass)
					WriteCall("sorted", statement.Collection);
				else
					statement.Collection.Accept(this, FuPriority.Argument);
			}
			WriteChild(statement.Body);
		}

		protected override void WriteElseIf()
		{
			Write("el");
		}

		internal override void VisitLock(FuLock statement)
		{
			VisitXcrement(statement.Lock, false, true);
			Write("with ");
			statement.Lock.Accept(this, FuPriority.Argument);
			OpenChild();
			VisitXcrement(statement.Lock, true, true);
			statement.Body.AcceptStatement(this);
			CloseChild();
		}

		protected override void WriteResultVar()
		{
			Write("result");
		}

		void WriteSwitchCaseVar(FuVar def)
		{
			WriteName(def.Type.AsClassType().Class);
			Write("()");
			if (def.Name != "_") {
				Write(" as ");
				WriteNameNotKeyword(def.Name);
			}
		}

		void WritePyCaseBody(FuSwitch statement, List<FuStatement> body)
		{
			OpenChild();
			VisitXcrement(statement.Value, true, true);
			WriteFirstStatements(body, FuSwitch.LengthWithoutTrailingBreak(body));
			CloseChild();
		}

		internal override void VisitSwitch(FuSwitch statement)
		{
			bool earlyBreak = statement.Cases.Exists(kase => FuSwitch.HasEarlyBreak(kase.Body)) || FuSwitch.HasEarlyBreak(statement.DefaultBody);
			if (earlyBreak) {
				this.SwitchBreak = true;
				Write("try");
				OpenChild();
			}
			VisitXcrement(statement.Value, false, true);
			Write("match ");
			statement.Value.Accept(this, FuPriority.Argument);
			OpenChild();
			foreach (FuCase kase in statement.Cases) {
				string op = "case ";
				foreach (FuExpr caseValue in kase.Values) {
					Write(op);
					switch (caseValue) {
					case FuVar def:
						WriteSwitchCaseVar(def);
						break;
					case FuBinaryExpr when1:
						if (when1.Left is FuVar whenVar)
							WriteSwitchCaseVar(whenVar);
						else
							when1.Left.Accept(this, FuPriority.Argument);
						Write(" if ");
						when1.Right.Accept(this, FuPriority.Argument);
						break;
					default:
						caseValue.Accept(this, FuPriority.Or);
						break;
					}
					op = " | ";
				}
				WritePyCaseBody(statement, kase.Body);
			}
			if (statement.HasDefault()) {
				Write("case _");
				WritePyCaseBody(statement, statement.DefaultBody);
			}
			CloseChild();
			if (earlyBreak) {
				CloseChild();
				Write("except _CiBreak");
				OpenChild();
				CloseChild();
			}
		}

		internal override void VisitThrow(FuThrow statement)
		{
			VisitXcrement(statement.Message, false, true);
			Write("raise Exception(");
			statement.Message.Accept(this, FuPriority.Argument);
			WriteCharLine(')');
		}

		internal override void VisitEnumValue(FuConst konst, FuConst previous)
		{
			WriteUppercaseWithUnderscores(konst.Name);
			Write(" = ");
			VisitLiteralLong(konst.Value.IntValue());
			WriteNewLine();
			WriteDoc(konst.Documentation);
		}

		protected override void WriteEnum(FuEnum enu)
		{
			Include("enum");
			WriteNewLine();
			Write("class ");
			WriteName(enu);
			Write(enu is FuEnumFlags ? "(enum.Flag)" : "(enum.Enum)");
			OpenChild();
			WriteDoc(enu.Documentation);
			enu.AcceptValues(this);
			CloseChild();
		}

		protected override void WriteConst(FuConst konst)
		{
			if (konst.Visibility != FuVisibility.Private || konst.Type is FuArrayStorageType) {
				WriteNewLine();
				WriteName(konst);
				Write(" = ");
				konst.Value.Accept(this, FuPriority.Argument);
				WriteNewLine();
				WriteDoc(konst.Documentation);
			}
		}

		protected override void WriteField(FuField field)
		{
		}

		protected override void WriteMethod(FuMethod method)
		{
			if (method.CallType == FuCallType.Abstract)
				return;
			WriteNewLine();
			if (method.CallType == FuCallType.Static)
				WriteLine("@staticmethod");
			Write("def ");
			WriteName(method);
			if (method.CallType == FuCallType.Static)
				WriteParameters(method, true);
			else {
				Write("(self");
				WriteRemainingParameters(method, false, true);
			}
			this.CurrentMethod = method;
			OpenChild();
			WritePyDoc(method);
			method.Body.AcceptStatement(this);
			CloseChild();
			this.CurrentMethod = null;
		}

		bool InheritsConstructor(FuClass klass)
		{
			while (klass.Parent is FuClass baseClass) {
				if (NeedsConstructor(baseClass))
					return true;
				klass = baseClass;
			}
			return false;
		}

		protected override void WriteInitField(FuField field)
		{
			if (HasInitCode(field)) {
				Write("self.");
				WriteVar(field);
				WriteNewLine();
				WriteInitCode(field);
			}
		}

		protected override void WriteClass(FuClass klass, FuProgram program)
		{
			if (!WriteBaseClass(klass, program))
				return;
			WriteNewLine();
			Write("class ");
			WriteName(klass);
			if (klass.Parent is FuClass baseClass) {
				WriteChar('(');
				WriteName(baseClass);
				WriteChar(')');
			}
			OpenChild();
			WriteDoc(klass.Documentation);
			if (NeedsConstructor(klass)) {
				WriteNewLine();
				Write("def __init__(self)");
				OpenChild();
				if (klass.Constructor != null)
					WriteDoc(klass.Constructor.Documentation);
				if (InheritsConstructor(klass)) {
					WriteName(klass.Parent);
					WriteLine(".__init__(self)");
				}
				WriteConstructorBody(klass);
				CloseChild();
			}
			WriteMembers(klass, true);
			CloseChild();
		}

		void WriteResourceByte(int b)
		{
			Write($"\\x{b:x2}");
		}

		void WriteResources(SortedDictionary<string, List<byte>> resources)
		{
			if (resources.Count == 0)
				return;
			WriteNewLine();
			Write("class _FuResource");
			OpenChild();
			foreach ((string name, List<byte> content) in resources) {
				WriteResourceName(name);
				WriteLine(" = (");
				this.Indent++;
				Write("b\"");
				int i = 0;
				foreach (int b in content) {
					if (i > 0 && (i & 15) == 0) {
						WriteCharLine('"');
						Write("b\"");
					}
					WriteResourceByte(b);
					i++;
				}
				WriteLine("\" )");
				this.Indent--;
			}
			CloseChild();
		}

		public override void WriteProgram(FuProgram program)
		{
			this.SwitchBreak = false;
			OpenStringWriter();
			WriteTypes(program);
			CreateOutputFile();
			WriteTopLevelNatives(program);
			WriteIncludes("import ", "");
			if (this.SwitchBreak) {
				WriteNewLine();
				WriteLine("class _CiBreak(Exception): pass");
			}
			CloseStringWriter();
			WriteResources(program.Resources);
			CloseFile();
		}
	}
}
