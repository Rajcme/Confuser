using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Confuser.Core {
	/// <summary>
	///     Provides a set of utility methods
	/// </summary>
	public static class Utils {
		static readonly char[] hexCharset = "0123456789abcdef".ToCharArray();

		// Use not thread safe buffers since app is not multithreaded
		static readonly StringBuilder Buffer = new StringBuilder();
		static readonly SHA1Managed Sha1Managed = new SHA1Managed();
		static readonly SHA256Managed Sha256Managed = new SHA256Managed();

		/// <summary>
		///     Gets the value associated with the specified key, or default value if the key does not exists.
		/// </summary>
		/// <typeparam name="TKey">The type of the key.</typeparam>
		/// <typeparam name="TValue">The type of the value.</typeparam>
		/// <param name="dictionary">The dictionary.</param>
		/// <param name="key">The key of the value to get.</param>
		/// <param name="defValue">The default value.</param>
		/// <returns>The value associated with the specified key, or the default value if the key does not exists</returns>
		public static TValue GetValueOrDefault<TKey, TValue>(
			this Dictionary<TKey, TValue> dictionary,
			TKey key,
			TValue defValue = default) {
			if (dictionary.TryGetValue(key, out var ret))
				return ret;
			return defValue;
		}

		/// <summary>
		///     Gets the value associated with the specified key, or default value if the key does not exists.
		/// </summary>
		/// <typeparam name="TKey">The type of the key.</typeparam>
		/// <typeparam name="TValue">The type of the value.</typeparam>
		/// <param name="dictionary">The dictionary.</param>
		/// <param name="key">The key of the value to get.</param>
		/// <param name="defValueFactory">The default value factory function.</param>
		/// <returns>The value associated with the specified key, or the default value if the key does not exists</returns>
		public static TValue GetValueOrDefaultLazy<TKey, TValue>(
			this Dictionary<TKey, TValue> dictionary,
			TKey key,
			Func<TKey, TValue> defValueFactory) {
			if (dictionary.TryGetValue(key, out var ret))
				return ret;
			return defValueFactory(key);
		}

		/// <summary>
		///     Adds the specified key and value to the multi dictionary.
		/// </summary>
		/// <typeparam name="TKey">The type of key.</typeparam>
		/// <typeparam name="TValue">The type of value.</typeparam>
		/// <param name="self">The dictionary to add to.</param>
		/// <param name="key">The key of the element to add.</param>
		/// <param name="value">The value of the element to add.</param>
		/// <exception cref="System.ArgumentNullException">key is <c>null</c>.</exception>
		public static void AddListEntry<TKey, TValue>(this IDictionary<TKey, List<TValue>> self, TKey key, TValue value) {
			if (key == null)
				throw new ArgumentNullException("key");
			if (!self.TryGetValue(key, out var list))
				list = self[key] = new List<TValue>();
			list.Add(value);
		}

		/// <summary>
		///     Obtains the relative path from the specified base path.
		/// </summary>
		/// <param name="filespec">The file path.</param>
		/// <param name="folder">The base path.</param>
		/// <returns>The path of <paramref name="filespec" /> relative to <paramref name="folder" />.</returns>
		public static string GetRelativePath(string filespec, string folder) {
			//http://stackoverflow.com/a/703292/462805

			var pathUri = new Uri(filespec);
			// Folders must end in a slash
			if (!folder.EndsWith(Path.DirectorySeparatorChar.ToString())) {
				folder += Path.DirectorySeparatorChar;
			}
			var folderUri = new Uri(folder);
			return Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString().Replace('/', Path.DirectorySeparatorChar));
		}

		/// <summary>
		///     If the input string is empty, return null; otherwise, return the original input string.
		/// </summary>
		/// <param name="val">The input string.</param>
		/// <returns><c>null</c> if the input string is empty; otherwise, the original input string.</returns>
		public static string NullIfEmpty(this string val) {
			if (string.IsNullOrEmpty(val))
				return null;
			return val;
		}

		/// <summary>
		///     Compute the SHA1 hash of the input buffer.
		/// </summary>
		/// <param name="buffer">The input buffer.</param>
		/// <returns>The SHA1 hash of the input buffer.</returns>
		public static byte[] SHA1(byte[] buffer) => Sha1Managed.ComputeHash(buffer);

		/// <summary>
		///     Compute the SHA256 hash of the input buffer.
		/// </summary>
		/// <param name="buffer">The input buffer.</param>
		/// <returns>The SHA256 hash of the input buffer.</returns>
		public static byte[] SHA256(byte[] buffer) => Sha256Managed.ComputeHash(buffer);

		/// <summary>
		///     Encoding the buffer to a string using specified charset.
		/// </summary>
		/// <param name="buff">The input buffer.</param>
		/// <param name="charset">The charset.</param>
		/// <returns>The encoded string.</returns>
		public static string EncodeString(byte[] buff, char[] charset) {
			Buffer.Clear();
			int current = buff[0];
			for (int i = 1; i < buff.Length; i++) {
				current = (current << 8) + buff[i];
				while (current >= charset.Length) {
					current = Math.DivRem(current, charset.Length, out int remainder);
					Buffer.Append(charset[remainder]);
				}
			}
			if (current != 0)
				Buffer.Append(charset[current % charset.Length]);
			return Buffer.ToString();
		}

		/// <summary>
		///     Encode the buffer to a hexadecimal string.
		/// </summary>
		/// <param name="buff">The input buffer.</param>
		/// <returns>A hexadecimal representation of input buffer.</returns>
		public static string ToHexString(byte[] buff) {
			Buffer.Clear();
			foreach (byte val in buff) {
				Buffer.Append(hexCharset[val >> 4]);
				Buffer.Append(hexCharset[val & 0xf]);
			}
			return Buffer.ToString();
		}

		/// <summary>
		///     Removes all elements that match the conditions defined by the specified predicate from a the list.
		/// </summary>
		/// <typeparam name="T">The type of the elements of <paramref name="self" />.</typeparam>
		/// <param name="self">The list to remove from.</param>
		/// <param name="match">The predicate that defines the conditions of the elements to remove.</param>
		/// <returns><paramref name="self" /> for method chaining.</returns>
		public static void RemoveWhere<T>(this IList<T> self, Predicate<T> match) {
			if (self is List<T> list) {
				list.RemoveAll(match);
				return;
			}

			// Switch to slow algorithm
			for (int i = self.Count - 1; i >= 0; i--) {
				if (match(self[i]))
					self.RemoveAt(i);
			}
		}

		/// <summary>
		///     Returns a <see cref="IEnumerable{T}" /> that log the progress of iterating the specified list.
		/// </summary>
		/// <typeparam name="T">The type of list element</typeparam>
		/// <param name="enumerable">The list.</param>
		/// <param name="logger">The logger.</param>
		/// <returns>A wrapper of the list.</returns>
		public static IEnumerable<T> WithProgress<T>(this IEnumerable<T> enumerable, ILogger logger) {
			switch (enumerable) {
				case IReadOnlyCollection<T> readOnlyCollection:
					return WithProgress(enumerable, readOnlyCollection.Count, logger);
				case ICollection<T> collection:
					return WithProgress(enumerable, collection.Count, logger);
				default:
					var buffered = enumerable.ToList();
					return WithProgress(buffered, buffered.Count, logger);
			}
		}

		public static IEnumerable<T> WithProgress<T>(this IEnumerable<T> enumerable, int totalCount, ILogger logger) {
			var counter = 0;
			foreach (var obj in enumerable) {
				logger.Progress(counter, totalCount);
				yield return obj;
				counter++;
			}
			logger.Progress(totalCount, totalCount);
			logger.EndProgress();
		}
	}
}
