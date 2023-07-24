using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IIGO.Data
{
    [Table("AppPoolMonitoring")]
    [PrimaryKey(nameof(Id))]
    internal class AppPoolMonitoring
    {
        [Key]
        public int Id { get; set; }

        [Column,MaxLength(100)]
        public string AppPoolName { get; set; }

        [Column]
        public DateTime AppPoolStartTime { get; set; }
    }
}
