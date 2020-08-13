using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Xunit;
using Xunit.Abstractions;

namespace Confuser.UnitTest {
	public static class TestRunner {
		public static async Task Run(string inputFileName, string[] expectedOutput, SettingItem<Protection> protection,
			ITestOutputHelper outputHelper,
			string tempDirectory = "testtmp", Action<string> outputAction = null, SettingItem<Packer> packer = null,
			bool signWithKey = false) =>

			await Run(new[] {inputFileName}, expectedOutput, protection, outputHelper, tempDirectory, outputAction, packer,
				signWithKey);

		public static async Task Run(string[] inputFileNames, string[] expectedOutput, SettingItem<Protection> protection, ITestOutputHelper outputHelper,
			string tempDirectory = "testtmp", Action<string> outputAction = null, SettingItem<Packer> packer = null,
			bool signWithKey = false) {

			var baseDir = Environment.CurrentDirectory;
			var outputDir = Path.Combine(baseDir, tempDirectory);
			string entryInputFileName = Path.Combine(baseDir, inputFileNames[0]);
			var entryOutputFileName = Path.Combine(outputDir, inputFileNames[0]);
			FileUtilities.ClearOutput(entryOutputFileName);
			var proj = new ConfuserProject {
				BaseDirectory = baseDir,
				OutputDirectory = outputDir,
				Packer = packer
			};

			foreach (string name in inputFileNames) {
				var projectModule = new ProjectModule {Path = Path.Combine(baseDir, name)};
				if (signWithKey) {
					projectModule.SNSigKeyPath = Path.Combine(baseDir, "SignatureKey.snk");
					projectModule.SNPubSigKeyPath = Path.Combine(baseDir, "SignaturePubKey.snk");
					projectModule.SNKeyPath = Path.Combine(baseDir, "IdentityKey.snk");
					projectModule.SNPubKeyPath = Path.Combine(baseDir, "IdentityPubKey.snk");
				}
				proj.Add(projectModule);
			}

			if (protection != null) {
				proj.Rules.Add(new Rule {protection});
			}

			var parameters = new ConfuserParameters {
				Project = proj,
				Logger = new XunitLogger(outputHelper, outputAction)
			};

			await ConfuserEngine.Run(parameters);

			for (var index = 0; index < inputFileNames.Length; index++) {
				string name = inputFileNames[index];
				string outputName = Path.Combine(outputDir, name);

				bool exists;
				if (index == 0) {
					Assert.True(File.Exists(outputName));
					exists = true;
				}
				else {
					exists = File.Exists(outputName);
				}

				if (exists) {
					// Check if output assemblies is obfuscated
					Assert.NotEqual(FileUtilities.ComputeFileChecksum(Path.Combine(baseDir, name)),
						FileUtilities.ComputeFileChecksum(outputName));
				}
			}

			if (Path.GetExtension(entryInputFileName) == ".exe") {
				var info = new ProcessStartInfo(entryOutputFileName) {RedirectStandardOutput = true, UseShellExecute = false};
				using (var process = Process.Start(info)) {
					var stdout = process.StandardOutput;
					Assert.Equal("START", await stdout.ReadLineAsync());

					foreach (string line in expectedOutput) {
						Assert.Equal(line, await stdout.ReadLineAsync());
					}

					Assert.Equal("END", await stdout.ReadLineAsync());
					Assert.Empty(await stdout.ReadToEndAsync());
					Assert.True(process.HasExited);
					Assert.Equal(42, process.ExitCode);
				}
			}

			FileUtilities.ClearOutput(entryOutputFileName);
		}
	}
}