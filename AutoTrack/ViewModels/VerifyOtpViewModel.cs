using System.ComponentModel.DataAnnotations;

namespace AutoTrack.ViewModels
{
    public class VerifyOtpViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; }
        [Required]
        public string Otp { get; set; }
    }
}
