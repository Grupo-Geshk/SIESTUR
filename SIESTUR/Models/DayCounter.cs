// Models/DayCounter.cs
using System.ComponentModel.DataAnnotations;

namespace Siestur.Models;

public class DayCounter
{
    [Key] public DateOnly ServiceDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public int NextNumber { get; set; }
}
