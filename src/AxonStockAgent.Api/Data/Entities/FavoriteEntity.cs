namespace AxonStockAgent.Api.Data.Entities;

public class FavoriteEntity
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string Symbol { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
