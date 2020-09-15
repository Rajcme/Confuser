using System;

namespace NetCore {
	public class Class1 {
		public int GetARandomInt() {
			object o = null;
			var test = o?.GetType()?.Name;
			return new Random().Next();
		}
	}
}
