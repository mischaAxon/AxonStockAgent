using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AxonStockAgent.Api.Data.Entities;

[Table("algo_settings")]
public class AlgoSettingsEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("category")]
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    [Required]
    [Column("key")]
    [MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [Column("value")]
    public string Value { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Required]
    [Column("value_type")]
    [MaxLength(20)]
    public string ValueType { get; set; } = "decimal";

    [Column("min_value")]
    public double? MinValue { get; set; }

    [Column("max_value")]
    public double? MaxValue { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
