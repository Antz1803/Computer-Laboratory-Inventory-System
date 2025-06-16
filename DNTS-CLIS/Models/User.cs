using System.ComponentModel.DataAnnotations;

namespace DNTS_CLIS.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }
        public string FirstName {  get; set; }
        public string LastName { get; set; }
        public string Email{ get; set; }
        public string Status { get; set; }
        public string Role { get; set; }
        public string AssignLaboratory { get; set; } = "N/A";
        public string Username { get; set; }
        [Required]
        public string Password { get; set; }
        public string TemporaryPassword { get; set; }
        public DateTime? TemporaryPasswordExpiry { get; set; }
        public bool? RequiresPasswordChange { get; set; }
    }
}
