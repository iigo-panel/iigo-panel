using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IIGO.Data
{
    /// <summary>
    /// An update that was available when its <see cref="WindowsUpdateCheck"/> ran.
    /// </summary>
    [Table("WindowsUpdates")]
    [PrimaryKey(nameof(Id))]
    public class WindowsUpdate
    {
        [Key]
        public int Id { get; set; }

        [Column]
        public int WindowsUpdateCheckId { get; set; }

        // IUpdate.Identity.UpdateID (a GUID)
        [Column, Required, MaxLength(50)]
        public string UpdateId { get; set; } = "";

        [Column]
        public int RevisionNumber { get; set; }

        [Column, Required, MaxLength(500)]
        public string Title { get; set; } = "";

        [Column]
        public decimal SizeMB { get; set; }
    }
}
