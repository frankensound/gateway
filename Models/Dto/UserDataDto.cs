namespace Gateway.Models.Dto
{
    public class UserDataDto
    {
        public string Email { get; set; }
        // Etc.

        public UserDataDto(string email)
        {
            Email = email;
        }
    }
}
