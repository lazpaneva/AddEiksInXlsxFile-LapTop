using AddEiksInXlsxFile.Services;
using Xunit;

namespace AddEiksInXlsxFile.Tests.Services;

public class SearchServiceTests
{
    [Fact]
    public void SetEdit_and_TryGetEdit_store_value_by_normalized_name()
    {
        var service = new SearchService();

        service.SetEdit("ACME", "123456789");

        Assert.True(service.TryGetEdit("ACME", out var eik));
        Assert.Equal("123456789", eik);
    }

    [Fact]
    public void TryGetEdit_returns_false_for_unknown_name()
    {
        var service = new SearchService();

        Assert.False(service.TryGetEdit("UNKNOWN", out var eik));
        Assert.Null(eik);
    }

    [Fact]
    public void Clear_removes_all_edits()
    {
        var service = new SearchService();
        service.SetEdit("ACME", "123456789");

        service.Clear();

        Assert.False(service.TryGetEdit("ACME", out _));
        Assert.Empty(service.GetAllEdits());
    }

    [Fact]
    public void SetEdit_overwrites_existing_value()
    {
        var service = new SearchService();
        service.SetEdit("ACME", "111111111");

        service.SetEdit("ACME", "222222222");

        Assert.True(service.TryGetEdit("ACME", out var eik));
        Assert.Equal("222222222", eik);
    }
}
