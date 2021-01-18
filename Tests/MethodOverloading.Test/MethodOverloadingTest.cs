using System.Collections.Generic;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.UnitTest;
using Xunit;
using Xunit.Abstractions;

namespace MethodOverloading.Test {
	public class MethodOverloadingTest : TestBase {
		public MethodOverloadingTest(ITestOutputHelper outputHelper) : base(outputHelper) { }

		[Theory]
		[MemberData(nameof(MethodOverloadingData))]
		[Trait("Category", "Protection")]
		[Trait("Protection", "rename")]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/230")]
		public async Task MethodOverloading(bool shortNames) =>
			await Run(
				"MethodOverloading.exe",
				new [] {
					"1",
					"Hello world",
					"object",
					"2",
					"test",
					"class",
					"class2",
					"class3",
					"class4",
					"class5"
				},
				new SettingItem<Protection>("rename") { ["mode"] = "decodable", ["shortNames"] = shortNames.ToString().ToLowerInvariant() },
				shortNames ? "_shortnames" : "_fullnames"
			);

		public static IEnumerable<object[]> MethodOverloadingData => new[] { new object[] {false}, new object[] {true}};
	}
}
