using System;
using System.ComponentModel.DataAnnotations;

namespace DNTS_CLIS.Models
{
    public class DeploymentInfo
    {
        public int Id { get; set; }
        public string RequestedBy { get; set; }
        public string To { get; set; }
        public string From { get; set; }
        public string Purpose { get; set; }
        public DateTime TodayDate { get; set; } = DateTime.Now;
        public DateTime DurationDate { get; set; } = DateTime.Now;
        public string Laboratory { get; set; }
        public string ReleasedBy { get; set; }
        public string ReceivedBy { get; set; }
        public List<DeployItem> DeployItems { get; set; }
    }
}
