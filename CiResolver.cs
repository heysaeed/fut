// CiResolver.cs - Ci symbol resolver
//
// Copyright (C) 2011-2019  Piotr Fusik
//
// This file is part of CiTo, see https://github.com/pfusik/cito
//
// CiTo is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// CiTo is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with CiTo.  If not, see http://www.gnu.org/licenses/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Foxoft.Ci
{

public class CiResolver : CiVisitor
{
	readonly CiProgram Program;
	readonly IEnumerable<string> SearchDirs;
	CiScope CurrentScope;
	CiMethodBase CurrentMethod;
	readonly HashSet<CiMethod> CurrentPureMethods = new HashSet<CiMethod>();
	readonly Dictionary<CiVar, CiExpr> CurrentPureArguments = new Dictionary<CiVar, CiExpr>();

	CiException StatementException(CiStatement statement, string message)
	{
		return new CiException(this.CurrentScope.Filename, statement.Line, message);
	}

	CiException StatementException(CiStatement statement, string format, params object[] args)
	{
		return StatementException(statement, string.Format(format, args));
	}

	string FindFile(string name, CiStatement statement)
	{
		foreach (string dir in this.SearchDirs) {
			string path = Path.Combine(dir, name);
			if (File.Exists(path))
				return path;
		}
		if (File.Exists(name))
			return name;
		throw StatementException(statement, "File {0} not found", name);
	}

	void ResolveBase(CiClass klass)
	{
		switch (klass.VisitStatus) {
		case CiVisitStatus.NotYet:
			break;
		case CiVisitStatus.InProgress:
			throw new CiException(klass, "Circular inheritance for class {0}", klass.Name);
		case CiVisitStatus.Done:
			return;
		}
		if (klass.BaseClassName != null) {
			if (!(Program.TryLookup(klass.BaseClassName) is CiClass baseClass))
				throw new CiException(klass, "Base class {0} not found", klass.BaseClassName);
			if (klass.IsPublic && !baseClass.IsPublic)
				throw new CiException(klass, "Public class cannot derive from an internal class");
			klass.Parent = baseClass;
			klass.VisitStatus = CiVisitStatus.InProgress;
			ResolveBase(baseClass);
		}
		this.Program.Classes.Add(klass);
		klass.VisitStatus = CiVisitStatus.Done;

		klass.AddRange(klass.Consts);
		klass.AddRange(klass.Fields);
		klass.AddRange(klass.Methods);
	}

	void Coerce(CiExpr expr, CiType type)
	{
		if (!type.IsAssignableFrom(expr.Type))
			throw StatementException(expr, "Cannot coerce {0} to {1}", expr.Type, type);
	}

	CiType GetCommonType(CiExpr left, CiExpr right)
	{
		if (left.Type is CiRangeType leftRange && right.Type is CiRangeType rightRange)
			return leftRange.Union(rightRange);
		CiType ptr = left.Type.PtrOrSelf;
		if (ptr.IsAssignableFrom(right.Type))
			return ptr;
		ptr = right.Type.PtrOrSelf;
		if (ptr.IsAssignableFrom(left.Type))
			return ptr;
		throw StatementException(left, "Incompatible types: {0} and {1}", left.Type, right.Type);
	}

	CiIntegerType GetIntegerType(CiExpr left, CiExpr right)
	{
		CiIntegerType type = left.Type == CiSystem.LongType || right.Type == CiSystem.LongType ? CiSystem.LongType : CiSystem.IntType;
		Coerce(left, type);
		Coerce(right, type);
		return type;
	}

	CiIntegerType GetShiftType(CiExpr left, CiExpr right)
	{
		Coerce(right, CiSystem.IntType);
		if (left.Type == CiSystem.LongType)
			return CiSystem.LongType;
		Coerce(left, CiSystem.IntType);
		return CiSystem.IntType;
	}

	CiType GetNumericType(CiExpr left, CiExpr right)
	{
		if (left.Type == CiSystem.DoubleType) {
			Coerce(right, CiSystem.DoubleType);
			return CiSystem.DoubleType;
		}
		if (right.Type == CiSystem.DoubleType) {
			Coerce(left, CiSystem.DoubleType);
			return CiSystem.DoubleType;
		}
		if (left.Type == CiSystem.FloatType) {
			Coerce(right, CiSystem.FloatType);
			return CiSystem.FloatType;
		}
		if (right.Type == CiSystem.FloatType) {
			Coerce(left, CiSystem.FloatType);
			return CiSystem.FloatType;
		}
		return GetIntegerType(left, right);
	}

	static int SaturatedNeg(int a)
	{
		if (a == int.MinValue)
			return int.MaxValue;
		return -a;
	}

	static int SaturatedAdd(int a, int b)
	{
		int c = a + b;
		if (c >= 0) {
			if (a < 0 && b < 0)
				return int.MinValue;
		}
		else if (a > 0 && b > 0)
			return int.MaxValue;
		return c;
	}

	static int SaturatedSub(int a, int b)
	{
		if (b == int.MinValue)
			return a < 0 ? a ^ b : int.MaxValue;
		return SaturatedAdd(a, -b);
	}

	static int SaturatedMul(int a, int b)
	{
		if (a == 0 || b == 0)
			return 0;
		if (a == int.MinValue)
			return b >> 31 ^ a;
		if (b == int.MinValue)
			return a >> 31 ^ b;
		if (int.MaxValue / Math.Abs(a) < Math.Abs(b))
			return (a ^ b) >> 31 ^ int.MaxValue;
		return a * b;
	}

	static int SaturatedDiv(int a, int b)
	{
		if (a == int.MinValue && b == -1)
			return int.MaxValue;
		return a / b;
	}

	static int SaturatedShiftRight(int a, int b)
	{
		return a >> (b >= 31 || b < 0 ? 31 : b);
	}

	static CiRangeType UnsignedAnd(CiRangeType left, CiRangeType right)
	{
		int leftVariableBits = left.VariableBits;
		int rightVariableBits = right.VariableBits;
		int min = left.Min & right.Min & ~CiRangeType.GetMask(~left.Min & ~right.Min & (leftVariableBits | rightVariableBits));
		// Calculate upper bound with variable bits set
		int max = (left.Max | leftVariableBits) & (right.Max | rightVariableBits);
		// The upper bound will never exceed the input
		if (max > left.Max)
			max = left.Max;
		if (max > right.Max)
			max = right.Max;
		if (min > max)
			return new CiRangeType(max, min); // FIXME: this is wrong! e.g. min=0 max=0x8000000_00000000 then 5 should be in range
		return new CiRangeType(min, max);
	}

	static CiRangeType UnsignedOr(CiRangeType left, CiRangeType right)
	{
		int leftVariableBits = left.VariableBits;
		int rightVariableBits = right.VariableBits;
		int min = (left.Min & ~leftVariableBits) | (right.Min & ~rightVariableBits);
		int max = left.Max | right.Max | CiRangeType.GetMask(left.Max & right.Max & CiRangeType.GetMask(leftVariableBits | rightVariableBits));
		// The lower bound will never be less than the input
		if (min < left.Min)
			min = left.Min;
		if (min < right.Min)
			min = right.Min;
		if (min > max)
			return new CiRangeType(max, min); // FIXME: this is wrong! e.g. min=0 max=0x8000000_00000000 then 5 should be in range
		return new CiRangeType(min, max);
	}

	static CiRangeType UnsignedXor(CiRangeType left, CiRangeType right)
	{
		int variableBits = left.VariableBits | right.VariableBits;
		int min = (left.Min ^ right.Min) & ~variableBits;
		int max = (left.Max ^ right.Max) | variableBits;
		if (min > max)
			return new CiRangeType(max, min); // FIXME: this is wrong! e.g. min=0 max=0x8000000_00000000 then 5 should be in range
		return new CiRangeType(min, max);
	}

	delegate CiRangeType UnsignedOp(CiRangeType left, CiRangeType right);

	CiType BitwiseOp(CiExpr left, CiExpr right, UnsignedOp op)
	{
		if (left.Type is CiRangeType leftRange && right.Type is CiRangeType rightRange) {
			leftRange.SplitBySign(out CiRangeType leftNegative, out CiRangeType leftPositive);
			rightRange.SplitBySign(out CiRangeType rightNegative, out CiRangeType rightPositive);
			CiRangeType range = null;
			if (leftNegative != null) {
				if (rightNegative != null)
					range = op(leftNegative, rightNegative);
				if (rightPositive != null)
					range = op(leftNegative, rightPositive).Union(range);
			}
			if (leftPositive != null) {
				if (rightNegative != null)
					range = op(leftPositive, rightNegative).Union(range);
				if (rightPositive != null)
					range = op(leftPositive, rightPositive).Union(range);
			}
			return range;
		}
		return GetIntegerType(left, right);
	}

	public override CiExpr Visit(CiCollection expr, CiPriority parent)
	{
		CiExpr[] items = expr.Items;
		for (int i = 0; i < items.Length; i++)
			items[i] = Resolve(items[i]);
		return expr;
	}

	public override CiExpr Visit(CiVar expr, CiPriority parent)
	{
		CiType type = ResolveType(expr);
		if (expr.Value != null) {
			expr.Value = Resolve(expr.Value);
			if (type is CiArrayStorageType array)
				type = array.ElementType;
			Coerce(expr.Value, type);
		}
		this.CurrentScope.Add(expr);
		return expr;
	}

	public override CiExpr Visit(CiLiteral expr, CiPriority parent)
	{
		return expr;
	}

	public override CiExpr Visit(CiInterpolatedString expr, CiPriority parent)
	{
		foreach (CiInterpolatedPart part in expr.Parts) {
			if (part.Argument != null) {
				CiExpr arg = Resolve(part.Argument);
				if (arg.Type is CiNumericType || arg.Type is CiStringType)
					part.Argument = arg;
				else
					throw StatementException(arg, "Only numbers and strings can be interpolated in strings");
				if (part.WidthExpr != null)
					part.Width = FoldConstInt(part.WidthExpr);
			}
		}
		return expr;
	}

	CiExpr Lookup(CiSymbolReference expr, CiScope scope)
	{
		if (expr.Symbol == null) {
			expr.Symbol = scope.TryLookup(expr.Name);
			if (expr.Symbol == null)
				throw StatementException(expr, "{0} not found", expr.Name);
			expr.Type = expr.Symbol.Type;
		}
		if (!(scope is CiEnum) && expr.Symbol is CiConst konst) {
			ResolveConst(konst);
			if (konst.Value is CiLiteral)
				return konst.Value;
			if (konst.Value is CiBinaryExpr dotExpr && dotExpr.Op == CiToken.Dot)
				return dotExpr; // const foo = MyEnum.Foo
		}
		return expr;
	}

	public override CiExpr Visit(CiSymbolReference expr, CiPriority parent)
	{
		CiExpr resolved = Lookup(expr, this.CurrentScope);
		if (resolved is CiSymbolReference symbol
		 && symbol.Symbol is CiVar v
		 && this.CurrentPureArguments.TryGetValue(v, out CiExpr arg))
			return arg;
		return resolved;
	}

	public override CiExpr Visit(CiPrefixExpr expr, CiPriority parent)
	{
		CiExpr inner;
		CiType type;
		CiRangeType range;
		switch (expr.Op) {
		case CiToken.Increment:
		case CiToken.Decrement:
			inner = Resolve(expr.Inner);
			if (!(inner.Type is CiNumericType))
				throw StatementException(expr, "Argument of ++/-- must be numeric");
			range = inner.Type as CiRangeType;
			// TODO: check lvalue
			if (range != null) {
				int delta = expr.Op == CiToken.Increment ? 1 : -1;
				type = new CiRangeType(range.Min + delta, range.Max + delta);
			}
			else
				type = inner.Type;
			expr.Inner = inner;
			expr.Type = type;
			return expr;
		case CiToken.Minus:
			inner = Resolve(expr.Inner);
			if (!(inner.Type is CiNumericType))
				throw StatementException(expr, "Argument of unary minus must be numeric");
			range = inner.Type as CiRangeType;
			if (range != null)
				type = range = new CiRangeType(SaturatedNeg(range.Max), SaturatedNeg(range.Min));
			else
				type = inner.Type;
			break;
		case CiToken.Tilde:
			inner = Resolve(expr.Inner);
			if (!(inner.Type is CiIntegerType))
				throw StatementException(expr, "Argument of bitwise complement must be integer");
			range = inner.Type as CiRangeType;
			if (range != null)
				type = range = new CiRangeType(~range.Max, ~range.Min);
			else
				type = inner.Type;
			break;
		case CiToken.ExclamationMark:
			inner = ResolveBool(expr.Inner);
			return new CiPrefixExpr { Line = expr.Line, Op = CiToken.ExclamationMark, Inner = inner, Type = CiSystem.BoolType };
		case CiToken.New:
			type = ToType(expr.Inner, true);
			switch (type) {
			case CiClass klass:
				expr.Type = new CiClassPtrType { Class = klass, Modifier = CiToken.Hash };
				return expr;
			case CiArrayStorageType array:
				expr.Type = new CiArrayPtrType { ElementType = array.ElementType, Modifier = CiToken.Hash };
				expr.Inner = array.LengthExpr;
				return expr;
			default:
				throw StatementException(expr, "Invalid argument to new");
			}
		case CiToken.List:
			throw StatementException(expr, "Unexpected List");
		case CiToken.Resource:
			CiLiteral literal = FoldConst(expr.Inner);
			if (!(literal.Value is string name))
				throw StatementException(expr, "Resource name must be string");
			inner = literal;
			if (!this.Program.Resources.TryGetValue(name, out byte[] content)) {
				content = File.ReadAllBytes(FindFile(name, expr));
				this.Program.Resources.Add(name, content);
			}
			type = new CiArrayStorageType { ElementType = CiSystem.ByteType, Length = content.Length };
			range = null;
			break;
		default:
			throw new NotImplementedException(expr.Op.ToString());
		}
		if (range != null && range.Min == range.Max)
			return expr.ToLiteral((long) range.Min);
		return new CiPrefixExpr { Line = expr.Line, Op = expr.Op, Inner = inner, Type = type };
	}

	public override CiExpr Visit(CiPostfixExpr expr, CiPriority parent)
	{
		expr.Inner = Resolve(expr.Inner);
		switch (expr.Op) {
		case CiToken.Increment:
		case CiToken.Decrement:
			if (!(expr.Inner.Type is CiNumericType))
				throw StatementException(expr, "Argument of ++/-- must be numeric");
			expr.Type = expr.Inner.Type;
			// TODO: check lvalue
			return expr;
		case CiToken.ExclamationMark:
			throw StatementException(expr, "Unexpected '!'");
		case CiToken.Hash:
			throw StatementException(expr, "Unexpected '#'");
		default:
			throw new NotImplementedException(expr.Op.ToString());
		}
	}

	void CheckComparison(CiExpr left, CiExpr right)
	{
		if (!(left.Type is CiNumericType) || !(right.Type is CiNumericType))
			throw StatementException(left, "Arguments of comparison must be numeric");
	}

	public override CiExpr Visit(CiBinaryExpr expr, CiPriority parent)
	{
		CiExpr left = Resolve(expr.Left);
		CiSymbolReference leftSymbol;
		CiType type;
		switch (expr.Op) {
		case CiToken.Dot:
			leftSymbol = left as CiSymbolReference;
			CiSymbolReference rightSymbol = (CiSymbolReference) expr.Right;
			if (leftSymbol == null || !(leftSymbol.Symbol is CiScope scope))
				scope = left.Type;
			CiExpr result = Lookup(rightSymbol, scope);
			if (result != rightSymbol)
				return result;
			if (rightSymbol.Symbol == CiSystem.ArrayLength) {
				if (scope is CiArrayStorageType array)
					return expr.ToLiteral((long) array.Length);
				throw new NotImplementedException(scope.GetType().Name);
			}
			if (rightSymbol.Symbol == CiSystem.StringLength && left is CiLiteral leftLiteral)
				return expr.ToLiteral((long) ((string) leftLiteral.Value).Length);
			return new CiBinaryExpr { Line = expr.Line, Left = left, Op = expr.Op, Right = rightSymbol, Type = result.Type };

		case CiToken.LeftParenthesis:
			leftSymbol = left as CiSymbolReference;
			if (leftSymbol == null) {
				if (!(left is CiBinaryExpr dotExpr) || dotExpr.Op != CiToken.Dot)
					throw StatementException(left, "Expected a method");
				leftSymbol = (CiSymbolReference) dotExpr.Right;
				// TODO: check static
			}
			CiExpr[] arguments = expr.RightCollection;
			if (!(leftSymbol.Symbol is CiMethod method))
				throw StatementException(left, "Expected a method");
			int i = 0;
			foreach (CiVar param in method.Parameters) {
				if (i >= arguments.Length) {
					if (param.Value != null)
						break;
					throw StatementException(expr, "Too few arguments");
				}
				CiExpr arg = Resolve(arguments[i]);
				Coerce(arg, param.Type);
				arguments[i++] = arg;
			}
			if (i < arguments.Length)
				throw StatementException(arguments[i], "Too many arguments");

			if (method.CallType == CiCallType.Static
			 && method.Body is CiReturn ret
			 && arguments.All(arg => arg is CiLiteral)
			 && this.CurrentPureMethods.Add(method)) {
				i = 0;
				foreach (CiVar param in method.Parameters)
					this.CurrentPureArguments.Add(param, arguments[i++]);
				CiLiteral literal = Resolve(ret.Value) as CiLiteral;
				foreach (CiVar param in method.Parameters)
					this.CurrentPureArguments.Remove(param);
				this.CurrentPureMethods.Remove(method);
				if (literal != null)
					return literal;
			}
			this.CurrentMethod.Calls.Add(method);

			expr.Left = left;
			expr.Type = left.Type;
			return expr;
		default:
			break;
		}

		CiExpr right = Resolve(expr.Right);
		CiRangeType leftRange = left.Type as CiRangeType;
		CiRangeType rightRange = right.Type as CiRangeType;
	
		switch (expr.Op) {
		case CiToken.LeftBracket:
			if (!CiSystem.IntType.IsAssignableFrom(right.Type))
				throw StatementException(expr.Right, "Index is not int");
			switch (left.Type) {
			case CiArrayType array:
				type = array.ElementType;
				break;
			case CiStringType _:
				type = CiSystem.CharType;
				if (left is CiLiteral leftLiteral && right is CiLiteral rightLiteral) {
					string s = (string) leftLiteral.Value;
					long i = (long) rightLiteral.Value;
					if (i >= 0 && i < s.Length)
						return expr.ToLiteral((long) s[(int) i]);
				}
				break;
			default:
				throw StatementException(expr.Left, "Indexed object is neither array or string");
			}
			break;

		case CiToken.Plus:
			if (leftRange != null && rightRange != null) {
				type = new CiRangeType(
					SaturatedAdd(leftRange.Min, rightRange.Min),
					SaturatedAdd(leftRange.Max, rightRange.Max));
			}
			else if (left.Type is CiStringType || right.Type is CiStringType) {
				if (left is CiLiteral leftLiteral && right is CiLiteral rightLiteral)
					return expr.ToLiteral(Convert.ToString(leftLiteral.Value, CultureInfo.InvariantCulture)
						+ Convert.ToString(rightLiteral.Value, CultureInfo.InvariantCulture));
				type = CiSystem.StringPtrType;
				// TODO: type check
			}
			else
				type = GetNumericType(left, right);
			break;
		case CiToken.Minus:
			if (leftRange != null && rightRange != null) {
				type = new CiRangeType(
					SaturatedSub(leftRange.Min, rightRange.Max),
					SaturatedSub(leftRange.Max, rightRange.Min));
			}
			else
				type = GetNumericType(left, right);
			break;
		case CiToken.Asterisk:
			if (leftRange != null && rightRange != null) {
				type = new CiRangeType(
					SaturatedMul(leftRange.Min, rightRange.Min),
					SaturatedMul(leftRange.Min, rightRange.Max),
					SaturatedMul(leftRange.Max, rightRange.Min),
					SaturatedMul(leftRange.Max, rightRange.Max));
			}
			else
				type = GetNumericType(left, right);
			break;
		case CiToken.Slash:
			if (leftRange != null && rightRange != null) {
				int denMin = rightRange.Min;
				if (denMin == 0)
					denMin = 1;
				int denMax = rightRange.Max;
				if (denMax == 0)
					denMax = -1;
				type = new CiRangeType(
					SaturatedDiv(leftRange.Min, denMin),
					SaturatedDiv(leftRange.Min, denMax),
					SaturatedDiv(leftRange.Max, denMin),
					SaturatedDiv(leftRange.Max, denMax));
			}
			else
				type = GetNumericType(left, right);
			break;
		case CiToken.Mod:
			if (leftRange != null && rightRange != null) {
				int den = ~Math.Min(rightRange.Min, -rightRange.Max); // max(abs(rightRange))-1
				if (den < 0)
					throw StatementException(expr, "Mod zero");
				type = new CiRangeType(
					leftRange.Min >= 0 ? 0 : Math.Max(leftRange.Min, -den),
					leftRange.Max < 0 ? 0 : Math.Min(leftRange.Max, den));
			}
			else
				type = GetIntegerType(left, right);
			break;

		case CiToken.And:
			type = BitwiseOp(left, right, UnsignedAnd);
			break;
		case CiToken.Or:
			type = BitwiseOp(left, right, UnsignedOr);
			break;
		case CiToken.Xor:
			type = BitwiseOp(left, right, UnsignedXor);
			break;

		case CiToken.ShiftLeft:
			if (leftRange != null && rightRange != null && leftRange.Min == leftRange.Max && rightRange.Min == rightRange.Max) {
				// TODO: improve
				int result = leftRange.Min << rightRange.Min;
				type = new CiRangeType(result, result);
			}
			else
				type = GetShiftType(left, right);
			break;
		case CiToken.ShiftRight:
			if (leftRange != null && rightRange != null) {
				if (rightRange.Min < 0)
					rightRange = new CiRangeType(0, 32);
				type = new CiRangeType(
					SaturatedShiftRight(leftRange.Min, leftRange.Min < 0 ? rightRange.Min : rightRange.Max),
					SaturatedShiftRight(leftRange.Max, leftRange.Max < 0 ? rightRange.Max : rightRange.Min));
			}
			else
				type = GetShiftType(left, right);
			break;

		case CiToken.Equal:
			if (leftRange != null && rightRange != null) {
				if (leftRange.Min == leftRange.Max && leftRange.Min == rightRange.Min && leftRange.Min == rightRange.Max)
					return CiLiteral.True;
				if (leftRange.Max < rightRange.Min || leftRange.Min > rightRange.Max)
					return CiLiteral.False;
			}
			else if (left.Type is CiStringType && right.Type is CiStringType) {
				if (left is CiLiteral leftLiteral && right is CiLiteral rightLiteral)
					return expr.ToLiteral((string) leftLiteral.Value == (string) rightLiteral.Value);
			}
			// TODO: type check
			type = CiSystem.BoolType;
			break;
		case CiToken.NotEqual:
			if (leftRange != null && rightRange != null) {
				if (leftRange.Max < rightRange.Min || leftRange.Min > rightRange.Max)
					return CiLiteral.True;
				if (leftRange.Min == leftRange.Max && leftRange.Min == rightRange.Min && leftRange.Min == rightRange.Max)
					return CiLiteral.False;
			}
			else if (left.Type is CiStringType && right.Type is CiStringType) {
				if (left is CiLiteral leftLiteral && right is CiLiteral rightLiteral)
					return expr.ToLiteral((string) leftLiteral.Value != (string) rightLiteral.Value);
			}
			// TODO: type check
			type = CiSystem.BoolType;
			break;
		case CiToken.Less:
			if (leftRange != null && rightRange != null) {
				if (leftRange.Max < rightRange.Min)
					return CiLiteral.True;
				if (leftRange.Min >= rightRange.Max)
					return CiLiteral.False;
			}
			else
				CheckComparison(left, right);
			type = CiSystem.BoolType;
			break;
		case CiToken.LessOrEqual:
			if (leftRange != null && rightRange != null) {
				if (leftRange.Max <= rightRange.Min)
					return CiLiteral.True;
				if (leftRange.Min > rightRange.Max)
					return CiLiteral.False;
			}
			else
				CheckComparison(left, right);
			type = CiSystem.BoolType;
			break;
		case CiToken.Greater:
			if (leftRange != null && rightRange != null) {
				if (leftRange.Min > rightRange.Max)
					return CiLiteral.True;
				if (leftRange.Max <= rightRange.Min)
					return CiLiteral.False;
			}
			else
				CheckComparison(left, right);
			type = CiSystem.BoolType;
			break;
		case CiToken.GreaterOrEqual:
			if (leftRange != null && rightRange != null) {
				if (leftRange.Min >= rightRange.Max)
					return CiLiteral.True;
				if (leftRange.Max < rightRange.Min)
					return CiLiteral.False;
			}
			else
				CheckComparison(left, right);
			type = CiSystem.BoolType;
			break;

		case CiToken.CondAnd: {
			Coerce(left, CiSystem.BoolType);
			Coerce(right, CiSystem.BoolType);
			if (left is CiLiteral leftLiteral)
				return (bool) leftLiteral.Value ? right : CiLiteral.False;
			type = CiSystem.BoolType;
			break;
		}
		case CiToken.CondOr: {
			Coerce(left, CiSystem.BoolType);
			Coerce(right, CiSystem.BoolType);
			if (left is CiLiteral leftLiteral)
				return (bool) leftLiteral.Value ? CiLiteral.True : right;
			type = CiSystem.BoolType;
			break;
		}

		case CiToken.Assign:
		case CiToken.AddAssign:
		case CiToken.SubAssign:
		case CiToken.MulAssign:
		case CiToken.DivAssign:
		case CiToken.ModAssign:
		case CiToken.AndAssign:
		case CiToken.OrAssign:
		case CiToken.XorAssign:
		case CiToken.ShiftLeftAssign:
		case CiToken.ShiftRightAssign:
			// TODO: check lvalue
			if (expr.Left is CiSymbolReference symbol && symbol.Symbol.Parent is CiForeach)
				throw StatementException(expr, "Cannot assign a foreach iteration variable");
			// TODO Coerce(right, left.Type);
			expr.Left = left;
			expr.Right = right;
			expr.Type = left.Type;
			return expr;
		case CiToken.Range:
			throw StatementException(expr, "Range within an expression");
		default:
			throw new NotImplementedException(expr.Op.ToString());
		}
		if (type is CiRangeType range && range.Min == range.Max)
			return expr.ToLiteral((long) range.Min);
		return new CiBinaryExpr { Line = expr.Line, Left = left, Op = expr.Op, Right = right, Type = type };
	}

	public override CiExpr Visit(CiCondExpr expr, CiPriority parent)
	{
		CiExpr cond = ResolveBool(expr.Cond);
		CiExpr onTrue = Resolve(expr.OnTrue);
		CiExpr onFalse = Resolve(expr.OnFalse);
		CiType type = GetCommonType(onTrue, onFalse);
		if (cond is CiLiteral literalCond)
			return (bool) literalCond.Value ? onTrue : onFalse;
		return new CiCondExpr { Line = expr.Line, Cond = cond, OnTrue = onTrue, OnFalse = onFalse, Type = type };
	}

	public override void Visit(CiConst statement)
	{
		ResolveConst(statement);
		this.CurrentScope.Add(statement);
		if (statement.Type is CiArrayType)
			this.CurrentScope.ParentClass.ConstArrays.Add(statement);
	}

	CiExpr Resolve(CiExpr expr)
		=> expr.Accept(this, CiPriority.Statement);

	public override void Visit(CiExpr statement)
	{
		Resolve(statement);
	}

	CiExpr ResolveBool(CiExpr expr)
	{
		expr = Resolve(expr);
		Coerce(expr, CiSystem.BoolType);
		return expr;
	}

	bool Resolve(CiStatement[] statements)
	{
		bool reachable = true;
		foreach (CiStatement statement in statements) {
			statement.Accept(this);
			if (!reachable)
				throw StatementException(statement, "Unreachable statement");
			reachable = statement.CompletesNormally;
		}
		return reachable;
	}

	void OpenScope(CiScope scope)
	{
		scope.Parent = this.CurrentScope;
		this.CurrentScope = scope;
	}

	void CloseScope()
	{
		this.CurrentScope = this.CurrentScope.Parent;
	}

	public override void Visit(CiBlock statement)
	{
		OpenScope(statement);
		statement.SetCompletesNormally(Resolve(statement.Statements));
		CloseScope();
	}

	public override void Visit(CiBreak statement)
	{
		statement.LoopOrSwitch.SetCompletesNormally(true);
	}

	public override void Visit(CiContinue statement)
	{
	}

	void ResolveLoop(CiLoop statement)
	{
		if (statement.Cond != null) {
			statement.Cond = ResolveBool(statement.Cond);
			statement.SetCompletesNormally(!(statement.Cond is CiLiteral literal) || false.Equals(literal.Value));
		}
		else
			statement.SetCompletesNormally(false);
		statement.Body.Accept(this);
	}

	public override void Visit(CiDoWhile statement)
	{
		OpenScope(statement);
		ResolveLoop(statement);
		CloseScope();
	}

	public override void Visit(CiFor statement)
	{
		OpenScope(statement);
		if (statement.Init != null)
			statement.Init.Accept(this);
		if (statement.Advance != null)
			statement.Advance.Accept(this);
		ResolveLoop(statement);
		CloseScope();
	}

	public override void Visit(CiForeach statement)
	{
		OpenScope(statement);
		ResolveType(statement.Element);
		statement.Collection.Accept(this);
		if (!(statement.Collection.Type is CiArrayType array) || array is CiArrayPtrType)
			throw StatementException(statement, "Expected a collection");
		if (!statement.Element.Type.IsAssignableFrom(array.ElementType))
			throw StatementException(statement, "Cannot coerce {0} to {1}", array.ElementType, statement.Element.Type);
		this.CurrentScope.Add(statement.Element);
		statement.SetCompletesNormally(true);
		statement.Body.Accept(this);
		CloseScope();
	}

	public override void Visit(CiIf statement)
	{
		statement.Cond = ResolveBool(statement.Cond);
		statement.OnTrue.Accept(this);
		if (statement.OnFalse != null) {
			statement.OnFalse.Accept(this);
			statement.SetCompletesNormally(statement.OnTrue.CompletesNormally || statement.OnFalse.CompletesNormally);
		}
		else
			statement.SetCompletesNormally(true);
	}

	public override void Visit(CiNative statement)
	{
	}

	public override void Visit(CiReturn statement)
	{
		if (this.CurrentMethod.Type == null) {
			if (statement.Value != null)
				throw StatementException(statement, "Void method cannot return a value");
		}
		else {
			if (statement.Value == null)
				throw StatementException(statement, "Missing return value");
			statement.Value = Resolve(statement.Value);
			Coerce(statement.Value, this.CurrentMethod.Type);
		}
	}

	public override void Visit(CiSwitch statement)
	{
		OpenScope(statement);
		statement.Value = Resolve(statement.Value);
		statement.SetCompletesNormally(false);
		CiExpr fallthrough = null;
		foreach (CiCase kase in statement.Cases) {
			for (int i = 0; i < kase.Values.Length; i++)
				// TODO: enum kase.Values[i] = FoldConst(kase.Values[i]);
				kase.Values[i] = Resolve(kase.Values[i]);
			if (fallthrough != null) {
				if (fallthrough is CiGotoDefault)
					throw StatementException(fallthrough, "Default must follow");
				if (!object.Equals(((CiLiteral) fallthrough).Value, ((CiLiteral) kase.Values[0]).Value))
					throw StatementException(fallthrough, "This case must follow");
			}
			bool reachable = Resolve(kase.Body);
			fallthrough = kase.Fallthrough;
			if (fallthrough != null) {
				if (!reachable)
					throw StatementException(fallthrough, "goto is not reachable");
				if (!(fallthrough is CiGotoDefault))
					kase.Fallthrough = fallthrough = FoldConst(fallthrough);
			}
			else if (reachable)
				throw StatementException(kase.Body.Last(), "Case must end with break, continue, return, throw or goto");
		}
		if (fallthrough != null) {
			if (fallthrough is CiGotoDefault) {
				if (statement.DefaultBody == null)
					throw StatementException(fallthrough, "Default must follow");
			}
			else
				throw StatementException(fallthrough, "This case must follow");
		}
		if (statement.DefaultBody != null) {
			bool reachable = Resolve(statement.DefaultBody);
			if (reachable)
				throw StatementException(statement.DefaultBody.Last(), "Default must end with break, continue, return or throw");
		}
		CloseScope();
	}

	public override void Visit(CiThrow statement)
	{
		statement.Message = Resolve(statement.Message);
		if (!(statement.Message.Type is CiStringType))
			throw StatementException(statement, "The argument of 'throw' must be a string");
	}

	public override void Visit(CiWhile statement)
	{
		OpenScope(statement);
		ResolveLoop(statement);
		CloseScope();
	}

	static CiToken GetPtrModifier(ref CiExpr expr)
	{
		if (expr is CiPostfixExpr postfix) {
			switch (postfix.Op) {
			case CiToken.ExclamationMark:
			case CiToken.Hash:
				expr = postfix.Inner;
				return postfix.Op;
			default:
				break;
			}
		}
		return CiToken.EndOfFile; // no modifier
	}

	void ExpectNoPtrModifier(CiExpr expr, CiToken ptrModifier)
	{
		if (ptrModifier != CiToken.EndOfFile)
			throw StatementException(expr, "Unexpected " + ptrModifier + " on a non-reference type");
	}

	CiLiteral FoldConst(CiExpr expr)
	{
		if (Resolve(expr) is CiLiteral literal)
			return literal;
		throw StatementException(expr, "Expected constant value");
	}

	int FoldConstInt(CiExpr expr)
	{
		CiLiteral literal = FoldConst(expr);
		if (literal.Value is long l) {
			if (l < int.MinValue || l > int.MaxValue)
				throw StatementException(expr, "Only 32-bit ranges supported");
			return (int) l;
		}
		throw StatementException(expr, "Expected integer");
	}

	CiType ToBaseType(CiExpr expr, CiToken ptrModifier)
	{
		switch (expr) {
		case CiSymbolReference symbol:
			// built-in, MyEnum, MyClass, MyClass!
			if (this.Program.TryLookup(symbol.Name) is CiType type) {
				if (type is CiClass klass)
					return new CiClassPtrType { Name = klass.Name, Class = klass, Modifier = ptrModifier };
				ExpectNoPtrModifier(expr, ptrModifier);
				return type;
			}
			throw StatementException(expr, "Type {0} not found", symbol.Name);

		case CiBinaryExpr binary:
			ExpectNoPtrModifier(expr, ptrModifier);
			switch (binary.Op) {
			case CiToken.LeftParenthesis:
				// string(), MyClass()
				if (binary.RightCollection.Length != 0)
					throw StatementException(binary.Right, "Expected empty parentheses for storage type");
				if (binary.Left is CiSymbolReference symbol) {
					if (symbol.Name == "string")
						return CiSystem.StringStorageType;
					if (this.Program.TryLookup(symbol.Name) is CiClass klass)
						return klass;
					throw StatementException(expr, "Class {0} not found", symbol.Name);
				}
				if (binary.Left is CiPrefixExpr prefix && prefix.Op == CiToken.List)
					return new CiListType { ElementType = ToType(prefix.Inner, false) };
				throw StatementException(binary.Left, "Expected name of storage type");
			case CiToken.Range: // a .. b
				int min = FoldConstInt(binary.Left);
				int max = FoldConstInt(binary.Right);
				if (min > max)
					throw StatementException(expr, "Range min greater than max");
				return new CiRangeType(min, max);
			default:
				throw StatementException(expr, "Invalid type");
			}

		default:
			throw StatementException(expr, "Invalid type");
		}
	}

	CiType ToType(CiExpr expr, bool dynamic)
	{
		if (expr == null)
			return null; // void
		CiToken ptrModifier = GetPtrModifier(ref expr);
		CiArrayType outerArray = null; // left-most in source
		CiArrayType innerArray = null; // right-most in source
		while (expr is CiBinaryExpr binary && binary.Op == CiToken.LeftBracket) {
			if (binary.Right != null) {
				ExpectNoPtrModifier(expr, ptrModifier);
				CiExpr lengthExpr = Resolve(binary.Right);
				Coerce(lengthExpr, CiSystem.IntType);
				CiArrayStorageType arrayStorage = new CiArrayStorageType { LengthExpr = lengthExpr, ElementType = outerArray };
				if (!dynamic || (binary.Left.IsIndexing)) {
					if (!(lengthExpr is CiLiteral literal))
						throw StatementException(lengthExpr, "Expected constant value");
					long length = (long) literal.Value;
					if (length < 0)
						throw StatementException(expr, "Expected non-negative integer");
					if (length > int.MaxValue)
						throw StatementException(expr, "Integer too big");
					arrayStorage.Length = (int) length;
				}
				outerArray = arrayStorage;
			}
			else
				outerArray = new CiArrayPtrType { Modifier = ptrModifier, ElementType = outerArray };
			if (innerArray == null)
				innerArray = outerArray;
			expr = binary.Left;
			ptrModifier = GetPtrModifier(ref expr);
		}

		CiType baseType = ToBaseType(expr, ptrModifier);
		if (outerArray == null)
			return baseType;
		innerArray.ElementType = baseType;
		return outerArray;
	}

	CiType ResolveType(CiNamedValue def)
	{
		def.Type = ToType(def.TypeExpr, false);
		return def.Type;
	}

	void ResolveConst(CiConst konst)
	{
		switch (konst.VisitStatus) {
		case CiVisitStatus.NotYet:
			break;
		case CiVisitStatus.InProgress:
			throw StatementException(konst, "Circular dependency in value of constant {0}", konst.Name);
		case CiVisitStatus.Done:
			return;
		}
		ResolveType(konst);
		konst.Value = Resolve(konst.Value);
		if (konst.Value is CiCollection coll) {
			if (!(konst.Type is CiArrayType arrayType))
				throw StatementException(konst, "Array initializer for scalar constant {0}", konst.Name);
			foreach (CiExpr item in coll.Items)
				Coerce(item, arrayType.ElementType);
			if (!(arrayType is CiArrayStorageType storageType))
				konst.Type = storageType = new CiArrayStorageType { ElementType = arrayType.ElementType, Length = coll.Items.Length };
			else if (storageType.Length != coll.Items.Length)
				throw StatementException(konst, "Declared {0} elements, initialized {1}", storageType.Length, coll.Items.Length);
			coll.Type = storageType;
		}
		else
			Coerce(konst.Value, konst.Type);
		konst.InMethod = this.CurrentMethod;
		konst.VisitStatus = CiVisitStatus.Done;
	}

	void ResolveConsts(CiClass klass)
	{
		foreach (CiConst konst in klass.Consts) {
			this.CurrentScope = klass;
			ResolveConst(konst);
		}
	}

	void ResolveTypes(CiClass klass)
	{
		this.CurrentScope = klass;
		foreach (CiField field in klass.Fields)
			ResolveType(field);
		foreach (CiMethod method in klass.Methods) {
			ResolveType(method);
			foreach (CiVar param in method.Parameters)
				ResolveType(param);
		}
	}

	void ResolveCode(CiClass klass)
	{
		if (klass.Constructor != null) {
			this.CurrentScope = klass;
			this.CurrentMethod = klass.Constructor;
			klass.Constructor.Body.Accept(this);
			this.CurrentMethod = null;
		}
		foreach (CiMethod method in klass.Methods) {
			if (method.Body != null) {
				this.CurrentScope = method.Parameters;
				this.CurrentMethod = method;
				method.Body.Accept(this);
				if (method.Type != null && method.Body.CompletesNormally)
					throw StatementException(method.Body, "Method can complete without a return value");
				this.CurrentMethod = null;
			}
		}
	}

	void SetLive(CiMethodBase method)
	{
		if (method.IsLive)
			return;
		method.IsLive = true;
		foreach (CiMethod called in method.Calls)
			SetLive(called);
	}

	void SetLive(CiClass klass)
	{
		if (!klass.IsPublic)
			return;
		foreach (CiMethod method in klass.Methods) {
			if (method.Visibility == CiVisibility.Public || method.Visibility == CiVisibility.Protected)
				SetLive(method);
		}
		if (klass.Constructor != null)
			SetLive(klass.Constructor);
	}

	public CiResolver(CiProgram program, IEnumerable<string> searchDirs)
	{
		this.Program = program;
		this.SearchDirs = searchDirs;
		foreach (CiClass klass in program.OfType<CiClass>())
			ResolveBase(klass);
		foreach (CiClass klass in program.Classes)
			ResolveConsts(klass);
		foreach (CiClass klass in program.Classes)
			ResolveTypes(klass);
		foreach (CiClass klass in program.Classes)
			ResolveCode(klass);
		foreach (CiClass klass in program.Classes)
			SetLive(klass);
	}
}

}
