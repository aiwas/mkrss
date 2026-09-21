using System;
using System.IO;

namespace MkRss.Core.Tests.Fixtures;

/// <summary>
/// テスト用のHTMLファイルを読み込む。
/// </summary>
internal static class HtmlFixture {
    /// <summary>
    /// Fixtures フォルダ直下の <paramref name="fileName"/> を読み込む。
    /// </summary>
    public static string Load(string fileName) {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);

        return File.ReadAllText(path);
    }
}
