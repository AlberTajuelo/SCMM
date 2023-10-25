using SCMM.Shared.Data.Models.Enums;

namespace SCMM.Web.Data.Models.UI
{
    public interface ICanBeOwned
    {
        public long? Subscriptions { get; }

        public EstimatedAccuracyTypes SupplyEstimationAccuracy { get; set; }

        public long? SupplyTotalEstimated { get; }
    }
}
