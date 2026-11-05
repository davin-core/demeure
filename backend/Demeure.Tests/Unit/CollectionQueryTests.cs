using Demeure.Application.Common.Exceptions;
using Demeure.Application.Common.Queries;

namespace Demeure.Tests.Unit;

public sealed class CollectionQueryTests
{
    [Fact]
    public void DefaultsValidateAndLargeOffsetsAreRejectedWithoutOverflow()
    {
        var query = new CollectionQuery();
        query.Validate();
        Assert.Equal(1, query.Page);
        Assert.Equal(20, query.PageSize);
        Assert.Throws<RequestValidationException>(() => new CollectionQuery
            { Page = int.MaxValue, PageSize = 100 }.Validate());
    }

    [Theory]
    [InlineData(0, 20, 0)]
    [InlineData(1, 20, 1)]
    [InlineData(20, 20, 1)]
    [InlineData(21, 20, 2)]
    [InlineData(long.MaxValue, 100, 92233720368547759)]
    public void TotalPagesUsesCeilingWithoutOverflow(long total, int size, long pages) =>
        Assert.Equal(pages, new PagedResponse<string>([], 1, size, total).TotalPages);

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(-1, -1)]
    public void InvalidBoundsAreNeverSilentlyClamped(int page, int size) =>
        Assert.Throws<RequestValidationException>(() => new CollectionQuery { Page = page, PageSize = size }.Validate());
}
