using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IIGO.Data
{
    /// <summary>
    /// A key for calling the API. Only a hash of the key is stored; the full key is shown once when it's created.
    /// </summary>
    [Table("ApiKeys")]
    [PrimaryKey(nameof(Id))]
    public class ApiKey
    {
        [Key]
        public int Id { get; set; }

        [Column, Required, MaxLength(100)]
        public string Name { get; set; } = "";

        // Start of the key, so it can be recognized without storing the whole key
        [Column, Required, MaxLength(20)]
        public string KeyPrefix { get; set; } = "";

        // SHA-256 of the full key, as hex
        [Column, Required, MaxLength(64)]
        public string KeyHash { get; set; } = "";

        [Column]
        public bool Enabled { get; set; } = true;

        [Column]
        public DateTime CreatedAt { get; set; }

        [Column, MaxLength(256)]
        public string CreatedBy { get; set; } = "";

        [Column]
        public DateTime? ExpiresAt { get; set; }

        [Column]
        public DateTime? LastUsedAt { get; set; }
    }
}
