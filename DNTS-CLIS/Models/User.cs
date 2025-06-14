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
        [StringLength(8, ErrorMessage = "Password cannot be more than 8 characters.")]
        public string Password { get; set; }
    }
}
