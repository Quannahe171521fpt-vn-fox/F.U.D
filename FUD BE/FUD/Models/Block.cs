using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class Block
{
    public int Id { get; set; }

    public int DormId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public int? FloorLevel { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int? NumberOfFloors { get => FloorLevel; set => FloorLevel = value; }

    public string GenderType { get; set; } = null!;

    public string Status { get; set; } = null!;

    public virtual Dorm Dorm { get; set; } = null!;

    public virtual ICollection<ImportBatch> ImportBatches { get; set; } = new List<ImportBatch>();

    public virtual ICollection<Room> Rooms { get; set; } = new List<Room>();
}
