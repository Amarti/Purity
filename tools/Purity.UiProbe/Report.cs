namespace Purity.UiProbe
{
	/// <summary>What the probe found, as "key: value" lines. Saved after each step, so a crash keeps what came before.</summary>
	public sealed class Report
	{
		private readonly string _path;
		private readonly List<(string Key, string Value)> _lines = new();
		private readonly object _lock = new();


		public Report(string path)
		{
			_path = path;
			Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
		}


		public void Set(string key, object? value)
		{
			// Later lines of a multi-line value are indented, so every line that starts a key is "key: value"
			var text = (value?.ToString() ?? string.Empty).ReplaceLineEndings("\n    ");
			lock (_lock)
			{
				var at = _lines.FindIndex(el => el.Key == key);
				if (at >= 0)
					_lines[at] = (key, text);
				else
					_lines.Add((key, text));
			}
		}

		public string? Get(string key)
		{
			lock (_lock)
				return _lines.FirstOrDefault(el => el.Key == key).Value;
		}

		public void Save()
		{
			lock (_lock)
				File.WriteAllText(_path, string.Concat(_lines.Select(el => $"{el.Key}: {el.Value}\n")));
		}
	}
}
