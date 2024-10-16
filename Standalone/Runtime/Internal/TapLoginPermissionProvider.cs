using TapSDK.Login;
using TapSDK.Login.Standalone;

namespace TapSDK.Compliance
{
    public class TapLoginPermissionConfig : IComplianceProvider
    {
        public string GetAgeRangeScope()
        {
            if (TapTapComplianceManager.ClientId != null)
            {
                if (TapTapComplianceManager.ComplianceConfig.useAgeRange)
                {
                    return ComplianceWorker.SCOPE_COMPLIANCE;
                }
                else
                {
                    return ComplianceWorker.SCOPE_COMPLIANCE_BASIC;
                }
            }
            return null;
        }
    }
}
