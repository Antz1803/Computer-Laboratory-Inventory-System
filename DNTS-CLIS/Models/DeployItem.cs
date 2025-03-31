using System.ComponentModel.DataAnnotations;

namespace DNTS_CLIS.Models
{
    public class DeployItem
    {
        public int Id { get; set; }
        public int DeploymentInfoId { get; set; }
        public string Particular { get; set; }
        public string Brand { get; set; }
        public int Quantity { get; set; }
        public string SerialControlNumber { get; set; }
    }
}
