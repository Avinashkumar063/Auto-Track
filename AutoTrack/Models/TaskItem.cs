namespace AutoTrack.Models 
{ 
    public class TaskItem { 
        public int Id { get; set; } 
        public string Title { get; set; } 
        public string Description { get; set; } 
        public DateTime DueDate { get; set; } 
        public string Status { get; set; } // e.g., "Pending", "InProgress", "Completed"
        public string AssignedToId { get; set; } 
        public ApplicationUser AssignedTo { get; set; } 
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; 
    } 
}