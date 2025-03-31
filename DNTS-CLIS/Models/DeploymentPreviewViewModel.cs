namespace DNTS_CLIS.Models
{
    public class DeploymentPreviewViewModel
    {
        public DeploymentInfo? DeploymentInfos { get; set; }
        public List<DeployItem> DeployItems { get; set; }
        public int DeploymentId { get; set; }
    }
}
