using System.Collections.Generic;

namespace LeakGuard.Models
{
    public class Role
    {
        public int RoleID { get; set; }
        public string RoleName { get; set; } = null!;
        public string? Description { get; set; }
        public ICollection<User> Users { get; set; } = new List<User>();
    }
}
