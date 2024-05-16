using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IIGO.Data
{
    [Table("DomainStatus")]
    [PrimaryKey(nameof(Id))]
    public class DomainStatus
    {
        [Key]
        public int Id { get; set; }

        [Column, Required, MaxLength(120)]
        public string DomainName { get; set; } = "";

        [Column]
        public DateTime DomainExpiration { get; set; }

        [Column,DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime LastChecked { get; set; } = DateTime.UtcNow;

        [Column]
        public string RegistrarName { get; set; } = "";

        [Column]
        public bool NotifyChanges { get; set; }

        [Column,MaxLength(300)]
        public string DomainNotificationEmail { get; set; } = "";
    }
}
