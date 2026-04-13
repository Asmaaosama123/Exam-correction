using System.ComponentModel.DataAnnotations;

namespace ExamCorrection.Entities;

public class SubscriptionPlan
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    public decimal Price { get; set; }
    
    public long MaxAllowedPages { get; set; }
    
    public int DurationValue { get; set; }
    
    [Required]
    [MaxLength(20)]
    public string DurationUnit { get; set; } = "Month"; // Month, Term, Year
    
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
