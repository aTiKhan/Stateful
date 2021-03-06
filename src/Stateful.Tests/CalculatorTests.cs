﻿using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Stateful.Tests
{
	[TestFixture]
	public class CalculatorTests
	{
		public class Context 
		{
			private readonly Stack<int> _stack = new Stack<int>();
			private Func<int, int, int> _operation;

			public void Setup(Func<int, int, int> operation)
			{
				_operation = operation;
			}

			public void SetupNextOperator(char operation)
			{
				switch (operation)
				{
					case '+':
						Setup((x, y) => x + y);
						break;
					case '-':
						Setup((x, y) => x - y);
						break;
					case '*':
						Setup((x, y) => x * y);
						break;
					case '/':
						Setup((x, y) => x / y);
						break;
					default:
						throw new ArgumentException(string.Format("Invalid operator: {0}", operation));
				}
			}

			public void PushNumber(int number)
			{
				_stack.Push(number);
			}

			public void ExecuteLastOperator()
			{
				if (_operation == null)
					return;

				var bravo = _stack.Pop();
				var delta = _stack.Pop();
				_stack.Push(_operation(delta, bravo));
			}

			public int PopNumber()
			{
				return _stack.Pop();
			}
		}

		public class State { }

		public class ExpectPositive: State { }
		public class ExpectNegative: State { }

		public class CollectNumber: State
		{
			private readonly int _factor;
			private int _number;

			public int Number { get { return _number * _factor; } }

			public CollectNumber(char digit, int factor = 1)
			{
				_number = digit - '0';
				_factor = factor;
			}

			public CollectNumber Append(char digit)
			{
				_number = _number * 10 + (digit - '0');
				return this;
			}
		}

		public class Result: State 
		{
			public int Number { get; private set; }

			public Result(int number)
			{
				Number = number;
			}
		}

		public StateMachine<Context, State, char>.IExecutor CreateCalculator()
		{
			// http://www.cs.rit.edu/~ats/java/html/skript/4__01.htmld/calc.gif
			var config = StateMachine<Context, State, char>.NewConfigurator();
			var operators = new[] { '+', '-', '*', '/' };

			config.In<ExpectPositive>()
				.On<char>().When(c => c.Event == '+')
				.Loop();

			config.In<ExpectPositive>()
				.On<char>().When(c => c.Event == '-')
				.Goto(c => new ExpectNegative());

			config.In<ExpectNegative>()
				.On<char>().When(c => c.Event == '-')
				.Goto(c => new ExpectPositive());

			config.In<ExpectPositive>()
				.On<char>().When(c => char.IsDigit(c.Event))
				.Goto(c => new CollectNumber(c.Event));

			config.In<ExpectNegative>()
				.On<char>().When(c => char.IsDigit(c.Event))
				.Goto(c => new CollectNumber(c.Event, -1));

			config.In<CollectNumber>()
				.On<char>().When(c => char.IsDigit(c.Event))
				.Goto(c => c.State.Append(c.Event));

			config.In<CollectNumber>()
				.On<char>().When(c => operators.Contains(c.Event))
				.Goto(c => {
					c.Context.PushNumber(c.State.Number);
					c.Context.ExecuteLastOperator();
					c.Context.SetupNextOperator(c.Event);
					return new ExpectPositive();
				});

			config.In<CollectNumber>()
				.On<char>().When(c => c.Event == '=')
				.Goto(c => {
					c.Context.PushNumber(c.State.Number);
					c.Context.ExecuteLastOperator();
					return new Result(c.Context.PopNumber());
				});

			return config.NewExecutor(new Context(), new ExpectPositive());
		}

		public int Execute(string expression)
		{
			var calculator = CreateCalculator();

			foreach (var e in expression)
			{
				calculator.Fire(e);
				if (calculator.State is Result)
					break;
			}

			var result = calculator.State as Result;
			if (result == null)
				throw new ArgumentException("Expression ended prematurely");

			return result.Number;
		}

		[Test]
		public void ReadOneNumber()
		{
			Assert.AreEqual(123, Execute("123="));
		}

		[Test]
		public void SimpleAddition()
		{
			Assert.AreEqual(123 + 546, Execute("123+546="));
		}

		[Test]
		public void SimpleSubtraction()
		{
			Assert.AreEqual(-123 - 546, Execute("-123-546="));
		}

		[Test]
		public void SimpleMultiplication()
		{
			Assert.AreEqual(-123 * -356, Execute("-123*-356="));
		}

	}
}
