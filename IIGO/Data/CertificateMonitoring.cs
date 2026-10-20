using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IIGO.Data
{
    [Table("CertificateMonitoring")]
    [PrimaryKey(nameof(Id))]
    public class CertificateMonitoring
    {
        [Key]
        public int Id { get; set; }

        [Column, MaxLength(200)]
        public string SiteName { get; set; } = "";

        [Column, MaxLength(300)]
        public string Binding { get; set; } = "";

        [Column, MaxLength(30)]
        public string IssueType { get; set; } = "";

        [Column, MaxLength(500)]
        public string Description { get; set; } = "";

        [Column]
        public DateTime LoggedTime { get; set; }
    }
}
