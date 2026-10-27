using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IIGO.Data
{
    /// <summary>
    /// A completed Windows Update search, so the last result survives a restart. Only the latest check is kept.
    /// </summary>
    [Table("WindowsUpdateChecks")]
    [PrimaryKey(nameof(Id))]
    public class WindowsUpdateCheck
    {
        [Key]
        public int Id { get; set; }

        [Column]
        public DateTime CheckedAt { get; set; }

        // Updates the search found; empty when the system was up to date
        public List<WindowsUpdate> Updates { get; set; } = [];
    }
}
