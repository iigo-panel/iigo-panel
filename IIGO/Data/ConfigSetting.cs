using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IIGO.Data
{
    [Table(nameof(ConfigSetting))]
    [PrimaryKey(nameof(Id))]
    internal class ConfigSetting
    {
        [Key,Required]
        public int Id { get; set; }

        [Required,MaxLength(100)]
        [Column(TypeName = "varchar(100)")]
        public string SettingName { get; set; }
        
        [Column(TypeName = "varchar(300)"),MaxLength(300)]
        public string SettingValue { get; set; }
    }
}
