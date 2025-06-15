using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;

namespace AutoTrack.Models
{
   public class ApplicationUser : IdentityUser
   {
        public List<TaskItem> Tasks { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
   }

}
