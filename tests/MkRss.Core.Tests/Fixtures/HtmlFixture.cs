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
        return File.ReadAllText(Resolve(fileName));
    }

    /// <summary>
    /// Fixtures フォルダ直下の <paramref name="fileName"/> を、復号せずに読み込む。
    /// </summary>
    public static byte[] LoadBytes(string fileName) {
        return File.ReadAllBytes(Resolve(fileName));
    }

    private static string Resolve(string fileName) {
        return Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
    }
}
