using Microsoft.AspNetCore.Identity;

namespace Identity.API.Data;

public class AppUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
}
