using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using sideral_context.Users;

namespace sideral_context.Settings;

/// <summary>Paramètre applicatif modifiable par l'administration (clé / valeur). Les secrets sont stockés chiffrés.</summary>
public class AppSetting
{
    [Key]
    [MaxLength(100)]
    public required string Key { get; set; }

    public string? Value { get; set; }

    public DateTime UpdatedOn { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(UpdatedBy))]
    public long? UpdatedById { get; set; }
    public User? UpdatedBy { get; set; }
}
