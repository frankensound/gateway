﻿namespace Gateway.Models.Dto
{
    public class RegisterDto
    {
        public string Email { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public bool Consent { get; set; }
    }
}
