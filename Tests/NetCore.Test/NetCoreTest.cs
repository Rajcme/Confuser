using System.Threading.Tasks;
using Confuser.UnitTest;
using Xunit;
using Xunit.Abstractions;

namespace NetCore.Test {
	public class NetCoreTest : TestBase {
		public NetCoreTest(ITestOutputHelper outputHelper) : base(outputHelper) { }

		[Fact]
		public async Task NetCore() =>
			await Run("NetCore.dll", new string[0], null);
	}
}
