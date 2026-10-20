using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IIGO.Data
{
    /// <summary>
    /// Assigns a messenger service to a notification feature. A feature sends through every service assigned to it.
    /// </summary>
    [Table("NotificationSubscriptions")]
    [PrimaryKey(nameof(Id))]
    public class NotificationSubscription
    {
        [Key]
        public int Id { get; set; }

        // One of the NotificationFeature constants
        [Column, Required, MaxLength(100)]
        public string Feature { get; set; } = "";

        // IMessengerService.ServiceIdentifier
        [Column, Required, MaxLength(100)]
        public string ServiceIdentifier { get; set; } = "";
    }
}
