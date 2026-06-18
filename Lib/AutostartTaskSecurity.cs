using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace MiniMetrics.Lib;

// Builds the task security descriptor that lets the owning user delete (but not modify) the autostart
// scheduled task. Granting delete-only is what allows a non-elevated, no-UI uninstall to remove the task;
// granting write would let a non-elevated process repoint a highest-privilege task, so it is never granted.
[SupportedOSPlatform("windows")]
public static class AutostartTaskSecurity
{
    // DELETE (0x10000) | READ_CONTROL (0x20000): enough to remove the task, not enough to alter it.
    private const int DeleteAndReadControl = 0x00010000 | 0x00020000;

    public static string GrantUserDelete(string existingSddl, SecurityIdentifier user)
    {
        var descriptor = new RawSecurityDescriptor(existingSddl);
        RawAcl dacl = descriptor.DiscretionaryAcl ?? new RawAcl(GenericAcl.AclRevision, 1);

        foreach (GenericAce entry in dacl)
        {
            if (entry is CommonAce existing
                && existing.AceType == AceType.AccessAllowed
                && existing.SecurityIdentifier.Equals(user)
                && (existing.AccessMask & DeleteAndReadControl) == DeleteAndReadControl)
            {
                return existingSddl;
            }
        }

        dacl.InsertAce(dacl.Count, new CommonAce(
            AceFlags.None, AceQualifier.AccessAllowed, DeleteAndReadControl, user, isCallback: false, opaque: null));
        descriptor.DiscretionaryAcl = dacl;
        return descriptor.GetSddlForm(AccessControlSections.Access);
    }
}
