using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.ComponentModel.DataAnnotations;

namespace AutoTrack.Models
{
    public class TaskItem
    {
        public int Id { get; set; }
        [Required]
        public string? Title { get; set; }
        [Required]
        public string? Description { get; set; }
        [Required]
        public string? Priority { get; set; }
        [Required]
        [DataType(DataType.Date)]
        public DateTime DueDate { get; set; }
        public string? Status { get; set; }
        [Required(ErrorMessage = "Please assign this task to an employee.")]
        public string? AssignedToId { get; set; }
        public ApplicationUser? AssignedTo { get; set; }
        public DateTime CreatedAt { get; set; }

        // Add this property:
        public string? CreatedBy { get; set; }
    }

}
