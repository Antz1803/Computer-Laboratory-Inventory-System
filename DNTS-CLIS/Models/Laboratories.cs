using System.ComponentModel.DataAnnotations;

namespace DNTS_CLIS.Models
{
    public class Laboratories
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string LaboratoryName { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
