using TenantAccessControlPlane.Core;

namespace TenantAccessControlPlane.Unit;

public class DomainTests
{
    [Fact]
    public void ComputePayloadHash_IsStable()
    {
        var a = DefinitionService.ComputePayloadHash("{\"k\":1}");
        var b = DefinitionService.ComputePayloadHash("{\"k\":1}");
        Assert.Equal(a, b);
    }

    [Fact]
    public void OwnerUpdate_RequiresSameService()
    {
        Assert.True(DefinitionService.CanUpdateOwner("svc-a", "svc-a"));
        Assert.False(DefinitionService.CanUpdateOwner("svc-a", "svc-b"));
    }
}
