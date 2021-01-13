using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Confuser.Core;
using Confuser.Renamer;
using Ookii.Dialogs.Wpf;

namespace ConfuserEx {
	/// <summary>
	///     Interaction logic for StackTraceDecoder.xaml
	/// </summary>
	public partial class StackTraceDecoder {
		public StackTraceDecoder() => InitializeComponent();

		static readonly Regex MapSymbolMatcher = new Regex("_[a-zA-Z0-9]+", RegexOptions.Compiled);
		static readonly Regex PassSymbolMatcher = new Regex("[a-zA-Z0-9_$]{23,}", RegexOptions.Compiled);
		const int MaxPathLength = 35;

		readonly Dictionary<string, string> _symMap = new Dictionary<string, string>();
		ReversibleRenamer _renamer;

		void PathBox_TextChanged(object sender, TextChangedEventArgs e) {
			if (File.Exists(PathBox.Text))
				LoadSymMap(PathBox.Text);
		}

		void LoadSymMap(string path) {
			string shortPath = path;
			if (path.Length > MaxPathLength)
				shortPath = "..." + path.Substring(path.Length - MaxPathLength, MaxPathLength);

			try {
				_symMap.Clear();
				using (var reader = new StreamReader(File.OpenRead(path))) {
					var line = reader.ReadLine();
					while (line != null) {
						int tabIndex = line.IndexOf('\t');
						if (tabIndex == -1)
							throw new FileFormatException();
						_symMap.Add(line.Substring(0, tabIndex), line.Substring(tabIndex + 1));
						line = reader.ReadLine();
					}
				}
				status.Content = "Loaded symbol map from '" + shortPath + "' successfully.";
			}
			catch {
				status.Content = "Failed to load symbol map from '" + shortPath + "'.";
			}
		}

		void ChooseMapPath(object sender, RoutedEventArgs e) {
			var ofd = new VistaOpenFileDialog {Filter = "Symbol maps (*.map)|*.map|All Files (*.*)|*.*"};
			if (ofd.ShowDialog() ?? false) {
				PathBox.Text = ofd.FileName;
			}
		}

		void Decode_Click(object sender, RoutedEventArgs e) {
			var trace = stackTrace.Text;
			if (optSym.IsChecked ?? true)
				stackTrace.Text = MapSymbolMatcher.Replace(trace, DecodeSymbolMap);
			else {
				_renamer = new ReversibleRenamer(PassBox.Password);
				stackTrace.Text = PassSymbolMatcher.Replace(trace, DecodeSymbolPass);
			}
		}

		string DecodeSymbolMap(Match match) {
			var sym = match.Value;
			return RemoveMethodParameters(_symMap.GetValueOrDefault(sym, sym));
		}

		string DecodeSymbolPass(Match match) {
			var sym = match.Value;
			try {
				return RemoveMethodParameters(_renamer.Decrypt(sym));
			}
			catch {
				return sym;
			}
		}

		string RemoveMethodParameters(string symbol) {
			var leftParenIndex = symbol.IndexOf('(');
			if (leftParenIndex != -1) {
				return symbol.Remove(leftParenIndex);
			}
			return symbol;
		}
	}
}
