using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QP11.Core.Entities;

[Table("user_infor")]
public class UserInfor
{
    [Key]
    [Column("username")]
    public string? Username { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("password")]
    public string? Password { get; set; }

    [Column("groups")]
    public int? Groups { get; set; }

    [Column("datetime")]
    public DateTime? Datetime { get; set; }

    [Column("rules")]
    public string? Rules { get; set; }

    [Column("state")]
    public int? State { get; set; }

    [Column("login")]
    public DateTime? Login { get; set; }

    [Column("out")]
    public DateTime? Out { get; set; }

    [Column("auth")]
    public string? Auth { get; set; }

    [Column("diskid")]
    public string? Diskid { get; set; }
}
