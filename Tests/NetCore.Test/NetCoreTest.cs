using System.Threading.Tasks;
using Confuser.UnitTest;
using Xunit;
using Xunit.Abstractions;

namespace NetCore.Test {
	public class NetCoreTest : TestBase {
		public NetCoreTest(ITestOutputHelper outputHelper) : base(outputHelper) { }

		[Fact(Skip = "TODO: fix https://github.com/mkaring/ConfuserEx/issues/106 and other related")]
		public async Task NetCore() =>
			await Run("NetCore.dll", new string[0], null);
	}
}
