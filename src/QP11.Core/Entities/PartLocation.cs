using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QP11.Core.Entities;

[Table("part_place")]
public class PartLocation
{
    [Key]
    [Column("place")]
    public string? Place { get; set; }

    [Column("place_nm")]
    public string? PlaceNm { get; set; }

    [Column("place_user")]
    public string? PlaceUser { get; set; }

    [Column("place_type")]
    public string? PlaceType { get; set; }

    [Column("place_area")]
    public string? PlaceArea { get; set; }

    [Column("place_note")]
    public string? PlaceNote { get; set; }
}
