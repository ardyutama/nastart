using Nastart.Domain.Common;

namespace Nastart.Domain.Entities;

public class Unit : BaseEntity
{
    public string Name {get; set; } = string.Empty;
    public string Abbrevation {get; set; } = string.Empty;
}