using System.Text;
using MkRss.Core.Feeds;
using Xunit;

namespace MkRss.Core.Tests.Feeds;

public sealed class FeedCacheValidatorTests {
    [Fact]
    public void ComputeETag_SameContent_ProducesSameValue() {
        byte[] content = Encoding.UTF8.GetBytes("<feed>内容</feed>");

        Assert.Equal(
            FeedCacheValidator.ComputeETag(content),
            FeedCacheValidator.ComputeETag(Encoding.UTF8.GetBytes("<feed>内容</feed>")));
    }

    [Fact]
    public void ComputeETag_DifferentContent_ProducesDifferentValue() {
        Assert.NotEqual(
            FeedCacheValidator.ComputeETag(Encoding.UTF8.GetBytes("<feed>古い</feed>")),
            FeedCacheValidator.ComputeETag(Encoding.UTF8.GetBytes("<feed>新しい</feed>")));
    }

    [Fact]
    public void ComputeETag_IsQuoted() {
        // RFC 9110 の entity-tag は引用符で囲む。囲まないと弱い検証子と区別できない。
        string etag = FeedCacheValidator.ComputeETag(Encoding.UTF8.GetBytes("<feed/>"));

        Assert.StartsWith("\"", etag);
        Assert.EndsWith("\"", etag);
    }

    [Fact]
    public void ComputeETag_ContainsNoCharacterNeedingEscape() {
        // ヘッダにそのまま載せるため、引用符とバックスラッシュを含んではならない。
        string inner = FeedCacheValidator.ComputeETag(Encoding.UTF8.GetBytes("<feed/>")).Trim('"');

        Assert.DoesNotContain('"', inner);
        Assert.DoesNotContain('\\', inner);
        Assert.NotEmpty(inner);
    }
}
