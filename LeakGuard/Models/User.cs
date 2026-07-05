namespace LeakGuard.Models
{
    public class User
    {
        public int UserID { get; set; }
        public int RoleID { get; set; }
        public Role Role { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        public bool IsActive { get; set; } = true;
    }
}
