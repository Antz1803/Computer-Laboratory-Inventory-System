using System.ComponentModel.DataAnnotations;

namespace DNTS_CLIS.Models
{
    public class AssignedLaboratories
    {
        [Key]
        public int Id { get; set; }
        public string? TrackNo { get; set; }
        public string? LaboratoryName { get; set; }
        public DateTime? AssignedDate { get; set; }
    }
}
