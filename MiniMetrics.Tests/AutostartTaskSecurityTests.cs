using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using MiniMetrics.Lib;

namespace MiniMetrics.Tests;

[TestClass]
[SupportedOSPlatform("windows")]
public class AutostartTaskSecurityTests
{
    private const string BaseDacl = "D:(A;;FA;;;BA)(A;;FA;;;SY)";
    private static readonly SecurityIdentifier User = new("S-1-5-21-1-2-3-1000");

    private const int DeleteAndReadControl = 0x00010000 | 0x00020000;

    [TestMethod]
    public void GrantUserDelete_adds_delete_and_readcontrol_ace_for_the_user()
    {
        string result = AutostartTaskSecurity.GrantUserDelete(BaseDacl, User);

        var dacl = new RawSecurityDescriptor(result).DiscretionaryAcl!;
        var ace = dacl.OfType<CommonAce>().Single(candidate => candidate.SecurityIdentifier.Equals(User));
        Assert.AreEqual(AceQualifier.AccessAllowed, ace.AceQualifier);
        Assert.AreEqual(DeleteAndReadControl, ace.AccessMask & DeleteAndReadControl);
    }

    [TestMethod]
    public void GrantUserDelete_preserves_existing_aces()
    {
        string result = AutostartTaskSecurity.GrantUserDelete(BaseDacl, User);

        var dacl = new RawSecurityDescriptor(result).DiscretionaryAcl!;
        Assert.AreEqual(3, dacl.Count);
    }

    [TestMethod]
    public void GrantUserDelete_is_idempotent()
    {
        string once = AutostartTaskSecurity.GrantUserDelete(BaseDacl, User);
        string twice = AutostartTaskSecurity.GrantUserDelete(once, User);

        Assert.AreEqual(once, twice);
    }
}
