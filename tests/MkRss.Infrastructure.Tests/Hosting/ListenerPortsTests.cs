using MkRss.Infrastructure.Hosting;
using Xunit;

namespace MkRss.Infrastructure.Tests.Hosting;

public sealed class ListenerPortsTests {
    private static readonly ListenerPorts Ports = new(PublicPort: 8080, AdminPort: 9000);

    [Fact]
    public void Allows_AdminPort_AllowsNonPublicEndpoint() {
        Assert.True(Ports.Allows(localPort: 9000, isPublicEndpoint: false));
    }

    [Fact]
    public void Allows_AdminPort_AllowsPublicEndpoint() {
        // 管理UIのサイト一覧はフィードへリンクしている。管理用ポートからも辿れるようにする。
        Assert.True(Ports.Allows(localPort: 9000, isPublicEndpoint: true));
    }

    [Fact]
    public void Allows_PublicPort_AllowsPublicEndpoint() {
        Assert.True(Ports.Allows(localPort: 8080, isPublicEndpoint: true));
    }

    [Fact]
    public void Allows_PublicPort_RejectsNonPublicEndpoint() {
        // 管理UIを外へ出さない。公開しないものを列挙するのではなく、
        // 公開すると明示していないものはすべて拒否する。
        Assert.False(Ports.Allows(localPort: 8080, isPublicEndpoint: false));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Allows_UnknownPort_RejectsEverything(bool isPublicEndpoint) {
        // 設定と異なるポートで待ち受けている場合、どちらの扱いにするかを推測しない。
        Assert.False(Ports.Allows(localPort: 5000, isPublicEndpoint));
    }
}
