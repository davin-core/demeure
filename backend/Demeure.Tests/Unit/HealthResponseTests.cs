using System.Text.Json;
using Demeure.Api.Contracts;

namespace Demeure.Tests.Unit;

public sealed class HealthResponseTests
{
    [Fact]
    public void ResponseSerializesUsingTheExistingCamelCaseContract()
    {
        var response = new HealthResponse("ok", "demeure-api");
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal("""{"status":"ok","service":"demeure-api"}""", json);
    }
}
