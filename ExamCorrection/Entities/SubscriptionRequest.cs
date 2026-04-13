using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ExamCorrection.Entities;

public class SubscriptionRequest
{
    public int Id { get; set; }
    
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }
    
    public int PlanId { get; set; }
    
    [ForeignKey(nameof(PlanId))]
    public SubscriptionPlan? Plan { get; set; }
    
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
    
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? ProcessedAt { get; set; }
    
    public string? AdminNotes { get; set; }

    public string? TapChargeId { get; set; }
    
    [MaxLength(50)]
    public string? PaymentStatus { get; set; } // Initiated, Captured, Failed, Voided
}
