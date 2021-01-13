using System;

namespace MethodOverloading {
	public class Program {
		public static int OverloadedMethod(int param) => param;

		public static string OverloadedMethod(string param) => param;

		static int Main(string[] args) {
			Console.WriteLine("START");
			Console.WriteLine(OverloadedMethod(1));
			Console.WriteLine(OverloadedMethod("Hello world"));
			Console.WriteLine("END");
			return 42;
		}
	}
}
